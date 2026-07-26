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

public class WikiUnitLoader : MonoBehaviour
{
    const string API_BASE = "https://bravefrontierglobal.fandom.com/api.php";
    const string LIST_BASE = "Unit_List";

    [Header("Output")]
    public string outputFolder = "BFUnits";

    [Header("Behaviour")]
    public bool debugLogging = true;
    public int throttleMs        = 300;  // ms between normal requests — 100 was too aggressive
    public int rateLimitBackoffS = 30;   // seconds to wait after a 403/429 before retrying
    public int maxRetriesPerUrl  = 3;    // how many times to retry a failed list page

    void Awake()
    {
        if (debugLogging) Debug.Log("[WikiUnitLoader] AWAKE()");
    }

    void Start()
    {
        if (debugLogging) Debug.Log("[WikiUnitLoader] START() - creating output folder and starting process");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));
        StartCoroutine(ProcessAllLists());
    }

    #region Request Helpers

    // The MediaWiki API endpoint is much less aggressively rate-limited than
    // the rendered HTML pages, and returns clean JSON so no HTML parsing is
    // needed for the list pages or the wikitext. The rendered HTML is still
    // fetched via the API's action=parse call so HtmlAgilityPack keeps working.

    // Build a minimal but realistic header set for API calls.
    // The API is more lenient than the CDN but we still send a proper UA.
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

    // Throttle with random jitter so requests don't arrive at a machine-perfect
    // fixed interval. Jitter adds up to 50% of the base wait.
    IEnumerator ThrottledWait()
    {
        float baseWait = throttleMs / 1000f;
        float jitter   = UnityEngine.Random.Range(0f, baseWait * 0.5f);
        yield return new WaitForSeconds(baseWait + jitter);
    }

    // Exponential backoff on 403/429. Doubles each consecutive hit, capped at 5 min.
    // Call ResetRateLimit() on any successful response.
    int _consecutiveRateLimits = 0;

    IEnumerator HandleRateLimit(string url)
    {
        _consecutiveRateLimits++;
        float wait = Mathf.Min(rateLimitBackoffS * Mathf.Pow(2f, _consecutiveRateLimits - 1), 300f);
        Debug.LogWarning($"[RateLimit] Hit #{_consecutiveRateLimits} on {url} — backing off {wait:0}s");
        yield return new WaitForSeconds(wait);
    }

    void ResetRateLimit() => _consecutiveRateLimits = 0;

    // Wrapper: fire a GET request and return the response text, or null on failure.
    // Handles retries and rate-limit backoff automatically.
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

        // Build the list of Unit_List page titles to fetch.
        // The wiki has pages: Unit_List, Unit_List:100 ... :1900, then :7000 ... :8700,
        // and Unit_List:Other for miscellaneous entries.
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

            // action=parse&prop=links returns all wiki links on the page as JSON —
            // no HTML parsing needed, and this endpoint is much less rate-limited.
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
                            if (title.Contains(":")) continue;           // skip categories/files
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

    // IsEnglish is still used to filter non-English unit names from the link list.
    bool IsEnglish(string name) => Regex.IsMatch(name, @"^[A-Za-z0-9_\-() ]+$");

    #endregion

    #region Unit Page Processing

    IEnumerator ProcessUnit(string pageTitle)
    {
        if (debugLogging) Debug.Log($"[Unit] Processing: {pageTitle}");

        string encodedTitle = UnityWebRequest.EscapeURL(pageTitle);

        // ── Step 1: Wikitext via action=query&prop=revisions ──────────────────
        // This is equivalent to the old ?action=edit page scrape but returns
        // clean JSON and uses a separate API rate-limit pool.
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
            // Pages are keyed by page ID (or "-1" for missing). First() gives us the only entry.
            JToken page = qJson["query"]?["pages"]?.First?.First;
            // MediaWiki API returns content under slots.main in newer versions,
            // falling back to the legacy * key for older Fandom wikis.
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

        if (debugLogging) Debug.Log($"[Unit] Wikitext length for '{pageTitle}': {wikitext.Length}");

        // ParseFieldsFromTemplate is completely unchanged — same wikitext string.
        Dictionary<string, string> fields = ParseFieldsFromTemplate(wikitext);
        if (debugLogging) Debug.Log($"[Unit] Parsed {fields.Count} fields from template for '{pageTitle}'");

        yield return StartCoroutine(ThrottledWait());

        // ── Step 2: Rendered HTML via action=parse&prop=text ─────────────────
        // Returns the fully rendered wiki HTML inside a JSON wrapper.
        // We unwrap it and pass to HtmlAgilityPack exactly as before — 
        // ParseEffectsFromRenderedPage is completely unchanged.
        string parseUrl = $"{API_BASE}?action=parse&page={encodedTitle}" +
                          $"&prop=text&disablelimitreport=1&format=json";

        string parseJson = null;
        yield return StartCoroutine(FetchURL(parseUrl,
            text => parseJson = text,
            ()   => Debug.LogWarning($"[Unit] Could not fetch rendered HTML for: {pageTitle}")));

        HtmlDocument docPage = null;
        if (parseJson != null)
        {
            try
            {
                JObject pJson = JObject.Parse(parseJson);
                string  html  = pJson["parse"]?["text"]?["*"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(html))
                {
                    docPage = new HtmlDocument();
                    docPage.LoadHtml(html);
                    if (debugLogging) Debug.Log($"[Unit] Rendered HTML loaded for '{pageTitle}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Unit] Parse JSON error for {pageTitle}: {ex.Message}");
            }
        }

        if (docPage == null)
            Debug.LogWarning($"[Unit] Proceeding without rendered page for '{pageTitle}' — effect tables will be empty");

        // ── Step 3: Build and save JSON (completely unchanged) ────────────────
        string id = fields.ContainsKey("id") ? fields["id"] : Guid.NewGuid().ToString();

        var effects = ParseEffectsFromRenderedPage(docPage, id);

        JObject obj = new JObject();
        obj["unitName"] = pageTitle;
        foreach (var kvp in fields)
            obj[kvp.Key] = CleanWikiText(kvp.Value);

        foreach (var kv in effects)
        {
            JArray arr = new JArray();
            foreach (var e in kv.Value)
            {
                JObject parsed = new JObject
                {
                    ["effect_type"]   = e.Parsed.effect_type,
                    ["description"]   = e.Parsed.description,
                    ["magnitudes"]    = JObject.FromObject(e.Parsed.magnitudes),
                    ["stats"]         = new JArray(e.Parsed.stats),
                    ["chance"]        = e.Parsed.chance,
                    ["duration"]      = e.Parsed.duration,
                    ["target"]        = e.Parsed.target,
                    ["elements"]      = new JArray(e.Parsed.elements),
                    ["required_item"] = e.Parsed.required_item,
                    ["is_random"]     = e.Parsed.is_random,
                    ["limits"]        = JObject.FromObject(e.Parsed.limits)
                };

                JObject eobj = new JObject
                {
                    ["name"]     = e.Name,
                    ["value"]    = e.Value,
                    ["duration"] = e.Duration,
                    ["target"]   = e.Target,
                    ["parsed"]   = parsed
                };
                arr.Add(eobj);
            }
            obj[kv.Key] = arr;
        }

        string json = obj.ToString(Formatting.Indented);
        string path = Path.Combine(Application.dataPath, outputFolder, $"unit_{id}.json");
        File.WriteAllText(path, json);

        if (debugLogging) Debug.Log($"[Unit] SAVED JSON → {path} (effects sections: {effects.Count})");
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

        // end already points one past the final '}', so length is simply end - start
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

    #region Effect Parsing

    class EffectEntry
    {
        public string Name;
        public string Value;
        public string Duration;
        public string Target;
        public string UnitId;
        public ParsedEffect Parsed;
    }

    class ParsedEffect
    {
        public string effect_type;
        public string description;
        public List<string> stats = new();
        public Dictionary<string, float> magnitudes = new();
        public Dictionary<string, int> limits = new();
        public float? chance;
        public int? duration;
        public string target;
        public List<string> elements = new();
        public string required_item;
        public bool is_random = false;
    }

    interface IEffectRule
    {
        bool Matches(string name);
        ParsedEffect Parse(string name, string text, string id);
    }

    static readonly List<IEffectRule> EffectRules = new()
    {
        // ── Damage ──────────────────────────────────────────────────────────
        new RegularDamageRule(),
        new RandomTargetDamageRule(),
        new LifestealDamageRule(),
        new ProportionalDamageRule(),
        new MultiElementDamageRule(),
        new NonLethalProportionalDamageRule(),
        new HPScaledDamageV2Rule(),
        new NegativeHPScaledDamageRule(),
        new BBScaledDamageRule(),
        new ConsecutiveDamageRule(),
        new ElementSquadScaledDamageRule(),
        new ElementTargetDamageRule(),
        new PiercingDamageRule(),
        new IgnoreDefDamageRule(),
        new DamageOverTimeRule(),
        new DamageVsStatusAfflictedRule(),

        // ── Healing ─────────────────────────────────────────────────────────
        new BurstHealingRule(),
        new GradualHealingRule(),
        new HealWhenAttackedRule(),
        new HealOnSparkRule(),
        new HPAbsorptionRule(),
        new HCEfficacyBoostRule(),
        new HealOnEnemyDefeatRule(),
        new HealOnBattleWonRule(),

        // ── KO / Survival ───────────────────────────────────────────────────
        new ReviveRule(),
        new ChanceReviveRule(),
        new GuaranteedKOResistanceRule(),
        new ChanceKOResistanceRule(),
        new KOResistanceNegationRule(),

        // ── Status Ailments ─────────────────────────────────────────────────
        new PoisonRule(),
        new WeaknessRule(),
        new InjuryRule(),
        new SicknessRule(),
        new CurseRule(),
        new ParalysisRule(),
        new DoomRule(),

        // ── Status Control ──────────────────────────────────────────────────
        new StatusCleanseRule(),
        new StatusNegationRule(),
        new ParameterReductionNegationRule(),
        new LeaderSkillNegationRule(),
        new ExtraSkillLockRule(),

        // ── Stat Buffs ──────────────────────────────────────────────────────
        new ParameterBoostRule(),
        new MaxHPBoostRule(),
        new BBATKBoostRule(),
        new ParameterConversionRule(),
        new StatConversionRule(),
        new HPConditionalStatBoostRule(),
        new BBConditionalStatBoostRule(),
        new BreakATKLimitRule(),
        new ConditionalParameterBoostRule(),

        // ── Damage Modifiers / Null ─────────────────────────────────────────
        new ElementalMitigationRule(), // must be before generic MitigationRule
        new MitigationRule(),
        new SparkBoostRule(),
        new ElementalCriticalRateBoostRule(), // before generic crit rate
        new CriticalRateBoostRule(),
        new CriticalDamageBoostRule(),
        new ElementalDamageBoostRule(),
        new SparkCriticalRule(),
        new NullCriticalRule(),
        new NullSparkRule(),
        new NullElementalWeaknessRule(),
        new NullIgnoreDefRule(),
        new DamageCounterRule(),
        new HitCountBoostRule(),
        new AddedElementRule(),
        new DamageVulnerabilityRule(),
        new SparkVulnerabilityRule(),

        // ── Defensive / Positional ──────────────────────────────────────────
        new BarrierRule(),
        new ShieldRule(),
        new TauntRule(),
        new StealthRule(),
        new EvadeRule(),
        new GuardBoostRule(),
        new AOENormalAttackRule(),

        // ── BB Gauge / BC / HC ──────────────────────────────────────────────
        new BurstBBGaugeFillRule(),
        new GradualBBGaugeBoostRule(),
        new BBGaugeRefillRule(),
        new BCEfficacyBoostRule(),
        new BCEfficacyReductionRule(),
        new BCDropRateBoostRule(),
        new HCDropRateBoostRule(),
        new HCFillPerTurnRule(),
        new BBFillOnDamageTakenRule(),
        new BBFillOnSparkRule(),
        new BCFillOnGuardRule(),
        new BBCostReductionRule(),
        new BBGaugeUsedReductionRule(),
        new ODGaugeBoostRule(),
        new ODEfficacyBoostRule(),
        new ExtraActionRule(),
        new BBRecastRule(),
        new BBActivationRule(),

        // ── Debuffs on Enemies ──────────────────────────────────────────────
        new ParameterReductionRule(),
        new EnemyBBGaugeReductionRule(),
        new EnemyBCFillRateReductionRule(),
        new AilmentInflictionRule(),
        new AilmentInflictOnCounterRule(),
        new DoomInflictRule(),
        new StatusCounterRule(),

        // ── Resource / Utility ──────────────────────────────────────────────
        new ZelBoostRule(),
        new KarmaBoostRule(),
        new ItemDropBoostRule(),
        new EXPBoostRule(),
        new ABPBoostRule(),
        new CBPBoostRule(),


        // ── Damage (new) ────────────────────────────────────────────────────
        new FixedDamageRule(),

        // ── Healing / Mitigation (new) ──────────────────────────────────────
        new ActiveHealingReductionRule(),
        new PassiveHealingReductionRule(),
        new HCEfficacyReductionRule(),
        new DotMitigationRule(),

        // ── KO / Survival (new) ─────────────────────────────────────────────
        new DamageReductionTo1Rule(),

        // ── Stat Buffs (new) ────────────────────────────────────────────────
        new SelfParameterBoostRule(),
        new SelfMaxHPBoostRule(),
        new SelfParameterConversionRule(),
        new SelfSparkBoostRule(),
        new SelfSparkBoostBasedOnHPRule(),
        new ElementalParameterBoostRule(),
        new GenderParameterBoostRule(),
        new ElementSquadBasedParameterBoostRule(),
        new ParameterBoostBasedOnHPRule(),
        new ParameterBoostForFirstXTurnsRule(),
        new TurnBasedParameterBoostRule(),
        new AttackBoostOnStatusAfflictedFoesRule(),
        new BBATKBoostBasedOnHPRule(),
        new ElementalCriticalDamageBoostRule(),
        new ElementalSparkBoostRule(),

        // ── Damage Modifiers (new) ──────────────────────────────────────────
        new NormalMitigationRule(),
        new NormalAttackMitigationRule(),
        new ChanceMitigationRule(),
        new ElementalMitigationForFirstXTurnsRule(),
        new SpecificDamageMitigationRule(),
        new IgnoreDefenseNegationRule(),

        // ── BB/BC/HC (new) ──────────────────────────────────────────────────
        new BCFillOnCriticalRule(),
        new BCFillOnEnemyDefeatRule(),
        new BCFillWhenAttackingRule(),
        new BCFillAfterDealingDamageRule(),
        new BCFillAfterTakingDamageRule(),
        new BCFillAfterReceivingHCRule(),
        new BCFillWhenAttackedWhileGuardingRule(),
        new GradualODFillRule(),
        new InstantODFillRule(),
        new ODFillRateRule(),
        new IncreasedBBActivationChanceRule(),

        // ── Debuffs (new) ───────────────────────────────────────────────────
        new MaxHPReductionRule(),
        new ParameterReductionAddedToAttackRule(),
        new ParameterReductionCounterRule(),
        new InflictEffectWhenAttackedRule(),
        new StatusInflictionAddedToAttackRule(),
        new StatusInflictionOnCriticalRule(),

        // ── Utility (new) ───────────────────────────────────────────────────
        new EffectDurationBoostRule(),
        new EffectPurgeRule(),

        // ── Conditional wrappers ────────────────────────────────────────────
        new AddedEffectBasedOnHPRule(),
        new AddedEffectToBraveBurstRule(),
        new ConditionalEffectAfterOverdrivingRule(),
        new ConditionalEffectAfterDealingDamageRule(),
        new ConditionalEffectAfterReceivingBCRule(),
        new ConditionalEffectAfterReceivingHCRule(),
        new ConditionalEffectAfterSparkingRule(),
        new ConditionalEffectAfterTakingDamageRule(),
        new ConditionalEffectBasedOnHPRule(),
        new ConditionalEffectOnCriticalRule(),
        new ConditionalEffectOnGuardRule(),

        // ── Resistance ───────────────────────────────────────────────────────
        new DamageResistanceRule(),
        new SparkDamageResistanceRule(),

        // ── Drop Rate Boost (combined BC+HC+Item) ───────────────────────────
        new DropRateBoostRule(),

        // ── Status Infliction ────────────────────────────────────────────────
        new StatusInflictionRule(),

        // ── Generic fallback (MUST be last) ─────────────────────────────────
        new GenericRule(),
    };

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

    #region Effect Rules — Damage

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class RegularDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Regular Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "regular_damage" };
            var m = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["base"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class RandomTargetDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Random Target Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "random_target_damage" };
            var m = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["base"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class LifestealDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Lifesteal Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "lifesteal_damage" };
            var dmgMatch = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (dmgMatch.Success) effect.magnitudes["base"] = float.Parse(dmgMatch.Groups[1].Value);
            var lsMatch = Regex.Match(text, @"drains (\d+)~(\d+)%", RegexOptions.IgnoreCase);
            if (lsMatch.Success)
            {
                effect.magnitudes["lifesteal_min"] = float.Parse(lsMatch.Groups[1].Value);
                effect.magnitudes["lifesteal_max"] = float.Parse(lsMatch.Groups[2].Value);
            }
            return effect;
        }
    }

    class ProportionalDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Proportional Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "proportional_damage" };
            var fixedMatch = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (fixedMatch.Success) { effect.magnitudes["base"] = float.Parse(fixedMatch.Groups[1].Value); effect.is_random = true; }
            var propMatch = Regex.Match(text, @"(\d+)~(\d+)% of enemy HP", RegexOptions.IgnoreCase);
            if (propMatch.Success)
            {
                effect.magnitudes["proportional_min"] = float.Parse(propMatch.Groups[1].Value);
                effect.magnitudes["proportional_max"] = float.Parse(propMatch.Groups[2].Value);
            }
            else
            {
                var singleMatch = Regex.Match(text, @"(\d+)% of enemy HP", RegexOptions.IgnoreCase);
                if (singleMatch.Success)
                {
                    effect.magnitudes["proportional_min"] = float.Parse(singleMatch.Groups[1].Value);
                    effect.magnitudes["proportional_max"] = float.Parse(singleMatch.Groups[1].Value);
                }
            }
            var chanceMatch = Regex.Match(text, @"(\d+)% chance", RegexOptions.IgnoreCase);
            effect.chance = chanceMatch.Success ? float.Parse(chanceMatch.Groups[1].Value) : 100f;
            return effect;
        }
    }

    class MultiElementDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Multi-element Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "multi_element_damage" };
            var dmgMatch = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (dmgMatch.Success) effect.magnitudes["base"] = float.Parse(dmgMatch.Groups[1].Value);
            var elemMatch = Regex.Match(text, @"with\s+([\w\s,]+)\s+element", RegexOptions.IgnoreCase);
            if (elemMatch.Success)
            {
                var elems = elemMatch.Groups[1].Value
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim()).ToList();
                effect.elements.AddRange(elems);
            }
            return effect;
        }
    }

    class NonLethalProportionalDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Non-Lethal Proportional Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "non_lethal_proportional_damage" };
            var fixedMatch = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (fixedMatch.Success) { effect.magnitudes["base"] = float.Parse(fixedMatch.Groups[1].Value); effect.is_random = true; }
            var propMatch = Regex.Match(text, @"(\d+)~(\d+)% of enemy HP", RegexOptions.IgnoreCase);
            if (propMatch.Success)
            {
                effect.magnitudes["proportional_min"] = float.Parse(propMatch.Groups[1].Value);
                effect.magnitudes["proportional_max"] = float.Parse(propMatch.Groups[2].Value);
            }
            else
            {
                var singleMatch = Regex.Match(text, @"(\d+)% of enemy HP", RegexOptions.IgnoreCase);
                if (singleMatch.Success)
                {
                    effect.magnitudes["proportional_min"] = float.Parse(singleMatch.Groups[1].Value);
                    effect.magnitudes["proportional_max"] = float.Parse(singleMatch.Groups[1].Value);
                }
            }
            var chanceMatch = Regex.Match(text, @"(\d+)% chance", RegexOptions.IgnoreCase);
            effect.chance = chanceMatch.Success ? float.Parse(chanceMatch.Groups[1].Value) : 100f;
            return effect;
        }
    }

    // HPScaledDamageRule replaced by HPScaledDamageV2Rule (see new rules section)

        class BBScaledDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("BB Scaled Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_scaled_damage" };
            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+", RegexOptions.IgnoreCase);
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%\s*\*\s*\(number of filled gauge", RegexOptions.IgnoreCase);
            if (scaleMatch.Success) effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);
            return effect;
        }
    }

    class ConsecutiveDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Consecutive Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "consecutive_scaled_damage" };
            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+", RegexOptions.IgnoreCase);
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%\s*\*\s*\(number of consecutive", RegexOptions.IgnoreCase);
            if (scaleMatch.Success) effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);
            var maxMatch = Regex.Match(text, @"max\s*(\d+)\s*times", RegexOptions.IgnoreCase);
            if (maxMatch.Success) effect.limits = new Dictionary<string, int> { ["max_stacks"] = int.Parse(maxMatch.Groups[1].Value) };
            return effect;
        }
    }

    class ElementSquadScaledDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Element Squad-scaled Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "element_squad_scaled_damage", elements = new List<string>(), magnitudes = new Dictionary<string, float>() };
            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+");
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%");
            if (scaleMatch.Success) effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);
            var parenMatch = Regex.Match(text, @"\(([^)]+)\)");
            if (!parenMatch.Success) return effect;
            string inside = parenMatch.Groups[1].Value.ToLowerInvariant();
            inside = Regex.Replace(inside, @"number\s+of\s+", "");
            inside = Regex.Replace(inside, @"\s+units?\s+in\s+party", "");
            inside = inside.Replace(" and ", ",");
            foreach (var p in inside.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string e = p.Trim();
                if (ValidElements.Contains(e)) effect.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
            }
            return effect;
        }
    }

    class ElementTargetDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Element Target Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "element_target_damage", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["base"] = float.Parse(m.Groups[1].Value);
            string elements = ElementTargetUnitList(id);
            foreach (var p in elements.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string e = p.Trim();
                if (ValidElements.Contains(e)) effect.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
            }
            return effect;
        }

        private string ElementTargetUnitList(string id)
        {
            var mapping = new Dictionary<string, string>
            {
                { "unit_11047", "earth" }, { "unit_810048", "fire, earth" }, { "unit_810588", "fire, earth" },
                { "unit_20967", "fire" },  { "unit_20987", "fire, water" }, { "unit_820048", "fire, water" },
                { "unit_820548", "fire" }, { "unit_830048", "earth, thunder" }, { "unit_830298", "thunder" },
                { "unit_40937", "water" }, { "unit_840048", "water, thunder" }, { "unit_840588", "water, thunder" },
                { "unit_50167", "dark" },  { "unit_51107", "dark" }, { "unit_850048", "light, dark" },
                { "unit_850508", "light, dark" }, { "unit_850548", "dark" }, { "unit_60177", "light" },
                { "unit_61087", "light" }, { "unit_61147", "light" }, { "unit_860048", "light, dark" },
                { "unit_11046", "earth" }, { "unit_20966", "fire" }, { "unit_20986", "fire" },
                { "unit_830297", "thunder" }, { "unit_40936", "water" }, { "unit_51106", "dark" },
                { "unit_850507", "dark" }, { "unit_61086", "light" }, { "unit_61146", "light" },
            };
            return mapping.ContainsKey("unit_" + id) ? mapping["unit_" + id] : "";
        }
    }

    class PiercingDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Piercing Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "piercing_damage", elements = new List<string>(), magnitudes = new Dictionary<string, float>() };
            var baseMatch = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var pierceMatch = Regex.Match(text, @"deals\s+(\d+)%\s+piercing damage", RegexOptions.IgnoreCase);
            if (pierceMatch.Success) effect.magnitudes["piercing"] = float.Parse(pierceMatch.Groups[1].Value);
            var againstMatch = Regex.Match(text, @"against\s+(.+?)\s+enemies", RegexOptions.IgnoreCase);
            if (againstMatch.Success)
            {
                string elementPart = againstMatch.Groups[1].Value.ToLowerInvariant().Replace(" and ", ",");
                foreach (var p in elementPart.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string e = p.Trim();
                    if (ValidElements.Contains(e)) effect.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
                }
            }
            return effect;
        }
    }

    class NegativeHPScaledDamageRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Negative HP-scaled Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "negative_hp_scaled_damage", magnitudes = new Dictionary<string, float>() };
            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+", RegexOptions.IgnoreCase);
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%\s*\*\s*\(percentage of hp lost\)", RegexOptions.IgnoreCase);
            if (scaleMatch.Success) effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);
            return effect;
        }
    }

    // ── New damage rules ─────────────────────────────────────────────────────

    class IgnoreDefDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Ignore Defense", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Ignore Def", StringComparison.OrdinalIgnoreCase) && name.Contains("Damage", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "ignore_def" };
            // "X% chance of ignoring Defense" or "X% chance of ignoring enemy's Def"
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);
            // damage modifier when attached to a damage skill
            var dmgM = Regex.Match(text, @"(\d+)%\s*damage modifier", RegexOptions.IgnoreCase);
            if (dmgM.Success) effect.magnitudes["base"] = float.Parse(dmgM.Groups[1].Value);
            return effect;
        }
    }

    class DamageOverTimeRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Damage over Time", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Damage each turn", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_over_time" };
            // "X% DoT modifier"
            var dotM = Regex.Match(text, @"(\d+)%\s+DoT\s+modifier", RegexOptions.IgnoreCase);
            if (dotM.Success) { effect.magnitudes["dot_modifier"] = float.Parse(dotM.Groups[1].Value); return effect; }
            // Flat range
            var rangeM = Regex.Match(text, @"(\d+)\s*~\s*(\d+)");
            if (rangeM.Success) { effect.magnitudes["flat_min"] = float.Parse(rangeM.Groups[1].Value); effect.magnitudes["flat_max"] = float.Parse(rangeM.Groups[2].Value); }
            // % of HP
            var pctM = Regex.Match(text, @"(\d+)%\s*(?:of\s*)?(?:max\s*)?HP", RegexOptions.IgnoreCase);
            if (pctM.Success) effect.magnitudes["hp_percent"] = float.Parse(pctM.Groups[1].Value);
            return effect;
        }
    }

    class DamageVsStatusAfflictedRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Damage vs", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Afflicted", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Status", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_vs_status_afflicted", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["bonus"] = float.Parse(m.Groups[1].Value);
            foreach (var ailment in AllAilments)
                if (text.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(ailment.ToLower());
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Healing

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class BurstHealingRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Burst Healing", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "burst_healing" };
            // "Heals X~Y + Z% Rec of HP instantly"
            var full = Regex.Match(text,
                @"[Hh]eals?\s+(\d+)~(\d+)\s*\+\s*(\d+)%\s*Rec",
                RegexOptions.IgnoreCase);
            if (full.Success)
            {
                effect.magnitudes["flat_min"]       = float.Parse(full.Groups[1].Value);
                effect.magnitudes["flat_max"]       = float.Parse(full.Groups[2].Value);
                effect.magnitudes["rec_percentage"] = float.Parse(full.Groups[3].Value);
                return effect;
            }
            var rangeMatch = Regex.Match(text, @"(\d+)~(\d+)");
            if (rangeMatch.Success)
            {
                effect.magnitudes["flat_min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["flat_max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            var recMatch = Regex.Match(text, @"(\d+)%\s*Rec", RegexOptions.IgnoreCase);
            if (recMatch.Success) effect.magnitudes["rec_percentage"] = float.Parse(recMatch.Groups[1].Value);
            return effect;
        }
    }

    class GradualHealingRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Gradual Healing", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "gradual_healing" };
            // "Heals X~Y + Z% Rec of HP each turn"
            var full = Regex.Match(text,
                @"[Hh]eals?\s+(\d+)~(\d+)\s*\+\s*(\d+)%\s*Rec",
                RegexOptions.IgnoreCase);
            if (full.Success)
            {
                effect.magnitudes["flat_min"]       = float.Parse(full.Groups[1].Value);
                effect.magnitudes["flat_max"]       = float.Parse(full.Groups[2].Value);
                effect.magnitudes["rec_percentage"] = float.Parse(full.Groups[3].Value);
                return effect;
            }
            // Fallback: bare "X~Y" range
            var rangeMatch = Regex.Match(text, @"(\d+)~(\d+)");
            if (rangeMatch.Success)
            {
                effect.magnitudes["flat_min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["flat_max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            var recMatch = Regex.Match(text, @"(\d+)%\s*Rec", RegexOptions.IgnoreCase);
            if (recMatch.Success) effect.magnitudes["rec_percentage"] = float.Parse(recMatch.Groups[1].Value);
            return effect;
        }
    }

    class HealWhenAttackedRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Heal when attacked", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "heal_on_hit" };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            // Range: "healing/recovering X~Y% of damage taken as HP"
            var rangeMatch = Regex.Match(text,
                @"(?:healing|recovering)\s+(\d+)~(\d+)%\s+of\s+damage\s+taken",
                RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                effect.magnitudes["heal_ratio_min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["heal_ratio_max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            else
            {
                // Single: "healing/recovering X% of damage taken as HP"
                var single = Regex.Match(text,
                    @"(?:healing|recovering)\s+(\d+)%\s+of\s+damage\s+taken",
                    RegexOptions.IgnoreCase);
                if (single.Success) effect.magnitudes["heal_ratio"] = float.Parse(single.Groups[1].Value);
            }
            return effect;
        }
    }

    class HealOnSparkRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Heal on Spark", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "heal_on_spark" };
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);

            // "Heals X~Y HP on spark", "Recovers X~Y HP per spark",
            // "X% chance of recovering X~Y HP per spark"
            var rangeM = Regex.Match(text,
                @"(?:[Hh]eals?|[Rr]ecovers?)\s+(\d+)~(\d+)\s*HP",
                RegexOptions.IgnoreCase);
            if (rangeM.Success)
            {
                effect.magnitudes["heal_min"] = float.Parse(rangeM.Groups[1].Value);
                effect.magnitudes["heal_max"] = float.Parse(rangeM.Groups[2].Value);
                return effect;
            }
            // Fallback: chance clause already stripped above — try raw "X~Y HP"
            var bare = Regex.Match(text, @"(\d+)~(\d+)\s*HP", RegexOptions.IgnoreCase);
            if (bare.Success)
            {
                effect.magnitudes["heal_min"] = float.Parse(bare.Groups[1].Value);
                effect.magnitudes["heal_max"] = float.Parse(bare.Groups[2].Value);
            }
            return effect;
        }
    }

    class HPAbsorptionRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("HP Absorption", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hp_absorption" };
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);

            // Percent drain: "Drains X~Y% of damage dealt as HP"
            //                "X% chance of draining X~Y% of damage dealt as HP"
            var pctM = Regex.Match(text, @"[Dd]rain(?:s|ing)?\s+(\d+)~(\d+)%\s+of\s+damage", RegexOptions.IgnoreCase);
            if (pctM.Success)
            {
                effect.magnitudes["drain_percent_min"] = float.Parse(pctM.Groups[1].Value);
                effect.magnitudes["drain_percent_max"] = float.Parse(pctM.Groups[2].Value);
                return effect;
            }

            // Flat recovery: "Recovers X~Y damage dealt as HP"
            //                "X% chance of recovering X~Y damage dealt as HP"
            var flatM = Regex.Match(text, @"[Rr]ecover(?:s|ing)?\s+(\d+)~(\d+)\s+damage(?:\s+dealt)?\s+as\s+HP", RegexOptions.IgnoreCase);
            if (flatM.Success)
            {
                effect.magnitudes["heal_min"] = float.Parse(flatM.Groups[1].Value);
                effect.magnitudes["heal_max"] = float.Parse(flatM.Groups[2].Value);
            }
            return effect;
        }
    }

    class HCEfficacyBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("HC Efficacy", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Reduction");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hc_efficacy_boost" };
            // "X% boost to HC Efficacy" or just "X% boost to HC Efficacy"
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+HC\s+Efficacy", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["hc_efficacy"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class HealOnEnemyDefeatRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Heal on Enemy Defeat", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "heal_on_enemy_defeat", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)[~]?(\d+)?%?\s*HP", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["min"] = float.Parse(m.Groups[1].Value);
                effect.magnitudes["max"] = m.Groups[2].Success ? float.Parse(m.Groups[2].Value) : effect.magnitudes["min"];
            }
            return effect;
        }
    }

    class HealOnBattleWonRule : IEffectRule
    {
        public bool Matches(string name)
            => name.Contains("Heal on Battle Won", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "heal_on_battle_won", stats = new List<string> { "HP" }, magnitudes = new Dictionary<string, float>(), target = "self" };
            var m = Regex.Match(text, @"(\d+)[~]?(\d+)?%?\s*HP", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["min"] = float.Parse(m.Groups[1].Value);
                effect.magnitudes["max"] = m.Groups[2].Success ? float.Parse(m.Groups[2].Value) : effect.magnitudes["min"];
            }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — KO / Survival

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class ReviveRule : IEffectRule
    {
        public bool Matches(string name) => name.Equals("Revive", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "revive" };
    }

    class ChanceReviveRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Chance Revive", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "chance_revive", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var hpMatch = Regex.Match(text, @"reviving with (\d+)% HP", RegexOptions.IgnoreCase);
            if (hpMatch.Success) effect.magnitudes["heal_ratio"] = float.Parse(hpMatch.Groups[1].Value);
            return effect;
        }
    }

    class GuaranteedKOResistanceRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Guaranteed KO Resistance", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "guaranteed_ko_resistance", magnitudes = new Dictionary<string, float>() };
            // "Becomes able to withstand X KO" or "resisting X KO"
            var koMatch = Regex.Match(text, @"(?:withstand|resisting)\s+(\d+)\s+KO", RegexOptions.IgnoreCase);
            if (koMatch.Success) effect.magnitudes["ko_count"] = float.Parse(koMatch.Groups[1].Value);
            var hpRestoreMatch = Regex.Match(text, @"restores\s+(\d+)%\s+of\s+unit'?s\s+HP", RegexOptions.IgnoreCase);
            if (hpRestoreMatch.Success) effect.magnitudes["hp_restore_percent"] = float.Parse(hpRestoreMatch.Groups[1].Value);
            return effect;
        }
    }

    class ChanceKOResistanceRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Chance KO Resistance", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "ko_resistance", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var koMatch = Regex.Match(text, @"resisting\s+(\d+)\s+KO", RegexOptions.IgnoreCase);
            if (koMatch.Success) effect.magnitudes["ko_count"] = float.Parse(koMatch.Groups[1].Value);
            var hpRestoreMatch = Regex.Match(text, @"restores\s+(\d+)%\s+of\s+unit'?s\s+HP", RegexOptions.IgnoreCase);
            if (hpRestoreMatch.Success) effect.magnitudes["hp_restore_percent"] = float.Parse(hpRestoreMatch.Groups[1].Value);
            return effect;
        }
    }

    // ── New KO rules ─────────────────────────────────────────────────────────

    /// <summary>Negates Angel Idol / KO resistance buffs on the party. Enemy-use effect.</summary>
    class KOResistanceNegationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("KO Resistance Negation", StringComparison.OrdinalIgnoreCase) ||
            ((name.Contains("KO Resistance", StringComparison.OrdinalIgnoreCase) ||
              name.Contains("Angel Idol", StringComparison.OrdinalIgnoreCase)) &&
             (name.Contains("Negat") || name.Contains("Null") || name.Contains("Invalid")));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "ko_resistance_negation" };
            // "For each foe, X% chance of negating KO Resistance effects (Y% chance max.)"
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+negating", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);
            var maxM = Regex.Match(text, @"\((\d+)%\s+chance\s+max\.", RegexOptions.IgnoreCase);
            if (maxM.Success) effect.magnitudes["max_chance"] = float.Parse(maxM.Groups[1].Value);
            if (text.Contains("each foe", StringComparison.OrdinalIgnoreCase)) effect.description = "per_foe";
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Status Ailments

    class PoisonRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Poison", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "poison" };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.magnitudes["hp_percent_per_turn"] = 10f;
            effect.duration = 3;
            return effect;
        }
    }

    class WeaknessRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Weakness", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "weakness", stats = new List<string> { "def" } };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.magnitudes["reduction"] = 50f;
            effect.duration = 3;
            return effect;
        }
    }

    class InjuryRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Injury", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "injury", stats = new List<string> { "atk" } };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.magnitudes["reduction"] = 50f;
            effect.duration = 3;
            return effect;
        }
    }

    class SicknessRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Sickness", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "sickness", stats = new List<string> { "rec" } };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.magnitudes["reduction"] = 50f;
            effect.duration = 3;
            return effect;
        }
    }

    class CurseRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Curse", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Cleanse") && !name.Contains("Negate");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "curse" };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.duration = 1;
            return effect;
        }
    }

    class ParalysisRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Paralysis", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "paralysis" };
            var m = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (m.Success) effect.chance = float.Parse(m.Groups[1].Value);
            effect.duration = 1;
            return effect;
        }
    }

    /// <summary>Doom: countdown timer KO. Cannot be blocked by Angel Idol.</summary>
    class DoomRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Doom", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Negate") && !name.Contains("Resist") && !name.Contains("Inflict");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "doom", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var countMatch = Regex.Match(text, @"countdown[:\s]+(\d+)", RegexOptions.IgnoreCase);
            if (countMatch.Success)
                effect.magnitudes["countdown"] = float.Parse(countMatch.Groups[1].Value);
            else
            {
                var fallback = Regex.Match(text, @"(\d+)\s*turn", RegexOptions.IgnoreCase);
                if (fallback.Success) effect.magnitudes["countdown"] = float.Parse(fallback.Groups[1].Value);
            }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Status Control

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class StatusCleanseRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Status Cleanse", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Status Cure", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) =>
            new ParsedEffect { effect_type = "status_cleanse" };
    }

    class StatusNegationRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Status Negation", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id) =>
            new ParsedEffect { effect_type = "status_negation" };
    }

    class ParameterReductionNegationRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Parameter Reduction Negation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_reduction_negation", stats = new List<string>() };
            var statMatch = Regex.Match(text, @"Negates\s+(.+?)\s+reduction\s+effects", RegexOptions.IgnoreCase);
            if (statMatch.Success)
            {
                foreach (var s in statMatch.Groups[1].Value.Replace("and", ",", StringComparison.OrdinalIgnoreCase).Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    switch (s.Trim().ToLower())
                    {
                        case "atk": case "attack": effect.stats.Add("atk"); break;
                        case "def": case "defense": effect.stats.Add("def"); break;
                        case "rec": case "recovery": effect.stats.Add("rec"); break;
                    }
                }
            }
            return effect;
        }
    }

    // ── New status control rules ─────────────────────────────────────────────

    /// <summary>Enemy effect that disables a unit's Leader Skill for N turns (or whole battle).</summary>
    class LeaderSkillNegationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Leader Skill", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Negat") || name.Contains("Disabl") || name.Contains("Invalid"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "leader_skill_negation" };
            var durMatch = Regex.Match(text, @"(\d+)\s*turn", RegexOptions.IgnoreCase);
            effect.duration = durMatch.Success ? int.Parse(durMatch.Groups[1].Value) : -1; // -1 = whole battle
            return effect;
        }
    }

    /// <summary>Enemy debuff that disables a unit's Extra Skill for N turns.</summary>
    class ExtraSkillLockRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Extra Skill", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Lock") || name.Contains("Disabl") || name.Contains("Negat"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "extra_skill_lock" };
            var durMatch = Regex.Match(text, @"(\d+)\s*turn", RegexOptions.IgnoreCase);
            if (durMatch.Success) effect.duration = int.Parse(durMatch.Groups[1].Value);
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Stat Buffs

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class ParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Parameter Boost", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("HP-conditional") &&
            !name.Contains("BB-conditional") &&
            !name.Contains("based on HP") &&
            !name.Contains("First X Turns") &&
            !name.Contains("Turn-based") &&
            !name.Contains("Element Squad");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var pe = new ParsedEffect { effect_type = "parameter_boost" };

            // Strip element clause before parsing stats so "of Fire units" doesn't interfere
            var elemMatch = Regex.Match(text, @"of\s+([\w\s,]+)\s+units", RegexOptions.IgnoreCase);
            string preText = elemMatch.Success ? text.Substring(0, elemMatch.Index) : text;
            // Also strip "Guild Raid Only" prefix
            preText = Regex.Replace(preText, @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

            // Three-step stat parser covers all real patterns:
            // A: "X% boost to Atk"  /  "X% boost to Atk and X% boost to Def"
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!pe.magnitudes.ContainsKey(stat)) pe.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!pe.stats.Contains(stat)) pe.stats.Add(stat);
            }

            // B: "Boosts Atk, Def and Rec by X%"  (shared single value)
            foreach (Match m in Regex.Matches(preText,
                @"[Bb]oosts?\s+(?:own\s+)?((?:(?:HP|Atk|Def|Rec|critical\s+rate)(?:\s*[,/]\s*|\s+and\s+))+(?:HP|Atk|Def|Rec|critical\s+rate))\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                float val = float.Parse(m.Groups[2].Value);
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec|critical\s+rate", RegexOptions.IgnoreCase))
                {
                    string stat = sm.Value.Trim().ToLower().Replace(" ", "_");
                    if (!pe.magnitudes.ContainsKey(stat)) pe.magnitudes[stat] = val;
                    if (!pe.stats.Contains(stat)) pe.stats.Add(stat);
                }
            }

            // C: individual "STAT by X%" segments (each has its own value, possibly negative)
            foreach (Match m in Regex.Matches(preText,
                @"(HP|Atk|Def|Rec|critical\s+rate)\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.Trim().ToLower().Replace(" ", "_");
                if (!pe.magnitudes.ContainsKey(stat)) pe.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!pe.stats.Contains(stat)) pe.stats.Add(stat);
            }

            if (elemMatch.Success)
            {
                string raw = elemMatch.Groups[1].Value.ToLowerInvariant().Replace(" and ", ",");
                foreach (var part in raw.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string e = part.Trim();
                    if (ValidElements.Contains(e))
                        pe.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
                }
            }
            return pe;
        }
    }

    // ── New stat buff rules ──────────────────────────────────────────────────

    /// <summary>Boosts max HP by a flat percentage.</summary>
    class MaxHPBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Max HP", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "max_hp_boost", stats = new List<string> { "hp" } };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Boosts ATK specifically during BB/SBB/UBB activation. Doesn't stack with itself across BB/SBB.</summary>
    class BBATKBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BB Atk", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("BB", StringComparison.OrdinalIgnoreCase) &&
             name.Contains("Attack", StringComparison.OrdinalIgnoreCase) &&
             name.Contains("Boost", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_atk_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Party-wide stat conversion (row name "Parameter Conversion").</summary>
    class ParameterConversionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Parameter Conversion", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_conversion" };
            // "Boosts Atk and Def relative to X% of HP"
            var m = Regex.Match(text,
                @"[Bb]oosts?\s+([\w\s,]+)\s+relative\s+to\s+(\d+)%\s+of\s+(\w+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["percent"] = float.Parse(m.Groups[2].Value);
                effect.stats.Add(m.Groups[3].Value.ToLower()); // source stat (HP/Def/Rec/etc.)
                // target stats (Atk, Def, ...)
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec", RegexOptions.IgnoreCase))
                    if (!effect.elements.Contains(sm.Value.ToLower()))
                        effect.elements.Add(sm.Value.ToLower()); // reuse elements for target list
            }
            return effect;
        }
    }

    /// <summary>Converts a % of one stat into a bonus to another (e.g. 30% of DEF as extra ATK).</summary>
    class StatConversionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Stat Conversion", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Convert", StringComparison.OrdinalIgnoreCase) &&
             (name.Contains("Atk") || name.Contains("Def") || name.Contains("Rec") || name.Contains("HP")));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "stat_conversion", magnitudes = new Dictionary<string, float>(), stats = new List<string>() };
            var convMatch = Regex.Match(text, @"(\d+)%\s+of\s+(Atk|Def|Rec|HP)\s+as\s+extra\s+(Atk|Def|Rec|HP)", RegexOptions.IgnoreCase);
            if (convMatch.Success)
            {
                effect.magnitudes["percent"] = float.Parse(convMatch.Groups[1].Value);
                effect.stats.Add(convMatch.Groups[2].Value.ToLower()); // source
                effect.stats.Add(convMatch.Groups[3].Value.ToLower()); // target
            }
            else
            {
                var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>Stat boost only active when HP is above/below a threshold.</summary>
    class HPConditionalStatBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("HP-conditional Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hp_conditional_stat_boost" };

            // Strip "Guild Raid Only" prefix
            string clean = Regex.Replace(text, @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

            // Extract threshold — "when HP is above/below X%" or "when HP is full"
            var threshM = Regex.Match(clean, @"when\s+HP\s+is\s+(above|below|full)\s*(\d+)?%?", RegexOptions.IgnoreCase);
            if (threshM.Success)
            {
                effect.description = threshM.Groups[1].Value.ToLower();
                if (threshM.Groups[2].Success)
                    effect.magnitudes["hp_threshold"] = float.Parse(threshM.Groups[2].Value);
            }

            string preText = threshM.Success ? clean.Substring(0, threshM.Index) : clean;

            // Three-step stat parser covers all real patterns:
            // A: "X% boost to Atk"  /  "X% boost to Atk and X% boost to Def"
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            // B: "Boosts Atk, Def and Rec by X%"  (shared single value)
            foreach (Match m in Regex.Matches(preText,
                @"[Bb]oosts?\s+(?:own\s+)?((?:(?:HP|Atk|Def|Rec|critical\s+rate)(?:\s*[,/]\s*|\s+and\s+))+(?:HP|Atk|Def|Rec|critical\s+rate))\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                float val = float.Parse(m.Groups[2].Value);
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec|critical\s+rate", RegexOptions.IgnoreCase))
                {
                    string stat = sm.Value.Trim().ToLower().Replace(" ", "_");
                    if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = val;
                    if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                }
            }

            // C: individual "STAT by X%" segments (each has its own value, possibly negative)
            foreach (Match m in Regex.Matches(preText,
                @"(HP|Atk|Def|Rec|critical\s+rate)\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            return effect;
        }
    }

    /// <summary>Stat boost only active when BB gauge is above a threshold.</summary>
    class BBConditionalStatBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BB-conditional Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_conditional_stat_boost" };

            string clean = Regex.Replace(text, @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

            var threshM = Regex.Match(clean, @"when\s+BB\s+gauge\s+is\s+(above|full)\s*(\d+)?%?", RegexOptions.IgnoreCase);
            if (threshM.Success)
            {
                effect.description = threshM.Groups[1].Value.ToLower();
                if (threshM.Groups[2].Success)
                    effect.magnitudes["bb_threshold"] = float.Parse(threshM.Groups[2].Value);
            }

            string preText = threshM.Success ? clean.Substring(0, threshM.Index) : clean;

            // Three-step stat parser covers all real patterns:
            // A: "X% boost to Atk"  /  "X% boost to Atk and X% boost to Def"
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            // B: "Boosts Atk, Def and Rec by X%"  (shared single value)
            foreach (Match m in Regex.Matches(preText,
                @"[Bb]oosts?\s+(?:own\s+)?((?:(?:HP|Atk|Def|Rec|critical\s+rate)(?:\s*[,/]\s*|\s+and\s+))+(?:HP|Atk|Def|Rec|critical\s+rate))\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                float val = float.Parse(m.Groups[2].Value);
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec|critical\s+rate", RegexOptions.IgnoreCase))
                {
                    string stat = sm.Value.Trim().ToLower().Replace(" ", "_");
                    if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = val;
                    if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                }
            }

            // C: individual "STAT by X%" segments (each has its own value, possibly negative)
            foreach (Match m in Regex.Matches(preText,
                @"(HP|Atk|Def|Rec|critical\s+rate)\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            return effect;
        }
    }

    /// <summary>Raises the ATK stat cap above 99,999.</summary>
    class BreakATKLimitRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Parameter Limit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Break Atk Limit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Raise Atk Limit", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "break_atk_parameter_limit", magnitudes = new Dictionary<string, float>() };
            var capMatch = Regex.Match(text, @"to\s+([\d,]+)", RegexOptions.IgnoreCase);
            if (capMatch.Success) effect.magnitudes["new_cap"] = float.Parse(capMatch.Groups[1].Value.Replace(",", ""));
            return effect;
        }
    }

    /// <summary>Stat boost scaling with party composition (element count, HP threshold, etc).</summary>
    class ConditionalParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Conditional", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Parameter", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_parameter_boost", magnitudes = new Dictionary<string, float>(), stats = new List<string>(), elements = new List<string>() };
            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+", RegexOptions.IgnoreCase);
            if (baseMatch.Success) effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);
            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%", RegexOptions.IgnoreCase);
            if (scaleMatch.Success) effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);
            foreach (var stat in new[] { "Atk", "Def", "Rec", "HP" })
                if (name.Contains(stat, StringComparison.OrdinalIgnoreCase)) effect.stats.Add(stat.ToLower());
            foreach (var elem in ValidElements)
                if (text.Contains(elem, StringComparison.OrdinalIgnoreCase)) effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Damage Modifiers / Null / Vulnerability

    /// <summary>
    /// Elemental Mitigation: reduces damage from specific elements or all elements.
    /// Must be registered BEFORE MitigationRule so "Elemental Mitigation" doesn't fall through.
    /// Real patterns:
    ///   "Reduces Fire damage by X%"
    ///   "Reduces Fire and Water damage taken by X%"
    ///   "Reduces Fire, Water, Earth and Thunder damage taken by X%"
    ///   "Reduces all elemental damage taken by X%"
    /// </summary>
    class ElementalMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Elemental Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_mitigation" };

            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);

            // "Reduces all elemental damage" → all elements
            if (text.Contains("all elemental", StringComparison.OrdinalIgnoreCase))
            {
                effect.description = "all";
                return effect;
            }

            // "Reduces Fire, Water and Dark damage..." — parse each element name
            var elemSection = Regex.Match(text,
                @"Reduces\s+([\w,\s]+?)\s+damage",
                RegexOptions.IgnoreCase);
            if (elemSection.Success)
            {
                string raw = elemSection.Groups[1].Value
                    .Replace(" and ", ",", StringComparison.OrdinalIgnoreCase);
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string e = part.Trim().ToLowerInvariant();
                    if (ValidElements.Contains(e))
                        effect.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
                }
            }

            // If no specific elements found, treat as all-elemental
            if (effect.elements.Count == 0)
                effect.description = "all";

            return effect;
        }
    }

    /// <summary>
    /// Generic damage reduction / mitigation fallback.
    /// Handles "Damage Mitigation", "Normal Mitigation", "Damage Reduction" row names.
    /// Specific mitigation types (Elemental, Guard, Chance, DoT, First X Turns) are
    /// handled by their own rules registered before this one.
    /// </summary>
    class MitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Damage Reduction", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class SparkBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Spark Boost", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Self Spark Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "spark_boost" };

            // Long-form: "Boosts spark BC drop rate by X%"
            var bc    = Regex.Match(text, @"spark\s+BC\s+drop\s+rate\s+by\s+(\d+)%",    RegexOptions.IgnoreCase);
            var hc    = Regex.Match(text, @"spark\s+HC\s+drop\s+rate\s+by\s+(\d+)%",    RegexOptions.IgnoreCase);
            var dmg   = Regex.Match(text, @"spark\s+damage\s+by\s+(\d+)%",              RegexOptions.IgnoreCase);
            var karma = Regex.Match(text, @"spark\s+Karma\s+drop\s+rate\s+by\s+(\d+)%", RegexOptions.IgnoreCase);
            var zel   = Regex.Match(text, @"spark\s+Zel\s+drop\s+rate\s+by\s+(\d+)%",   RegexOptions.IgnoreCase);
            if (bc.Success)    effect.magnitudes["bc_drop_rate"]    = float.Parse(bc.Groups[1].Value);
            if (hc.Success)    effect.magnitudes["hc_drop_rate"]    = float.Parse(hc.Groups[1].Value);
            if (dmg.Success)   effect.magnitudes["damage"]          = float.Parse(dmg.Groups[1].Value);
            if (karma.Success) effect.magnitudes["karma_drop_rate"] = float.Parse(karma.Groups[1].Value);
            if (zel.Success)   effect.magnitudes["zel_drop_rate"]   = float.Parse(zel.Groups[1].Value);

            // Short-form: "X% boost to spark BC drop rate / spark damage / ..."
            if (!bc.Success)
            {
                var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+spark\s+BC\s+drop\s+rate", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["bc_drop_rate"] = float.Parse(m.Groups[1].Value);
            }
            if (!hc.Success)
            {
                var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+spark\s+HC\s+drop\s+rate", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["hc_drop_rate"] = float.Parse(m.Groups[1].Value);
            }
            if (!dmg.Success)
            {
                var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+[Ss]park\s+damage", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["damage"] = float.Parse(m.Groups[1].Value);
            }
            if (!karma.Success)
            {
                var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+spark\s+Karma\s+drop\s+rate", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["karma_drop_rate"] = float.Parse(m.Groups[1].Value);
            }
            if (!zel.Success)
            {
                var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+spark\s+Zel\s+drop\s+rate", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["zel_drop_rate"] = float.Parse(m.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>Must be registered BEFORE CriticalRateBoostRule since it's more specific.</summary>
    class ElementalCriticalRateBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Critical Rate", StringComparison.OrdinalIgnoreCase) &&
            ValidElements.Any(e => name.Contains(e, StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_critical_rate_boost", magnitudes = new Dictionary<string, float>(), elements = new List<string>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            foreach (var elem in ValidElements)
                if (name.Contains(elem, StringComparison.OrdinalIgnoreCase) || text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            return effect;
        }
    }

    class CriticalRateBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Critical Rate", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Critical", StringComparison.OrdinalIgnoreCase) && name.Contains("Rate"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "critical_rate_boost" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class CriticalDamageBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Critical Damage", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "critical_damage_boost" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class ElementalDamageBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Elemental Damage Boost", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Elemental Weakness Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_damage_boost" };
            string t = Regex.Replace(text, @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);
            var m = Regex.Match(t, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            // Specific element (may be absent for "all elemental damage")
            foreach (var elem in ValidElements)
                if (t.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            if (effect.elements.Count == 0) effect.description = "all";
            return effect;
        }
    }

    /// <summary>Crits during a spark deal extra bonus damage. Separate from regular crit.</summary>
    class SparkCriticalRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Spark", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Critical", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "spark_critical", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var dmgMatch = Regex.Match(text, @"(\d+)%\s*(?:extra|bonus|additional)\s*damage", RegexOptions.IgnoreCase);
            if (dmgMatch.Success) effect.magnitudes["bonus_damage"] = float.Parse(dmgMatch.Groups[1].Value);
            return effect;
        }
    }

    class NullCriticalRule : IEffectRule
    {
        public bool Matches(string name) =>
            (name.Contains("Null", StringComparison.OrdinalIgnoreCase) || name.Contains("Negate", StringComparison.OrdinalIgnoreCase)) &&
            name.Contains("Crit", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "null_critical" };
    }

    class NullSparkRule : IEffectRule
    {
        public bool Matches(string name) =>
            (name.Contains("Null", StringComparison.OrdinalIgnoreCase) || name.Contains("Negate", StringComparison.OrdinalIgnoreCase)) &&
            name.Contains("Spark", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "null_spark" };
    }

    class NullElementalWeaknessRule : IEffectRule
    {
        public bool Matches(string name) =>
            (name.Contains("Null", StringComparison.OrdinalIgnoreCase) || name.Contains("Negate", StringComparison.OrdinalIgnoreCase)) &&
            name.Contains("Elemental", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "null_elemental_weakness" };
    }

    class NullIgnoreDefRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Null", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Ignore", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Def", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "null_ignore_def" };
    }

    /// <summary>Reflects a % (or X~Y%) of incoming damage back to the attacker.</summary>
    class DamageCounterRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Damage Counter", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Counter Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_counter" };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            // Range: "Reflects X~Y% of damage"
            var rangeMatch = Regex.Match(text, @"Reflects\s+(\d+)~(\d+)%\s+of\s+damage", RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                effect.magnitudes["reflect_min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["reflect_max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            else
            {
                // Single: "Reflects X% of damage" or "reflecting X% of damage"
                var single = Regex.Match(text, @"[Rr]eflect(?:s|ing)\s+(\d+)%\s+of\s+damage", RegexOptions.IgnoreCase);
                if (single.Success) effect.magnitudes["reflect_percent"] = float.Parse(single.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>Increases number of hits per attack, yielding more BC drops per action.</summary>
    class HitCountBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Hit Count", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hit_count_boost", magnitudes = new Dictionary<string, float>() };
            var hitMatch = Regex.Match(text, @"\+?\s*(\d+)\s+extra\s+hit", RegexOptions.IgnoreCase);
            if (hitMatch.Success) effect.magnitudes["extra_hits"] = float.Parse(hitMatch.Groups[1].Value);
            return effect;
        }
    }

    class AddedElementRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Add Element", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Added Element", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "add_element" };
            foreach (var elem in ValidElements)
                if (name.Contains(elem, StringComparison.OrdinalIgnoreCase) || text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            return effect;
        }
    }

    /// <summary>
    /// Damage Vulnerability: inflicts elemental and/or critical vulnerability on enemies.
    /// Real: "Inflicts X% elemental and critical vulnerability"
    ///       "X% chance of inflicting X% critical vulnerability"
    /// </summary>
    class DamageVulnerabilityRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Damage Vulnerability", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Vulnerability", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_vulnerability" };

            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);

            // Amplify value: "Inflicts X% elemental..." or "inflicting X% critical..."
            var ampM = Regex.Match(text, @"(\d+)%\s+(?:elemental|critical)", RegexOptions.IgnoreCase);
            if (!ampM.Success) ampM = Regex.Match(text, @"inflicting\s+(\d+)%", RegexOptions.IgnoreCase);
            if (ampM.Success) effect.magnitudes["amplify"] = float.Parse(ampM.Groups[1].Value);

            if (text.Contains("elemental", StringComparison.OrdinalIgnoreCase)) effect.elements.Add("elemental");
            if (text.Contains("critical",  StringComparison.OrdinalIgnoreCase)) effect.elements.Add("critical");
            return effect;
        }
    }

    /// <summary>Debuff on enemy: amplifies spark damage taken. Stacks additively with party spark buffs.</summary>
    class SparkVulnerabilityRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Spark Vulnerability", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Spark", StringComparison.OrdinalIgnoreCase) && name.Contains("Vulnerability", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "spark_vulnerability", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var ampMatch = Regex.Match(text, @"(\d+)%\s*(?:more|increased|additional|extra)\s*spark\s*damage", RegexOptions.IgnoreCase);
            if (ampMatch.Success) effect.magnitudes["amplify"] = float.Parse(ampMatch.Groups[1].Value);
            else { var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase); if (m.Success) effect.magnitudes["amplify"] = float.Parse(m.Groups[1].Value); }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Defensive / Positional

    /// <summary>Flat HP absorb shield. "Activates [Element] barrier with X HP"</summary>
    class BarrierRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Barrier", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "barrier", elements = new List<string>() };
            // "Activates Dark barrier with 50000 HP"
            var hpMatch = Regex.Match(text, @"with\s+([\d,]+)\s+HP", RegexOptions.IgnoreCase);
            if (!hpMatch.Success) hpMatch = Regex.Match(text, @"([\d,]+)\s+HP", RegexOptions.IgnoreCase);
            if (hpMatch.Success) effect.magnitudes["hp"] = float.Parse(hpMatch.Groups[1].Value.Replace(",", ""));
            // Element from text: "Activates Dark barrier..."
            foreach (var elem in ValidElements)
                if (text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            return effect;
        }
    }

    /// <summary>Elemental absorb shield. "Activates Dark Shield (X HP & Y Def)"</summary>
    class ShieldRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Shield", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "shield", elements = new List<string>() };
            // "Activates Dark Shield (50000 HP & 500 Def)"
            var hpMatch  = Regex.Match(text, @"\((\d+)\s+HP\s*&\s*(\d+)\s+Def\)", RegexOptions.IgnoreCase);
            if (hpMatch.Success)
            {
                effect.magnitudes["hp"]  = float.Parse(hpMatch.Groups[1].Value);
                effect.magnitudes["def"] = float.Parse(hpMatch.Groups[2].Value);
            }
            foreach (var elem in ValidElements)
                if (text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));
            // non-elemental shield
            if (text.Contains("non-elemental", StringComparison.OrdinalIgnoreCase))
                effect.elements.Add("all");
            return effect;
        }
    }

    /// <summary>Forces all enemies to target this unit. "Activates Taunt, also boosts Atk by X%..."</summary>
    class TauntRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Taunt", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "taunt", target = "self" };
            // Bonus stats bundled with Taunt
            foreach (Match m in Regex.Matches(text,
                @"boosts?\s+(Atk|Def|Rec|critical\s+rate)\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.ToLower().Replace(" ", "_");
                effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }
            return effect;
        }
    }

    /// <summary>Untargetable. "Activates Stealth, also boosts Atk by X%..."</summary>
    class StealthRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Stealth", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "stealth", target = "self" };
            // Bonus stats bundled with Stealth
            foreach (Match m in Regex.Matches(text,
                @"boosts?\s+(Atk|Def|Rec|critical\s+rate)\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.ToLower().Replace(" ", "_");
                effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }
            return effect;
        }
    }

    /// <summary>% chance to dodge attacks. Row name: "Evasion"</summary>
    class EvadeRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Evade", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Evasion", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dodge", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "evade" };
            // "X% chance of evading hits from an attack (Y% chance of applying)"
            var m = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+evading", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.chance = float.Parse(m.Groups[1].Value);
                // secondary apply-chance
                var applyM = Regex.Match(text, @"\((\d+)%\s+chance\s+of\s+applying\)", RegexOptions.IgnoreCase);
                if (applyM.Success) effect.magnitudes["apply_chance"] = float.Parse(applyM.Groups[1].Value);
            }
            else effect.chance = 100f;
            return effect;
        }
    }

    /// <summary>Extra damage reduction on top of the base 50% guard reduction.</summary>
    class GuardBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Guard", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Boost") || name.Contains("Reduction") || name.Contains("Damage"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "guard_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["extra_reduction"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Normal attacks hit all enemies. "X% chance of attacking all enemies (-Y% AoE damage modifier)"</summary>
    class AOENormalAttackRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("AoE Normal Attack", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Normal Attack", StringComparison.OrdinalIgnoreCase) &&
             (name.Contains("All") || name.Contains("AoE") || name.Contains("AOE")));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "aoe_normal_attack", target = "all_enemies" };
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+attacking", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);
            var dmgM = Regex.Match(text, @"\((-?\d+)%\s+AoE\s+damage\s+modifier\)", RegexOptions.IgnoreCase);
            if (dmgM.Success) effect.magnitudes["damage_modifier"] = float.Parse(dmgM.Groups[1].Value);
            return effect;
        }
    }

    #endregion

    #region Effect Rules — BB Gauge / BC / HC

    // ── Original rules (unchanged) ───────────────────────────────────────────

    class BurstBBGaugeFillRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Burst BB Gauge Fill", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "burst_bb_gauge_fill", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"Boosts\s+BB\s+gauge\s+by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["bc"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class GradualBBGaugeBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Gradual BB Gauge Boost", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_gauge_fill_over_time" };
            // "Boosts BB gauge by X BC each turn" or just "Boosts BB gauge by X BC"
            var m = Regex.Match(text, @"[Bb]oosts?\s+BB\s+gauge\s+by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["bc_per_turn"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class BBGaugeRefillRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("BB Gauge Refill", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_gauge_refill", target = "self" };
            // "Refills BB gauge to X%"
            var pct = Regex.Match(text, @"[Rr]efills?\s+BB\s+gauge\s+to\s+(\d+)%", RegexOptions.IgnoreCase);
            if (pct.Success) { effect.magnitudes["percent"] = float.Parse(pct.Groups[1].Value); return effect; }
            // "Refills BB gauge by X BC"
            var bc = Regex.Match(text, @"[Rr]efills?\s+BB\s+gauge\s+by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (bc.Success) effect.magnitudes["bc"] = float.Parse(bc.Groups[1].Value);
            return effect;
        }
    }

    class BCEfficacyBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BC Efficacy", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Reduction");
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_efficacy_boost" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+BC\s+Efficacy", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class BBFillOnDamageTakenRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill when attacked", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Damage taken boosts BB gauge", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_when_attacked" };
            // "Damage taken has a X% chance of boosting BB gauge by X~Y BC"
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);
            var rangeM = Regex.Match(text, @"by\s+(\d+)~(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (rangeM.Success) { effect.magnitudes["min"] = float.Parse(rangeM.Groups[1].Value); effect.magnitudes["max"] = float.Parse(rangeM.Groups[2].Value); return effect; }
            var singleM = Regex.Match(text, @"by\s+(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (singleM.Success) effect.magnitudes["value"] = float.Parse(singleM.Groups[1].Value);
            return effect;
        }
    }

    // ── New BB/BC/HC rules ───────────────────────────────────────────────────

    /// <summary>Enemy debuff: reduces how much BB gauge allies fill per BC collected.</summary>
    class BCEfficacyReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BC Efficacy", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_efficacy_reduction", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Increases the rate at which enemies drop Battle Crystals.</summary>
    class BCDropRateBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BC Drop", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Fill") && !name.Contains("Gauge");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_drop_rate_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Increases the rate at which enemies drop Heal Crystals.</summary>
    class HCDropRateBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("HC Drop", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hc_drop_rate_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Automatically adds HC to the party's healing pool each turn.</summary>
    class HCFillPerTurnRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("HC", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("per turn") || name.Contains("each turn") || name.Contains("Fill"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hc_fill_per_turn", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)\s*HC", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["hc_per_turn"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Fills BB gauge on guard. "Boosts BB gauge by X BC when guarding" / "Fills X BC upon guarding"</summary>
    class BCFillOnGuardRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill on Guard", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_on_guard" };
            var m = Regex.Match(text, @"(?:by|Fills?)\s+(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["bc"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Fills BB gauge on spark. "Boosts BB gauge by X BC on spark" / "Fills X~Y BC per spark"</summary>
    class BBFillOnSparkRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill on Spark", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_on_spark" };

            var chanceM = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);

            // Range: "Fills X~Y BC per spark", "Boosts BB gauge by X~Y BC on spark",
            //        "X% chance of filling X~Y BC per spark"
            var rangeM = Regex.Match(text, @"(\d+)~(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (rangeM.Success)
            {
                effect.magnitudes["min"] = float.Parse(rangeM.Groups[1].Value);
                effect.magnitudes["max"] = float.Parse(rangeM.Groups[2].Value);
                return effect;
            }
            // Single value
            var single = Regex.Match(text, @"(?:by|[Ff]ills?|filling)\s+(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (single.Success) effect.magnitudes["bc"] = float.Parse(single.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Reduces the BC cost required to activate BB/SBB.</summary>
    class BBCostReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BB Cost", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Cost Reduction", StringComparison.OrdinalIgnoreCase) && name.Contains("BB", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_cost_reduction", magnitudes = new Dictionary<string, float>() };
            var flatMatch = Regex.Match(text, @"(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (flatMatch.Success) effect.magnitudes["flat_bc"] = float.Parse(flatMatch.Groups[1].Value);
            var pctMatch = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (pctMatch.Success) effect.magnitudes["percent"] = float.Parse(pctMatch.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Reduces how much BB gauge is consumed on activation (different from cost reduction).</summary>
    class BBGaugeUsedReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BB Gauge Used", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gauge Consumption", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_gauge_used_reduction", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class ODGaugeBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("OD", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Fill") || name.Contains("Boost") || name.Contains("Gauge")) &&
            !name.Contains("Efficacy");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "od_gauge_boost" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Increases how quickly the Overdrive gauge fills.</summary>
    class ODEfficacyBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("OD", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Efficacy", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "od_efficacy_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Unit gets an extra action. "X% chance of acting Y extra time(s)"</summary>
    class ExtraActionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Extra Action", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Additional Action", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "extra_action" };
            var chanceM = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+acting", RegexOptions.IgnoreCase);
            if (chanceM.Success) effect.chance = float.Parse(chanceM.Groups[1].Value);
            var countM = Regex.Match(text, @"acting\s+(\d+)\s+extra\s+times?", RegexOptions.IgnoreCase);
            effect.magnitudes["count"] = countM.Success ? float.Parse(countM.Groups[1].Value) : 1f;
            return effect;
        }
    }

    /// <summary>Re-triggers BB/SBB without consuming gauge again.</summary>
    class BBRecastRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Recast", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("BB", StringComparison.OrdinalIgnoreCase) && name.Contains("Repeat", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_recast", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            effect.chance = chanceMatch.Success ? float.Parse(chanceMatch.Groups[1].Value) : 100f;
            return effect;
        }
    }

    /// <summary>Triggers a BB/SBB automatically under a condition (e.g. HP drops below threshold).</summary>
    class BBActivationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BB Activation", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Activate", StringComparison.OrdinalIgnoreCase) && name.Contains("BB", StringComparison.OrdinalIgnoreCase));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_activation", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var hpThreshold = Regex.Match(text, @"when\s+HP\s+(?:drops\s+)?(?:below|under|<)\s*(\d+)%", RegexOptions.IgnoreCase);
            if (hpThreshold.Success) effect.magnitudes["hp_threshold"] = float.Parse(hpThreshold.Groups[1].Value);
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Debuffs on Enemies

    /// <summary>Reduces ATK/DEF/REC of enemies by a flat subtractive amount.</summary>
    class ParameterReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Parameter Reduction", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Negation") &&
            !name.Contains("Added to Attack") &&
            !name.Contains("Counter");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_reduction", stats = new List<string>(), magnitudes = new Dictionary<string, float>() };

            // Parse all stat reduction segments. Two real formats:
            //   "X% chance of reducing Atk by Y%"
            //   "X% chance of reducing Y% Atk"
            // Both may appear multiple times in the same string for multi-stat reductions.
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+chance\s+of\s+reducing\s+(?:(\d+)%\s+)?(Atk|Def|Rec)(?:\s+by\s+(\d+)%)?",
                RegexOptions.IgnoreCase))
            {
                string stat    = m.Groups[3].Value.ToLower();
                // amount is in group 4 ("reducing Atk by Y%") or group 2 ("reducing Y% Atk")
                float  amount  = m.Groups[4].Success ? float.Parse(m.Groups[4].Value)
                               : m.Groups[2].Success ? float.Parse(m.Groups[2].Value) : 0f;
                float  chance  = float.Parse(m.Groups[1].Value);

                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                effect.magnitudes[stat + "_reduction"] = amount;
                // Use the first chance found as the overall chance
                if (effect.chance == null) effect.chance = chance;
            }
            return effect;
        }
    }

    class EnemyBBGaugeReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BB Gauge Reduction", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gauge Fill Rate Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "enemy_bb_gauge_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Slows how much BC enemies gain from being hit.</summary>
    class EnemyBCFillRateReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("BC Fill Rate", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Reduction") || name.Contains("Decrease") || name.Contains("Debuff"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "enemy_bc_fill_rate_reduction", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Inflicts one or more status ailments with per-ailment or global chance.</summary>
    class AilmentInflictionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Ailment", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Inflict") || name.Contains("Chance")) &&
            !name.Contains("Counter");

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "ailment_inflict", magnitudes = new Dictionary<string, float>(), elements = new List<string>() };
            foreach (var ailment in AllAilments)
            {
                var m = Regex.Match(text, ailment + @"[:\s]+(\d+)%|(\d+)%\s+chance\s+to\s+inflict\s+" + ailment, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string val = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    effect.magnitudes[ailment.ToLower() + "_chance"] = float.Parse(val);
                    effect.elements.Add(ailment.ToLower());
                }
                else if (text.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(ailment.ToLower());
            }
            var globalChance = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (globalChance.Success) effect.chance = float.Parse(globalChance.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>Inflicts an ailment on the attacker when this unit is hit.</summary>
    class AilmentInflictOnCounterRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Counter", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Ailment", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "ailment_inflict_on_counter", magnitudes = new Dictionary<string, float>(), elements = new List<string>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            foreach (var ailment in AllAilments)
                if (text.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(ailment.ToLower());
            return effect;
        }
    }

    /// <summary>Places a Doom countdown on enemies; when it reaches 0 they are KO'd (bypasses Angel Idol).</summary>
    class DoomInflictRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Doom", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Inflict", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "doom_inflict", magnitudes = new Dictionary<string, float>() };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            var countMatch = Regex.Match(text, @"countdown[:\s]+(\d+)", RegexOptions.IgnoreCase);
            if (countMatch.Success)
                effect.magnitudes["countdown"] = float.Parse(countMatch.Groups[1].Value);
            else
            {
                var fallback = Regex.Match(text, @"(\d+)\s*turn", RegexOptions.IgnoreCase);
                if (fallback.Success) effect.magnitudes["countdown"] = float.Parse(fallback.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>Inflicts a status ailment back onto the attacker when this unit is hit.</summary>
    class StatusCounterRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Status Counter", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "status_counter", elements = new List<string>() };

            // "Damage taken has a X% chance of inflicting Curse and X% chance of inflicting Sick"
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+chance\s+of\s+inflicting\s+([\w,\s]+?)(?=\s+and\s+\d+%|$)",
                RegexOptions.IgnoreCase))
            {
                float  chance      = float.Parse(m.Groups[1].Value);
                string ailmentList = m.Groups[2].Value;
                foreach (var ailment in AllAilments)
                {
                    if (ailmentList.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    {
                        string key = ailment.ToLower() + "_chance";
                        if (!effect.magnitudes.ContainsKey(key) || effect.magnitudes[key] < chance)
                            effect.magnitudes[key] = chance;
                        if (!effect.elements.Contains(ailment.ToLower()))
                            effect.elements.Add(ailment.ToLower());
                    }
                }
            }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Resource / Utility

    class ZelBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Zel", StringComparison.OrdinalIgnoreCase) && (name.Contains("Boost") || name.Contains("Increase"));
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "zel_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase); if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class KarmaBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Karma", StringComparison.OrdinalIgnoreCase) && (name.Contains("Boost") || name.Contains("Increase"));
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "karma_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase); if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class ItemDropBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("Item Drop", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "item_drop_boost", magnitudes = new Dictionary<string, float>() };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase); if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class EXPBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("EXP", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Boost") || name.Contains("Increase") || name.Contains("Gain"));

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "exp_boost" };
            // Determine whether this is Player EXP or Summoner EXP
            if (name.Contains("Summoner", StringComparison.OrdinalIgnoreCase))
                effect.description = "summoner";
            else if (name.Contains("Player", StringComparison.OrdinalIgnoreCase))
                effect.description = "player";
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    class ABPBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("ABP", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("ABP & CBP Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "abp_cbp_boost" };
            // "Increases ABP gain by X% and CBP gain by Y%"
            var abpM = Regex.Match(text, @"ABP\s+gain\s+by\s+(\d+)%", RegexOptions.IgnoreCase);
            var cbpM = Regex.Match(text, @"CBP\s+gain\s+by\s+(\d+)%", RegexOptions.IgnoreCase);
            if (abpM.Success) effect.magnitudes["abp_percent"] = float.Parse(abpM.Groups[1].Value);
            if (cbpM.Success) effect.magnitudes["cbp_percent"] = float.Parse(cbpM.Groups[1].Value);
            // fallback single %
            if (!abpM.Success && !cbpM.Success)
            {
                var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
                if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            }
            return effect;
        }
    }

    class CBPBoostRule : IEffectRule
    {
        public bool Matches(string name) => name.Contains("CBP", StringComparison.OrdinalIgnoreCase);
        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "cbp_boost" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    #endregion

    #region Effect Rules — New (55 missing effects)

    class FixedDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Fixed Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "fixed_damage" };
            var m = Regex.Match(text, @"Deals\s+([\d,]+)\s+damage", RegexOptions.IgnoreCase);
            if (m.Success)
                effect.magnitudes["damage"] = float.Parse(m.Groups[1].Value.Replace(",", ""));
            return effect;
        }
    }

    /// <summary>
    /// HP-Scaled Damage v2: covers both (current HP / base max HP) and
    /// (base max HP / current HP) variants from the wiki.
    /// Patterns:
    ///   "[number]% + [number]% * (current HP / base max HP) damage modifier"
    ///   "[number]% + [number]% * (base max HP / current HP) damage modifier"
    /// The old HPScaledDamageRule only matched "current HP" — this replaces it.
    /// </summary>
    class HPScaledDamageV2Rule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("HP-Scaled Damage", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("HP-scaled Damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hp_scaled_damage", target = "enemy" };

            var baseMatch = Regex.Match(text, @"^(\d+)%\s*\+", RegexOptions.IgnoreCase);
            if (baseMatch.Success)
                effect.magnitudes["base"] = float.Parse(baseMatch.Groups[1].Value);

            var scaleMatch = Regex.Match(text, @"\+\s*(\d+)%\s*\*\s*\(", RegexOptions.IgnoreCase);
            if (scaleMatch.Success)
                effect.magnitudes["scaling"] = float.Parse(scaleMatch.Groups[1].Value);

            // Direction: "base max HP / current HP" = inverse (low HP = more damage)
            //            "current HP / base max HP" = normal  (high HP = more damage)
            if (text.Contains("base max HP / current HP", StringComparison.OrdinalIgnoreCase))
                effect.description = "inverse"; // more damage at low HP
            else
                effect.description = "normal";  // more damage at high HP

            var critMatch = Regex.Match(text, @"\((\d+)%\s+innate\s+crit\s+rate\)", RegexOptions.IgnoreCase);
            if (critMatch.Success)
                effect.magnitudes["innate_crit_rate"] = float.Parse(critMatch.Groups[1].Value);

            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // HEALING / MITIGATION
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces active (burst) healing effects received by X%.
    /// Pattern: "Reduces active healing effects by [number]%"
    /// </summary>
    class ActiveHealingReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Active Healing Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "active_healing_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Reduces passive (gradual/per-turn) healing effects received by X%.
    /// Pattern: "Reduces passive healing effects by [number]%"
    /// </summary>
    class PassiveHealingReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Passive Healing Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "passive_healing_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Reduces HC Efficacy (how much HP each HC restores) by X%.
    /// Pattern: "Reduces HC efficacy effects by [number]%"
    /// </summary>
    class HCEfficacyReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("HC Efficacy Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "hc_efficacy_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Reduces Damage over Time (DoT) damage received by X%.
    /// Wiki uses both "Damage over Time Mitigation" and "DoT Mitigation" as names.
    /// Pattern: "Reduces DoT damage by [number]%"
    /// </summary>
    class DotMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("DoT Mitigation", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Damage over Time Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "dot_mitigation" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // KO / SURVIVAL
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// X% chance of reducing all damage to 1 for that hit.
    /// Pattern: "[number]% chance of taking [number] damage"
    /// </summary>
    class DamageReductionTo1Rule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Contains("Damage Reduction to 1", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_reduction_to_1" };
            var chanceMatch = Regex.Match(text, @"(\d+)%\s*chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // STAT BUFFS — SELF
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Self-only stat boost. Distinct from party-wide Parameter Boost.
    /// Patterns: "Boosts own Atk by X%", "X% boost to own Atk", etc.
    /// Note: can have negative values (e.g. "Boosts own Def by -20%").
    /// </summary>
    class SelfParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Self Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "self_parameter_boost", target = "self" };
            string preText = Regex.Replace(text, @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

                        // A0: "X% boost to own STAT"
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+own\s+(HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }
            // Three-step stat parser covers all real patterns:
            // A: "X% boost to Atk"  /  "X% boost to Atk and X% boost to Def"
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            // B: "Boosts Atk, Def and Rec by X%"  (shared single value)
            foreach (Match m in Regex.Matches(preText,
                @"[Bb]oosts?\s+(?:own\s+)?((?:(?:HP|Atk|Def|Rec|critical\s+rate)(?:\s*[,/]\s*|\s+and\s+))+(?:HP|Atk|Def|Rec|critical\s+rate))\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                float val = float.Parse(m.Groups[2].Value);
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec|critical\s+rate", RegexOptions.IgnoreCase))
                {
                    string stat = sm.Value.Trim().ToLower().Replace(" ", "_");
                    if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = val;
                    if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                }
            }

            // C: individual "STAT by X%" segments (each has its own value, possibly negative)
            foreach (Match m in Regex.Matches(preText,
                @"(HP|Atk|Def|Rec|critical\s+rate)\s+by\s+(-?\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            return effect;
        }
    }

    /// <summary>
    /// Self-only max HP boost.
    /// Pattern: "[number]% boost to own HP"
    /// </summary>
    class SelfMaxHPBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Self Max HP Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "self_max_hp_boost", target = "self" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+own\s+HP", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            else
            {
                var fallback = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
                if (fallback.Success) effect.magnitudes["percent"] = float.Parse(fallback.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Self-only stat conversion. Converts a % of one stat into another.
    /// Pattern: "Boosts own Atk relative to [number]% of Def"
    /// </summary>
    class SelfParameterConversionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Self Parameter Conversion", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "self_parameter_conversion", target = "self" };

            // "Boosts own Atk relative to X% of Def"
            // "Boosts own Atk and Def relative to X% of HP"
            var m = Regex.Match(text,
                @"[Bb]oosts?\s+own\s+([\w\s,]+?)\s+relative\s+to\s+(\d+)%\s+of\s+(\w+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["percent"] = float.Parse(m.Groups[2].Value);
                effect.stats.Add(m.Groups[3].Value.Trim().ToLower()); // source stat
                // target stats
                foreach (Match sm in Regex.Matches(m.Groups[1].Value,
                    @"HP|Atk|Def|Rec", RegexOptions.IgnoreCase))
                    if (!effect.elements.Contains(sm.Value.ToLower()))
                        effect.elements.Add(sm.Value.ToLower());
            }
            return effect;
        }
    }

    /// <summary>
    /// Self-only spark damage boost.
    /// Pattern: "[number]% boost to own spark damage"
    /// </summary>
    class SelfSparkBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Self Spark Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "self_spark_boost", target = "self" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Self spark boost that only applies above/below an HP threshold.
    /// Patterns: "Boosts spark damage by X% when HP is above/below Y%"
    /// </summary>
    class SelfSparkBoostBasedOnHPRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Self Spark Boost based on HP", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "self_spark_boost_hp_conditional", target = "self" };

            var m = Regex.Match(text,
                @"Boosts\s+spark\s+damage\s+by\s+(\d+)%\s+when\s+HP\s+is\s+(above|below)\s+(\d+)%",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["percent"]      = float.Parse(m.Groups[1].Value);
                effect.magnitudes["hp_threshold"] = float.Parse(m.Groups[3].Value);
                effect.description = m.Groups[2].Value.ToLower(); // "above" or "below"
            }
            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // STAT BUFFS — ELEMENTAL / GENDER / SQUAD
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Boosts stats of units matching a specific element.
    /// Patterns: "X% boost to Atk of Fire units", "X% boost to all parameters of Dark units", etc.
    /// Also covers the "& X% boost to critical rate" variants.
    /// </summary>
    class ElementalParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Elemental Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_parameter_boost" };

            var elemMatch = Regex.Match(text, @"of\s+([\w,\s]+)\s+units", RegexOptions.IgnoreCase);
            string preText = Regex.Replace(
                elemMatch.Success ? text.Substring(0, elemMatch.Index) : text,
                @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

            // A: "X% boost to STAT" per stat
            foreach (Match m in Regex.Matches(preText,
                @"(\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            if (elemMatch.Success)
            {
                string raw = elemMatch.Groups[1].Value.ToLowerInvariant().Replace(" and ", ",");
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string e = part.Trim();
                    if (WikiUnitLoader.ValidElements.Contains(e))
                        effect.elements.Add(char.ToUpper(e[0]) + e.Substring(1));
                }
            }
            return effect;
        }
    }

    /// <summary>
    /// Boosts stats of units matching a specific gender (Male/Female/Genderless).
    /// Pattern: "X% boost to all parameters of Female units"
    /// </summary>
    class GenderParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Gender Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "gender_parameter_boost" };

            var genderMatch = Regex.Match(text, @"of\s+(Male|Female|Genderless)\s+units", RegexOptions.IgnoreCase);
            if (genderMatch.Success) effect.description = genderMatch.Groups[1].Value.ToLower();

            string preText = Regex.Replace(
                genderMatch.Success ? text.Substring(0, genderMatch.Index) : text,
                @"^Guild\s+Raid\s+Only\s+", "", RegexOptions.IgnoreCase);

            foreach (Match m in Regex.Matches(preText,
                @"(-?\d+)%\s+boost\s+to\s+(all\s+parameters|HP|Atk|Def|Rec|critical\s+rate)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.Trim().ToLower().Replace(" ", "_");
                if (!effect.magnitudes.ContainsKey(stat)) effect.magnitudes[stat] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }
            return effect;
        }
    }

    /// <summary>
    /// Boosts stats based on how many different elements are present in the party.
    /// Pattern: "Boosts X% boost to Atk when N (or more) elements are present"
    /// </summary>
    class ElementSquadBasedParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Element Squad-based Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "element_squad_based_parameter_boost" };

            // Per-stat boosts: "X% boost to Atk"
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+boost\s+to\s+(HP|Atk|Def|Rec)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.ToLower();
                effect.magnitudes[stat + "_boost"] = float.Parse(m.Groups[1].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            // Required element count
            var countMatch = Regex.Match(text,
                @"when\s+(\d+)(?:\s+or\s+more)?\s+elements\s+are\s+present",
                RegexOptions.IgnoreCase);
            if (countMatch.Success)
            {
                effect.magnitudes["element_count"] = float.Parse(countMatch.Groups[1].Value);
                effect.description = text.Contains("or more", StringComparison.OrdinalIgnoreCase)
                    ? "or_more" : "exact";
            }
            return effect;
        }
    }

    /// <summary>
    /// Stat boost that scales with current HP (lost or remaining).
    /// Pattern: "X~Y% boost to Atk based on HP lost/remaining"
    /// </summary>
    class ParameterBoostBasedOnHPRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Parameter Boost based on HP", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_boost_based_on_hp" };

            // Real patterns:
            //   "X~Y% boost to Atk based on HP lost"
            //   "X~Y% boost to Atk and Def based on HP lost"
            //   "X~Y% boost to Atk based on HP lost and X~Y% boost to Def based on HP lost"
            // Strategy: find each "X~Y% boost to <stat_list> based on HP <dir>" segment
            foreach (Match m in Regex.Matches(text,
                @"(\d+)~(\d+)%\s+(?:boost|decrease)\s+to\s+((?:(?:HP|Atk|Def|Rec)(?:\s*,\s*|\s+and\s+)?)+(?:HP|Atk|Def|Rec))\s+based\s+on\s+HP\s+(lost|remaining)",
                RegexOptions.IgnoreCase))
            {
                float  minV      = float.Parse(m.Groups[1].Value);
                float  maxV      = float.Parse(m.Groups[2].Value);
                string statList  = m.Groups[3].Value;
                string direction = m.Groups[4].Value.ToLower();

                foreach (Match sm in Regex.Matches(statList,
                    @"HP|Atk|Def|Rec", RegexOptions.IgnoreCase))
                {
                    string stat = sm.Value.ToLower();
                    string key  = stat + "_" + direction;
                    effect.magnitudes[key + "_min"] = minV;
                    effect.magnitudes[key + "_max"] = maxV;
                    if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                }
            }

            // Single-stat fallback: "X~Y% boost to Atk based on HP lost"
            if (!effect.magnitudes.Any())
            {
                var m = Regex.Match(text,
                    @"(\d+)~(\d+)%\s+(?:boost|decrease)\s+to\s+(HP|Atk|Def|Rec)\s+based\s+on\s+HP\s+(lost|remaining)",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string stat = m.Groups[3].Value.ToLower();
                    string dir  = m.Groups[4].Value.ToLower();
                    effect.magnitudes[stat + "_" + dir + "_min"] = float.Parse(m.Groups[1].Value);
                    effect.magnitudes[stat + "_" + dir + "_max"] = float.Parse(m.Groups[2].Value);
                    effect.stats.Add(stat);
                }
            }

            effect.description = text.Contains("HP lost", StringComparison.OrdinalIgnoreCase)
                ? "hp_lost" : "hp_remaining";

            return effect;
        }
    }

    /// <summary>
    /// Stat boost only active for the first N turns of battle.
    /// Pattern: "Boosts Atk by X% for the first N turns"
    /// </summary>
    class ParameterBoostForFirstXTurnsRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Parameter Boost for First X Turns", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_boost_first_x_turns" };

            foreach (Match m in Regex.Matches(text,
                @"(Atk|Def|Rec)\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[1].Value.ToLower();
                effect.magnitudes[stat] = float.Parse(m.Groups[2].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            // "for the first N turns"
            var durMatch = Regex.Match(text,
                @"for\s+the\s+first\s+(\d+)\s+turns",
                RegexOptions.IgnoreCase);
            if (durMatch.Success)
                effect.magnitudes["active_turns"] = float.Parse(durMatch.Groups[1].Value);

            return effect;
        }
    }

    /// <summary>
    /// Stat boost that increases incrementally each turn up to a cap.
    /// Pattern: "X.Y% boost to Atk (Z% max, up to N turns)"
    /// </summary>
    class TurnBasedParameterBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Turn-based Parameter Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "turn_based_parameter_boost" };

            // "X.Y% boost to Atk (Z% max, up to N turns)"
            // "Guild Raid Only X.Y% boost to Atk (...)"
            foreach (Match m in Regex.Matches(text,
                @"([\d.]+)%\s+boost\s+to\s+(Atk|Def|Rec|HP)\s*\(\s*(\d+)%\s+max,\s+up\s+to\s+(\d+)\s+turns\s*\)",
                RegexOptions.IgnoreCase))
            {
                string stat = m.Groups[2].Value.ToLower();
                effect.magnitudes[stat + "_per_turn"] = float.Parse(m.Groups[1].Value);
                effect.magnitudes[stat + "_max"]      = float.Parse(m.Groups[3].Value);
                effect.magnitudes["max_turns"]        = float.Parse(m.Groups[4].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
            }

            return effect;
        }
    }

    /// <summary>
    /// ATK boost that only applies when the current enemy has a status ailment.
    /// Pattern: "X% boost to Atk when enemy is status afflicted"
    /// </summary>
    class AttackBoostOnStatusAfflictedFoesRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Attack Boost on Status Afflicted Foes", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "atk_boost_vs_status_afflicted" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+Atk", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            effect.stats.Add("atk");
            return effect;
        }
    }

    /// <summary>
    /// BB ATK boost conditional on current HP being above a threshold.
    /// Pattern: "Boosts BB Atk by X% when HP is above Y%"
    /// </summary>
    class BBATKBoostBasedOnHPRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BB Atk Boost based on HP", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_atk_boost_hp_conditional" };
            var m = Regex.Match(text,
                @"Boosts\s+BB\s+Atk\s+by\s+(\d+)%\s+when\s+HP\s+is\s+above\s+(\d+)%",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["percent"]      = float.Parse(m.Groups[1].Value);
                effect.magnitudes["hp_threshold"] = float.Parse(m.Groups[2].Value);
                effect.description = "above";
            }
            return effect;
        }
    }

    /// <summary>
    /// Boosts critical hit damage of units matching a specific element.
    /// Pattern: "X% boost to critical damage of Fire units"
    /// </summary>
    class ElementalCriticalDamageBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Elemental Critical Damage Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_critical_damage_boost" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+critical\s+damage", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);

            foreach (var elem in WikiUnitLoader.ValidElements)
                if (text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));

            return effect;
        }
    }

    /// <summary>
    /// Boosts spark damage of units matching a specific element.
    /// Pattern: "X% boost to spark damage of Fire units"
    /// </summary>
    class ElementalSparkBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Elemental Spark Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_spark_boost" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+spark\s+damage", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);

            foreach (var elem in WikiUnitLoader.ValidElements)
                if (text.Contains(elem, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(char.ToUpper(elem[0]) + elem.Substring(1));

            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // DAMAGE MODIFIERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Standard unconditional damage mitigation (Normal Mitigation in wiki terms).
    /// Pattern: "Reduces damage taken by X%"
    /// Separate from MitigationRule only because the wiki labels them differently —
    /// both map to the same effect_type "damage_reduction" but Normal Mitigation
    /// applies broadly while "Damage Mitigation" / "Specific Damage Mitigation" may
    /// have extra qualifiers. We keep a unified effect_type and let the battle script decide.
    /// </summary>
    class NormalMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Normal Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Reduces damage from normal (non-BB/SBB) attacks only.
    /// Pattern: "Reduces normal attack damage by X%"
    /// </summary>
    class NormalAttackMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Normal Attack Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "normal_attack_mitigation" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Chance-based damage mitigation: X% chance of reducing damage taken by Y%.
    /// Pattern: "[number]% chance of reducing damage taken by [number]%"
    /// </summary>
    class ChanceMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Chance Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "chance_mitigation" };
            var m = Regex.Match(text,
                @"(\d+)%\s+chance\s+of\s+reducing\s+damage\s+taken\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.chance = float.Parse(m.Groups[1].Value);
                effect.magnitudes["percent"] = float.Parse(m.Groups[2].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Elemental mitigation that only applies for the first N turns of battle.
    /// Pattern: "Reduces all elemental damage taken by X% for the first N turns"
    /// </summary>
    class ElementalMitigationForFirstXTurnsRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Elemental Mitigation for First X Turns", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "elemental_mitigation_first_x_turns" };
            var m = Regex.Match(text,
                @"Reduces\s+all\s+elemental\s+damage\s+taken\s+by\s+(\d+)%\s+for\s+the\s+first\s+(\d+)\s+turns",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["percent"]      = float.Parse(m.Groups[1].Value);
                effect.magnitudes["active_turns"] = float.Parse(m.Groups[2].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Specific Damage Mitigation: can increase OR decrease damage taken.
    /// Pattern: "Increases/Reduces damage taken by X%"
    /// Used for situational modifiers (e.g. vs specific damage types in Guild Raid).
    /// </summary>
    class SpecificDamageMitigationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Specific Damage Mitigation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "specific_damage_mitigation" };
            bool increases = text.Contains("Increases damage taken", StringComparison.OrdinalIgnoreCase);
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                float val = float.Parse(m.Groups[1].Value);
                // Store as negative if it increases damage taken (debuff)
                effect.magnitudes["percent"] = increases ? -val : val;
            }
            return effect;
        }
    }

    /// <summary>
    /// Negates DEF-ignoring damage (NullIgnoreDef from ally side).
    /// Pattern: "Negates Def-ignoring damage"
    /// Different name from the old NullIgnoreDefRule which matched "Null Ignore Def".
    /// </summary>
    class IgnoreDefenseNegationRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Ignore Defense Negation", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id) =>
            new ParsedEffect { effect_type = "null_ignore_def" };
    }

    // ─────────────────────────────────────────────────────────────
    // BB GAUGE / BC / HC — NEW FILL TRIGGERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills BB gauge on landing a critical hit.
    /// Patterns: "Boosts BB gauge by X BC on critical hit",
    ///           "Boosts BB gauge by X~Y BC on critical hit",
    ///           "X% chance of boosting BB gauge by X~Y BC on critical hit"
    /// </summary>
    class BCFillOnCriticalRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill on Critical", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_on_critical" };

            var chanceMatch = Regex.Match(text, @"(\d+[\.\d]*)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            // Range: X~Y or X-Y
            var rangeMatch = Regex.Match(text, @"(\d+)\s*[~\-]\s*(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                effect.magnitudes["min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            else
            {
                var single = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
                if (single.Success) effect.magnitudes["value"] = float.Parse(single.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge after defeating an enemy.
    /// Pattern: "X% chance of boosting BB gauge by Y BC after defeating an enemy"
    /// </summary>
    class BCFillOnEnemyDefeatRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill on Enemy Defeat", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_on_enemy_defeat" };
            var m = Regex.Match(text,
                @"(\d+)%\s+chance\s+of\s+boosting\s+BB\s+gauge\s+by\s+(\d+)\s+BC",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.chance = float.Parse(m.Groups[1].Value);
                effect.magnitudes["bc"] = float.Parse(m.Groups[2].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge each time the unit performs a normal attack.
    /// Patterns: "Boosts BB gauge by X BC when attacking",
    ///           "X% chance of boosting BB gauge by X~Y BC when attacking"
    /// </summary>
    class BCFillWhenAttackingRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill when attacking", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_when_attacking" };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            var rangeMatch = Regex.Match(text, @"(\d+)\s*~\s*(\d+)\s*BC", RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                effect.magnitudes["min"] = float.Parse(rangeMatch.Groups[1].Value);
                effect.magnitudes["max"] = float.Parse(rangeMatch.Groups[2].Value);
            }
            else
            {
                var single = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
                if (single.Success) effect.magnitudes["value"] = float.Parse(single.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge (or fully fills) when a damage threshold is exceeded in one hit.
    /// Patterns: "Boosts BB gauge by X BC when Y or more damage is dealt",
    ///           "Fully fills BB gauge when Y or more damage is dealt"
    /// </summary>
    class BCFillAfterDealingDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill after dealing damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_after_dealing_damage" };

            if (text.Contains("Fully fills BB gauge", StringComparison.OrdinalIgnoreCase))
            {
                effect.description = "full";
                effect.magnitudes["bc"] = -1f; // sentinel: full fill
            }

            var threshM = Regex.Match(text,
                @"when\s+([\d,]+)\s+or\s+more\s+damage\s+is\s+dealt",
                RegexOptions.IgnoreCase);
            if (threshM.Success)
                effect.magnitudes["damage_threshold"] = float.Parse(threshM.Groups[1].Value.Replace(",", ""));

            var bcM = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (bcM.Success) effect.magnitudes["bc"] = float.Parse(bcM.Groups[1].Value);

            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge when damage taken exceeds a threshold.
    /// Pattern: "Boosts BB gauge by X BC when Y or more damage is taken"
    /// </summary>
    class BCFillAfterTakingDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill after taking damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_after_taking_damage" };

            var threshMatch = Regex.Match(text,
                @"when\s+([\d,]+)\s+or\s+more\s+damage\s+is\s+taken",
                RegexOptions.IgnoreCase);
            if (threshMatch.Success)
                effect.magnitudes["damage_threshold"] = float.Parse(threshMatch.Groups[1].Value.Replace(",", ""));

            var bcMatch = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (bcMatch.Success) effect.magnitudes["bc"] = float.Parse(bcMatch.Groups[1].Value);

            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge when HC collected exceeds a threshold.
    /// Pattern: "Boosts BB gauge by X BC when Y or more HC is collected"
    /// </summary>
    class BCFillAfterReceivingHCRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill after receiving HC", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_after_receiving_hc" };

            var threshMatch = Regex.Match(text,
                @"when\s+(\d+)\s+or\s+more\s+HC\s+is\s+collected",
                RegexOptions.IgnoreCase);
            if (threshMatch.Success)
                effect.magnitudes["hc_threshold"] = float.Parse(threshMatch.Groups[1].Value);

            var bcMatch = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (bcMatch.Success) effect.magnitudes["bc"] = float.Parse(bcMatch.Groups[1].Value);

            return effect;
        }
    }

    /// <summary>
    /// Fills BB gauge from damage taken specifically while the unit is guarding.
    /// Pattern: "Damage taken boosts BB gauge by X BC when guarding"
    /// </summary>
    class BCFillWhenAttackedWhileGuardingRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("BC Fill when attacked while guarding", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bc_fill_when_attacked_while_guarding" };
            var m = Regex.Match(text, @"by\s+(\d+)\s+BC", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["bc"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Fills OD gauge by a flat point amount each turn.
    /// Pattern: "Fills X OD points at the end of each turn"
    /// </summary>
    class GradualODFillRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Gradual OD Fill", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "gradual_od_fill" };
            var m = Regex.Match(text, @"Fills\s+(\d+)\s+OD\s+points", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["od_points_per_turn"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Instantly fills a percentage of the OD gauge.
    /// Patterns: "Fills X% of the OD gauge",
    ///           "For each unit alive, fill X% of the OD gauge"
    /// </summary>
    class InstantODFillRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Instant OD Fill", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "instant_od_fill" };
            // "Fills X% of the OD gauge" or "For each unit alive, fill X% of the OD gauge"
            var m = Regex.Match(text, @"[Ff]ills?\s+(\d+)%\s+of\s+the\s+OD\s+gauge", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);

            if (text.Contains("For each unit alive", StringComparison.OrdinalIgnoreCase))
                effect.description = "per_unit_alive";

            return effect;
        }
    }

    /// <summary>
    /// Boosts the rate at which the OD gauge fills each turn.
    /// Wiki uses both "OD Gauge Fill Rate" and "OD Gauge Fill Rate Boost" as names — same mechanic.
    /// Pattern: "X% boost to OD fill rate"
    /// </summary>
    class ODFillRateRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("OD Gauge Fill Rate", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("OD Gauge Fill Rate Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "od_fill_rate_boost" };
            var m = Regex.Match(text, @"(\d+)%\s+boost\s+to\s+OD\s+fill\s+rate", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Boosts BB activation chance in Arena modes only.
    /// Pattern: "Boosts BB activation rates in Arena modes by X%"
    /// </summary>
    class IncreasedBBActivationChanceRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Increased Brave Burst Activation Chance", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "bb_activation_chance_boost" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            effect.description = "arena_only";
            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // DEBUFFS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces max HP of target by X%.
    /// Pattern: "Reduces max HP by X%"
    /// </summary>
    class MaxHPReductionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Max HP Reduction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "max_hp_reduction" };
            var m = Regex.Match(text, @"(\d+)%", RegexOptions.IgnoreCase);
            if (m.Success) effect.magnitudes["percent"] = float.Parse(m.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Parameter reduction attached to every normal attack hit.
    /// Patterns: "Added to attack: X% chance of reducing Atk by Y%"
    ///           "X% chance of reducing X% Atk (& Def)"
    /// </summary>
    class ParameterReductionAddedToAttackRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Parameter Reduction Added to Attack", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_reduction_added_to_attack", stats = new List<string>() };

            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+chance\s+of\s+reducing\s+(?:(\d+)%\s+)?(Atk|Def|Rec)(?:\s+by\s+(\d+)%)?",
                RegexOptions.IgnoreCase))
            {
                string stat   = m.Groups[3].Value.ToLower();
                float  amount = m.Groups[4].Success ? float.Parse(m.Groups[4].Value)
                              : m.Groups[2].Success ? float.Parse(m.Groups[2].Value) : 0f;
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                effect.magnitudes[stat + "_reduction"] = amount;
                if (effect.chance == null) effect.chance = float.Parse(m.Groups[1].Value);
            }
            return effect;
        }
    }

    /// <summary>
    /// Inflicts parameter reduction when this unit is hit (counter debuff).
    /// Pattern: "Damage taken has a X% chance of inflicting Y% Atk reduction for Z turns"
    /// </summary>
    class ParameterReductionCounterRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Parameter Reduction Counter", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "parameter_reduction_counter", stats = new List<string>() };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            // "inflicting Y% Atk reduction" or "reducing Y% Atk"
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+(Atk|Def|Rec)\s+reduction|reducing\s+(\d+)%\s+(Atk|Def|Rec)",
                RegexOptions.IgnoreCase))
            {
                string stat   = m.Groups[2].Success ? m.Groups[2].Value.ToLower() : m.Groups[4].Value.ToLower();
                float  amount = m.Groups[1].Success ? float.Parse(m.Groups[1].Value) : float.Parse(m.Groups[3].Value);
                if (!effect.stats.Contains(stat)) effect.stats.Add(stat);
                effect.magnitudes[stat + "_reduction"] = amount;
            }

            var durMatch = Regex.Match(text, @"for\s+(\d+)\s+turns?", RegexOptions.IgnoreCase);
            if (durMatch.Success) effect.duration = int.Parse(durMatch.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Inflicts a debuff (stat reduction or BC efficacy reduction) on the attacker when hit.
    /// Patterns: "Damage taken has a X% chance of reducing Atk by Y% for Z turns"
    ///           "Damage taken has a X% chance of reducing BC efficacy by Y% for Z turns"
    /// </summary>
    class InflictEffectWhenAttackedRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Inflict Effect when attacked", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "inflict_effect_when_attacked" };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+reducing", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            // Stat reduction
            var statMatch = Regex.Match(text,
                @"reducing\s+(Atk|Def|Rec)\s+by\s+(\d+)%\s+for\s+(\d+)\s+turns?",
                RegexOptions.IgnoreCase);
            if (statMatch.Success)
            {
                string stat = statMatch.Groups[1].Value.ToLower();
                effect.stats.Add(stat);
                effect.magnitudes[stat + "_reduction"] = float.Parse(statMatch.Groups[2].Value);
                effect.duration = int.Parse(statMatch.Groups[3].Value);
                effect.description = "stat_reduction";
            }

            // BC efficacy reduction
            var bcMatch = Regex.Match(text,
                @"reducing\s+BC\s+efficacy\s+by\s+(\d+)%\s+for\s+(\d+)\s+turns?",
                RegexOptions.IgnoreCase);
            if (bcMatch.Success)
            {
                effect.magnitudes["bc_efficacy_reduction"] = float.Parse(bcMatch.Groups[1].Value);
                effect.duration = int.Parse(bcMatch.Groups[2].Value);
                effect.description = "bc_efficacy_reduction";
            }

            return effect;
        }
    }

    /// <summary>
    /// Status ailment(s) inflicted on every normal attack hit.
    /// Pattern: "Add to attack: X% chance of inflicting Curse (and Paralysis ...)"
    /// Groups ailments by chance tier: some units have two groups with different %s.
    /// </summary>
    class StatusInflictionAddedToAttackRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Status Infliction Added to Attack", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect
            {
                effect_type = "status_infliction_added_to_attack",
                elements    = new List<string>()
            };

            // Match all "X% chance of inflicting A (and B ...)" groups
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+chance\s+of\s+inflicting\s+([\w,\s]+?)(?=\s+and\s+\d+%|\s*$)",
                RegexOptions.IgnoreCase))
            {
                float chance = float.Parse(m.Groups[1].Value);
                string ailmentList = m.Groups[2].Value;

                foreach (var ailment in WikiUnitLoader.AllAilmentsPublic)
                {
                    if (ailmentList.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    {
                        string key = ailment.ToLower() + "_chance";
                        // Keep higher chance if already set
                        if (!effect.magnitudes.ContainsKey(key) || effect.magnitudes[key] < chance)
                            effect.magnitudes[key] = chance;
                        if (!effect.elements.Contains(ailment.ToLower()))
                            effect.elements.Add(ailment.ToLower());
                    }
                }
            }

            return effect;
        }
    }

    /// <summary>
    /// Inflicts all 6 ailments when landing a critical hit.
    /// Pattern: "X% chance of inflicting Curse, Injury, Paralysis, Poison, Sick and Weaken
    ///           when landing a critical hit"
    /// </summary>
    class StatusInflictionOnCriticalRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Status Infliction on Critical", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect
            {
                effect_type = "status_infliction_on_critical",
                elements    = new List<string>()
            };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+inflicting", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);

            foreach (var ailment in WikiUnitLoader.AllAilmentsPublic)
                if (text.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    effect.elements.Add(ailment.ToLower());

            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduces the duration of specific enemy debuffs on the party by N turns.
    /// Pattern: "Reduces buff durations of the following effects by N turns: Turn Skip"
    /// </summary>
    class EffectDurationBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Effect Duration Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "effect_duration_reduction" };

            var m = Regex.Match(text,
                @"Reduces\s+buff\s+durations\s+of\s+the\s+following\s+effects\s+by\s+(\d+)\s+turns?:\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["turns_reduced"] = float.Parse(m.Groups[1].Value);
                // Store the affected effect names as a comma-separated description
                effect.description = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Removes (purges) one or more active effects from targets.
    /// Patterns: "Purges effects: DoT",
    ///           "X% chance of purging effects: Fire Barrier, Water Barrier, ..."
    /// </summary>
    class EffectPurgeRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Effect Purge", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "effect_purge" };

            var chanceMatch = Regex.Match(text, @"(\d+)%\s+chance\s+of\s+purging", RegexOptions.IgnoreCase);
            if (chanceMatch.Success) effect.chance = float.Parse(chanceMatch.Groups[1].Value);
            else effect.chance = 100f;

            // Everything after "Purges effects:" or "purging effects:"
            var listMatch = Regex.Match(text,
                @"purges?\s+effects?:\s+(.+)",
                RegexOptions.IgnoreCase);
            if (listMatch.Success)
                effect.description = listMatch.Groups[1].Value.Trim();

            return effect;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // CONDITIONAL WRAPPERS
    // All "Conditional Effect" rules store:
    //   condition_type  → the trigger (e.g. "hp_below", "on_spark", "on_critical")
    //   threshold       → numeric threshold where applicable
    //   sub_effects     → raw text of the sub-effect block (battle script parses this)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates sub-effects when HP drops below a threshold.
    /// Pattern: "When HP is below X% HP, activate the following effect(s): ..."
    /// </summary>
    class AddedEffectBasedOnHPRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Added Effect based on HP", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "hp_below" };

            var m = Regex.Match(text,
                @"When\s+HP\s+is\s+below\s+(\d+)%\s+HP,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["hp_threshold"] = float.Parse(m.Groups[1].Value);
                effect.required_item = m.Groups[2].Value.Trim(); // reuse field for sub-effect text
            }
            return effect;
        }
    }

    /// <summary>
    /// Adds sub-effects to the unit's BB/SBB/UBB when they are activated.
    /// Pattern: "Adds the following effect(s) to BB/SBB/UBB: ..."
    /// </summary>
    class AddedEffectToBraveBurstRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Added Effect to Brave Burst", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "added_to_bb" };

            // Which skills it applies to: BB/SBB/UBB, BB/SBB, SBB/UBB, BB, SBB
            var skillsMatch = Regex.Match(text,
                @"Adds\s+the\s+following\s+effect\(s\)\s+to\s+([\w/]+):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (skillsMatch.Success)
            {
                effect.stats.Add(skillsMatch.Groups[1].Value.Trim()); // e.g. "BB/SBB"
                effect.required_item = skillsMatch.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when the unit enters Overdrive (OD) mode.
    /// Pattern: "When unit overdrives, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterOverdrivingRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after Overdriving", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_overdrive" };
            var m = Regex.Match(text,
                @"When\s+unit\s+overdrives,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success) effect.required_item = m.Groups[1].Value.Trim();
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when dealing X or more damage in one attack.
    /// Pattern: "When X or more damage is dealt, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterDealingDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after dealing damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_damage_dealt" };
            var m = Regex.Match(text,
                @"When\s+([\d,]+)\s+or\s+more\s+damage\s+is\s+dealt,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["damage_threshold"] = float.Parse(m.Groups[1].Value.Replace(",", ""));
                effect.required_item = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when collecting X or more BC in one turn.
    /// Pattern: "When X or more BC is collected, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterReceivingBCRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after receiving BC", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_bc_collected" };
            var m = Regex.Match(text,
                @"When\s+(\d+)\s+or\s+more\s+BC\s+is\s+collected,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["bc_threshold"] = float.Parse(m.Groups[1].Value);
                effect.required_item = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when collecting X or more HC in one turn.
    /// Pattern: "When X or more HC is collected, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterReceivingHCRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after receiving HC", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_hc_collected" };
            var m = Regex.Match(text,
                @"When\s+(\d+)\s+or\s+more\s+HC\s+is\s+collected,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["hc_threshold"] = float.Parse(m.Groups[1].Value);
                effect.required_item = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects after performing X or more sparks in one turn.
    /// Pattern: "When X or more sparks are performed, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterSparkingRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after sparking", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_spark_count" };
            var m = Regex.Match(text,
                @"When\s+(\d+)\s+or\s+more\s+sparks?\s+are\s+performed,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["spark_threshold"] = float.Parse(m.Groups[1].Value);
                effect.required_item = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when taking X or more damage in one hit.
    /// Pattern: "When X or more damage is taken, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectAfterTakingDamageRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect after taking damage", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_damage_taken" };
            var m = Regex.Match(text,
                @"When\s+([\d,]+)\s+or\s+more\s+damage\s+is\s+taken,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["damage_threshold"] = float.Parse(m.Groups[1].Value.Replace(",", ""));
                effect.required_item = m.Groups[2].Value.Trim();
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates a sub-effect when HP is above/below a threshold (passive/always-on variant).
    /// Pattern: "Boosts BB Atk by X% when HP is above Y%"
    /// (The "Conditional Effect based on HP" wiki entry is a sub-category of BB-conditional boosts.)
    /// </summary>
    class ConditionalEffectBasedOnHPRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect based on HP", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "hp_threshold" };

            var m = Regex.Match(text,
                @"Boosts\s+BB\s+Atk\s+by\s+(\d+)%\s+when\s+HP\s+is\s+(above|below)\s+(\d+)%",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                effect.magnitudes["boost_percent"] = float.Parse(m.Groups[1].Value);
                effect.magnitudes["hp_threshold"]  = float.Parse(m.Groups[3].Value);
                effect.description = "hp_" + m.Groups[2].Value.ToLower(); // "hp_above" / "hp_below"
                effect.stats.Add("bb_atk");
            }
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when landing a critical hit.
    /// Pattern: "When landing a critical hit, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectOnCriticalRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect on Critical", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_critical" };
            var m = Regex.Match(text,
                @"When\s+landing\s+a\s+critical\s+hit,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success) effect.required_item = m.Groups[1].Value.Trim();
            return effect;
        }
    }

    /// <summary>
    /// Activates sub-effects when the unit guards.
    /// Pattern: "When guarding, activate the following effect(s): ..."
    /// </summary>
    class ConditionalEffectOnGuardRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Conditional Effect on Guard", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "conditional_effect", description = "on_guard" };
            var m = Regex.Match(text,
                @"When\s+guarding,\s+activate\s+the\s+following\s+effect\(s\):\s+(.+)",
                RegexOptions.IgnoreCase);
            if (m.Success) effect.required_item = m.Groups[1].Value.Trim();
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Resistance / Negation

    /// <summary>
    /// Damage Resistance: negates critical and/or elemental damage.
    /// Real: "Negates critical damage", "Negates critical and elemental damage"
    /// </summary>
    class DamageResistanceRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Damage Resistance", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Critical Resistance", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "damage_resistance" };
            if (text.Contains("critical",  StringComparison.OrdinalIgnoreCase)) effect.elements.Add("critical");
            if (text.Contains("elemental", StringComparison.OrdinalIgnoreCase)) effect.elements.Add("elemental");
            var pctM = Regex.Match(text, @"(\d+)%\s+resistance", RegexOptions.IgnoreCase);
            if (pctM.Success) effect.magnitudes["resistance_percent"] = float.Parse(pctM.Groups[1].Value);
            return effect;
        }
    }

    /// <summary>
    /// Spark Damage Resistance: negates or reduces spark damage taken.
    /// Real: "Negates spark damage" / "X% resistance to spark damage"
    /// </summary>
    class SparkDamageResistanceRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Spark Damage Resistance", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "spark_damage_resistance" };
            var pctM = Regex.Match(text, @"(\d+)%\s+resistance", RegexOptions.IgnoreCase);
            if (pctM.Success)
                effect.magnitudes["resistance_percent"] = float.Parse(pctM.Groups[1].Value);
            else
                effect.description = "negate"; // "Negates spark damage"
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Drop Rate Boost

    /// <summary>
    /// Drop Rate Boost: can boost BC, HC, and/or Item drop rates in one effect row.
    /// Real patterns: "Boosts BC drop rate by X% and HC drop rate by Y%"
    /// </summary>
    class DropRateBoostRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Drop Rate Boost", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "drop_rate_boost" };
            // Parse every "X drop rate by Y%" segment — covers BC, HC, Item, Zel, Karma
            foreach (Match m in Regex.Matches(text,
                @"(BC|HC|Item|Zel|Karma)\s+drop\s+rate\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase))
            {
                string key = m.Groups[1].Value.ToLower() + "_drop_rate";
                effect.magnitudes[key] = float.Parse(m.Groups[2].Value);
            }
            // Short-form: "Boosts Karma drop rate by X%"
            foreach (Match m in Regex.Matches(text,
                @"[Bb]oosts?\s+(BC|HC|Item|Zel|Karma)\s+drop\s+rate\s+by\s+(\d+)%",
                RegexOptions.IgnoreCase))
            {
                string key = m.Groups[1].Value.ToLower() + "_drop_rate";
                if (!effect.magnitudes.ContainsKey(key))
                    effect.magnitudes[key] = float.Parse(m.Groups[2].Value);
            }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Status Infliction (direct BB effect)

    /// <summary>
    /// Status Infliction: inflicts one or more ailments directly from a BB/SBB/UBB.
    /// Real patterns:
    ///   "X% chance of inflicting Curse"
    ///   "X% chance of inflicting Curse and Injury"
    ///   "X% chance of inflicting Curse and X% chance of inflicting Paralysis"
    /// </summary>
    class StatusInflictionRule : IEffectRule
    {
        public bool Matches(string name) =>
            name.Equals("Status Infliction", StringComparison.OrdinalIgnoreCase);

        public ParsedEffect Parse(string name, string text, string id)
        {
            var effect = new ParsedEffect { effect_type = "status_infliction", elements = new List<string>() };

            // Each "X% chance of inflicting A (and B ...)" segment
            foreach (Match m in Regex.Matches(text,
                @"(\d+)%\s+chance\s+of\s+inflicting\s+([\w,\s]+?)(?=\s+and\s+\d+%|$)",
                RegexOptions.IgnoreCase))
            {
                float  chance      = float.Parse(m.Groups[1].Value);
                string ailmentList = m.Groups[2].Value;
                foreach (var ailment in AllAilments)
                {
                    if (ailmentList.Contains(ailment, StringComparison.OrdinalIgnoreCase))
                    {
                        string key = ailment.ToLower() + "_chance";
                        if (!effect.magnitudes.ContainsKey(key) || effect.magnitudes[key] < chance)
                            effect.magnitudes[key] = chance;
                        if (!effect.elements.Contains(ailment.ToLower()))
                            effect.elements.Add(ailment.ToLower());
                    }
                }
            }
            return effect;
        }
    }

    #endregion

    #region Effect Rules — Generic Fallback

    class GenericRule : IEffectRule
    {
        public bool Matches(string name) => true; // always matches — must be last
        public ParsedEffect Parse(string name, string text, string id) => new ParsedEffect { effect_type = "generic" };
    }

    #endregion

    #region Effect Table Parsing

    Dictionary<string, List<EffectEntry>> ParseEffectsFromRenderedPage(HtmlDocument doc, string id)
    {
        var result = new Dictionary<string, List<EffectEntry>>();
        if (doc == null) return result;

        var skillBox = doc.DocumentNode
            .SelectSingleNode("//div[contains(@class,'unit-container')]//div[contains(@class,'unit-skills')]");
        if (skillBox == null) return result;

        var headers = skillBox.SelectNodes(".//div[b]");
        if (headers == null) return result;

        foreach (var header in headers)
        {
            string headerText = CleanWikiText(header.InnerText).Trim().ToLower();
            string key = MapSkillHeaderToKey(headerText);
            if (key == null) continue;

            var skillBlock = header.SelectSingleNode("following-sibling::div[1]");
            if (skillBlock == null) continue;

            var table = skillBlock.SelectSingleNode(".//table");
            if (table == null) continue;

            var parsed = ParseSkillTable(table, id);
            if (parsed.Count > 0) result[key] = parsed;
        }
        return result;
    }

    string MapSkillHeaderToKey(string h)
    {
        if (h.Contains("leader skill")) return "leader_skill_effects";
        if (h.Contains("extra skill")) return "extra_skill_effects";
        if (h.Contains("super bb")) return "sbb_skill_effects";
        if (h.Contains("ultimate bb")) return "ubb_skill_effects";
        if (h.Contains("brave burst")) return "bb_skill_effects";
        return "unknown";
    }

    List<EffectEntry> ParseSkillTable(HtmlNode table, string id)
    {
        var list = new List<EffectEntry>();
        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count == 0) return list;

        for (int i = 2; i < rows.Count; i++)
        {
            var cells = rows[i].SelectNodes("./td");
            if (cells == null || cells.Count == 0) continue;

            string name = CleanWikiText(cells[0].InnerText).Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name == "Frames" || name.StartsWith("Distribution") || name == "Effect Delay") continue;

            string value    = cells.Count >= 2 ? CleanWikiText(cells[1].InnerText).Trim() : "";
            string duration = cells.Count >= 3 ? CleanWikiText(cells[2].InnerText).Trim() : "";
            string target   = cells.Count >= 4 ? CleanWikiText(cells[3].InnerText).Trim() : "";

            var entry = new EffectEntry
            {
                Name = name, Value = value, Duration = duration, Target = target, UnitId = id,
                Parsed = ParseEffectByRules(name, value, duration, target, id)
            };
            list.Add(entry);
        }
        return list;
    }

    ParsedEffect ParseEffectByRules(string name, string value, string duration, string target, string id)
    {
        foreach (var rule in EffectRules)
        {
            if (rule.Matches(name))
            {
                var parsed = rule.Parse(name, value, id);

                var req = Regex.Match(value, @"Requires\s+(.+?)\s+equipped", RegexOptions.IgnoreCase);
                if (req.Success) parsed.required_item = req.Groups[1].Value.Trim();

                var dur = Regex.Match(duration, @"(\d+)\s*turn", RegexOptions.IgnoreCase);
                if (dur.Success) parsed.duration = int.Parse(dur.Groups[1].Value);

                Debug.Log(target);
                if      (target.Contains("all enemies",   StringComparison.OrdinalIgnoreCase)) parsed.target = "all_enemies";
                else if (target.Contains("all allies",    StringComparison.OrdinalIgnoreCase)) parsed.target = "all_allies";
                else if (target.Contains("to self",       StringComparison.OrdinalIgnoreCase)) parsed.target = "self";
                else if (target.Contains("single enemy",  StringComparison.OrdinalIgnoreCase)) parsed.target = "single_enemy";
                else if (target.Contains("single ally",   StringComparison.OrdinalIgnoreCase)) parsed.target = "single_ally";
                else if (target.Contains("random enemies",StringComparison.OrdinalIgnoreCase)) parsed.target = "random_enemy";

                return parsed;
            }
        }
        return new ParsedEffect { effect_type = "unknown" };
    }

    #endregion
}