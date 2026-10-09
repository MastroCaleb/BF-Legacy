// SkillMstTable.cs
// Shared lookup over the SKILL_MST data (bbId → raw effectFrames string).
// Ported from JsonToSOUnit's LoadSkillMstFolder: first file that claims a bbId
// wins, matching the old per-run dictionary semantics exactly.
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class SkillMstTable
{
    readonly Dictionary<string, JObject> _entriesByBbId = new Dictionary<string, JObject>();

    public int EntryCount => _entriesByBbId.Count;

    public void AddFromText(string json)
    {
        JArray arr = JArray.Parse(json);
        foreach (JToken token in arr)
        {
            if (token is not JObject obj) continue;
            string bbId = obj["bbId"]?.ToString();
            if (!string.IsNullOrEmpty(bbId) && !_entriesByBbId.ContainsKey(bbId))
                _entriesByBbId[bbId] = obj;
        }
    }

    public bool TryGetEntry(string bbId, out JObject entry) => _entriesByBbId.TryGetValue(bbId, out entry);

    /// <summary>Raw "skillFrame:groupId,..." effectFrames field for the given bbId.</summary>
    public bool TryGetEffectFrames(string bbId, out string effectFramesRaw)
    {
        effectFramesRaw = null;
        if (!TryGetEntry(bbId, out JObject entry)) return false;
        effectFramesRaw = entry["effectFrames"]?.ToString();
        return true;
    }
}
