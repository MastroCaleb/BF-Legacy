using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Parses mission JSON files into Mission ScriptableObjects using Newtonsoft.Json.
///
/// EDITOR USAGE (battle JSONs + optional missions.json for metadata):
///   MissionParser.ParseAndSaveAll("Assets/Data/MissionJsons", "Assets/Data/Missions");
///
/// EDITOR USAGE (missions.json only, no battle data):
///   MissionParser.ParseAndSaveAllMetadataOnly("Assets/Data/MissionJsons/missions.json", "Assets/Data/Missions");
///
/// RUNTIME USAGE (returns in-memory objects, not saved as assets):
///   List<Mission> missions = MissionParser.ParseJson(jsonString);
/// </summary>
public static class MissionParser
{
    // Cached metadata from missions.json, keyed by numeric id string (e.g. "10")
    private static Dictionary<string, JObject> _metadataById   = null;
    // Same data, keyed by human-readable name (e.g. "The Thief's Hideout") for battle file lookups
    private static Dictionary<string, JObject> _metadataByName = null;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads missions.json into the metadata cache.
    /// Call this before ParseJson / ParseAndSaveAll to enrich missions with metadata.
    /// </summary>
    public static void LoadMissionMetadata(string metadataJsonPath)
    {
        if (!File.Exists(metadataJsonPath))
        {
            Debug.LogWarning($"[MissionParser] missions.json not found at: {metadataJsonPath}");
            _metadataById   = null;
            _metadataByName = null;
            return;
        }

        string rawJson = File.ReadAllText(metadataJsonPath);
        var root = JObject.Parse(rawJson);
        _metadataById   = new Dictionary<string, JObject>();
        _metadataByName = new Dictionary<string, JObject>();

        foreach (var entry in root)
        {
            var obj = entry.Value as JObject;
            if (obj == null) continue;

            _metadataById[entry.Key] = obj;

            // Also index by human-readable name so battle files can match by mission name
            string missionName = obj["name"]?.Value<string>();
            if (!string.IsNullOrEmpty(missionName))
                _metadataByName[missionName] = obj;
        }

        Debug.Log($"[MissionParser] Loaded metadata for {_metadataById.Count} missions.");
    }

    /// <summary>
    /// Parses a battle JSON string into Mission objects.
    /// If LoadMissionMetadata was called beforehand, metadata fields are applied automatically.
    /// </summary>
    public static List<Mission> ParseJson(string json)
    {
        var result = new List<Mission>();
        var root   = JObject.Parse(json);

        foreach (var missionEntry in root)
        {
            Mission mission     = ScriptableObject.CreateInstance<Mission>();
            mission.name        = missionEntry.Key;
            mission.missionName = missionEntry.Key;
            mission.rounds      = BuildRounds(missionEntry.Value["battles"] as JArray);
            ApplyMetadata(mission, missionEntry.Key);
            result.Add(mission);
        }

        return result;
    }

