using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public class EffectImporter : EditorWindow
{
    [Serializable]
    private class EffectEntry
    {
        public string effectId;
        public string effectType;
        public string battleEffectGroupId;
        public string resource;
    }

    private TextAsset jsonFile;

    // Separate roots because the resources are stored separately.
    private string plistRoot = "Assets/Data/Particles";
    private string spriteRoot = "Assets/BF_Assets/content/effect/img";
    private string cggRoot = "Assets/BF_Assets/content/effect/cgg";
    private string cgsRoot = "Assets/BF_Assets/content/effect/cgs";
    private string samRoot = "Assets/AddressableContent/Sams/Effect_SAMS/sam";

    private string outputRoot;

    [MenuItem("Tools/Particle Effect Importer")]
    private static void Open()
    {
        GetWindow<EffectImporter>(
            "Particle Effect Importer"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Particle Effect Importer",
            EditorStyles.boldLabel
        );

        jsonFile = (TextAsset)EditorGUILayout.ObjectField(
            "JSON",
            jsonFile,
            typeof(TextAsset),
            false
        );

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Resource Roots",
            EditorStyles.boldLabel
        );

        plistRoot = EditorGUILayout.TextField(
            "PLIST Root",
            plistRoot
        );

        spriteRoot = EditorGUILayout.TextField(
            "Sprite Root",
            spriteRoot
        );

        cggRoot = EditorGUILayout.TextField(
            "CGG Root",
            cggRoot
        );

        cgsRoot = EditorGUILayout.TextField(
            "CGS Root",
            cgsRoot
        );

        samRoot = EditorGUILayout.TextField(
            "SAM Root",
            samRoot
        );

        EditorGUILayout.Space();

        outputRoot = EditorGUILayout.TextField(
            "Output Folder",
            outputRoot
        );

        EditorGUILayout.Space();

        GUI.enabled =
            jsonFile != null &&
            !string.IsNullOrEmpty(outputRoot);

        if (GUILayout.Button(
                "Import Particle Effects",
                GUILayout.Height(40)))
        {
            Import();
        }

        GUI.enabled = true;
    }

    private void Import()
    {

        if (!AssetDatabase.IsValidFolder(outputRoot))
        {
            Debug.LogError(
                $"Invalid output folder: {outputRoot}"
            );

            return;
        }

        List<EffectEntry> effects;

        try
        {
            effects = ParseJson(jsonFile.text);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"Failed to parse JSON:\n{e}"
            );

            return;
        }

        int imported = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < effects.Count; i++)
            {
                EffectEntry entry = effects[i];

                EditorUtility.DisplayProgressBar(
                    "Importing Particle Effects",
                    $"{entry.effectId} ({i + 1}/{effects.Count})",
                    (float)i / effects.Count
                );

                if (ImportEffect(entry, outputRoot))
                    imported++;
                else
                    failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Particle effect import finished. " +
            $"Imported: {imported}, Failed: {failed}"
        );
    }

    private bool ImportEffect(
        EffectEntry entry,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(entry.resource))
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Has no resource."
            );

            return false;
        }

        ParticleEffect effect =
            CreateInstance<ParticleEffect>();

        effect.effectId =
            entry.effectId;

        effect.effectType =
            entry.effectType;

        effect.battleEffectGroupId =
            entry.battleEffectGroupId;

        string resource =
            entry.resource.Trim();

        bool success;

        if (resource.Contains(":"))
        {
            success = ImportCGG(
                effect,
                entry
            );
        }
        else if (resource.EndsWith(
                     ".plist",
                     StringComparison.OrdinalIgnoreCase))
        {
            success = ImportPLIST(
                effect,
                entry
            );
        }
        else if (resource.EndsWith(
                     ".sam",
                     StringComparison.OrdinalIgnoreCase))
        {
            success = ImportSAM(
                effect,
                entry
            );
        }
        else
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Unknown resource: {resource}"
            );

            DestroyImmediate(effect);
            return false;
        }

        if (!success)
        {
            DestroyImmediate(effect);
            return false;
        }

        string assetName =
            MakeSafeFileName(entry.effectId);

        string assetPath =
            $"{outputPath}/{assetName}.asset";

        assetPath =
            AssetDatabase.GenerateUniqueAssetPath(
                assetPath
            );

        AssetDatabase.CreateAsset(
            effect,
            assetPath
        );

        return true;
    }

    private bool ImportCGG(
        ParticleEffect effect,
        EffectEntry entry)
    {
        effect.particleType =
            ParticleType.CGG;

        string[] parts =
            entry.resource.Split(':');

        if (parts.Length != 3)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Invalid CGG resource: " +
                entry.resource
            );

            return false;
        }

        string spriteFile =
            parts[0].Trim();

        string cggFile =
            parts[1].Trim();

        string cgsFile =
            parts[2].Trim();

        /*
         * IMPORTANT:
         *
         * Each resource has its OWN root.
         *
         * We search:
         *
         * spriteRoot -> sprite folder
         * cggRoot    -> cgg folder
         * cgsRoot    -> cgs folder
         *
         * independently.
         */

        string spriteFolderName =
            Path.GetFileNameWithoutExtension(
                spriteFile
            );

        string cggFolderName =
            Path.GetFileNameWithoutExtension(
                cggFile
            );

        string cgsFolderName =
            Path.GetFileNameWithoutExtension(
                cgsFile
            );

        if (spriteRoot == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Sprite folder not found: " +
                spriteFolderName
            );

            return false;
        }

        if (cggRoot == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] CGG folder not found: " +
                cggFolderName
            );

            return false;
        }

        if (cgsRoot == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] CGS folder not found: " +
                cgsFolderName
            );

            return false;
        }

        string spritePath =
            FindFile(
                spriteRoot,
                spriteFile
            );

        string cggPath =
            FindFile(
                cggRoot,
                cggFile
            );

        string cgsPath =
            FindFile(
                cgsRoot,
                cgsFile
            );

        if (spritePath == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Sprite not found: " +
                spriteFile
            );

            return false;
        }

        if (cggPath == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] CGG not found: " +
                cggFile
            );

            return false;
        }

        if (cgsPath == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] CGS not found: " +
                cgsFile
            );

            return false;
        }

        effect.spriteSheet =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                spritePath
            );

        effect.cggJson =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                cggPath
            );

        effect.cgsJson =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                cgsPath
            );

        if (effect.spriteSheet == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Could not load Sprite: " +
                spritePath
            );

            return false;
        }

        if (effect.cggJson == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Could not load CGG: " +
                cggPath
            );

            return false;
        }

        if (effect.cgsJson == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] Could not load CGS: " +
                cgsPath
            );

            return false;
        }

        return true;
    }

    private bool ImportPLIST(
        ParticleEffect effect,
        EffectEntry entry)
    {
        effect.particleType =
            ParticleType.PLIST;

        string fileName =
            Path.GetFileName(entry.resource);

        fileName = Path.ChangeExtension(
            fileName,
            ".json"
        );

        string folderName =
            Path.GetFileNameWithoutExtension(
                entry.resource
            );

        string folder =
            FindFolder(
                plistRoot,
                folderName
            );

        if (folder == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] PLIST folder not found: " +
                folderName
            );

            return false;
        }

        string path =
            FindFile(
                folder,
                fileName
            );

        if (path == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] PLIST not found: " +
                fileName
            );

            return false;
        }

        effect.plistJson =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                path
            );

        return effect.plistJson != null;
    }

    private bool ImportSAM(
        ParticleEffect effect,
        EffectEntry entry)
    {
        effect.particleType =
            ParticleType.SAM;

        string fileName =
            Path.GetFileName(entry.resource);

        fileName = Path.ChangeExtension(
            fileName,
            ".json"
        );

        string folderName =
            Path.GetFileNameWithoutExtension(
                entry.resource
            );

        string folder =
            FindFolder(
                samRoot,
                folderName
            );

        if (folder == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] SAM folder not found: " +
                folderName
            );

            return false;
        }

        string path =
            FindFile(
                folder,
                fileName
            );

        if (path == null)
        {
            Debug.LogWarning(
                $"[{entry.effectId}] SAM not found: " +
                fileName
            );

            return false;
        }

        effect.samJson =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                path
            );

        return effect.samJson != null;
    }

    private static string FindFolder(
        string root,
        string folderName)
    {
        if (string.IsNullOrEmpty(root))
            return null;

        string[] guids =
            AssetDatabase.FindAssets(
                $"t:Folder {folderName}",
                new[] { root }
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            if (!AssetDatabase.IsValidFolder(path))
                continue;

            string name =
                Path.GetFileName(
                    path.TrimEnd(
                        '/',
                        '\\'
                    )
                );

            if (string.Equals(
                    name,
                    folderName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private static string FindFile(
        string folder,
        string fileName)
    {
        string exactPath =
            $"{folder}/{fileName}";

        if (File.Exists(exactPath))
            return exactPath;

        string stem =
            Path.GetFileNameWithoutExtension(
                fileName
            );

        string[] guids =
            AssetDatabase.FindAssets(
                stem,
                new[] { folder }
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            if (!File.Exists(path))
                continue;

            if (string.Equals(
                    Path.GetFileName(path),
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private static string MakeSafeFileName(
        string value)
    {
        foreach (char c in
                 Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrEmpty(value)
            ? "ParticleEffect"
            : value;
    }

    private static List<EffectEntry> ParseJson(string json)
    {
        var effects = new List<EffectEntry>();

        JToken root = JToken.Parse(json);

        IEnumerable<JObject> entries;

        if (root is JArray array)
        {
            entries = array.OfType<JObject>();
        }
        else if (root is JObject singleObject)
        {
            entries = new[] { singleObject };
        }
        else
        {
            throw new Exception(
                "Expected JSON root to be an object or array."
            );
        }

        foreach (JObject obj in entries)
        {
            var entry = new EffectEntry
            {
                effectId = obj.Value<string>("1TED5ZSi"),
                effectType = obj.Value<string>("effectType"),
                battleEffectGroupId = obj.Value<string>("1NijIP2a"),
                resource = obj.Value<string>("Heg8ZDQ7")
            };

            if (string.IsNullOrWhiteSpace(entry.effectId))
            {
                Debug.LogWarning(
                    "Skipping effect entry because effectId (1TED5ZSi) is missing."
                );

                continue;
            }

            effects.Add(entry);
        }

        return effects;
    }
}
#endif