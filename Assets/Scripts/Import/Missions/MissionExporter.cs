using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reverse of MissionParser: serializes Mission ScriptableObjects back into JSON
/// matching the original battle-JSON / missions.json format.
///
/// NOTE ON DATA LOSS: Mission/Enemy never stored several fields from the source
/// JSON because MissionParser.BuildEnemy() never reads them into any field:
/// monster "_name", "desc", "desc2", "element", "group name", "skills", and the
/// "ai"."name" label. Since MissionParser doesn't consume those fields either,
/// omitting them round-trips cleanly through your own parser. "_name" is filled
/// with unitId as a placeholder so the JSON stays valid.
///
/// EDITOR USAGE (battle JSONs, one file per mission + missions.json metadata):
///   MissionExporter.ExportAndSaveAll("Assets/Data/Missions", "Assets/Data/MissionJsonsExported");
///
/// RUNTIME USAGE (in-memory string):
///   string json = MissionExporter.ExportMissionsToBattleJson(missionList);
/// </summary>
public static class MissionExporter
{
    // -------------------------------------------------------------------------
    // Public API — battle JSON
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the root JSON object for a list of missions:
    /// { "<missionName>": { "battles": [ ... ] }, ... }
    /// </summary>
    public static string ExportMissionsToBattleJson(List<Mission> missions)
    {
        var root = new JObject();
        foreach (Mission mission in missions)
            root[mission.missionName] = BuildMissionBattleObject(mission);

        return root.ToString();
    }

    /// <summary>
    /// Builds a single-mission JSON string: { "<missionName>": { "battles": [ ... ] } }
    /// </summary>
    public static string ExportMissionToBattleJson(Mission mission)
    {
        var root = new JObject { [mission.missionName] = BuildMissionBattleObject(mission) };
        return root.ToString();
    }

    private static JObject BuildMissionBattleObject(Mission mission)
    {
        var battles = new JArray();
        if (mission.rounds != null)
            foreach (Round round in mission.rounds)
                battles.Add(BuildRoundObject(round));

        return new JObject { ["battles"] = battles };
    }

    private static JObject BuildRoundObject(Round round)
    {
        var monsters = new JArray();
        if (round.enemies != null)
            foreach (Enemy enemy in round.enemies)
                monsters.Add(BuildEnemyObject(enemy));

        return new JObject { ["monsters"] = monsters };
    }

    private static JObject BuildEnemyObject(Enemy e)
    {
        var obj = new JObject
        {
            ["_name"] = e.unitId, // original display name isn't stored on Enemy; placeholder
            ["act counts"] = $"{e.actCounts.min}~{e.actCounts.max}",
            ["ai id"] = e.aiType,
            ["condition resists"] = BuildConditionResistObject(e.conditionResist),
            ["item drop"] = BuildItemDropsArray(e.itemDrops),
            ["karma drop count"] = e.karmaDropCount,
            ["karma max drop"] = e.karmaMaxDrop,
            ["stats"] = new JObject
            {
                ["atk"] = e.atk,
                ["def"] = e.def,
                ["hp"] = e.health
            },
            ["treasure drop"] = BuildTreasureDropObject(e.treasureDrop),
            ["unit drop"] = BuildUnitDropObject(e.unitDrop),
            ["unit id"] = int.TryParse(e.unitId, out int uid) ? (JToken)uid : e.unitId,
            ["zel drop count"] = e.zelDropCount,
            ["zel max drop"] = e.zelMaxDrop
        };

        if (e.actions != null && e.actions.Count > 0)
            obj["ai"] = new JObject { ["actions"] = BuildActionsArray(e.actions) };

        return obj;
    }

    private static JArray BuildActionsArray(List<Action> actions)
    {
        var array = new JArray();
        foreach (Action a in actions)
        {
            array.Add(new JObject
            {
                ["priority"] = a.priority,
                ["percent"] = a.percentChance,
                ["target type"] = (int)a.targetType,
                ["target conditions"] = TargetConditionToString(a.targetCondition),
                ["action"] = new JObject
                {
                    ["type"] = ActionTypeToString(a.actionType),
                    ["parameters"] = a.actionParameters
                },
                ["conditions/set parameters"] = BuildConditionsArray(a.selfConditions),
                ["party conditions/set parameters"] = BuildConditionsArray(a.partyConditions)
            });
        }
        return array;
    }

    private static JArray BuildConditionsArray(List<AiCondition> conditions)
    {
        var array = new JArray();
        if (conditions == null) return array;
        foreach (AiCondition c in conditions)
            array.Add(new JObject { ["type"] = c.type, ["parameters"] = c.parameters });
        return array;
    }

    private static JObject BuildConditionResistObject(ConditionResist r)
    {
        return new JObject
        {
            ["curse%"] = r.curse,
            ["injury%"] = r.injury,
            ["paralysis%"] = r.paralysis,
            ["poison%"] = r.poison,
            ["sick%"] = r.sick,
            ["weaken%"] = r.weaken
        };
    }

