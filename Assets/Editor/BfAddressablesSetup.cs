using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Addresses deliberately equal the OLD Resources paths so runtime
/// path-building code (UnitRegistry, SamAnimator, OldSamAnimator,
/// PrefabCache) works unchanged:
///   Units/unit_10011/unit_10011
///   Sams/Effect_SAMS/sam/{folder}/{file}
///   Sams/Unit_SAMS/unit_sam/{folder}/{file}
///   {prefabName}
///
/// Raw .sam files are skipped entirely — they drop out of the build.
///
/// Usage: Tools/BF/Addressables/1. Create Groups And Mark Content
/// Then:  Window > Asset Management > Addressables > Analyze
///        (run "Check Duplicate Bundle Dependencies" > Fix Selected)
/// </summary>
public static class BfAddressablesSetup
{
    const int UnitsPerGroup = 150;       // unit folders per bundle group
    const int EffectDirsPerGroup = 100;  // effect SAM dirs per bundle group
    const int UnitSamDirsPerGroup = 64;  // unit SAM dirs per bundle group

    const string UnitsRoot = "Assets/AddressableContent/Units";
    const string SamEffectRoot = "Assets/AddressableContent/Sams/Effect_SAMS/sam";
    const string SamUnitRoot = "Assets/AddressableContent/Sams/Unit_SAMS/unit_sam";
    const string PrefabsRoot = "Assets/AddressableContent/Prefabs";

    static readonly string[] OwnedGroupPrefixes =
        { "Units_", "Sams_Effect_", "Sams_Units_", "Common_Prefabs" };

    [MenuItem("Tools/BF/Addressables/1. Create Groups And Mark Content")]
    public static void MarkAllContent()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[BfAddressables] No AddressableAssetSettings — open Window/Asset Management/Addressables/Groups once, then retry.");
            return;
        }

        try
        {
            int total = MarkUnits(settings);
            total += MarkSamFolders(settings, SamEffectRoot, "Sams_Effect_", "Sams/Effect_SAMS/sam", EffectDirsPerGroup);
            total += MarkSamFolders(settings, SamUnitRoot, "Sams_Units_", "Sams/Unit_SAMS/unit_sam", UnitSamDirsPerGroup);
            total += MarkPrefabs(settings);

            AssetDatabase.SaveAssets();
            Debug.Log($"[BfAddressables] Done — {total} entries marked. Next: Addressables Analyze > 'Check Duplicate Bundle Dependencies' > Fix Selected, then a Packed Play Mode test.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/BF/Addressables/2. Remove BF Content Groups")]
    public static void RemoveGroups()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        foreach (var group in settings.groups.Where(g => OwnedGroupPrefixes.Any(p => g.name.StartsWith(p))).ToList())
            settings.RemoveGroup(group);

        AssetDatabase.SaveAssets();
        Debug.Log("[BfAddressables] BF content groups removed.");
    }

    static int MarkUnits(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(UnitsRoot))
        {
            Debug.LogWarning($"[BfAddressables] {UnitsRoot} not found — skipping units.");
            return 0;
        }

        var dirs = Directory.GetDirectories(UnitsRoot).OrderBy(d => d).ToArray();
        int count = 0;

        for (int i = 0; i < dirs.Length; i++)
        {
            if (Progress("Units", i, dirs.Length)) return count;

            string dirName = Path.GetFileName(dirs[i]);
            var group = GetOrCreateGroup(settings, $"Units_{i / UnitsPerGroup:00}");

            // Top-level unit_{id}.asset only — Abilities/ become implicit bundle deps.
            foreach (var assetPath in Directory.GetFiles(dirs[i], "*.asset"))
            {
                if (MarkAsset(settings, assetPath, $"Units/{dirName}/{Path.GetFileNameWithoutExtension(assetPath)}", group))
                    count++;
            }
        }
        return count;
    }

    static int MarkSamFolders(AddressableAssetSettings settings, string root, string groupPrefix, string addressRoot, int dirsPerGroup)
    {
        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[BfAddressables] {root} not found — skipping.");
            return 0;
        }

        var dirs = Directory.GetDirectories(root).OrderBy(d => d).ToArray();
        int count = 0;

        for (int i = 0; i < dirs.Length; i++)
        {
            if (Progress(groupPrefix, i, dirs.Length)) return count;

            string dirName = Path.GetFileName(dirs[i]);
            var group = GetOrCreateGroup(settings, $"{groupPrefix}{i / dirsPerGroup:00}");

            foreach (var file in Directory.GetFiles(dirs[i]))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".json")
                    continue; // skip .sam raws + .meta

                if (MarkAsset(settings, file, $"{addressRoot}/{dirName}/{Path.GetFileNameWithoutExtension(file)}", group))
                    count++;
            }
        }
        return count;
    }

    static int MarkPrefabs(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(PrefabsRoot))
        {
            Debug.LogWarning($"[BfAddressables] {PrefabsRoot} not found — skipping prefabs.");
            return 0;
        }

        var group = GetOrCreateGroup(settings, "Common_Prefabs");
        int count = 0;

        foreach (var file in Directory.GetFiles(PrefabsRoot, "*.prefab"))
        {
            if (MarkAsset(settings, file, Path.GetFileNameWithoutExtension(file), group))
                count++;
        }
        return count;
    }

    static bool MarkAsset(AddressableAssetSettings settings, string assetPath, string address, AddressableAssetGroup group)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
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
        bool cancel = EditorUtility.DisplayCancelableProgressBar(
            "BF Addressables Setup", $"{label}: {index}/{total}", (float)index / Mathf.Max(1, total));
        return cancel;
    }
}
