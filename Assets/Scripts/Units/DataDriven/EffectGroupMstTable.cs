// EffectGroupMstTable.cs
// Shared lookup over the EFFECT_GROUP_MST data (battleEffectGroupId → particle
// entries + sound entries). Ported 1:1 from JsonToSOUnit's LoadEffectGroupMst,
// including the delimited field formats and first-wins duplicate handling:
//   "effectFrames": "subFrame:particleId:placement:xAnchor:yAnchor:offX:offY,..."
//   "xW6TKu9G":     "subFrame:clip.mp3,subFrame:clip.mp3,..."
// particleId may stack layered particles joined with '@'.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public class EffectGroupMstTable
{
    public struct ParticleEntry
    {
        public int subFrame;
        public List<string> particleIds;
        public int placementType;
        public int xAnchor;
        public int yAnchor;
        public float offsetX;
        public float offsetY;
    }

    public struct SoundEntry
    {
        public int subFrame;
        public string clipName;
    }

    readonly Dictionary<string, List<ParticleEntry>> _particlesByGroup = new Dictionary<string, List<ParticleEntry>>();
    readonly Dictionary<string, List<SoundEntry>> _soundsByGroup = new Dictionary<string, List<SoundEntry>>();

    public int GroupCount => _particlesByGroup.Count;

    public void AddFromText(string json)
    {
        JArray arr = JArray.Parse(json);
        foreach (JToken token in arr)
        {
            if (token is not JObject obj) continue;
            string groupId = obj["battleEffectGroupId"]?.ToString();
            if (string.IsNullOrEmpty(groupId)) continue;

            if (!_soundsByGroup.ContainsKey(groupId))
                _soundsByGroup[groupId] = ParseSounds(obj["xW6TKu9G"]?.ToString() ?? "");

            if (!_particlesByGroup.ContainsKey(groupId))
                _particlesByGroup[groupId] = ParseParticles(obj["effectFrames"]?.ToString() ?? "");
        }
    }

    static List<SoundEntry> ParseSounds(string soundRaw)
    {
        var list = new List<SoundEntry>();
        foreach (string entry in soundRaw.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0].Trim(), out int sf)) continue;
            list.Add(new SoundEntry { subFrame = sf, clipName = parts[1].Trim() });
        }
        return list;
    }

    static List<ParticleEntry> ParseParticles(string framesRaw)
    {
        var list = new List<ParticleEntry>();
        foreach (string entry in framesRaw.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = entry.Split(':');
            if (p.Length < 7) continue; // malformed — skip rather than guess

            if (!int.TryParse(p[0].Trim(), out int sf)) continue;

            // Layered particles are stacked with '@' (e.g. "907@906"); a normal
            // single-id entry just becomes a one-element list.
            List<string> ids = p[1].Trim().Split('@').Select(s => s.Trim()).ToList();

            list.Add(new ParticleEntry
            {
                subFrame      = sf,
                particleIds   = ids,
                placementType = int.TryParse(p[2], out int pt) ? pt : 1,
                xAnchor       = int.TryParse(p[3], out int xa) ? xa : 2,
                yAnchor       = int.TryParse(p[4], out int ya) ? ya : 2,
                offsetX = float.TryParse(p[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ox) ? ox : 0f,
                offsetY = float.TryParse(p[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float oy) ? oy : 0f
            });
        }
        return list;
    }

    public bool TryGetGroup(string groupId, out List<ParticleEntry> particles, out List<SoundEntry> sounds)
    {
        bool hasParticles = _particlesByGroup.TryGetValue(groupId, out particles);
        bool hasSounds    = _soundsByGroup.TryGetValue(groupId, out sounds);
        return hasParticles || hasSounds;
    }
}
