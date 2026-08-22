using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Hybrid unit data builder.
///
/// PRIMARY SOURCE: the three regional info.json files (global/eu/jp). Global
/// wins on conflicts; a unit missing from Global falls back to EU then JP.
///
/// FALLBACK SOURCE: the Global and EU fandom wikis, used ONLY to fill in
/// fields info.json structurally cannot contain (descriptions, evolution
/// chain, display positions, sell price, summon cost, ai/max level, bb type).
/// Global wiki wins; EU wiki fills in only units Global's wiki doesn't have
/// at all. There is no English JP wiki, so JP-exclusive units will have
/// blank text fields — this is reported, not guessed.
/// </summary>
public class HybridUnitLoader : MonoBehaviour
{
    // ── Info.json sources ────────────────────────────────────────────────────
    [Header("Info JSON sources (JSON files in Assets/EditorData)")]
    public string globalInfoFile = "info";
    public string euInfoFile = "info_eu";
    public string jpInfoFile = "info_jp";

    public enum RegionPriority { GlobalThenEuThenJp, EuThenGlobalThenJp, JpThenGlobalThenEu }
    [Tooltip("Which region wins when the same id appears in more than one info.json.")]
    public RegionPriority regionPriority = RegionPriority.GlobalThenEuThenJp;

    // ── Wiki sources ─────────────────────────────────────────────────────────
    [Header("Wiki fallback sources")]
    const string GLOBAL_API_BASE = "https://bravefrontierglobal.fandom.com/api.php";
    const string EU_API_BASE = "https://bravefrontiereurope.fandom.com/api.php";
    const string LIST_BASE = "Unit_List";

    // ── Unit Master Files (structured display-position/sell data) ────────────
    [Header("Unit Master Files")]
    public string unitMasterFile1 = "F_UNIT_MST_1_Ver1084"; // without .json extension
    public string unitMasterFile2 = "F_UNIT_MST_2_Ver1084";

    [Header("Behaviour")]
    public bool debugLogging = true;
    public int throttleMs = 300;
    public int rateLimitBackoffS = 30;
    public int maxRetriesPerUrl = 3;
    [Tooltip("Seconds before a single request is aborted and retried. UnityWebRequest's default timeout is 0 (infinite) — without this, one stalled connection can hang the whole import silently.")]
    public int requestTimeoutSeconds = 20;
    [Tooltip("Log progress every N units fetched, so a long-running import doesn't look stalled.")]
    public int progressLogEvery = 25;
    [Tooltip("Consecutive empty list pages before we stop paginating the EU wiki (Global uses a hand-verified jump table instead).")]
    public int euEmptyPagesBeforeStop = 2;
    [Tooltip("Hard safety cap on EU list pages fetched, in case the wiki returns non-empty results for pages that don't really exist (e.g. shared nav links), which would otherwise make the empty-page check never trigger.")]
    public int euMaxListPages = 200;

    // ── Output ───────────────────────────────────────────────────────────────
    [Header("Output")]
    public string outputFolder = "BFUnits";

    // ── Runtime state ────────────────────────────────────────────────────────
    private Dictionary<string, JObject> _infoById = new Dictionary<string, JObject>();
    private Dictionary<string, Dictionary<string, string>> _wikiFieldsById = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, string> _wikiOriginById = new Dictionary<string, string>(); // "Global" or "EU", for logging
    private Dictionary<string, string> _wikiTitleById = new Dictionary<string, string>();
    private Dictionary<string, JObject> _unitMasterById = new Dictionary<string, JObject>();

    // Master-file field names exactly as they appear in F_UNIT_MST_* — these
    // are written to output verbatim, suffix and all, matching your actual
    // schema. sellCaution is stored as an int (0/1) in the source and
    // normalised to a real bool on write; every other field is a straight
    // passthrough with no renaming.
    private static readonly string[] UnitMasterFields =
    {
        "unitDisplayHomePosition_1W9CxaFK",
        "unitDisplayDetailPosition_6z54rgb3",
        "unitDisplayConfirmImagePosition_MYK1fq6c",
        "unitDisplayCutInImagePosition_7hLR6pDN",
        "unitDisplaySummonPosition_KC3Jk8Br",
        "unitDisplayHpPosition_3BpHN6VD",
        "unitDisplayCursorPosition",
        "sellPrice",
        "sellCaution",
        "effectFrames",
    };

