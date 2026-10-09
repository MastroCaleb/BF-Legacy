// EffectFrameResolver.cs
// SKILL_MST → EFFECT_GROUP_MST chain, ported from JsonToSOUnit's
// ParseEffectFrames / ParseNormalEffectFrames. Emits typed EffectFrameData with
// the absolute frame already computed (absoluteFrame = skillFrame + subFrame,
// 60 fps ticks) so the runtime never needs to redo the table math.
//
// Tables come from either editor files (regen tool) or Addressables
// (labels dd_skillmst / dd_effectgroupmst, set up by the Setup Addressables
// tool). resolveAudioClipPath maps "clip.mp3" → addressable clip address; the
// regen tool supplies the mp3/wav/ogg cascade, runtime callers may pass null
// (audio paths are normally already baked into the emitted ability JSONs).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class EffectFrameResolver
{
    static SkillMstTable s_skillMst;
    static EffectGroupMstTable s_effectGroups;
    static bool s_warnedNoTables;

    public static bool TablesLoaded => s_skillMst != null && s_effectGroups != null;

    /// <summary>Current SKILL_MST table (null until SetTables/LoadTablesFromAddressables).</summary>
    public static SkillMstTable SkillMst => s_skillMst;

    public static void SetTables(SkillMstTable skillMst, EffectGroupMstTable effectGroups)
    {
        s_skillMst = skillMst;
        s_effectGroups = effectGroups;
    }

    /// <summary>Loads the MST tables through Addressables by their labels (main thread).</summary>
    public static bool LoadTablesFromAddressables()
    {
        var skillHandle = Addressables.LoadAssetsAsync<TextAsset>(DataDrivenAddresses.SkillMstLabel, null);
        var groupHandle = Addressables.LoadAssetsAsync<TextAsset>(DataDrivenAddresses.EffectGroupMstLabel, null);

        IList<TextAsset> skills = skillHandle.WaitForCompletion();
        IList<TextAsset> groups = groupHandle.WaitForCompletion();

        if (skills == null || groups == null || skills.Count == 0 || groups.Count == 0)
        {
            Debug.LogWarning($"[EffectFrameResolver] MST tables not found via labels — " +
                             $"{skills?.Count ?? 0} skill files, {groups?.Count ?? 0} group files. " +
                             "Run Tools/Data-Driven Units/Setup Addressables.");
            return false;
        }

        var skillTable = new SkillMstTable();
        foreach (TextAsset t in skills) skillTable.AddFromText(t.text);

        var groupTable = new EffectGroupMstTable();
        foreach (TextAsset t in groups) groupTable.AddFromText(t.text);

        SetTables(skillTable, groupTable);
        Debug.Log($"[EffectFrameResolver] Loaded {skillTable.EntryCount} skill entries, {groupTable.GroupCount} effect groups.");
        return true;
    }

    /// <summary>
    /// SKILL_MST effectFrames ("skillFrame:groupId,...") → typed frames via the
    /// EFFECT_GROUP_MST chain. Sound-only sub-frames that don't line up with a
    /// particle entry still emit audio-only frames.
    /// </summary>
    public static List<EffectFrameData> ResolveSkillFrames(string raw, Func<string, string> resolveAudioClipPath = null)
    {
        var list = new List<EffectFrameData>();
        if (string.IsNullOrEmpty(raw)) return list;

        if (!TablesLoaded)
        {
            WarnNoTables();
            return list;
        }

        foreach (string entry in raw.Split(','))
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0].Trim(), out int skillFrame)) continue;
            string groupId = parts[1].Trim();

            bool hasGroup = s_effectGroups.TryGetGroup(groupId, out var particleEntries, out var soundEntries);
            bool hasParticles = hasGroup && particleEntries != null && particleEntries.Count > 0;
            bool hasSounds    = hasGroup && soundEntries != null && soundEntries.Count > 0;

            if (hasParticles)
            {
                foreach (var pe in particleEntries)
                {
                    string audioPath = null;
                    if (hasSounds)
                    {
                        var match = soundEntries.Find(s => s.subFrame == pe.subFrame);
                        if (match.clipName != null)
                            audioPath = resolveAudioClipPath?.Invoke(match.clipName);
                    }

                    for (int i = 0; i < pe.particleIds.Count; i++)
                    {
                        list.Add(new EffectFrameData
                        {
                            frame               = skillFrame + pe.subFrame,
                            battleEffectGroupId = groupId,
                            particleEffectId    = pe.particleIds[i],
                            placementType       = pe.placementType,
                            xAnchor             = pe.xAnchor,
                            yAnchor             = pe.yAnchor,
                            offsetX             = pe.offsetX,
                            offsetY             = pe.offsetY,
                            // only the first stacked particle carries the sound —
                            // avoids the same clip firing twice for one visual moment
                            audioClipPath       = i == 0 ? audioPath : null
                        });
                    }
                }
            }

            if (hasSounds)
            {
                foreach (var se in soundEntries)
                {
                    bool alreadyCovered = hasParticles && particleEntries.Exists(p => p.subFrame == se.subFrame);
                    if (alreadyCovered) continue;

                    list.Add(new EffectFrameData
                    {
                        frame               = skillFrame + se.subFrame,
                        battleEffectGroupId = groupId,
                        audioClipPath       = resolveAudioClipPath?.Invoke(se.clipName)
                    });
                }
            }

            if (!hasGroup)
                Debug.LogWarning($"[EffectFrameResolver] Effect group '{groupId}' referenced by SKILL_MST but not found in loaded EFFECT_GROUP_MST data.");
        }

        return list;
    }

    /// <summary>
    /// Normal-attack effectFrames ("frame:particleId[:placement],...") straight
    /// from the unit's own F_UNIT_MST entry — no groupId/anchor/offset data, so
    /// those default to centered/no-offset. The particleId doubles as a lookup
    /// key into the sound dictionary, matched by absolute frame.
    /// </summary>
    public static List<EffectFrameData> ResolveNormalFrames(string raw, Func<string, string> resolveAudioClipPath = null)
    {
        var list = new List<EffectFrameData>();
        if (string.IsNullOrEmpty(raw)) return list;

        bool tablesAvailable = TablesLoaded;
        if (!tablesAvailable) WarnNoTables();

        foreach (string entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = entry.Split(':');
            if (p.Length < 2) continue; // need at least frame + particleId

            if (!int.TryParse(p[0].Trim(), out int frame)) continue;

            List<string> ids = p[1].Trim().Split('@').Select(s => s.Trim()).ToList();
            int placementType = p.Length > 2 && int.TryParse(p[2].Trim(), out int pt) ? pt : 1;

            List<EffectGroupMstTable.SoundEntry> soundEntries = null;
            bool hasSounds = false;
            if (tablesAvailable)
                hasSounds = s_effectGroups.TryGetGroup(ids[0], out _, out soundEntries);

            string audioPath = null;
            if (hasSounds)
            {
                var match = soundEntries.Find(s => s.subFrame == frame);
                if (match.clipName != null)
                    audioPath = resolveAudioClipPath?.Invoke(match.clipName);
            }

            for (int i = 0; i < ids.Count; i++)
            {
                list.Add(new EffectFrameData
                {
                    frame               = frame,
                    battleEffectGroupId = "",
                    particleEffectId    = ids[i],
                    placementType       = placementType,
                    xAnchor             = 2,
                    yAnchor             = 2,
                    offsetX             = 0f,
                    offsetY             = 0f,
                    audioClipPath       = i == 0 ? audioPath : null
                });
            }

            // Sound sub-frames under this particleId-as-group that didn't line up
            // with this exact frame still need to fire on their own.
            if (hasSounds)
            {
                foreach (var se in soundEntries)
                {
                    if (se.subFrame == frame) continue;

                    list.Add(new EffectFrameData
                    {
                        frame               = se.subFrame,
                        battleEffectGroupId = "",
                        audioClipPath       = resolveAudioClipPath?.Invoke(se.clipName)
                    });
                }
            }
        }

        return list;
    }

    static void WarnNoTables()
    {
        if (s_warnedNoTables) return;
        s_warnedNoTables = true;
        Debug.LogWarning("[EffectFrameResolver] MST tables not loaded — call SetTables/LoadTablesFromAddressables first. Effect frames may be incomplete.");
    }
}
