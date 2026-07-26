using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class EffectImporter : MonoBehaviour
{
    [Header("JSON containing effect list")]
    public TextAsset effectListJson;

    [Header("Folder where PlistConverterBatch wrote results (inside Assets/)")]
    public string convertedPlistRoot = "ConvertedParticles";

    void Start()
    {
        ImportEffects();
    }

    // -------------------------------------------------------------
    // RAW JSON ENTRY
    // -------------------------------------------------------------
    [Serializable]
    private class EffectEntryRaw
    {
        [JsonProperty("1TED5ZSi")]
        public string effectId;

        public string name;    // name (ignored)

        public string effectType;

        [JsonProperty("Heg8ZDQ7")]
        public string plistFile;

        [JsonProperty("1NijIP2a")]
        public string battleEffectGroupId;
    }

    // -------------------------------------------------------------
    // MAIN IMPORT OPERATION
    // -------------------------------------------------------------
    void ImportEffects()
    {
        if (effectListJson == null)
        {
            Debug.LogError("No effect JSON assigned.");
            return;
        }

        try
        {
            // Parse JSON directly using Newtonsoft.Json
            List<EffectEntryRaw> effects = JsonConvert.DeserializeObject<List<EffectEntryRaw>>(effectListJson.text);

            if (effects == null || effects.Count == 0)
            {
                Debug.LogError("No effects found in JSON.");
                return;
            }

            Debug.Log($"Found {effects.Count} effects to process");

            foreach (var raw in effects)
            {
                Debug.Log($"Processing effect - ID: {raw.effectId}, Plist: {raw.plistFile}");
                CreateEffectSO(raw);
            }

            Debug.Log("All effects imported.");
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON parsing error: {e.Message}\n{e.StackTrace}");
        }
    }

    // -------------------------------------------------------------
    // CREATE SCRIPTABLEOBJECT
    // -------------------------------------------------------------
    void CreateEffectSO(EffectEntryRaw raw)
    {
        // Skip entries that do NOT use plist files
        if (string.IsNullOrEmpty(raw.plistFile) || !raw.plistFile.EndsWith(".plist", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("Skipping non-plist entry: " + raw.plistFile);
            return;
        }

        string plistName = Path.GetFileNameWithoutExtension(raw.plistFile);
        string plistFolderUnity = Path.Combine(convertedPlistRoot, plistName).Replace("\\", "/");

        if (!Directory.Exists(plistFolderUnity))
        {
            Debug.LogWarning($"Missing converted plist folder: {plistFolderUnity}");
            return;
        }

#if UNITY_EDITOR
        string jsonPath = Path.Combine(plistFolderUnity, plistName + ".json").Replace("\\", "/");

        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning($"Missing plist JSON: {jsonPath}");
            return;
        }

        UnityEngine.TextAsset plistJsonAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(jsonPath);

        if (plistJsonAsset == null)
        {
            Debug.LogWarning($"Could not load TextAsset from: {jsonPath}");
            return;
        }

        string[] pngFiles = Directory.GetFiles(plistFolderUnity, "*.png");
        if (pngFiles.Length == 0)
        {
            Debug.LogWarning($"No PNG found for plist: {plistFolderUnity}");
            return;
        }

        string pngPath = pngFiles[0].Replace("\\", "/");
        Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);

        if (sprite != null)
        {
            var importer = UnityEditor.AssetImporter.GetAtPath(pngPath) as UnityEditor.TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point;
                importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        if (sprite == null)
        {
            Debug.LogWarning($"Could not load sprite from: {pngPath}");
            return;
        }

        ParticleEffect pe = ScriptableObject.CreateInstance<ParticleEffect>();
        pe.effectId = raw.effectId;
        pe.effectType = raw.effectType;
        pe.battleEffectGroupId = raw.battleEffectGroupId;
        pe.plistJson = plistJsonAsset;
        pe.sprite = sprite;

        string soPath = Path.Combine(plistFolderUnity, plistName + "_Effect.asset").Replace("\\", "/");
        UnityEditor.AssetDatabase.CreateAsset(pe, soPath);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        Debug.Log($"Created ParticleEffect asset → {soPath} with ID: {pe.effectId}");
#endif
    }
}
