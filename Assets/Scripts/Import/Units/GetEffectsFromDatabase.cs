using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

public class GetEffectsFromDatabase : MonoBehaviour
{
    [Serializable]
    public class ProcEffectInfo
    {
        public string procId;
        public bool isPassive;
        public HashSet<string> fieldNames = new HashSet<string>();
    }

    public class ProcEffectGroup
    {
        public string procId;
        public ProcEffectInfo nonPassiveInfo;
        public ProcEffectInfo passiveInfo;
    }

    void Start()
    {
        ExtractEffectFieldsByProcId();
    }

    void ExtractEffectFieldsByProcId()
    {
        TextAsset dbAsset = EditorDataLoader.LoadJson("info");
        if (dbAsset == null)
        {
            Debug.LogError("Could not load info.json from Assets/EditorData");
            return;
        }

        JObject database = JObject.Parse(dbAsset.text);

        // proc id + isPassive -> effect info
        Dictionary<string, ProcEffectInfo> procEffectMap = new Dictionary<string, ProcEffectInfo>();

        // bb/sbb/ubb have effects nested under levels[]
        List<string> leveledSkillKeys = new List<string> { "bb", "sbb", "ubb" };
        // leader skill / extra skill have effects directly on the skill object
        List<string> flatSkillKeys = new List<string> { "leader skill", "extra skill" };

        int totalEffectsFound = 0;

        foreach (var unitEntry in database)
        {
            JToken unitData = unitEntry.Value;
            if (unitData == null || unitData.Type != JTokenType.Object) continue;

            // --- bb / sbb / ubb (non-passive) ---
            foreach (string skillKey in leveledSkillKeys)
            {
                JToken skill = unitData[skillKey];
                if (skill == null || skill.Type != JTokenType.Object) continue;

                JArray levels = skill["levels"] as JArray;
                if (levels == null) continue;

                foreach (JToken level in levels)
                {
                    JArray effects = level["effects"] as JArray;
                    if (effects == null) continue;

                    foreach (JToken effectToken in effects)
                    {
                        if (effectToken.Type != JTokenType.Object) continue;
                        ProcessEffect(effectToken as JObject, procEffectMap, false);
                        totalEffectsFound++;
                    }
                }
            }

            // --- leader skill / extra skill (passive) ---
            foreach (string skillKey in flatSkillKeys)
            {
                JToken skill = unitData[skillKey];
                if (skill == null || skill.Type != JTokenType.Object) continue;

                JArray effects = skill["effects"] as JArray;
                if (effects == null) continue;

                foreach (JToken effectToken in effects)
                {
                    if (effectToken.Type != JTokenType.Object) continue;
                    ProcessEffect(effectToken as JObject, procEffectMap, true);
                    totalEffectsFound++;
                }
            }
        }

        Debug.Log($"[EFFECTS] Total effects processed: {totalEffectsFound}");
        Debug.Log($"[EFFECTS] Total unique proc IDs: {procEffectMap.Count}");

        OutputResults(procEffectMap);
    }

    void ProcessEffect(JObject effect, Dictionary<string, ProcEffectInfo> procEffectMap, bool isPassive)
    {
        string procId = effect["proc id"]?.ToString()
                     ?? effect["passive id"]?.ToString()
                     ?? "unknown";

        string key = $"{procId}_{(isPassive ? "PASSIVE" : "NONPASSIVE")}";

        if (!procEffectMap.ContainsKey(key))
        {
            procEffectMap[key] = new ProcEffectInfo { procId = procId, isPassive = isPassive };
            Debug.Log($"[FOUND] New ID: {procId} ({(isPassive ? "PASSIVE" : "NON-PASSIVE")}) | Fields: {string.Join(", ", effect.Properties().Select(p => p.Name))}");
        }

        var info = procEffectMap[key];

        foreach (var prop in effect.Properties())
        {
            info.fieldNames.Add(prop.Name);

            // Recurse into nested objects (e.g. "buff #1": { "atk% buff (2)": -50, "proc chance%": 30 })
            if (prop.Value.Type == JTokenType.Object)
            {
                foreach (var nestedProp in ((JObject)prop.Value).Properties())
                {
                    // Store as "buff #1 > atk% buff (2)" so you know the nesting context
                    info.fieldNames.Add($"{prop.Name} > {nestedProp.Name}");
                }
            }
        }
    }

    void OutputResults(Dictionary<string, ProcEffectInfo> procEffectMap)
    {
        // Group by procId and separate passive/non-passive
        Dictionary<string, ProcEffectGroup> groupedProcs = new Dictionary<string, ProcEffectGroup>();

        foreach (var procEffect in procEffectMap.Values)
        {
            if (!groupedProcs.ContainsKey(procEffect.procId))
            {
                groupedProcs[procEffect.procId] = new ProcEffectGroup { procId = procEffect.procId };
            }

            if (procEffect.isPassive)
                groupedProcs[procEffect.procId].passiveInfo = procEffect;
            else
                groupedProcs[procEffect.procId].nonPassiveInfo = procEffect;
        }

        string output = "\n========== EFFECT FIELDS BY PROC ID (SEPARATED BY PASSIVE/NON-PASSIVE) ==========\n\n";
        output += $"Total Unique Proc IDs: {groupedProcs.Count}\n\n";

        var sortedProcs = groupedProcs.Values.OrderBy(p =>
        {
            if (int.TryParse(p.procId, out int num)) return (double)num;
            return double.MaxValue;
        }).ToList();

        int index = 1;
        foreach (var procGroup in sortedProcs)
        {
            output += $"\n{index}. ID: {procGroup.procId}\n";
            output += "=" + new string('=', procGroup.procId.Length + 3) + "\n";

            // Non-passive version
            if (procGroup.nonPassiveInfo != null)
            {
                output += $"\n   [NON-PASSIVE]\n";
                output += $"   Fields ({procGroup.nonPassiveInfo.fieldNames.Count}): {string.Join(", ", procGroup.nonPassiveInfo.fieldNames.OrderBy(f => f))}\n";
            }

            // Passive version
            if (procGroup.passiveInfo != null)
            {
                output += $"\n   [PASSIVE]\n";
                output += $"   Fields ({procGroup.passiveInfo.fieldNames.Count}): {string.Join(", ", procGroup.passiveInfo.fieldNames.OrderBy(f => f))}\n";
            }

            output += "\n";
            index++;
        }

        output += "\n========== CSV FORMAT ==========\n";
        output += "ID, Type, Field Count, Field Names\n";
        foreach (var procGroup in sortedProcs)
        {
            if (procGroup.nonPassiveInfo != null)
            {
                output += $"{procGroup.procId}, NON-PASSIVE, {procGroup.nonPassiveInfo.fieldNames.Count}, \"{string.Join("; ", procGroup.nonPassiveInfo.fieldNames.OrderBy(f => f))}\"\n";
            }
            if (procGroup.passiveInfo != null)
            {
                output += $"{procGroup.procId}, PASSIVE, {procGroup.passiveInfo.fieldNames.Count}, \"{string.Join("; ", procGroup.passiveInfo.fieldNames.OrderBy(f => f))}\"\n";
            }
        }

        Debug.Log(output);

        string filePath = Path.Combine(Application.dataPath, "Temp", "EffectFieldNames.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        File.WriteAllText(filePath, output);
        Debug.Log($"✓ Saved to: {filePath}");
    }
}