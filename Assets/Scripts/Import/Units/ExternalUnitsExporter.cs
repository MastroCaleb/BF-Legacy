// ExternalUnitsExporter.cs
// Stages the regen output (unit + ability JSONs, typed fields only) flat into
// <project>/UnitsExternal/units/ so it can be deployed as the external
// override folder: copy it next to the .exe, or
//   adb push UnitsExternal/units /storage/emulated/0/Android/data/<pkg>/files/units
// Bundled Addressables JSONs stay authoritative; the external folder only
// overrides per unit/ability id (see UnitDataRegistry).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExternalUnitsExporter
{
    private const string SourceRoot = "Assets/AddressableContent/UnitData";

    [MenuItem("Tools/Data-Driven Units/3. Export External Units")]
    public static void Export()
    {
        string absSource = Path.Combine(Application.dataPath, SourceRoot.Replace("Assets/", ""));
        if (!Directory.Exists(absSource))
        {
            Debug.LogError($"[ExternalUnits] Nothing to export — {SourceRoot} not found. Run Tools/Data-Driven Units/2. Import BF Units (Data-Driven) first.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string absTarget = Path.Combine(projectRoot, "UnitsExternal", "units");
        Directory.CreateDirectory(absTarget);

        string[] files = Directory.GetFiles(absSource, "*.json").OrderBy(f => f).ToArray();
        foreach (string file in files)
            File.Copy(file, Path.Combine(absTarget, Path.GetFileName(file)), overwrite: true);

        Debug.Log($"[ExternalUnits] Exported {files.Length} JSON files → {absTarget}\n" +
                  "Deploy: copy the folder next to the .exe, or adb push it to <persistentDataPath>/units.");
        EditorUtility.RevealInFinder(absTarget);
    }
}
