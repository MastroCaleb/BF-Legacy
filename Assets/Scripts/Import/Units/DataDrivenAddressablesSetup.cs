// DataDrivenAddressablesSetup.cs
// One-time Addressables registration for the data-driven units pipeline:
//   - BF_Assets/content/unit/img, /cgg, /cgs + sound   (addresses mirror source paths)
//   - Assets/Particles/*.asset                          (address = effectId)
//   - EditorData SkillMSTs / EFFECT_GROUP_MST files     (runtime tables, labeled)
//   - Assets/AddressableContent/UnitData/*.json         (regen output, if present —
//     the regen tool registers its own output too, this is just a re-run safety net)
// Output groups are chunked (prefix "DD_") so the Addressables build does not
// OOM on one monolithic group.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class DataDrivenAddressablesSetup
{
    const int FilesPerGroup = 500;
    const int ParticlesPerGroup = 250;

    const string SpriteRoot  = "Assets/BF_Assets/content/unit/img";
    const string CggRoot     = "Assets/BF_Assets/content/unit/cgg";
    const string CgsRoot     = "Assets/BF_Assets/content/unit/cgs";
    const string SoundRoot   = "Assets/BF_Assets/content/sound";
    const string ParticlesRoot = "Assets/Particles";
    const string SkillMstRoot  = "Assets/EditorData/SkillMSTs";
    const string UnitDataRoot  = "Assets/AddressableContent/UnitData";
    const string EffectGroupMst1 = "Assets/EditorData/F_EFFECT_GROUP_MST_1.json";
    const string EffectGroupMst2 = "Assets/EditorData/F_EFFECT_GROUP_MST_2.json";

    const string GroupPrefix = "DD_";

    [MenuItem("Tools/Data-Driven Units/1. Setup Addressables")]
    public static void Setup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[DataDrivenUnits] No AddressableAssetSettings — open Window/Asset Management/Addressables/Groups once, then retry.");
            return;
        }

        settings.AddLabel(DataDrivenAddresses.SkillMstLabel);
        settings.AddLabel(DataDrivenAddresses.EffectGroupMstLabel);

        try
        {
            int total = 0;
            total += MarkFileSet(settings, SpriteRoot, "*.png", DataDrivenAddresses.SpriteRoot, "Img", false, null);
            total += MarkFileSet(settings, CggRoot, "*.csv", DataDrivenAddresses.CggRoot, "Cgg", false, null);
            total += MarkFileSet(settings, CgsRoot, "*.csv", DataDrivenAddresses.CgsRoot, "Cgs", false, null);
            total += MarkFileSet(settings, SoundRoot, "*.mp3", DataDrivenAddresses.SoundRoot, "Sound", false, null);
            total += MarkFileSet(settings, SoundRoot, "*.wav", DataDrivenAddresses.SoundRoot, "Sound", false, null);
            total += MarkFileSet(settings, SoundRoot, "*.ogg", DataDrivenAddresses.SoundRoot, "Sound", false, null);
            total += MarkParticles(settings);
            total += MarkSkillMsts(settings);
            total += MarkEffectGroupMsts(settings);
            total += MarkFileSet(settings, UnitDataRoot, "*.json", null, "UnitData", false,
                                 f => Path.GetFileNameWithoutExtension(f));

            AssetDatabase.SaveAssets();
            Debug.Log($"[DataDrivenUnits] Setup done — {total} addressable entries. " +
                      "Next: Tools/Data-Driven Units/2. Import BF Units (Data-Driven).");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Data-Driven Units/4. Toggle Data-Driven Units (Playtest)")]
    public static void ToggleDataDriven()
    {
        UnitRegistry.UseDataDriven = !UnitRegistry.UseDataDriven;
        Debug.Log($"[DataDrivenUnits] UseDataDriven = {UnitRegistry.UseDataDriven} " +
                  "(session-only; baked Unit SOs remain the default while both paths exist).");
    }

    [MenuItem("Tools/Data-Driven Units/4. Toggle Data-Driven Units (Playtest)", true)]
    public static bool ToggleDataDrivenValidate()
    {
        Menu.SetChecked("Tools/Data-Driven Units/4. Toggle Data-Driven Units (Playtest)", UnitRegistry.UseDataDriven);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    //  MARKING
    // ─────────────────────────────────────────────────────────────

    /// <summary>Marks files whose address is derived from their path under root.</summary>
    static int MarkFileSet(AddressableAssetSettings settings, string root, string searchPattern,
                           string addressRoot, string groupPrefix, bool recursive,
                           Func<string, string> customAddress)
    {
        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[DataDrivenUnits] {root} not found — skipping {groupPrefix}.");
            return 0;
        }

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] files = Directory.GetFiles(root, searchPattern, option).OrderBy(f => f).ToArray();
        int count = 0;

        for (int i = 0; i < files.Length; i++)
        {
            if (Progress(groupPrefix, i, files.Length)) return count;

            string address = customAddress != null
                ? customAddress(files[i])
                : $"{addressRoot}/{Path.GetFileNameWithoutExtension(files[i])}";

            var group = GetOrCreateGroup(settings, $"{GroupPrefix}{groupPrefix}_{i / FilesPerGroup:00}");
            if (MarkAsset(settings, files[i], address, group))
                count++;
        }
        return count;
    }

    /// <summary>Marks every ParticleEffect SO by its effectId (quirky file names exist, so read the SO).</summary>
    static int MarkParticles(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(ParticlesRoot))
        {
            Debug.LogWarning($"[DataDrivenUnits] {ParticlesRoot} not found — skipping particles.");
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:ParticleEffect", new[] { ParticlesRoot });
        var usedAddresses = new HashSet<string>();
        int count = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            if (Progress("Particles", i, guids.Length)) return count;

            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var pe = AssetDatabase.LoadAssetAtPath<ParticleEffect>(path);
            if (pe == null || string.IsNullOrEmpty(pe.effectId)) continue;

            if (!usedAddresses.Add(pe.effectId))
            {
                Debug.LogWarning($"[DataDrivenUnits] Duplicate ParticleEffect effectId '{pe.effectId}' at {path} — keeping the first, skipping this one.");
                continue;
            }

            var group = GetOrCreateGroup(settings, $"{GroupPrefix}Particles_{i / ParticlesPerGroup:00}");
            if (MarkAsset(settings, path, pe.effectId, group))
                count++;
        }
        return count;
    }

    static int MarkSkillMsts(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(SkillMstRoot))
        {
            Debug.LogWarning($"[DataDrivenUnits] {SkillMstRoot} not found — skipping skill MST tables.");
            return 0;
        }

        var group = GetOrCreateGroup(settings, $"{GroupPrefix}Tables");
        int count = 0;

        foreach (string file in Directory.GetFiles(SkillMstRoot, "*.json").OrderBy(f => f))
        {
            string assetPath = file.Replace('\\', '/');
            string address = $"EditorData/{SkillMstRoot.Split('/').Last()}/{Path.GetFileNameWithoutExtension(file)}";
            if (!MarkAsset(settings, assetPath, address, group)) continue;

            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
            entry?.SetLabel(DataDrivenAddresses.SkillMstLabel, true);
            count++;
        }
        return count;
    }

    static int MarkEffectGroupMsts(AddressableAssetSettings settings)
    {
        var group = GetOrCreateGroup(settings, $"{GroupPrefix}Tables");
        int count = 0;

        foreach (string file in new[] { EffectGroupMst1, EffectGroupMst2 })
        {
            if (!File.Exists(file))
            {
                Debug.LogWarning($"[DataDrivenUnits] {file} not found — skipping.");
                continue;
            }

            string address = $"EditorData/{Path.GetFileNameWithoutExtension(file)}";
            if (!MarkAsset(settings, file, address, group)) continue;

            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(file));
            entry?.SetLabel(DataDrivenAddresses.EffectGroupMstLabel, true);
            count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────────
    //  GROUP HELPERS (mirrors BfAddressablesSetup)
    // ─────────────────────────────────────────────────────────────

    static bool MarkAsset(AddressableAssetSettings settings, string assetPath, string address, AddressableAssetGroup group)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath.Replace('\\', '/'));
        if (string.IsNullOrEmpty(guid)) return false;

        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;
        return true;
    }

    static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
    {
        var group = settings.FindGroup(name);
        if (group != null) return group;

        group = settings.CreateGroup(name, false, false, false,
            new List<AddressableAssetGroupSchema> { new BundledAssetGroupSchema(), new ContentUpdateGroupSchema() });

        var bundled = group.GetSchema<BundledAssetGroupSchema>();
        bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        bundled.BuildPath.SetVariableByName(settings, "Local.BuildPath");
        bundled.LoadPath.SetVariableByName(settings, "Local.LoadPath");

        group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
        return group;
    }

    static bool Progress(string label, int index, int total)
    {
        return EditorUtility.DisplayCancelableProgressBar(
            "Data-Driven Units Setup", $"{label}: {index}/{total}", (float)index / Mathf.Max(1, total));
    }
}
