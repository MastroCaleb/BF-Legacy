using UnityEditor;
using UnityEngine;

/// <summary>
/// Loads editor-only JSON data (info*.json, unit master files, SkillMSTs)
/// from Assets/EditorData. These files were moved out of Resources so they
/// never ship in player builds; they are consumed only by import tooling.
/// </summary>
public static class EditorDataLoader
{
    const string Root = "Assets/EditorData";

    public static TextAsset LoadJson(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string fileName = name.EndsWith(".json") ? name : name + ".json";
        if (fileName == "info_global.json") fileName = "info.json"; // legacy serialized field value
        return AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/{fileName}");
    }

    public static string GetFolderPath(string subFolder)
    {
        return $"{Root}/{subFolder}";
    }
}