    /// <summary>
    /// Parses missions.json directly, saving one asset per entry with all available metadata.
    /// Rounds will be empty since there is no battle data.
    /// </summary>
    public static List<Mission> ParseMetadataOnly(string metadataJsonPath)
    {
        LoadMissionMetadata(metadataJsonPath);
        var result = new List<Mission>();
        if (_metadataById == null) return result;

        foreach (var kvp in _metadataById)
        {
            Mission mission     = ScriptableObject.CreateInstance<Mission>();
            mission.name        = kvp.Value["name"]?.Value<string>() ?? kvp.Key;
            mission.missionName = mission.name;
            mission.rounds      = new List<Round>();
            ApplyMetadata(mission, kvp.Key);
            result.Add(mission);
        }

        return result;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Parses all battle JSON files in jsonFolder and saves Mission assets to outputFolder.
    /// If missions.json exists in jsonFolder, its metadata is merged in automatically.
    /// Also creates/updates DungeonLevel assets grouped by the "dungeon" field in missions.json.
    /// </summary>
    /// <param name="onlyOverwriteZeroBattleExisting">
    /// When true: if a Mission asset already exists on disk at the target path AND it already
    /// has 1+ battle rounds (meaning it was properly imported before, and may since have been
    /// hand-modified in the inspector), it is left completely untouched. Assets that don't
    /// exist yet, or exist but still have 0 rounds (never properly imported), are written/overwritten
    /// as normal. When false, every asset is always overwritten (the old, unprotected behavior).
    /// </param>
    public static void ParseAndSaveAll(string jsonFolder, string outputFolder, bool onlyOverwriteZeroBattleExisting = false)
    {
        if (!Directory.Exists(jsonFolder))
        {
            Debug.LogError($"[MissionParser] JSON folder not found: {jsonFolder}");
            return;
        }

        // Load metadata if missions.json is present alongside the battle files
        string metadataPath = Path.Combine(jsonFolder, "missions.json");
        LoadMissionMetadata(metadataPath);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        string[] files = Directory.GetFiles(jsonFolder, "*.json");
        int created = 0;
        int updated = 0;
        int skipped = 0;

        foreach (string filePath in files)
        {
            // Skip missions.json itself — it's metadata, not a battle file
            if (Path.GetFileName(filePath).Equals("missions.json", StringComparison.OrdinalIgnoreCase))
                continue;

            string json = File.ReadAllText(filePath);
            List<Mission> missions = ParseJson(json);

            foreach (Mission mission in missions)
            {
                string safeName  = SanitizeFileName(mission.missionName);
                string assetPath = $"{outputFolder}/{safeName}.asset";

                SaveOrUpdateResult result = SaveOrUpdateMissionAsset(mission, assetPath, onlyOverwriteZeroBattleExisting);
                switch (result)
                {
                    case SaveOrUpdateResult.Created: created++; break;
                    case SaveOrUpdateResult.Updated: updated++; break;
                    case SaveOrUpdateResult.Skipped: skipped++; break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionParser] Done — {created} created, {updated} updated, {skipped} skipped (already had battle data).");

        BuildAndSaveDungeonLevels(outputFolder);
    }

    /// <summary>
    /// Saves one Mission asset per entry in missions.json, with no battle/round data.
    /// Useful when battle JSON files are not yet available.
    /// Also creates/updates DungeonLevel assets grouped by the "dungeon" field in missions.json.
    /// </summary>
    /// <param name="onlyOverwriteZeroBattleExisting">See ParseAndSaveAll for behavior.</param>
    public static void ParseAndSaveAllMetadataOnly(string metadataJsonPath, string outputFolder, bool onlyOverwriteZeroBattleExisting = false)
    {
        List<Mission> missions = ParseMetadataOnly(metadataJsonPath);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        int created = 0;
        int updated = 0;
        int skipped = 0;

        foreach (Mission mission in missions)
        {
            string safeName  = SanitizeFileName(mission.missionName);
            string assetPath = $"{outputFolder}/{safeName}.asset";

            SaveOrUpdateResult result = SaveOrUpdateMissionAsset(mission, assetPath, onlyOverwriteZeroBattleExisting);
            switch (result)
            {
                case SaveOrUpdateResult.Created: created++; break;
                case SaveOrUpdateResult.Updated: updated++; break;
                case SaveOrUpdateResult.Skipped: skipped++; break;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionParser] Done — {created} created, {updated} updated, {skipped} skipped (already had battle data).");

        BuildAndSaveDungeonLevels(outputFolder);
    }

    private enum SaveOrUpdateResult { Created, Updated, Skipped }

    /// <summary>
    /// Writes a freshly-parsed Mission into the asset at assetPath.
    /// - No asset there yet -> creates it.
    /// - Asset exists but has 0 battle rounds (never properly imported) -> overwrites it in place.
    /// - Asset exists and already has 1+ battle rounds -> left untouched if onlyOverwriteZeroBattleExisting is true.
    /// </summary>
    private static SaveOrUpdateResult SaveOrUpdateMissionAsset(Mission mission, string assetPath, bool onlyOverwriteZeroBattleExisting)
    {
        Mission existing = AssetDatabase.LoadAssetAtPath<Mission>(assetPath);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(mission, assetPath);
            Debug.Log($"[MissionParser] Created: {assetPath}");
            return SaveOrUpdateResult.Created;
        }

        bool existingHasBattles = existing.rounds != null && existing.rounds.Count > 0;
        if (onlyOverwriteZeroBattleExisting && existingHasBattles)
        {
            Debug.Log($"[MissionParser] Skipped '{mission.missionName}' — existing asset already has {existing.rounds.Count} battle round(s).");
            return SaveOrUpdateResult.Skipped;
        }

        // Overwrite the existing asset's data in place (keeps the same file/GUID, so references stay intact).
        EditorUtility.CopySerialized(mission, existing);
        EditorUtility.SetDirty(existing);
        Debug.Log($"[MissionParser] Updated: {assetPath}");
        return SaveOrUpdateResult.Updated;
    }

    /// <summary>
    /// Reads _metadataById to group missions by dungeon name, then creates one
    /// DungeonLevel asset per dungeon with missions sorted by numeric id (ascending).
    /// Must be called after Mission assets are already saved to outputFolder.
    ///
    /// If a DungeonLevel asset already exists at the target path, it is loaded and only its
    /// levelName/missions are updated — bg, backGroundSams, foreGroundSams, and bgm are left
    /// exactly as they were (never reset to null/empty).
    /// </summary>
    private static void BuildAndSaveDungeonLevels(string outputFolder)
    {
        if (_metadataById == null)
        {
            Debug.LogWarning("[MissionParser] No metadata loaded — skipping DungeonLevel creation.");
            return;
        }

        // Group mission ids by dungeon name
        var dungeonToIds = new Dictionary<string, List<int>>();

        foreach (var kvp in _metadataById)
        {
            string dungeonName = kvp.Value["dungeon"]?.Value<string>();
            if (string.IsNullOrEmpty(dungeonName)) continue;

            if (!int.TryParse(kvp.Key, out int numericId)) continue;

            if (!dungeonToIds.ContainsKey(dungeonName))
                dungeonToIds[dungeonName] = new List<int>();

            dungeonToIds[dungeonName].Add(numericId);
        }

        string dungeonFolder = Path.Combine(outputFolder, "DungeonLevels");
        if (!Directory.Exists(dungeonFolder))
            Directory.CreateDirectory(dungeonFolder);

        int created = 0;
        int updated = 0;

        foreach (var kvp in dungeonToIds)
        {
            string dungeonName = kvp.Key;
            kvp.Value.Sort(); // ascending numeric id order

            string levelPath = $"{dungeonFolder}/{SanitizeFileName(dungeonName)}.asset";

            // Try to reuse an existing asset so bg/backGroundSams/foreGroundSams/bgm survive re-imports.
            DungeonLevel level  = AssetDatabase.LoadAssetAtPath<DungeonLevel>(levelPath);
            bool isNewAsset     = level == null;

            if (isNewAsset)
            {
                level = ScriptableObject.CreateInstance<DungeonLevel>();
                level.bg             = null;
                level.backGroundSams = new List<TextAsset>();
                level.foreGroundSams = new List<TextAsset>();
                level.bgm            = null;
            }

            level.name      = dungeonName;
            level.levelName = dungeonName;
            level.missions  = new List<Mission>();
            // NOTE: bg, backGroundSams, foreGroundSams, bgm intentionally left untouched here.

            foreach (int id in kvp.Value)
            {
                // Resolve the saved Mission asset by its human-readable name
                string missionName = _metadataById[id.ToString()]["name"]?.Value<string>();
                if (string.IsNullOrEmpty(missionName)) continue;

                string assetPath     = $"{outputFolder}/{SanitizeFileName(missionName)}.asset";
                Mission missionAsset = AssetDatabase.LoadAssetAtPath<Mission>(assetPath);

                if (missionAsset != null)
                    level.missions.Add(missionAsset);
                else
                    Debug.LogWarning($"[MissionParser] DungeonLevel '{dungeonName}': Mission asset not found at {assetPath}");
            }

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(level, levelPath);
                created++;
                Debug.Log($"[MissionParser] Created DungeonLevel: {levelPath}");
            }
            else
            {
                EditorUtility.SetDirty(level);
                updated++;
                Debug.Log($"[MissionParser] Updated DungeonLevel (missions only): {levelPath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionParser] Done — {created} DungeonLevel asset(s) created, {updated} updated in place.");
    }
#endif

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string SanitizeFileName(string name)
    {
        // Replace all characters invalid in Unity asset paths, then spaces
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = name;
        foreach (char c in invalid)
            result = result.Replace(c, '_');
        return result.Replace(' ', '_');
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    private static void ApplyMetadata(Mission mission, string nameOrId)
    {
        if (_metadataById == null) return;

        // Battle files are keyed by mission name — try name lookup first, then id
        JObject m = null;
        if (_metadataByName != null)
            _metadataByName.TryGetValue(nameOrId, out m);
        if (m == null)
            _metadataById.TryGetValue(nameOrId, out m);
        if (m == null)
            return;

        // Prefer the human-readable name from metadata over the raw id key
        string metaName = m["name"]?.Value<string>();
        if (!string.IsNullOrEmpty(metaName))
        {
            mission.missionName = metaName;
            mission.name        = metaName;
        }

        mission.missionId         = m["id"]?.Value<string>();
        mission.description       = m["desc"]?.Value<string>();
        mission.areaName          = m["area"]?.Value<string>();
        mission.landName          = m["land"]?.Value<string>();
        mission.difficulty        = m["difficulty"]?.Value<int>()   ?? 0;
        mission.battleCount       = m["battle_count"]?.Value<int>() ?? 0;
        mission.canContinue       = m["continue"]?.Value<bool>()    ?? false;
        mission.experienceReward  = m["xp"]?.Value<int>()           ?? 0;
        mission.zelReward         = m["zel"]?.Value<int>()          ?? 0;
        mission.energyCost        = m["energy_use"]?.Value<int>()   ?? 0;
        mission.karmaReward       = m["karma"]?.Value<int>()        ?? 0;
        mission.requiresMissionId = m["requires"]?.Value<string>();
    }

    // -------------------------------------------------------------------------
    // Internal builders
    // -------------------------------------------------------------------------

    private static List<Round> BuildRounds(JArray battles)
    {
        var rounds = new List<Round>();
        if (battles == null) return rounds;

        foreach (JToken battle in battles)
        {
            var round = new Round { enemies = new List<Enemy>() };

            if (battle["monsters"] is JArray monsters)
                foreach (JToken m in monsters)
                    round.enemies.Add(BuildEnemy(m));

            rounds.Add(round);
        }

        return rounds;
    }

    private static Enemy BuildEnemy(JToken m)
    {
        string[] parts = m["act counts"]?.Value<string>()?.Split('~');
        return new Enemy
        {
            unitId          = m["unit id"]?.Value<int>().ToString(),
            actCounts       = new ActCounts
            {
                min = parts != null ? int.Parse(parts[0]) : 1,
                max = parts != null ? int.Parse(parts[1]) : 1
            },
            health          = m["stats"]?["hp"]?.Value<int>()  ?? 0,
            atk             = m["stats"]?["atk"]?.Value<int>() ?? 0,
            def             = m["stats"]?["def"]?.Value<int>() ?? 0,
            aiType          = m["ai id"]?.Value<int>()         ?? 0,
            karmaDropCount  = m["karma drop count"]?.Value<float>() ?? 0,
            karmaMaxDrop    = m["karma max drop"]?.Value<float>()   ?? 0,
            zelDropCount    = m["zel drop count"]?.Value<float>()   ?? 0,
            zelMaxDrop      = m["zel max drop"]?.Value<float>()     ?? 0,
            conditionResist = BuildConditionResist(m["condition resists"]),
            itemDrops       = BuildItemDrops(m["item drop"] as JArray),
            treasureDrop    = BuildTreasureDrop(m["treasure drop"]),
            unitDrop        = BuildUnitDrop(m["unit drop"]),
            actions         = BuildActions(m["ai"]?["actions"] as JArray)
        };
    }

    private static List<Action> BuildActions(JArray aiActions)
    {
        var actions = new List<Action>();
        if (aiActions == null) return actions;

        foreach (JToken a in aiActions)
        {
            actions.Add(new Action
            {
                priority         = a["priority"]?.Value<int>()        ?? 0,
                percentChance    = a["percent"]?.Value<float>()       ?? 0,
                targetType       = TranslateTargetType(a["target type"]?.Value<int>() ?? 2),
                targetCondition  = TranslateTargetCondition(a["target conditions"]?.Value<string>()),
                actionType       = TranslateActionType(a["action"]?["type"]?.Value<string>()),
                actionParameters = a["action"]?["parameters"]?.Value<string>(),
                selfConditions   = BuildConditions(a["conditions/set parameters"] as JArray),
                partyConditions  = BuildConditions(a["party conditions/set parameters"] as JArray)
            });
        }

        return actions;
    }

    private static List<AiCondition> BuildConditions(JArray condArray)
    {
        var list = new List<AiCondition>();
        if (condArray == null) return list;

        foreach (JToken c in condArray)
            list.Add(new AiCondition
            {
                type       = c["type"]?.Value<string>(),
                parameters = c["parameters"]?.Value<string>()
            });

        return list;
    }

    private static ActionType TranslateActionType(string raw)
    {
        return raw switch
        {
            "attack"   => ActionType.Attack,
            "skill"    => ActionType.Skill,
            "wait"     => ActionType.Wait,
            "guard"    => ActionType.Guard,
            "turn_end" => ActionType.TurnEnd,
            _          => ActionType.Attack
        };
    }

    // JSON: 1 = self, 2 = enemy party (player squad), 3 = ally party (other enemies)
    private static ActionTargetType TranslateTargetType(int jsonType)
    {
        return jsonType switch
        {
            1 => ActionTargetType.Self,
            2 => ActionTargetType.EnemyParty,
            3 => ActionTargetType.AllyParty,
            _ => ActionTargetType.EnemyParty
        };
    }

    private static TargetCondition TranslateTargetCondition(string raw)
    {
        return raw switch
        {
            "hp_min"        => TargetCondition.HpMin,
            "hp_max"        => TargetCondition.HpMax,
            "hp_25pr_over"  => TargetCondition.HpOver25,
            "hp_25pr_under" => TargetCondition.HpUnder25,
            "hp_50pr_over"  => TargetCondition.HpOver50,
            "hp_50pr_under" => TargetCondition.HpUnder50,
            "hp_75pr_over"  => TargetCondition.HpOver75,
            "hp_75pr_under" => TargetCondition.HpUnder75,
            "atk_min"       => TargetCondition.AtkMin,
            "atk_max"       => TargetCondition.AtkMax,
            "def_min"       => TargetCondition.DefMin,
            "def_max"       => TargetCondition.DefMax,
            "atk_down"      => TargetCondition.AtkDown,
            "def_down"      => TargetCondition.DefDown,
            "heal_down"     => TargetCondition.HealDown,
            "stdown_buff"   => TargetCondition.StatDebuffed,
            "stup_buff"     => TargetCondition.StatBuffed,
            "bad_status"    => TargetCondition.BadStatus,
            "poison"        => TargetCondition.Poison,
            "paralysis"     => TargetCondition.Paralysis,
            "guard"         => TargetCondition.Guard,
            "weakness"      => TargetCondition.Weakness,
            "bb_100pr"      => TargetCondition.BbFull,
            "bb_50pr_over"  => TargetCondition.BbOver50,
            "bb_50pr_under" => TargetCondition.BbUnder50,
            "bb_attack"     => TargetCondition.BbAttack,
            "bb_heal"       => TargetCondition.BbHeal,
            "bb_support"    => TargetCondition.BbSupport,
            "elem_fire"     => TargetCondition.ElemFire,
            "elem_water"    => TargetCondition.ElemWater,
            "elem_tree"     => TargetCondition.ElemEarth,
            "elem_earth"    => TargetCondition.ElemEarth,
            "elem_thunder"  => TargetCondition.ElemThunder,
            "elem_light"    => TargetCondition.ElemLight,
            "elem_dark"     => TargetCondition.ElemDark,
            "non"           => TargetCondition.Non,
            _               => TargetCondition.Random
        };
    }

    private static ConditionResist BuildConditionResist(JToken r)
    {
        if (r == null) return new ConditionResist();
        return new ConditionResist
        {
            curse     = r["curse%"]?.Value<float>()     ?? 0,
            injury    = r["injury%"]?.Value<float>()    ?? 0,
            paralysis = r["paralysis%"]?.Value<float>() ?? 0,
            poison    = r["poison%"]?.Value<float>()    ?? 0,
            sick      = r["sick%"]?.Value<float>()      ?? 0,
            weaken    = r["weaken%"]?.Value<float>()    ?? 0
        };
    }

    private static List<ItemDrop> BuildItemDrops(JArray drops)
    {
        var list = new List<ItemDrop>();
        if (drops == null) return list;

        foreach (JToken d in drops)
            list.Add(new ItemDrop
            {
                itemName   = d["_name"]?.Value<string>(),
                dropChance = d["percent"]?.Value<float>()    ?? 0,
                dropRest   = d["drop rest?"]?.Value<float>() ?? 0
            });

        return list;
    }

    private static TreasureDrop BuildTreasureDrop(JToken t)
    {
        if (t == null || !t.HasValues) return new TreasureDrop();
        return new TreasureDrop
        {
            chestDropChance = t["chest drop chance%"]?.Value<float>() ?? 0,
            bcOrHcAmount    = t["bc/hc amount"]?.Value<float>()       ?? 0,
            bcOrHcChance    = t["bc/hc chance%"]?.Value<float>()      ?? 0,
            itemName        = t["item name"]?.Value<string>(),
            itemDropChance  = t["item chance%"]?.Value<float>()       ?? 0,
            karmaAmount     = t["karma amount"]?.Value<float>()       ?? 0,
            karmaChance     = t["karma chance%"]?.Value<float>()      ?? 0,
            zelAmount       = t["zel amount"]?.Value<float>()         ?? 0,
            zelChance       = t["zel chance%"]?.Value<float>()        ?? 0
        };
    }

    private static UnitDrop BuildUnitDrop(JToken u)
    {
        if (u == null || !u.HasValues) return new UnitDrop();
        JToken bonus = u["bonus"];
        return new UnitDrop
        {
            unitId          = u["_name"]?.Value<string>(),
            dropChance      = u["percent"]?.Value<float>() ?? 0,
            level           = u["level"]?.Value<float>()   ?? 0,
            type            = u["type"]?.Value<string>(),
            bonusUnitId     = bonus?["_name"]?.Value<string>(),
            bonusDropChance = bonus?["percent"]?.Value<float>() ?? 0,
            bonusLevel      = bonus?["level"]?.Value<float>()   ?? 0,
            bonusType       = bonus?["type"]?.Value<string>()
        };
    }
}

// =============================================================================
// EDITOR MENU / WINDOW
// =============================================================================
#if UNITY_EDITOR
public static class MissionParserMenu
{
    [MenuItem("Tools/Parse Mission JSONs")]
    private static void ParseMissions()
    {
        MissionParser.ParseAndSaveAll(
            jsonFolder:   "Assets/Data/MissionJsons",
            outputFolder: "Assets/Data/Missions"
        );
    }

    [MenuItem("Tools/Parse Missions Metadata Only")]
    private static void ParseMissionsMetadataOnly()
    {
        MissionParser.ParseAndSaveAllMetadataOnly(
            metadataJsonPath: "Assets/Data/MissionJsons/missions.json",
            outputFolder:     "Assets/Data/Missions"
        );
    }

    [MenuItem("Tools/Mission Parser Window")]
    private static void OpenWindow()
    {
        MissionParserWindow.ShowWindow();
    }
}

/// <summary>
/// Simple editor window exposing the mission import options as UI controls,
/// including the "protect existing missions that already have battle data" checkbox.
/// </summary>
public class MissionParserWindow : EditorWindow
{
    private string _jsonFolder   = "Assets/Data/MissionJsons";
    private string _outputFolder = "Assets/Data/Missions";
    private bool   _onlyOverwriteZeroBattleExisting = false;

    public static void ShowWindow()
    {
        var window = GetWindow<MissionParserWindow>("Mission Parser");
        window.minSize = new Vector2(420, 160);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mission JSON Import", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _jsonFolder = EditorGUILayout.TextField("JSON Folder", _jsonFolder);
        _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

        EditorGUILayout.Space();
        _onlyOverwriteZeroBattleExisting = EditorGUILayout.ToggleLeft(
            "Only overwrite existing Mission SOs with 0 battles (protects hand-modified missions that were already properly imported)",
            _onlyOverwriteZeroBattleExisting);

        EditorGUILayout.Space();
        if (GUILayout.Button("Parse Mission JSONs"))
        {
            MissionParser.ParseAndSaveAll(_jsonFolder, _outputFolder, _onlyOverwriteZeroBattleExisting);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "DungeonLevel assets are always updated in place when they already exist — " +
            "bg, backGroundSams, foreGroundSams, and bgm are never reset by this importer.",
            MessageType.Info);
    }
}
#endif