    private static JArray BuildItemDropsArray(List<ItemDrop> drops)
    {
        var array = new JArray();
        if (drops == null) return array;
        foreach (ItemDrop d in drops)
            array.Add(new JObject
            {
                ["_name"] = d.itemName,
                ["drop rest?"] = d.dropRest,
                ["percent"] = d.dropChance
            });
        return array;
    }

    private static JObject BuildTreasureDropObject(TreasureDrop t)
    {
        bool isEmpty = t == null ||
            (t.chestDropChance == 0 && t.bcOrHcAmount == 0 && t.bcOrHcChance == 0 &&
             string.IsNullOrEmpty(t.itemName) && t.itemDropChance == 0 &&
             t.karmaAmount == 0 && t.karmaChance == 0 && t.zelAmount == 0 && t.zelChance == 0);

        if (isEmpty) return new JObject();

        return new JObject
        {
            ["bc/hc amount"] = t.bcOrHcAmount,
            ["bc/hc chance%"] = t.bcOrHcChance,
            ["chest drop chance%"] = t.chestDropChance,
            ["item chance%"] = t.itemDropChance,
            ["item name"] = t.itemName,
            ["karma amount"] = t.karmaAmount,
            ["karma chance%"] = t.karmaChance,
            ["zel amount"] = t.zelAmount,
            ["zel chance%"] = t.zelChance
        };
    }

    private static JObject BuildUnitDropObject(UnitDrop u)
    {
        bool isEmpty = u == null ||
            (string.IsNullOrEmpty(u.unitId) && u.dropChance == 0 && u.level == 0 &&
             string.IsNullOrEmpty(u.type) && string.IsNullOrEmpty(u.bonusUnitId));

        if (isEmpty) return new JObject();

        var obj = new JObject
        {
            ["_name"] = u.unitId,
            ["level"] = u.level,
            ["percent"] = u.dropChance,
            ["type"] = u.type
        };

        if (!string.IsNullOrEmpty(u.bonusUnitId))
        {
            obj["bonus"] = new JObject
            {
                ["_name"] = u.bonusUnitId,
                ["level"] = u.bonusLevel,
                ["percent"] = u.bonusDropChance,
                ["type"] = u.bonusType
            };
        }

        return obj;
    }

    // -------------------------------------------------------------------------
    // Public API — missions.json metadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds missions.json: { "<missionId>": { ...metadata... }, ... }
    /// dungeonByMission is optional — pass it if you want the "dungeon" field
    /// restored (Mission doesn't store its own dungeon, only DungeonLevel does).
    /// </summary>
    public static string ExportMissionsMetadata(List<Mission> missions, Dictionary<Mission, string> dungeonByMission = null)
    {
        var root = new JObject();
        for (int i = 0; i < missions.Count; i++)
        {
            Mission mission = missions[i];
            string key = !string.IsNullOrEmpty(mission.missionId) ? mission.missionId : i.ToString();

            string dungeon = null;
            dungeonByMission?.TryGetValue(mission, out dungeon);

            root[key] = BuildMetadataObject(mission, dungeon);
        }
        return root.ToString();
    }

    private static JObject BuildMetadataObject(Mission mission, string dungeonName)
    {
        var obj = new JObject
        {
            ["name"] = mission.missionName,
            ["id"] = mission.missionId,
            ["desc"] = mission.description,
            ["area"] = mission.areaName,
            ["land"] = mission.landName,
            ["difficulty"] = mission.difficulty,
            ["battle_count"] = mission.battleCount,
            ["continue"] = mission.canContinue,
            ["xp"] = mission.experienceReward,
            ["zel"] = mission.zelReward,
            ["energy_use"] = mission.energyCost,
            ["karma"] = mission.karmaReward,
            ["requires"] = mission.requiresMissionId
        };

        if (!string.IsNullOrEmpty(dungeonName))
            obj["dungeon"] = dungeonName;

        return obj;
    }

    // -------------------------------------------------------------------------
    // Reverse enum translations
    // -------------------------------------------------------------------------

    private static string ActionTypeToString(ActionType type)
    {
        return type switch
        {
            ActionType.Attack => "attack",
            ActionType.Skill => "skill",
            ActionType.Wait => "wait",
            ActionType.Guard => "guard",
            ActionType.TurnEnd => "turn_end",
            _ => "attack"
        };
    }

