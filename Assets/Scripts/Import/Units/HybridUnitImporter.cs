using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Hybrid unit data builder.
///
/// PRIMARY SOURCES (local files): the three regional info.json files
/// (global/eu/jp; Global wins, then EU, then JP), the F_UNIT_MST_* master
/// files (name, max level, display positions, sell data) and the
/// F_UNIT_EVO_MST file (evolution target, costs, materials).
///
/// FALLBACK SOURCE: the Global and EU fandom wikis. Exactly like the old
/// script, ALL wiki unit pages are loaded from the Unit_List pages and
/// indexed by the unit id found in each page's template; units are then
/// matched by ID (never by name). The wiki is only loaded when at least one
/// unit has an empty required field after reading the files, or when
/// fetchFlavorText is on (description / summon / fusion / evolution text,
/// which no local file contains).
/// Files always win: the wiki only fills fields that are empty.
/// </summary>
public class HybridUnitLoader : EditorWindow
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
    [Tooltip("If true, the wiki is loaded even when no unit is missing a required field (to get description/summon/fusion/evolution text).")]
    public bool fetchFlavorText = false;

    // ── Master Files ─────────────────────────────────────────────────────────
    [Header("Master Files (without .json extension)")]
    public string unitMasterFile1 = "F_UNIT_MST_1_Ver1084";
    public string unitMasterFile2 = "F_UNIT_MST_2_Ver1084";
    public string evoMasterFile = "F_UNIT_EVO_MST_Ver597";

    [Header("Behaviour")]
    public bool debugLogging = true;
    public int throttleMs = 50;
    public int rateLimitBackoffS = 30;
    public int maxRetriesPerUrl = 3;
    [Tooltip("Seconds before a single request is aborted and retried. UnityWebRequest's default timeout is 0 (infinite) — without this, one stalled connection can hang the whole import silently.")]
    public int requestTimeoutSeconds = 20;
    [Tooltip("Log progress every N units / wiki pages processed, so a long-running import doesn't look stalled.")]
    public int progressLogEvery = 25;
    [Tooltip("Consecutive empty list pages before we stop paginating the EU wiki (Global uses a hand-verified jump table instead).")]
    public int euEmptyPagesBeforeStop = 2;
    [Tooltip("Hard safety cap on EU list pages fetched, in case the wiki returns non-empty results for pages that don't really exist (e.g. shared nav links), which would otherwise make the empty-page check never trigger.")]
    public int euMaxListPages = 200;

    // ── Output ───────────────────────────────────────────────────────────────
    [Header("Output")]
    public string outputFolder = "JsonData/Units";

    // ── Runtime state ────────────────────────────────────────────────────────
    private Dictionary<string, JObject> _infoById = new Dictionary<string, JObject>();
    private Dictionary<string, JObject> _unitMasterById = new Dictionary<string, JObject>();
    private Dictionary<string, JObject> _evoByUnitId = new Dictionary<string, JObject>();
    private Dictionary<string, string> _evoFromById = new Dictionary<string, string>();

    // Wiki index, keyed by NormId(unit id read from the page template).
    private Dictionary<string, Dictionary<string, string>> _wikiFieldsById = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, string> _wikiOriginById = new Dictionary<string, string>(); // "Global" or "EU"
    private Dictionary<string, string> _wikiTitleById = new Dictionary<string, string>();

    // ── Editor window state ──────────────────────────────────────────────
    private bool _isRunning;
    private bool _cancelRequested;
    private string _status = "Ready";
    private Vector2 _scrollPosition;
    private EditorCoroutineRunner _runner;

    [MenuItem("Tools/Brave Frontier/Hybrid Unit Loader")]
    public static void OpenWindow()
    {
        var window = GetWindow<HybridUnitLoader>("BF Unit Loader");
        window.minSize = new Vector2(480, 650);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Brave Frontier — Hybrid Unit Loader",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Build unit JSON files from local regional data and master files, " +
            "using the Global/EU wikis (matched by unit ID) as fallback.",
            MessageType.Info);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Info JSON Sources", EditorStyles.boldLabel);

        globalInfoFile = EditorGUILayout.TextField("Global", globalInfoFile);
        euInfoFile = EditorGUILayout.TextField("Europe", euInfoFile);
        jpInfoFile = EditorGUILayout.TextField("Japan", jpInfoFile);

        regionPriority = (RegionPriority)EditorGUILayout.EnumPopup(
            "Region Priority", regionPriority);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Master Files", EditorStyles.boldLabel);

        unitMasterFile1 = EditorGUILayout.TextField(
            "Unit Master 1", unitMasterFile1);
        unitMasterFile2 = EditorGUILayout.TextField(
            "Unit Master 2", unitMasterFile2);
        evoMasterFile = EditorGUILayout.TextField(
            "Evolution Master", evoMasterFile);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Wiki Fallback", EditorStyles.boldLabel);

        fetchFlavorText = EditorGUILayout.Toggle(
            "Fetch Flavor Text", fetchFlavorText);

        euEmptyPagesBeforeStop = EditorGUILayout.IntField(
            "EU Empty Pages Before Stop", euEmptyPagesBeforeStop);
        euMaxListPages = EditorGUILayout.IntField(
            "EU Max List Pages", euMaxListPages);

        EditorGUILayout.HelpBox(
            "The wiki is loaded (all unit pages, indexed by unit ID) only if some unit " +
            "is missing a required field after reading the files, or if this option is " +
            "enabled. Units are then matched by ID, never by name.",
            MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Request Settings", EditorStyles.boldLabel);

        debugLogging = EditorGUILayout.Toggle("Debug Logging", debugLogging);
        throttleMs = EditorGUILayout.IntField("Throttle (ms)", throttleMs);
        rateLimitBackoffS = EditorGUILayout.IntField(
            "Rate Limit Backoff (s)", rateLimitBackoffS);
        maxRetriesPerUrl = EditorGUILayout.IntField(
            "Max Retries", maxRetriesPerUrl);
        requestTimeoutSeconds = EditorGUILayout.IntField(
            "Request Timeout (s)", requestTimeoutSeconds);
        progressLogEvery = EditorGUILayout.IntField(
            "Progress Log Interval", progressLogEvery);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        outputFolder = EditorGUILayout.TextField(
            "Folder under Assets", outputFolder);

        EditorGUILayout.HelpBox(
            "Output path: Assets/" + outputFolder,
            MessageType.None);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !_isRunning;

            if (GUILayout.Button("Build Unit JSON", GUILayout.Height(32)))
                StartImport();

            GUI.enabled = _isRunning;

            if (GUILayout.Button("Cancel", GUILayout.Height(32)))
                _cancelRequested = true;

            GUI.enabled = true;
        }

        if (_isRunning)
        {
            EditorGUILayout.HelpBox(
                $"Processed: {_unitsProcessed} | " +
                $"Wiki pages indexed: {_wikiFieldsById.Count} | " +
                $"Wiki needed: {_unitsNeedingWiki} | " +
                $"No wiki page: {_unitsWithNoWikiPageAtAll}",
                MessageType.None);
        }
    }

    void StartImport()
    {
        if (_isRunning)
            return;

        if (string.IsNullOrWhiteSpace(outputFolder) ||
            outputFolder.Contains("..") ||
            Path.IsPathRooted(outputFolder))
        {
            EditorUtility.DisplayDialog(
                "Invalid Output Folder",
                "Enter a relative folder path under Assets, such as BFUnits.",
                "OK");
            return;
        }

        if (throttleMs < 0 ||
            rateLimitBackoffS < 0 ||
            maxRetriesPerUrl < 1 ||
            requestTimeoutSeconds < 1 ||
            progressLogEvery < 1 ||
            euEmptyPagesBeforeStop < 1 ||
            euMaxListPages < 1)
        {
            EditorUtility.DisplayDialog(
                "Invalid Settings",
                "Throttle and backoff must be non-negative. Retries, timeout, " +
                "progress interval and the EU page limits must be at least 1.",
                "OK");
            return;
        }

        // Reset counters and cached data for a fresh import.
        _infoById.Clear();
        _unitMasterById.Clear();
        _evoByUnitId.Clear();
        _evoFromById.Clear();
        _wikiFieldsById.Clear();
        _wikiOriginById.Clear();
        _wikiTitleById.Clear();
        _neverTranslatable.Clear();

        _unitsProcessed = 0;
        _unitsNeedingWiki = 0;
        _unitsWithNoWikiPageAtAll = 0;
        _consecutiveRateLimits = 0;

        _cancelRequested = false;
        _isRunning = true;
        _status = "Preparing import...";

        _runner = new EditorCoroutineRunner();
        _runner.Start(RunEditorImport());

        Repaint();
    }

    IEnumerator RunEditorImport()
    {
        try
        {
            yield return Run();
            _status = _cancelRequested
                ? "Import cancelled."
                : "Import completed.";
        }
        finally
        {
            _isRunning = false;
            _runner = null;
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    void OnInspectorUpdate()
    {
        if (_isRunning)
            Repaint();
    }

    void OnDisable()
    {
        if (_runner != null)
        {
            _runner.Stop();
            _runner = null;
        }

        _isRunning = false;
        EditorUtility.ClearProgressBar();
    }


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

    // If any of these is empty after reading the files, the wiki is needed.
    private static readonly string[] RequiredFields = { "unitName", "element", "rarity", "cost", "maxlv", "gender", "kind" };

    private readonly HashSet<string> _neverTranslatable = new HashSet<string>();
    private int _unitsProcessed = 0;
    private int _unitsNeedingWiki = 0;
    private int _unitsWithNoWikiPageAtAll = 0;

    IEnumerator Run()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));

        // ── Phase 1: local files ────────────────────────────────────────────
        _status = "Loading local files...";
        LoadInfoJsons();
        LoadUnitMasterFile(unitMasterFile1);
        LoadUnitMasterFile(unitMasterFile2);
        LoadEvolutionMasterFile(evoMasterFile);
        if (debugLogging) Debug.Log($"[HybridUnitLoader] Unit master lookup contains {_unitMasterById.Count} entries.");

        var allIds = new HashSet<string>(_infoById.Keys);
        allIds.UnionWith(_unitMasterById.Keys);
        List<string> orderedIds = allIds.OrderBy(x => x).ToList();

        // ── Phase 2: do we need the wiki at all? ────────────────────────────
        _status = "Checking which units are missing fields...";
        int unitsMissingFields = 0;
        for (int n = 0; n < orderedIds.Count; n++)
        {
            if (_cancelRequested) yield break;
            if (MissingRequiredFields(orderedIds[n]).Count > 0) unitsMissingFields++;
            if (n % 200 == 199) yield return null;
        }

        bool needWiki = fetchFlavorText || unitsMissingFields > 0;
        if (debugLogging)
            Debug.Log($"[HybridUnitLoader] {unitsMissingFields} unit(s) have empty required fields after reading the files. " +
                      (needWiki ? "Loading the wikis." : "Wiki not needed."));

        // ── Phase 3: load ALL wiki pages and index them by unit id ──────────
        if (needWiki)
        {
            yield return ScrapeWikiList(GLOBAL_API_BASE, "Global", useHandVerifiedJumpTable: true);
            if (_cancelRequested) yield break;
            yield return ScrapeWikiList(EU_API_BASE, "EU", useHandVerifiedJumpTable: false);
            if (_cancelRequested) yield break;

            if (debugLogging)
                Debug.Log($"[HybridUnitLoader] Wiki text resolved for {_wikiFieldsById.Count} unit ids " +
                          $"({_wikiFieldsById.Count(kv => _wikiOriginById[kv.Key] == "Global")} from Global, " +
                          $"{_wikiFieldsById.Count(kv => _wikiOriginById[kv.Key] == "EU")} from EU).");
        }

        // ── Phase 4: build final per-unit files, cross-checking wiki by id ──
        int i = 0;
        foreach (string id in orderedIds)
        {
            if (_cancelRequested)
                break;

            _status = $"Writing unit {id} ({i + 1}/{orderedIds.Count})";

            EditorUtility.DisplayProgressBar(
                "Hybrid Unit Loader",
                _status,
                orderedIds.Count > 0 ? (float)i / orderedIds.Count : 0f);

            List<string> missing;
            JObject obj = BuildFromFiles(id, out missing);

            if (needWiki && (missing.Count > 0 || fetchFlavorText))
            {
                _unitsNeedingWiki++;
                ApplyWikiFields(id, obj);

                foreach (string key in missing)
                {
                    if (IsEmpty(obj[key]))
                        NeverTranslatable(
                            $"unit {id}: '{key}' not found in info/master/wiki");
                }
            }

            File.WriteAllText(
                Path.Combine(
                    Application.dataPath,
                    outputFolder,
                    $"unit_{id}.json"),
                obj.ToString(Formatting.Indented));

            _unitsProcessed++;
            i++;

            if (debugLogging && i % progressLogEvery == 0)
                Debug.Log($"[HybridUnitLoader] ...{i}/{orderedIds.Count} units written");

            if (i % 50 == 0)
                yield return null;
        }

        if (debugLogging)
        {
            Debug.Log($"[HybridUnitLoader] DONE. Wrote {_unitsProcessed} unit files, {_unitsNeedingWiki} used the wiki " +
                      $"({_unitsWithNoWikiPageAtAll} of those had no wiki page with a matching id).");

            if (_neverTranslatable.Count > 0)
                Debug.LogWarning("[HybridUnitLoader] Still missing after all sources:\n - " +
                                  string.Join("\n - ", _neverTranslatable.OrderBy(s => s)));
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 1 — local files
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

    void LoadEvolutionMasterFile(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return;
        TextAsset asset = EditorDataLoader.LoadJson(resourceName);
        if (asset == null)
        {
            Debug.LogWarning($"[HybridUnitLoader] Could not load evolution master '{resourceName}' — skipping.");
            return;
        }

        try
        {
            JArray arr = JArray.Parse(asset.text);
            foreach (JToken token in arr)
            {
                if (!(token is JObject e)) continue;
                string from = e["unitId"]?.ToString();
                string to = e["evoUnitId"]?.ToString();
                if (string.IsNullOrEmpty(from) || from == "0") continue;

                if (!_evoByUnitId.ContainsKey(from)) _evoByUnitId[from] = e;
                if (!string.IsNullOrEmpty(to) && to != "0" && !_evoFromById.ContainsKey(to))
                    _evoFromById[to] = from;
            }
            if (debugLogging)
                Debug.Log($"[HybridUnitLoader] Evolution master '{resourceName}' → {_evoByUnitId.Count} entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HybridUnitLoader] Failed to parse evolution master '{resourceName}': {ex.Message}");
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
    // Build unit from files
    // ══════════════════════════════════════════════════════════════════════

    static bool IsEmpty(JToken t) => t == null || t.Type == JTokenType.Null || string.IsNullOrWhiteSpace(t.ToString());

    static string FirstNonEmpty(params string[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";

    static string NormId(string id)
    {
        string t = (id ?? "").Trim().TrimStart('0');
        return t.Length == 0 ? "0" : t;
    }

    // Scalar fields: info.json first, MST second. Shared by the "missing
    // fields" pre-check and the real build so the two can never disagree.
    // element/gender/kind are strings in info.json ("earth", "female", "normal");
    // the MST only has numeric codes, so they are not mapped from there.
    void FillScalars(JObject obj, JObject src, JObject m)
    {
        // Name comes from info.json. The MST "unitName" is often just a localisation
        // key (e.g. "MST_UNIT_10011_NAME"), so it is used only if it isn't one.
        string mstName = m?["unitName"]?.ToString();
        if (!string.IsNullOrWhiteSpace(mstName) && Regex.IsMatch(mstName.Trim(), @"^MST_[A-Z0-9_]+$"))
            mstName = "";
        obj["unitName"] = FirstNonEmpty(Str(src, "name"), mstName);
        obj["element"] = FirstNonEmpty(Str(src, "element"));
        obj["rarity"] = FirstNonEmpty(Str(src, "rarity"), m?["rarity"]?.ToString());
        obj["cost"] = FirstNonEmpty(Str(src, "cost"), m?["unitCost"]?.ToString());
        obj["maxlv"] = FirstNonEmpty(m?["unitMaxLevel"]?.ToString());
        obj["gender"] = FirstNonEmpty(Str(src, "gender"));
        obj["kind"] = FirstNonEmpty(Str(src, "kind"));
        obj["no"] = FirstNonEmpty(Str(src, "guide_id"));
    }

    List<string> MissingRequiredFields(string id)
    {
        JObject src = _infoById.TryGetValue(id, out var s) ? s : null;
        JObject m = FindMasterEntry(id);

        var probe = new JObject();
        FillScalars(probe, src, m);

        var missing = new List<string>();
        foreach (string key in RequiredFields)
            if (IsEmpty(probe[key])) missing.Add(key);
        return missing;
    }

    JObject BuildFromFiles(string id, out List<string> missing)
    {
        missing = new List<string>();
        JObject src = _infoById.TryGetValue(id, out var s) ? s : null;
        JObject m = FindMasterEntry(id);

        var obj = new JObject();
        obj["id"] = id;

        // ── info.json: skills/stats + passthrough of every other top-level key
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

            var handled = new HashSet<string> { "stats", "leader skill", "extra skill", "bb", "sbb", "ubb", "id" };
            foreach (var p in src.Properties())
                if (!handled.Contains(p.Name)) obj[p.Name] = p.Value.DeepClone();
        }
        else
        {
            NeverTranslatable($"unit {id}: no info.json entry in any region — stats/bb/sbb/ubb/leader_skill/extra_skill unavailable");
        }

        FillScalars(obj, src, m);

        // ── Unit master: display positions and sell data, written verbatim
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

        if (m != null)
        {
            foreach (string field in UnitMasterFields)
            {
                if (!m.ContainsKey(field)) continue;
                JToken raw = m[field];

                if (field == "sellCaution")
                    obj["sellCaution"] = raw?.Type == JTokenType.Boolean ? raw.Value<bool>() : raw?.ToString() != "0";
                else
                    obj[field] = raw?.DeepClone() ?? JValue.CreateNull();
            }
        }
        else
        {
            NeverTranslatable($"unit {id}: no unit master entry — display positions/sellPrice/maxlv left blank");
        }

        AddEvolution(id, obj);

        foreach (string key in RequiredFields)
            if (IsEmpty(obj[key])) missing.Add(key);

        return obj;
    }

    void AddEvolution(string id, JObject obj)
    {
        obj["evofrom"] = _evoFromById.TryGetValue(id, out string f) ? f : "";
        obj["evointo"] = "";
        obj["evozelcost"] = "";
        obj["evokarmacost"] = "";
        obj["evoMaterials"] = new JArray();

        if (!_evoByUnitId.TryGetValue(id, out JObject evo)) return;

        string to = evo["evoUnitId"]?.ToString();
        obj["evointo"] = (to == "0" || to == null) ? "" : to;
        obj["evozelcost"] = evo["zel"]?.ToString() ?? "";
        obj["evokarmacost"] = evo["karma"]?.ToString() ?? "";

        // Aggregate slots 1..10 by (type,id): repeats = quantity, id 0 = empty slot.
        // evoMaterialType is passed through raw (meaning of 1 vs 2 is not interpreted).
        var counts = new List<(string type, string mid, int count)>();
        for (int n = 1; n <= 10; n++)
        {
            string mid = evo[$"evoMaterialId{n}"]?.ToString();
            string mtype = evo[$"evoMaterialType{n}"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(mid) || mid == "0") continue;

            int idx = counts.FindIndex(c => c.type == mtype && c.mid == mid);
            if (idx >= 0) counts[idx] = (mtype, mid, counts[idx].count + 1);
            else counts.Add((mtype, mid, 1));
        }

        var mats = new JArray();
        foreach (var c in counts)
            mats.Add(new JObject { ["type"] = c.type, ["id"] = c.mid, ["count"] = c.count });
        obj["evoMaterials"] = mats;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Wiki: load every unit page, index by unit id, then match by id
    // ══════════════════════════════════════════════════════════════════════

    // Fills only fields that are still empty, using the wiki page whose
    // template id matches this unit's id.
    void ApplyWikiFields(string id, JObject obj)
    {
        string key = NormId(id);
        if (!_wikiFieldsById.TryGetValue(key, out Dictionary<string, string> fields))
        {
            _unitsWithNoWikiPageAtAll++;
            return;
        }

        if (IsEmpty(obj["unitName"]) && _wikiTitleById.TryGetValue(key, out string title))
            obj["unitName"] = title;

        foreach (var kvp in fields)
        {
            if (kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;

            // Files always win: the wiki only fills what is still empty.
            if (!IsEmpty(obj[kvp.Key])) continue;

            bool keepRaw = kvp.Key.Equals("summon", StringComparison.OrdinalIgnoreCase)
                        || kvp.Key.Equals("fusion", StringComparison.OrdinalIgnoreCase)
                        || kvp.Key.Equals("evolution", StringComparison.OrdinalIgnoreCase);
            obj[kvp.Key] = keepRaw ? kvp.Value : CleanWikiText(kvp.Value);
        }
    }

    IEnumerator ScrapeWikiList(string apiBase, string originLabel, bool useHandVerifiedJumpTable)
    {
        var allTitles = new List<string>();
        var seen = new HashSet<string>();

        if (useHandVerifiedJumpTable)
        {
            List<string> listTitles = new List<string> { LIST_BASE };

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

            if (debugLogging) Debug.Log($"[HybridUnitLoader] [{originLabel}] Fetching {listTitles.Count} list page(s).");

            int done = 0;
            foreach (string listTitle in listTitles)
            {
                if (_cancelRequested) yield break;
                _status = $"[{originLabel}] list page {++done}/{listTitles.Count}: {listTitle}";

                List<string> titles = null;
                yield return FetchListTitles(apiBase, originLabel, listTitle, (t, raw) => titles = t);
                AddTitles(allTitles, seen, titles);
                yield return ThrottledWait();
            }
        }
        else
        {
            // EU's page numbering isn't verified — paginate generically and
            // stop after N consecutive empty pages. Adjust euEmptyPagesBeforeStop
            // if this cuts off too early or runs too long.
            _status = $"[{originLabel}] list page: {LIST_BASE}";
            List<string> baseTitles = null;
            yield return FetchListTitles(apiBase, originLabel, LIST_BASE, (t, raw) => baseTitles = t);
            AddTitles(allTitles, seen, baseTitles);
            yield return ThrottledWait();

            int consecutiveEmpty = 0;
            int pagesFetched = 0;
            for (int i = 100; consecutiveEmpty < euEmptyPagesBeforeStop && pagesFetched < euMaxListPages; i += 100)
            {
                if (_cancelRequested) yield break;

                string pageTitle = $"{LIST_BASE}:{i}";
                _status = $"[{originLabel}] list page: {pageTitle}";

                List<string> titles = null;
                int rawLinks = 0;
                yield return FetchListTitles(apiBase, originLabel, pageTitle, (t, raw) => { titles = t; rawLinks = raw; });

                if (rawLinks == 0) consecutiveEmpty++; else consecutiveEmpty = 0;
                AddTitles(allTitles, seen, titles);
                pagesFetched++;
                yield return ThrottledWait();
            }

            if (pagesFetched >= euMaxListPages && debugLogging)
                Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Hit the {euMaxListPages}-page safety cap without a clean empty-page stop — " +
                                   "the wiki may be returning non-empty results (e.g. shared nav links) for pages that don't exist. Check the last few pages manually.");
        }

        if (debugLogging) Debug.Log($"[HybridUnitLoader] [{originLabel}] {allTitles.Count} unit page titles found.");

        int fetched = 0;
        foreach (string title in allTitles)
        {
            if (_cancelRequested) yield break;

            _status = $"[{originLabel}] unit page {fetched + 1}/{allTitles.Count}: {title}";
            EditorUtility.DisplayProgressBar(
                "Hybrid Unit Loader",
                _status,
                allTitles.Count > 0 ? (float)fetched / allTitles.Count : 0f);

            yield return FetchAndStoreWikiUnit(apiBase, originLabel, title);
            fetched++;
            if (debugLogging && fetched % progressLogEvery == 0)
                Debug.Log($"[HybridUnitLoader] [{originLabel}] ...{fetched}/{allTitles.Count} unit pages fetched");
            yield return ThrottledWait();
        }
    }

    static void AddTitles(List<string> dst, HashSet<string> seen, List<string> titles)
    {
        if (titles == null) return;
        foreach (string t in titles)
            if (seen.Add(t)) dst.Add(t);
    }

    // Returns the unit page titles linked from a list page, plus the raw
    // link count (used by the EU "consecutive empty pages" stop check).
    IEnumerator FetchListTitles(string apiBase, string originLabel, string pageTitle, System.Action<List<string>, int> onDone)
    {
        string apiUrl = $"{apiBase}?action=parse&page={UnityWebRequest.EscapeURL(pageTitle)}" +
                        $"&prop=links&format=json&disablelimitreport=1";

        string responseText = null;
        yield return FetchURL(apiUrl,
            text => responseText = text,
            () => Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Giving up on: {pageTitle}"));

        var titles = new List<string>();
        int rawCount = 0;

        if (responseText != null)
        {
            try
            {
                JObject json = JObject.Parse(responseText);
                JArray links = json["parse"]?["links"] as JArray;
                if (links != null)
                {
                    rawCount = links.Count;
                    foreach (var link in links)
                    {
                        string title = link["*"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(title)) continue;
                        if (title.Contains(":")) continue; // skip meta/namespace pages
                        // No ASCII-only filter: JP-named pages on the Global wiki are included on purpose.
                        titles.Add(title);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] JSON parse error for {pageTitle}: {ex.Message}");
            }
        }

        onDone(titles, rawCount);
    }

    IEnumerator FetchAndStoreWikiUnit(string apiBase, string originLabel, string pageTitle)
    {
        string encodedTitle = UnityWebRequest.EscapeURL(pageTitle);
        string wikitextUrl = $"{apiBase}?action=query&titles={encodedTitle}" +
                             $"&prop=revisions&rvprop=content&rvslots=main&format=json";

        string wikitextJson = null;
        yield return FetchURL(wikitextUrl,
            text => wikitextJson = text,
            () => Debug.LogWarning($"[HybridUnitLoader] [{originLabel}] Could not fetch wikitext for: {pageTitle}"));

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
        string rawId = fields.ContainsKey("id") ? CleanWikiText(fields["id"]) : "";
        if (string.IsNullOrEmpty(rawId)) yield break;

        string id = NormId(rawId);

        // Global-priority: first writer wins. EU only fills ids Global didn't have.
        if (!_wikiFieldsById.ContainsKey(id))
        {
            _wikiFieldsById[id] = fields;
            _wikiTitleById[id] = pageTitle;
            _wikiOriginById[id] = originLabel;
        }
    }

    #region Network helpers

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
        yield return new EditorWait(baseWait + jitter);
    }

    int _consecutiveRateLimits = 0;

    IEnumerator HandleRateLimit(string url)
    {
        _consecutiveRateLimits++;
        float wait = Mathf.Min(rateLimitBackoffS * Mathf.Pow(2f, _consecutiveRateLimits - 1), 300f);
        Debug.LogWarning($"[RateLimit] Hit #{_consecutiveRateLimits} on {url} — backing off {wait:0}s");
        yield return new EditorWait(wait);
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
                www.Dispose();
                break;
            }

            long code = www.responseCode;
            Debug.LogWarning($"[Fetch] Failed (attempt {attempt}/{maxRetriesPerUrl}): {url} — HTTP {code} {www.error}");
            www.Dispose();

            if ((code == 403 || code == 429) && attempt < maxRetriesPerUrl)
                yield return HandleRateLimit(url);
            else
                break;
        }

        if (result != null) onSuccess(result);
        else onFail?.Invoke();
    }

    #endregion

    #region Template parsing/cleaning

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

    void NeverTranslatable(string field) => _neverTranslatable.Add(field);

    JToken DeepCloneOrEmpty(JToken t) => t != null ? t.DeepClone() : new JObject();
    string Str(JObject o, string key) => o?[key]?.ToString() ?? "";
}

internal sealed class EditorCoroutineRunner
{
    private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
    private double _waitUntil;
    private AsyncOperation _operation;
    private bool _stopped;

    public void Start(IEnumerator routine)
    {
        _stack.Clear();
        _stack.Push(routine);
        _stopped = false;
        _waitUntil = 0;
        _operation = null;

        EditorApplication.update += Tick;
    }

    public void Stop()
    {
        _stopped = true;
        EditorApplication.update -= Tick;
        _stack.Clear();
        _operation = null;
    }

    private void Tick()
    {
        if (_stopped)
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        if (_operation != null)
        {
            if (!_operation.isDone)
                return;

            _operation = null;
        }

        if (EditorApplication.timeSinceStartup < _waitUntil)
            return;

        // Process several immediately-completing yields per editor update,
        // but prevent a routine from monopolising the editor.
        for (int steps = 0; steps < 100; steps++)
        {
            if (_stack.Count == 0)
            {
                Stop();
                return;
            }

            IEnumerator current = _stack.Peek();

            bool moved;
            try
            {
                moved = current.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Stop();
                return;
            }

            if (!moved)
            {
                _stack.Pop();
                continue;
            }

            object yielded = current.Current;

            if (yielded == null)
                return;

            if (yielded is IEnumerator nested)
            {
                _stack.Push(nested);
                continue;
            }

            if (yielded is AsyncOperation asyncOperation)
            {
                _operation = asyncOperation;
                return;
            }

            if (yielded is EditorWait delay)
            {
                _waitUntil = EditorApplication.timeSinceStartup
                             + Math.Max(0, delay.Seconds);
                return;
            }

            // Unsupported yield instructions are not silently treated as
            // completed delays. Add an explicit handler if one is needed.
            Debug.LogWarning(
                $"[HybridUnitLoader] Unsupported editor yield type: " +
                $"{yielded.GetType().FullName}");
            return;
        }
    }
}

internal sealed class EditorWait
{
    public double Seconds { get; }

    public EditorWait(double seconds)
    {
        Seconds = seconds;
    }
}