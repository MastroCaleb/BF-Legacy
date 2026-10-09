// JsonToUnitData.cs
// One-shot regen tool for the data-driven units pipeline ("Import BF Units
// (Data-Driven)"). All raw-data parsing/resolution lives in RawUnitParser
// (shared with UnitDatabaseBuilder, which packs the same DTOs straight into
// units.db); this window only drives the loop and emits the typed JSON files:
//   Assets/AddressableContent/UnitData/unit_{id}.json        → UnitData
//   Assets/AddressableContent/UnitData/ability_{skillId}.json → AbilityData (deduplicated)
// Output files are registered into chunked DD_UnitData_* Addressables groups
// (no .asset baking anywhere).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class JsonToUnitData : EditorWindow
{
    [MenuItem("Tools/Data-Driven Units/2. Import BF Units (Data-Driven)")]
    public static void ShowWindow() => GetWindow<JsonToUnitData>("Data-Driven Unit Importer");

    private const string OutputRoot      = "Assets/AddressableContent/UnitData";
    private const string BakedUnitsRoot  = "Assets/AddressableContent/Units";
    private const int    FilesPerGroup   = 250;

    private readonly RawUnitParser _parser = new RawUnitParser();

    private void OnGUI()
    {
        GUILayout.Label("Import Brave Frontier Units (Data-Driven)", EditorStyles.boldLabel);
        _parser.jsonFolderPath    = EditorGUILayout.TextField("JSON Folder",     _parser.jsonFolderPath);
        _parser.cggFolderPath     = EditorGUILayout.TextField("CGG Folder",      _parser.cggFolderPath);
        _parser.cgsFolderPath     = EditorGUILayout.TextField("CGS Folder",      _parser.cgsFolderPath);
        _parser.spriteFolderPath  = EditorGUILayout.TextField("Sprite Folder",   _parser.spriteFolderPath);
        _parser.samRootFolderPath = EditorGUILayout.TextField("SAM Root Folder", _parser.samRootFolderPath);
        _parser.soundFolderPath   = EditorGUILayout.TextField("Sound Folder",    _parser.soundFolderPath);

        GUILayout.Label($"Output: {OutputRoot} (typed JSON, no .asset baking)", EditorStyles.miniLabel);
        if (GUILayout.Button("Import Units (Data-Driven)")) ImportUnits();
    }

    // ─────────────────────────────────────────────────────────────
    //  IMPORT LOOP
    // ─────────────────────────────────────────────────────────────

    private void ImportUnits()
    {
        _parser.ResetState();
        _parser.LoadParticleIdIndex();
        _parser.LoadTables();

        if (!_parser.TryGetRawUnitFiles(out string[] files)) return;
        Directory.CreateDirectory(Application.dataPath + "/" + OutputRoot.Replace("Assets/", ""));
        Debug.Log($"[DataDrivenUnits] Importing {files.Length} unit files → {OutputRoot}");

        var units = new List<UnitData>(files.Length);
        bool cancelled = false;
        for (int i = 0; i < files.Length; i++)
        {
            if (i % 25 == 0 && EditorUtility.DisplayCancelableProgressBar(
                    "Data-Driven Unit Import", $"{i}/{files.Length}", (float)i / files.Length))
            {
                cancelled = true;
                break;
            }

            UnitData unit = _parser.ParseUnitFile(files[i]);
            if (unit != null) units.Add(unit);
        }
        EditorUtility.ClearProgressBar();

        // Parsing is done — flush every DTO to disk (abilities come back
        // deduplicated across units from the parser).
        foreach (UnitData unit in units)
            WriteJson($"unit_{unit.unitId}.json", unit);
        foreach (AbilityData ability in _parser.EmittedAbilities.Values)
            WriteJson($"ability_{ability.abilityId}.json", ability);

        AssetDatabase.Refresh();
        RegisterAddressables();

        LogDiffAndSummary(cancelled);
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT + ADDRESSABLE REGISTRATION
    // ─────────────────────────────────────────────────────────────

    private static void WriteJson(string fileName, object payload)
    {
        string path = Path.Combine(Application.dataPath, OutputRoot.Replace("Assets/", ""), fileName);
        File.WriteAllText(path, JsonConvert.SerializeObject(payload, DataDrivenJson.Settings));
    }

    private void RegisterAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[DataDrivenUnits] No AddressableAssetSettings — JSON written but not registered. Run Tools/Data-Driven Units/1. Setup Addressables.");
            return;
        }

        string absRoot = Path.Combine(Application.dataPath, OutputRoot.Replace("Assets/", ""));
        string[] files = Directory.GetFiles(absRoot, "*.json").OrderBy(f => f).ToArray();
        int count = 0;

        for (int i = 0; i < files.Length; i++)
        {
            if (i % 200 == 0 && EditorUtility.DisplayCancelableProgressBar(
                    "Data-Driven Unit Import", $"Registering {i}/{files.Length}", (float)i / Mathf.Max(1, files.Length)))
                break;

            string assetPath = $"{OutputRoot}/{Path.GetFileName(files[i])}";
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) continue;

            var group = GetOrCreateGroup(settings, $"DD_UnitData_{i / FilesPerGroup:00}");
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = Path.GetFileNameWithoutExtension(files[i]);
            count++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"[DataDrivenUnits] Registered {count} JSON files into chunked DD_UnitData_* groups.");
    }

    // ─────────────────────────────────────────────────────────────
    //  DIFF VS BAKED SET + SUMMARY
    // ─────────────────────────────────────────────────────────────

    private void LogDiffAndSummary(bool cancelled)
    {
        var bakedIds = Directory.Exists(BakedUnitsRoot)
            ? Directory.GetDirectories(BakedUnitsRoot)
                .Select(Path.GetFileName)
                .Where(n => n != null && n.StartsWith("unit_"))
                .Select(n => n.Substring("unit_".Length))
                .ToHashSet()
            : new HashSet<string>();

        var onlyInBaked = bakedIds.Except(_parser.EmittedUnitIds).ToList();
        var onlyInData  = _parser.EmittedUnitIds.Except(bakedIds).ToList();

        int bakedAbilityCount = 0;
        if (Directory.Exists(BakedUnitsRoot))
        {
            foreach (string d in Directory.GetDirectories(BakedUnitsRoot))
            {
                string abilitiesDir = Path.Combine(d, "Abilities");
                if (Directory.Exists(abilitiesDir))
                    bakedAbilityCount += Directory.GetFiles(abilitiesDir, "*.asset").Length;
            }
        }

        Debug.Log(
            "[DataDrivenUnits] ── Import summary " +
            (cancelled ? "(CANCELLED — partial output)" : "") + "──\n" +
            $"units emitted: {_parser.EmittedUnitIds.Count} (baked set: {bakedIds.Count})\n" +
            $"abilities emitted (unique): {_parser.EmittedAbilities.Count} (baked per-unit SOs: {bakedAbilityCount})\n" +
            $"only in baked set ({onlyInBaked.Count}): {string.Join(", ", onlyInBaked.Take(20))}{(onlyInBaked.Count > 20 ? " …" : "")}\n" +
            $"only in data-driven set ({onlyInData.Count}): {string.Join(", ", onlyInData.Take(20))}{(onlyInData.Count > 20 ? " …" : "")}\n" +
            $"missing refs — sprites: {_parser.MissingSprites}, particles: {_parser.MissingParticles}, " +
            $"audio: {_parser.MissingAudio}, animations: {_parser.MissingAnimations}");
    }

    // ─────────────────────────────────────────────────────────────
    //  GROUP HELPERS (mirrors BfAddressablesSetup)
    // ─────────────────────────────────────────────────────────────

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
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
}