    private static string TargetConditionToString(TargetCondition condition)
    {
        return condition switch
        {
            TargetCondition.HpMin => "hp_min",
            TargetCondition.HpMax => "hp_max",
            TargetCondition.HpOver25 => "hp_25pr_over",
            TargetCondition.HpUnder25 => "hp_25pr_under",
            TargetCondition.HpOver50 => "hp_50pr_over",
            TargetCondition.HpUnder50 => "hp_50pr_under",
            TargetCondition.HpOver75 => "hp_75pr_over",
            TargetCondition.HpUnder75 => "hp_75pr_under",
            TargetCondition.AtkMin => "atk_min",
            TargetCondition.AtkMax => "atk_max",
            TargetCondition.DefMin => "def_min",
            TargetCondition.DefMax => "def_max",
            TargetCondition.AtkDown => "atk_down",
            TargetCondition.DefDown => "def_down",
            TargetCondition.HealDown => "heal_down",
            TargetCondition.StatDebuffed => "stdown_buff",
            TargetCondition.StatBuffed => "stup_buff",
            TargetCondition.BadStatus => "bad_status",
            TargetCondition.Poison => "poison",
            TargetCondition.Paralysis => "paralysis",
            TargetCondition.Guard => "guard",
            TargetCondition.Weakness => "weakness",
            TargetCondition.BbFull => "bb_100pr",
            TargetCondition.BbOver50 => "bb_50pr_over",
            TargetCondition.BbUnder50 => "bb_50pr_under",
            TargetCondition.BbAttack => "bb_attack",
            TargetCondition.BbHeal => "bb_heal",
            TargetCondition.BbSupport => "bb_support",
            TargetCondition.ElemFire => "elem_fire",
            TargetCondition.ElemWater => "elem_water",
            TargetCondition.ElemEarth => "elem_earth",
            TargetCondition.ElemThunder => "elem_thunder",
            TargetCondition.ElemLight => "elem_light",
            TargetCondition.ElemDark => "elem_dark",
            TargetCondition.Non => "non",
            _ => "random"
        };
    }

#if UNITY_EDITOR
    // -------------------------------------------------------------------------
    // Editor folder-based export
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads every Mission asset in missionsFolder, writes one battle JSON file
    /// per mission plus a combined missions.json into outputFolder.
    /// If missionsFolder/DungeonLevels contains DungeonLevel assets, their
    /// grouping is used to restore each mission's "dungeon" field.
    /// </summary>
    public static void ExportAndSaveAll(string missionsFolder, string outputFolder)
    {
        if (!Directory.Exists(missionsFolder))
        {
            Debug.LogError($"[MissionExporter] Missions folder not found: {missionsFolder}");
            return;
        }

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        List<Mission> missions = LoadAllMissions(missionsFolder);
        Dictionary<Mission, string> dungeonByMission = LoadDungeonGrouping(missionsFolder, missions);

        int savedBattles = 0;
        foreach (Mission mission in missions)
        {
            string safeName = SanitizeFileName(mission.missionName);
            string path = Path.Combine(outputFolder, $"{safeName}.json");
            File.WriteAllText(path, ExportMissionToBattleJson(mission));
            savedBattles++;
        }

        string metadataJson = ExportMissionsMetadata(missions, dungeonByMission);
        File.WriteAllText(Path.Combine(outputFolder, "missions.json"), metadataJson);

        AssetDatabase.Refresh();
        Debug.Log($"[MissionExporter] Done — {savedBattles} battle JSON file(s) + missions.json written to {outputFolder}.");
    }

    private static List<Mission> LoadAllMissions(string missionsFolder)
    {
        var result = new List<Mission>();
        string[] guids = AssetDatabase.FindAssets("t:Mission", new[] { missionsFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mission mission = AssetDatabase.LoadAssetAtPath<Mission>(path);
            if (mission != null) result.Add(mission);
        }
        return result;
    }

    private static Dictionary<Mission, string> LoadDungeonGrouping(string missionsFolder, List<Mission> missions)
    {
        var map = new Dictionary<Mission, string>();
        string dungeonFolder = Path.Combine(missionsFolder, "DungeonLevels");
        if (!Directory.Exists(dungeonFolder)) return map;

        string[] guids = AssetDatabase.FindAssets("t:DungeonLevel", new[] { dungeonFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DungeonLevel level = AssetDatabase.LoadAssetAtPath<DungeonLevel>(path);
            if (level?.missions == null) continue;

            foreach (Mission mission in level.missions)
                if (mission != null)
                    map[mission] = level.levelName;
        }
        return map;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = name;
        foreach (char c in invalid)
            result = result.Replace(c, '_');
        return result.Replace(' ', '_');
    }
#endif
}

// =============================================================================
// EDITOR MENU
// =============================================================================
#if UNITY_EDITOR
public static class MissionExporterMenu
{
    [MenuItem("Tools/Export Missions To JSON")]
    private static void ExportMissions()
    {
        MissionExporter.ExportAndSaveAll(
            missionsFolder: "Assets/Data/Missions",
            outputFolder: "Assets/Data/MissionJsonsExported"
        );
    }
}
#endif
