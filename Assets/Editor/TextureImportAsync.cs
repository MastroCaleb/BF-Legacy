
using UnityEditor;
using UnityEngine;

public static class TextureImportAsync
{
    static readonly string[] Roots =
    {
        "Assets/BF_Assets",
        "Assets/Resources",
        "Assets/AddressableContent",
    };

    // Textures that must keep Read/Write: BraveFrontierFrameAnimator.GetPixels() at runtime
    static readonly string[] SkipPaths =
    {
        "Assets/AddressableContent/Sams/Unit_SAMS",
        "Assets/TextMesh Pro",
        "Assets/Fonts",
    };

    [MenuItem("Tools/BF/Fix Textures/Phase 1 - Write import settings (fast, cancelable)")]
    static void WriteImportSettings()
    {
        int fixedCount = 0, skipped = 0, excluded = 0;
        bool cancelled = false;

        try
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", Roots);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Phase 1: writing import settings", path, (float)i / guids.Length))
                { cancelled = true; break; }

                if (IsExcluded(path)) { excluded++; continue; }

                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { skipped++; continue; }

                if (!NeedsFix(imp)) { skipped++; continue; }   // makes Phase 1 resumable

                Apply(imp);
                AssetDatabase.WriteImportSettingsIfDirty(path); // meta only — no import triggered
                fixedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Phase 1 {(cancelled ? "CANCELLED (rerun to resume)" : "complete")}: " +
                  $"{fixedCount} metas written, {skipped} already fixed, {excluded} excluded. " +
                  "Run Phase 2 when ready.");
    }

    [MenuItem("Tools/BF/Fix Textures/Phase 2 - Parallel reimport (not cancelable)")]
    static void ParallelReimport()
    {
        if (!EditorUtility.DisplayDialog("Parallel reimport",
                "Unity will be busy importing for a while (progress in the main window) and it cannot be cancelled.\n\nContinue?",
                "Import", "Cancel"))
            return;

        AssetDatabase.Refresh();
        Debug.Log("Phase 2 complete. Verify sample textures (RW off, ASTC/BC7 overrides) and watch the console for import errors.");
    }

    static bool IsExcluded(string path)
    {
        foreach (var skip in SkipPaths)
            if (path.StartsWith(skip)) return true;
        return false;
    }

    static bool NeedsFix(TextureImporter imp)
    {
        if (imp.isReadable) return true;
        if (imp.textureCompression != TextureImporterCompression.Compressed) return true;
        var android = imp.GetPlatformTextureSettings("Android");
        if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6) return true;
        var standalone = imp.GetPlatformTextureSettings("Standalone");
        if (!standalone.overridden || standalone.format != TextureImporterFormat.BC7) return true;
        return false;
    }

    public static void Apply(TextureImporter imp)   // public so a future AssetPostprocessor can reuse it
    {
        imp.isReadable = false;
        imp.textureCompression = TextureImporterCompression.Compressed;

        var android = imp.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;   // bump to ASTC_4x4 per-folder if banding appears
        imp.SetPlatformTextureSettings(android);

        var standalone = imp.GetPlatformTextureSettings("Standalone");
        standalone.overridden = true;
        standalone.maxTextureSize = 2048;
        standalone.format = TextureImporterFormat.BC7;
        imp.SetPlatformTextureSettings(standalone);
    }
}