    private readonly HashSet<string> _neverTranslatable = new HashSet<string>();
    private int _unitsProcessed = 0;
    private int _unitsWithNoWikiPageAtAll = 0;

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));

        // ── Phase 1: info.json merge (primary source) ───────────────────────
        LoadInfoJsons();

        // ── Phase 1b: unit master files (structured display/sell fallback) ──
        LoadUnitMasterFile(unitMasterFile1);
        LoadUnitMasterFile(unitMasterFile2);
        if (debugLogging) Debug.Log($"[HybridUnitLoader] Unit master lookup contains {_unitMasterById.Count} entries.");

        // ── Phase 2: wiki fallback data (Global first, EU fills gaps) ───────
        yield return StartCoroutine(ScrapeWikiList(GLOBAL_API_BASE, "Global", useHandVerifiedJumpTable: true));
        yield return StartCoroutine(ScrapeWikiList(EU_API_BASE, "EU", useHandVerifiedJumpTable: false));

        if (debugLogging)
            Debug.Log($"[HybridUnitLoader] Wiki text resolved for {_wikiFieldsById.Count} unit ids " +
                      $"({_wikiFieldsById.Count(kv => _wikiOriginById[kv.Key] == "Global")} from Global, " +
                      $"{_wikiFieldsById.Count(kv => _wikiOriginById[kv.Key] == "EU")} from EU).");

        // ── Phase 3: build final per-unit files ─────────────────────────────
        var allIds = new HashSet<string>(_infoById.Keys);
        allIds.UnionWith(_wikiFieldsById.Keys);

        int i = 0;
        foreach (string id in allIds.OrderBy(x => x))
        {
            ProcessUnit(id);
            i++;
            if (i % 50 == 0) yield return null;
        }

        if (debugLogging)
        {
            Debug.Log($"[HybridUnitLoader] DONE. Wrote {_unitsProcessed} unit files.");
            Debug.Log($"[HybridUnitLoader] {_unitsWithNoWikiPageAtAll} units had NO wiki page on either Global or EU (likely JP-exclusive) — their description/evolution/display fields are blank.");

            if (_neverTranslatable.Count > 0)
                Debug.LogWarning("[HybridUnitLoader] Fields NEVER available from any source for ANY unit:\n - " +
                                  string.Join("\n - ", _neverTranslatable.OrderBy(s => s)));
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 1 — info.json
    // ══════════════════════════════════════════════════════════════════════

    void LoadInfoJsons()
    {
        JObject global = LoadInfoFile(globalInfoFile);
        JObject eu = LoadInfoFile(euInfoFile);
        JObject jp = LoadInfoFile(jpInfoFile);

        List<JObject> ordered = regionPriority switch
        {
            RegionPriority.EuThenGlobalThenJp => new List<JObject> { eu, global, jp },
            RegionPriority.JpThenGlobalThenEu => new List<JObject> { jp, global, eu },
            _ => new List<JObject> { global, eu, jp },
        };
        ordered = ordered.Where(o => o != null).ToList();

        foreach (var file in ordered)
        {
            foreach (var prop in file.Properties())
            {
                if (!_infoById.ContainsKey(prop.Name) && prop.Value is JObject jo)
                    _infoById[prop.Name] = jo;
            }
        }

        if (debugLogging) Debug.Log($"[HybridUnitLoader] info.json merge complete — {_infoById.Count} unique unit ids.");
    }

    JObject LoadInfoFile(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        TextAsset asset = EditorDataLoader.LoadJson(resourceName);
        if (asset == null)
        {
            Debug.LogWarning($"[HybridUnitLoader] Could not load '{resourceName}' from Assets/EditorData — skipping that region.");
            return null;
        }
        try { return JObject.Parse(asset.text); }
        catch (Exception ex)
        {
            Debug.LogError($"[HybridUnitLoader] Failed to parse '{resourceName}': {ex.Message}");
            return null;
        }
    }

    void LoadUnitMasterFile(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return;
        TextAsset asset = EditorDataLoader.LoadJson(resourceName);
        if (asset == null)
        {
            Debug.LogWarning($"[HybridUnitLoader] Could not load unit master file '{resourceName}' from Assets/EditorData — skipping.");
            return;
        }

        try
        {
            JArray arr = JArray.Parse(asset.text);
            int added = 0;
            foreach (JToken token in arr)
            {
                if (token is JObject entry)
                {
                    string uid = entry["unitId"]?.ToString();
                    if (!string.IsNullOrEmpty(uid) && !_unitMasterById.ContainsKey(uid))
                    {
                        _unitMasterById[uid] = entry;
                        added++;
                    }
                }
            }
            if (debugLogging) Debug.Log($"[HybridUnitLoader] Unit master '{resourceName}' → added {added} entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HybridUnitLoader] Failed to parse unit master '{resourceName}': {ex.Message}");
        }
    }

    JObject FindMasterEntry(string id)
    {
        if (_unitMasterById.TryGetValue(id, out JObject entry)) return entry;

        string trimmed = id.TrimStart('0');
        if (!string.IsNullOrEmpty(trimmed) && trimmed != id && _unitMasterById.TryGetValue(trimmed, out entry))
            return entry;

        if (int.TryParse(id, out int idInt) && _unitMasterById.TryGetValue(idInt.ToString(), out entry))
            return entry;

        return null;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 2 — wiki fallback
    // ══════════════════════════════════════════════════════════════════════

    IEnumerator ScrapeWikiList(string apiBase, string originLabel, bool useHandVerifiedJumpTable)
    {
        List<string> listTitles = new List<string> { LIST_BASE };

        if (useHandVerifiedJumpTable)
        {
            // Hand-verified against the Global wiki's actual page numbering gaps.
            for (int i = 100; true; i += 100)
            {
                listTitles.Add($"{LIST_BASE}:{i}");
                if (i == 1900) i = 6900;
                if (i == 7100) i = 7900;
                if (i == 8700) { listTitles.Add($"{LIST_BASE}:Other"); break; }
            }

            // -----------------------------------------------------------------
            // Global wiki also contains EU-exclusive unit pages under
            // Unit_List:1700_(EU), Unit_List:1800_(EU), ...
            //
            // These are still hosted on the GLOBAL wiki and therefore should be
            // scraped before falling back to the standalone EU wiki.
            //
            // Hand-verified range: 1700 up through 7000, then a gap, resuming
            // at 7200 through 8000 (the last page in this range).
            // -----------------------------------------------------------------

            for (int i = 1700; true; i += 100)
            {
                listTitles.Add($"{LIST_BASE}:{i}_(EU)");
                if (i == 1700) i = 6900; // next += 100 lands on 7100, skipping the gap
                if (i == 8000) break;
            }
        }
        else
        {
            // EU's page numbering isn't verified — paginate generically and
            // stop after N consecutive empty pages. Adjust euEmptyPagesBeforeStop
            // if this cuts off too early or runs too long.
            int consecutiveEmpty = 0;
            int pagesFetched = 0;
            for (int i = 100; consecutiveEmpty < euEmptyPagesBeforeStop && pagesFetched < euMaxListPages; i += 100)
            {
                string pageTitle = $"{LIST_BASE}:{i}";
                int linksFound = 0;
                yield return StartCoroutine(FetchListLinks(apiBase, pageTitle, links => linksFound = links));
                if (linksFound == 0) consecutiveEmpty++; else consecutiveEmpty = 0;
                listTitles.Add(pageTitle);
                pagesFetched++;
                yield return StartCoroutine(ThrottledWait());
            }
            if (pagesFetched >= euMaxListPages && debugLogging)
                Debug.LogWarning($"[HybridUnitLoader] [EU] Hit the {euMaxListPages}-page safety cap without a clean empty-page stop — " +
                                   "the wiki may be returning non-empty results (e.g. shared nav links) for pages that don't exist. Check the last few pages manually.");
        }

        if (debugLogging) Debug.Log($"[HybridUnitLoader] [{originLabel}] Fetching {listTitles.Count} list page(s).");

        List<string> allTitles = new List<string>();
        foreach (string listTitle in listTitles)
        {
            string apiUrl = $"{apiBase}?action=parse&page={UnityWebRequest.EscapeURL(listTitle)}" +
                            $"&prop=links&format=json&disablelimitreport=1";

            string responseText = null;
            yield return StartCoroutine(FetchURL(apiUrl,
                text => responseText = text,
                () => Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Giving up on: {listTitle}")));

            if (responseText != null)
            {
                try
                {
                    JObject json = JObject.Parse(responseText);
                    JArray links = json["parse"]?["links"] as JArray;
                    if (links != null)
                    {
                        foreach (var link in links)
                        {
                            string title = link["*"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(title)) continue;
                            if (title.Contains(":")) continue; // still skip meta/namespace pages
                            // NOTE: no ASCII-only filter here anymore — JP-named
                            // pages on the Global wiki are included on purpose.
                            if (!allTitles.Contains(title)) allTitles.Add(title);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] JSON parse error for {listTitle}: {ex.Message}");
                }
            }

            yield return StartCoroutine(ThrottledWait());
        }

        if (debugLogging) Debug.Log($"[HybridUnitLoader] [{originLabel}] {allTitles.Count} unit page titles found.");

        int fetched = 0;
        foreach (string title in allTitles)
        {
            yield return StartCoroutine(FetchAndStoreWikiUnit(apiBase, originLabel, title));
            fetched++;
            if (debugLogging && fetched % progressLogEvery == 0)
                Debug.Log($"[HybridUnitLoader] [{originLabel}] ...{fetched}/{allTitles.Count} unit pages fetched");
            yield return StartCoroutine(ThrottledWait());
        }
    }

    IEnumerator FetchListLinks(string apiBase, string pageTitle, System.Action<int> onCount)
    {
        string apiUrl = $"{apiBase}?action=parse&page={UnityWebRequest.EscapeURL(pageTitle)}" +
                        $"&prop=links&format=json&disablelimitreport=1";
        string responseText = null;
        yield return StartCoroutine(FetchURL(apiUrl, text => responseText = text, () => { }));
        int count = 0;
        if (responseText != null)
        {
            try
            {
                JObject json = JObject.Parse(responseText);
                JArray links = json["parse"]?["links"] as JArray;
                count = links?.Count ?? 0;
            }
            catch { /* treated as empty */ }
        }
        onCount(count);
    }

    IEnumerator FetchAndStoreWikiUnit(string apiBase, string originLabel, string pageTitle)
    {
        string encodedTitle = UnityWebRequest.EscapeURL(pageTitle);
        string wikitextUrl = $"{apiBase}?action=query&titles={encodedTitle}" +
                             $"&prop=revisions&rvprop=content&rvslots=main&format=json";

        string wikitextJson = null;
        yield return StartCoroutine(FetchURL(wikitextUrl,
            text => wikitextJson = text,
            () => Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Could not fetch wikitext for: {pageTitle}")));

        if (wikitextJson == null) yield break;

        string wikitext = "";
        try
        {
            JObject qJson = JObject.Parse(wikitextJson);
            JToken page = qJson["query"]?["pages"]?.First?.First;
            wikitext = page?["revisions"]?[0]?["slots"]?["main"]?["*"]?.ToString()
                    ?? page?["revisions"]?[0]?["*"]?.ToString()
                    ?? "";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Wikitext parse error for {pageTitle}: {ex.Message}");
            yield break;
        }

        if (string.IsNullOrEmpty(wikitext)) yield break;

        Dictionary<string, string> fields = ParseFieldsFromTemplate(wikitext);
        string id = fields.ContainsKey("id") ? CleanWikiText(fields["id"]) : "";
        if (string.IsNullOrEmpty(id)) yield break;

        // Global-priority: first writer wins. EU only fills ids Global didn't have.
        if (!_wikiFieldsById.ContainsKey(id))
        {
            _wikiFieldsById[id] = fields;
            _wikiTitleById[id] = pageTitle;
            _wikiOriginById[id] = originLabel;
        }
    }

    #region Network helpers (unchanged from WikiUnitLoader_2)

    void SetAPIHeaders(UnityWebRequest www)
    {
        www.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        www.SetRequestHeader("Accept", "application/json, text/javascript, */*");
        www.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        www.SetRequestHeader("Accept-Encoding", "identity");
    }

    IEnumerator ThrottledWait()
    {
        float baseWait = throttleMs / 1000f;
        float jitter = UnityEngine.Random.Range(0f, baseWait * 0.5f);
        yield return new WaitForSeconds(baseWait + jitter);
    }

    int _consecutiveRateLimits = 0;

    IEnumerator HandleRateLimit(string url)
    {
        _consecutiveRateLimits++;
        float wait = Mathf.Min(rateLimitBackoffS * Mathf.Pow(2f, _consecutiveRateLimits - 1), 300f);
        Debug.LogWarning($"[RateLimit] Hit #{_consecutiveRateLimits} on {url} — backing off {wait:0}s");
        yield return new WaitForSeconds(wait);
    }

    void ResetRateLimit() => _consecutiveRateLimits = 0;

    IEnumerator FetchURL(string url, System.Action<string> onSuccess, System.Action onFail = null)
    {
        string result = null;
        for (int attempt = 1; attempt <= maxRetriesPerUrl; attempt++)
        {
            UnityWebRequest www = UnityWebRequest.Get(url);
            www.timeout = requestTimeoutSeconds;
            SetAPIHeaders(www);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                ResetRateLimit();
                result = www.downloadHandler.text;
                break;
            }

            long code = www.responseCode;
            Debug.LogWarning($"[Fetch] Failed (attempt {attempt}/{maxRetriesPerUrl}): {url} — HTTP {code} {www.error}");

            if ((code == 403 || code == 429) && attempt < maxRetriesPerUrl)
                yield return StartCoroutine(HandleRateLimit(url));
            else
                break;
        }

        if (result != null) onSuccess(result);
        else onFail?.Invoke();
    }

    #endregion

    #region Template parsing/cleaning (unchanged from WikiUnitLoader_2)

    Dictionary<string, string> ParseFieldsFromTemplate(string wikitext)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(wikitext)) return dict;

        int start = wikitext.IndexOf("{{");
        if (start < 0) return dict;

        int depth = 0, end = -1;
        for (int i = start; i < wikitext.Length - 1; i++)
        {
            if (wikitext[i] == '{' && wikitext[i + 1] == '{') { depth++; i++; continue; }
            if (wikitext[i] == '}' && wikitext[i + 1] == '}') { depth--; i++; if (depth == 0) { end = i + 1; break; } }
        }
        if (end < 0) return dict;

        string template = wikitext.Substring(start, end - start);
        var lines = template.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i].Trim();
            if (raw.StartsWith("|")) raw = raw.Substring(1).Trim();
            if (!raw.Contains("=")) continue;
            int eq = raw.IndexOf('=');
            if (eq < 0) continue;

            string key = raw.Substring(0, eq).Trim();
            string val = raw.Substring(eq + 1).Trim();
            dict[key] = val;

            if (key.Equals("evozelcost", StringComparison.OrdinalIgnoreCase)) break;
        }

        var keysToRemove = dict.Keys.Where(k => k.Contains("{{") || k.Contains("}}") || k.StartsWith("{{{")).ToList();
        foreach (var k in keysToRemove) dict.Remove(k);

        return dict;
    }

    string CleanWikiText(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        string s = WebUtility.HtmlDecode(input);

        s = Regex.Replace(s, @"<ref[^>]*>.*?</ref>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);

        for (int i = 0; i < 8; i++)
        {
            string before = s;
            s = Regex.Replace(s, @"\{\{[^{}]*\}\}", "", RegexOptions.Singleline);
            if (s == before) break;
        }

        s = Regex.Replace(s, @"\[\[(?:[^\|\]]*\|)?([^\]]+)\]\]", "$1");
        s = s.Replace("[", "").Replace("]", "");
        s = Regex.Replace(s, @"<[^>]+>", "", RegexOptions.Singleline);
        s = Regex.Replace(s, @"\r\n|\r|\n", " ");
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();
        return s;
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    // Phase 3 — build final per-unit JSON
    // ══════════════════════════════════════════════════════════════════════

    void ProcessUnit(string id)
    {
        JObject src = _infoById.TryGetValue(id, out var s) ? s : null;
        Dictionary<string, string> wiki = _wikiFieldsById.TryGetValue(id, out var w) ? w : null;
        JObject masterEntry = FindMasterEntry(id);
        if (wiki == null) _unitsWithNoWikiPageAtAll++;

        JObject obj = new JObject();
        obj["id"] = id;

        // ── Step 1: dump ALL raw wiki template fields verbatim, exactly like
        // the original script — same key names (cost, ai, maxlv, no, bbtype,
        // evofrom, evointo, evomats1..5, etc.), since JsonToSOUnit.cs reads
        // these exact flat names directly off the JSON. Only summon/fusion/
        // evolution are kept RAW (skip CleanWikiText) to preserve <br> line
        // breaks; everything else gets cleaned the same way the original did.
        if (wiki != null)
        {
            obj["unitName"] = _wikiTitleById.TryGetValue(id, out var title) ? title : "";
            foreach (var kvp in wiki)
            {
                bool keepRaw = kvp.Key.Equals("summon", StringComparison.OrdinalIgnoreCase)
                            || kvp.Key.Equals("fusion", StringComparison.OrdinalIgnoreCase)
                            || kvp.Key.Equals("evolution", StringComparison.OrdinalIgnoreCase);
                obj[kvp.Key] = keepRaw ? kvp.Value : CleanWikiText(kvp.Value);
            }
        }
        else
        {
            // No wiki page found for this id at all (likely JP-exclusive with
            // nothing on Global/EU wikis). Fall back to whatever info.json's
            // own "name" field gives us so the unit isn't nameless.
            obj["unitName"] = src != null ? Str(src, "name") : "";
            NeverTranslatable($"unit {id}: no wiki page on Global or EU — every wiki-sourced field " +
                                "(description, summon/fusion/evolution text, cost, maxlv, ai, evolution chain, etc.) is unavailable");
        }

        // ── Step 2: info.json overlays these specific keys with richer data,
        // matching the original script's dbEntry merge exactly. This
        // deliberately overwrites the wiki's own flat "bb"/"sbb"/"ubb" (which
        // are just the ability's plain name string on the wiki) with the
        // full per-level effect objects. "stats" is a new key with no wiki
        // collision. "leader_skill"/"extra_skill" likewise don't collide with
        // the wiki's "ls"/"es" fields, which are left alone.
        if (src != null)
        {
            obj["stats"] = new JObject
            {
                ["_base"] = DeepCloneOrEmpty(src["stats"]?["_base"]),
                ["_lord"] = DeepCloneOrEmpty(src["stats"]?["_lord"]),
                ["anima"] = DeepCloneOrEmpty(src["stats"]?["anima"]),
                ["breaker"] = DeepCloneOrEmpty(src["stats"]?["breaker"]),
                ["guardian"] = DeepCloneOrEmpty(src["stats"]?["guardian"]),
                ["oracle"] = DeepCloneOrEmpty(src["stats"]?["oracle"]),
            };
            obj["leader_skill"] = DeepCloneOrEmpty(src["leader skill"]);
            obj["extra_skill"] = DeepCloneOrEmpty(src["extra skill"]);
            obj["bb"] = DeepCloneOrEmpty(src["bb"]);
            obj["sbb"] = DeepCloneOrEmpty(src["sbb"]);
            obj["ubb"] = DeepCloneOrEmpty(src["ubb"]);
        }
        else
        {
            NeverTranslatable($"unit {id}: no info.json entry in any region — stats/bb/sbb/ubb/leader_skill/extra_skill are entirely unavailable");
        }

        // ── Step 3: unit master files — authoritative for display positions
        // and sell data, exactly as in the original script. Keys are written
        // verbatim (suffix included) since that's the real schema.
        obj["unitDisplayHomePosition_1W9CxaFK"] = null;
        obj["unitDisplayDetailPosition_6z54rgb3"] = null;
        obj["unitDisplayConfirmImagePosition_MYK1fq6c"] = null;
        obj["unitDisplayCutInImagePosition_7hLR6pDN"] = null;
        obj["unitDisplaySummonPosition_KC3Jk8Br"] = null;
        obj["unitDisplayHpPosition_3BpHN6VD"] = null;
        obj["unitDisplayCursorPosition"] = null;
        obj["sellPrice"] = "";
        obj["sellCaution"] = false;
        obj["effectFrames"] = "";

        if (masterEntry != null)
        {
            foreach (string field in UnitMasterFields)
            {
                if (!masterEntry.ContainsKey(field)) continue;
                JToken raw = masterEntry[field];

                if (field == "sellCaution")
                {
                    obj["sellCaution"] = raw?.Type == JTokenType.Boolean ? raw.Value<bool>() : raw?.ToString() != "0";
                }
                else
                {
                    obj[field] = raw?.DeepClone() ?? JValue.CreateNull();
                }
            }
        }
        else
        {
            NeverTranslatable($"unit {id}: no unit master entry found — display positions/sellPrice/sellCaution left blank");
        }

        string path = Path.Combine(Application.dataPath, outputFolder, $"unit_{id}.json");
        File.WriteAllText(path, obj.ToString(Formatting.Indented));
        _unitsProcessed++;
    }

    void NeverTranslatable(string field) => _neverTranslatable.Add(field);

    JToken DeepCloneOrEmpty(JToken t) => t != null ? t.DeepClone() : new JObject();
    string Str(JObject o, string key) => o?[key]?.ToString() ?? "";
}
