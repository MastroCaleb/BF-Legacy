using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

public class WikiUnitLoader_2 : MonoBehaviour
{
    const string API_BASE = "https://bravefrontierglobal.fandom.com/api.php";
    const string LIST_BASE = "Unit_List";

    [Header("Output")]
    public string outputFolder = "BFUnits";

    [Header("Unit Database")]
    public string unitDatabaseFilename = "unitData.json";

    // ── NEW: filenames for the two unit-master JSON files inside Resources ───
    [Header("Unit Master Files")]
    public string unitMasterFile1 = "F_UNIT_MST_1_Ver1084";   // without .json extension
    public string unitMasterFile2 = "F_UNIT_MST_2_Ver1084";

    [Header("Behaviour")]
    public bool debugLogging = true;
    public int throttleMs        = 300;
    public int rateLimitBackoffS = 30;
    public int maxRetriesPerUrl  = 3;

    private JObject _unitDatabase = null;

    // ── NEW: merged lookup by unitId (string) from the two master files ──────
    // Keys the unit master entries by their "unitId" field so ProcessUnit()
    // can do an O(1) lookup when writing the display-position / sell fields.
    private Dictionary<string, JObject> _unitMasterById = new Dictionary<string, JObject>();

    // The display / sell field names we want to copy from the master JSON.
    // Using a static array avoids any typo divergence between load and write.
    private static readonly string[] UnitMasterFields = new[]
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
    };

    void Awake()
    {
        if (debugLogging) Debug.Log("[WikiUnitLoader] AWAKE()");
    }

    void Start()
    {
        if (debugLogging) Debug.Log("[WikiUnitLoader] START()");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));
        StartCoroutine(LoadDatabaseThenProcess());
    }

    IEnumerator LoadDatabaseThenProcess()
    {
        // ── Load skill database ───────────────────────────────────────────────
        TextAsset dbAsset = EditorDataLoader.LoadJson(unitDatabaseFilename);

        if (dbAsset == null)
        {
            Debug.LogError($"[DB] Could not load unit database from Assets/EditorData: {unitDatabaseFilename}");
            _unitDatabase = new JObject();
        }
        else
        {
            if (debugLogging) Debug.Log($"[DB] Loading unit database: {unitDatabaseFilename}");
            try
            {
                _unitDatabase = JObject.Parse(dbAsset.text);
                if (debugLogging) Debug.Log($"[DB] Loaded {_unitDatabase.Count} unit entries.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DB] Failed to parse unit database JSON: {ex.Message}");
                _unitDatabase = new JObject();
            }
        }

        // ── NEW: Load the two unit-master files and build the merged lookup ───
        LoadUnitMasterFile(unitMasterFile1);
        LoadUnitMasterFile(unitMasterFile2);
        if (debugLogging) Debug.Log($"[UnitMaster] Merged lookup contains {_unitMasterById.Count} entries.");

        yield return StartCoroutine(ProcessAllLists());
    }

    // ── NEW: helper — loads one unit-master Resource file into _unitMasterById ─
    void LoadUnitMasterFile(string resourceName)
    {
        TextAsset asset = EditorDataLoader.LoadJson(resourceName);
        if (asset == null)
        {
            Debug.LogWarning($"[UnitMaster] Could not load '{resourceName}' from Assets/EditorData — skipping.");
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
            if (debugLogging) Debug.Log($"[UnitMaster] '{resourceName}' → added {added} entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnitMaster] Failed to parse '{resourceName}': {ex.Message}");
        }
    }

    #region Request Helpers

    void SetAPIHeaders(UnityWebRequest www)
    {
        www.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        www.SetRequestHeader("Accept", "application/json, text/javascript, */*");
        www.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        www.SetRequestHeader("Accept-Encoding", "identity");
        www.SetRequestHeader("Connection", "keep-alive");
    }

    IEnumerator ThrottledWait()
    {
        float baseWait = throttleMs / 1000f;
        float jitter   = UnityEngine.Random.Range(0f, baseWait * 0.5f);
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

        if (result != null)
            onSuccess(result);
        else
            onFail?.Invoke();
    }

    #endregion

    #region List & Unit Processing

    IEnumerator ProcessAllLists()
    {
        List<string> allUnitTitles = new List<string>();

        List<string> listTitles = new List<string> { LIST_BASE };
        for (int i = 100; true; i += 100)
        {
            listTitles.Add($"{LIST_BASE}:{i}");
            if (i == 1900) i = 6900;
            if (i == 7100) i = 7900;
            if (i == 8700)
            {
                listTitles.Add($"{LIST_BASE}:Other");
                break;
            }
        }

        if (debugLogging) Debug.Log($"[Lists] Fetching {listTitles.Count} list pages via API");

        foreach (string listTitle in listTitles)
        {
            if (debugLogging) Debug.Log($"[Lists] Fetching: {listTitle}");

            string apiUrl = $"{API_BASE}?action=parse&page={UnityWebRequest.EscapeURL(listTitle)}" +
                            $"&prop=links&format=json&disablelimitreport=1";

            string responseText = null;
            yield return StartCoroutine(FetchURL(apiUrl,
                text => responseText = text,
                ()   => Debug.LogWarning($"[Lists] Giving up on: {listTitle}")));

            if (responseText != null)
            {
                try
                {
                    JObject json  = JObject.Parse(responseText);
                    JArray  links = json["parse"]?["links"] as JArray;

                    if (links != null)
                    {
                        foreach (var link in links)
                        {
                            string title = link["*"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(title)) continue;
                            if (title.Contains(":")) continue;
                            if (!IsEnglish(title.Replace(" ", "_"))) continue;
                            if (!allUnitTitles.Contains(title)) allUnitTitles.Add(title);
                        }
                        if (debugLogging) Debug.Log($"[Lists] {allUnitTitles.Count} unique units so far");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Lists] JSON parse error for {listTitle}: {ex.Message}");
                }
            }

            yield return StartCoroutine(ThrottledWait());
        }

        if (debugLogging) Debug.Log($"[Lists] Total English unit pages found: {allUnitTitles.Count}");

        foreach (string title in allUnitTitles)
        {
            yield return StartCoroutine(ProcessUnit(title));
            yield return StartCoroutine(ThrottledWait());
        }

        if (debugLogging) Debug.Log("✔ DONE — All unit JSON files saved!");
    }

    bool IsEnglish(string name) => Regex.IsMatch(name, @"^[A-Za-z0-9_\-() ]+$");

    #endregion

    #region Unit Page Processing

    IEnumerator ProcessUnit(string pageTitle)
    {
        if (debugLogging) Debug.Log($"[Unit] Processing: {pageTitle}");

        string encodedTitle = UnityWebRequest.EscapeURL(pageTitle);

        // ── Step 1: Wikitext → template fields (metadata) ────────────────────
        string wikitextUrl = $"{API_BASE}?action=query&titles={encodedTitle}" +
                             $"&prop=revisions&rvprop=content&rvslots=main&format=json";

        string wikitextJson = null;
        yield return StartCoroutine(FetchURL(wikitextUrl,
            text => wikitextJson = text,
            ()   => Debug.LogWarning($"[Unit] Could not fetch wikitext for: {pageTitle}")));

        if (wikitextJson == null)
        {
            Debug.LogWarning($"[Unit] Skipping — no wikitext: {pageTitle}");
            yield break;
        }

        string wikitext = "";
        try
        {
            JObject qJson = JObject.Parse(wikitextJson);
            JToken  page  = qJson["query"]?["pages"]?.First?.First;
            wikitext = page?["revisions"]?[0]?["slots"]?["main"]?["*"]?.ToString()
                    ?? page?["revisions"]?[0]?["*"]?.ToString()
                    ?? "";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Unit] Wikitext JSON parse error for {pageTitle}: {ex.Message}");
            yield break;
        }

        if (string.IsNullOrEmpty(wikitext))
        {
            Debug.LogWarning($"[Unit] Empty wikitext for: {pageTitle}");
            yield break;
        }

        Dictionary<string, string> fields = ParseFieldsFromTemplate(wikitext);
        if (debugLogging) Debug.Log($"[Unit] Parsed {fields.Count} template fields for '{pageTitle}'");

        string id = fields.ContainsKey("id") ? CleanWikiText(fields["id"]) : "";

        // ── Step 2: Skill data from pre-loaded JSON database ─────────────────
        JToken dbEntry = null;
        if (!string.IsNullOrEmpty(id))
        {
            dbEntry = _unitDatabase[id];
            if (dbEntry == null)
                dbEntry = _unitDatabase[id.TrimStart('0')];
            if (dbEntry == null && int.TryParse(id, out int idInt))
                dbEntry = _unitDatabase[idInt.ToString()];
        }

        if (dbEntry != null)
            if (debugLogging) Debug.Log($"[Unit] Found skill-database entry for id={id}");
        else
            if (debugLogging) Debug.Log($"[Unit] No skill-database entry for id={id} — saving wiki metadata only");

        // ── Step 3: Display / sell data from unit-master lookup ───────────────
        // Try the id as-is, then without leading zeros, then numeric string.
        JObject masterEntry = null;
        if (!string.IsNullOrEmpty(id))
        {
            if (!_unitMasterById.TryGetValue(id, out masterEntry))
            {
                string trimmed = id.TrimStart('0');
                if (!string.IsNullOrEmpty(trimmed) && trimmed != id)
                    _unitMasterById.TryGetValue(trimmed, out masterEntry);
            }
            if (masterEntry == null && int.TryParse(id, out int idInt2))
                _unitMasterById.TryGetValue(idInt2.ToString(), out masterEntry);
        }

        if (masterEntry != null)
            if (debugLogging) Debug.Log($"[Unit] Found unit-master entry for id={id}");
        else
            if (debugLogging) Debug.Log($"[Unit] No unit-master entry for id={id} — display/sell fields will be empty strings");

        // ── Step 4: Build output JObject ──────────────────────────────────────
        JObject obj = new JObject();

        obj["unitName"] = pageTitle;
        foreach (var kvp in fields)
        {
            if (!kvp.Key.Equals("summon") && !kvp.Key.Equals("fusion") && !kvp.Key.Equals("evolution"))
                obj[kvp.Key] = CleanWikiText(kvp.Value);
            else
                obj[kvp.Key] = kvp.Value;
        }

        // Skill sections from skill database
        if (dbEntry != null)
        {
            JToken ls = dbEntry["leader skill"];
            obj["leader_skill"] = ls != null ? ls.DeepClone() : new JObject();

            JToken es = dbEntry["extra skill"];
            obj["extra_skill"] = es != null ? es.DeepClone() : new JObject();

            JToken bb = dbEntry["bb"];
            obj["bb"] = bb != null ? bb.DeepClone() : new JObject();

            JToken sbb = dbEntry["sbb"];
            obj["sbb"] = sbb != null ? sbb.DeepClone() : new JObject();

            JToken ubb = dbEntry["ubb"];
            obj["ubb"] = ubb != null ? ubb.DeepClone() : new JObject();

            JToken stats = dbEntry["stats"];
            obj["stats"] = stats != null ? stats.DeepClone() : new JObject();
        }
        else
        {
            obj["leader_skill"] = new JObject();
            obj["extra_skill"]  = new JObject();
            obj["bb"]           = new JObject();
            obj["sbb"]          = new JObject();
            obj["ubb"]          = new JObject();
            obj["stats"]        = new JObject();
        }

        // ── NEW: Display positions + sell data from unit-master ───────────────
        // sellCaution is stored as an int (0 or 1) in the source; we write it
        // as a bool so consumers get a proper JSON boolean rather than a string.
        foreach (string field in UnitMasterFields)
        {
            if (masterEntry != null && masterEntry.ContainsKey(field))
            {
                JToken raw = masterEntry[field];

                if (field == "sellCaution")
                {
                    // Normalise to a proper JSON boolean
                    bool cautionBool = raw?.Type == JTokenType.Boolean
                        ? raw.Value<bool>()
                        : raw?.ToString() != "0";
                    obj[field] = cautionBool;
                }
                else
                {
                    obj[field] = raw?.DeepClone() ?? JValue.CreateNull();
                }
            }
            else
            {
                // Guarantee the key always exists so consumers don't have to
                // null-check. Use sensible typed defaults.
                obj[field] = field == "sellCaution" ? (JToken)false
                           : field == "sellPrice"   ? (JToken)""
                           : (JToken)"";
            }
        }

        // ── Step 5: Write file ────────────────────────────────────────────────
        string fallbackId = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
        string json = obj.ToString(Formatting.Indented);
        string path = Path.Combine(Application.dataPath, outputFolder, $"unit_{fallbackId}.json");
        File.WriteAllText(path, json);

        if (debugLogging) Debug.Log($"[Unit] SAVED → {path}");
    }

    #endregion

    #region Template Parsing & Cleaning

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

    #region Shared Data

    public static readonly HashSet<string> ValidElements = new()
    {
        "fire", "water", "earth", "thunder", "light", "dark"
    };

    public static readonly string[] AllAilmentsPublic =
    {
        "Poison", "Weakness", "Injury", "Sickness", "Curse", "Paralysis"
    };

    static readonly string[] AllAilments =
    {
        "Poison", "Weakness", "Injury", "Sickness", "Curse", "Paralysis"
    };

    #endregion
}