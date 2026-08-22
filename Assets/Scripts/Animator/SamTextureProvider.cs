using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables-backed replacement for the static texture caches that used to
/// live in SamAnimator / OldSamAnimator. Paths are identical to the old
/// Resources paths ("Sams/Unit_SAMS/unit_sam/{folder}/{file}" etc.) because
/// bundle addresses were assigned to mirror them.
///
/// Sync facade + warmup: Load() blocks via WaitForCompletion (local bundles
/// only); Preload()/PreloadUnitSam() kick async loads ahead of battle start so
/// Load() normally completes instantly.
/// </summary>
public static class SamTextureProvider
{
    static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, AsyncOperationHandle<Texture2D>> s_handles = new Dictionary<string, AsyncOperationHandle<Texture2D>>();

    public const string UnitSamRoot = "Sams/Unit_SAMS/unit_sam";
    public const string EffectSamRoot = "Sams/Effect_SAMS/sam";

    public static void Preload(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path) || s_cache.ContainsKey(path) || s_handles.ContainsKey(path)) continue;
            s_handles[path] = Addressables.LoadAssetAsync<Texture2D>(path);
        }
    }

    /// <summary>Queues async loads for every frame texture of a unit SAM json.</summary>
    public static void PreloadUnitSam(TextAsset samJson)
    {
        if (samJson == null) return;
        try
        {
            var anim = JsonConvert.DeserializeObject<SamAnimation>(samJson.text);
            if (anim?.mImageVector == null) return;

            string folderName = Path.GetFileNameWithoutExtension(samJson.name).Replace("_anime", "");
            var paths = new List<string>(anim.mImageVector.Length);
            foreach (var img in anim.mImageVector)
            {
                if (string.IsNullOrEmpty(img.mImageName)) continue;
                paths.Add($"{UnitSamRoot}/{folderName}/{Path.GetFileNameWithoutExtension(img.mImageName)}");
            }
            Preload(paths);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SamTextureProvider] Failed to preload '{samJson.name}': {ex.Message}");
        }
    }

    public static Texture2D Load(string path)
    {
        if (s_cache.TryGetValue(path, out var cached)) return cached;

        if (!s_handles.TryGetValue(path, out var handle))
        {
            handle = Addressables.LoadAssetAsync<Texture2D>(path);
            s_handles[path] = handle;
        }

        Texture2D tex = handle.WaitForCompletion();
        s_cache[path] = tex; // cache misses (null) too, like the old Resources.Load cache
        return tex;
    }

    /// <summary>Releases all SAM textures. Call when leaving the battle scene.</summary>
    public static void UnloadAll()
    {
        foreach (var handle in s_handles.Values)
            if (handle.IsValid())
                Addressables.Release(handle);
        s_handles.Clear();
        s_cache.Clear();
    }
}
