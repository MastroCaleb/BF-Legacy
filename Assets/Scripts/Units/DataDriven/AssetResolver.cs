// AssetResolver.cs
// Runtime resolution of the string addresses stored in UnitData/AbilityData.
// Addressables-backed, session-long cache (same policy as UnitRegistry v1 —
// PlayerUnitInventoryDatabase holds live references, nothing is released).
// Sync facade via WaitForCompletion for local bundles, plus Preload() to kick
// async loads ahead of use.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AssetResolver
{
    static readonly Dictionary<string, UnityEngine.Object> s_cache = new Dictionary<string, UnityEngine.Object>();

    public static T Load<T>(string address) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(address)) return null;
        if (s_cache.TryGetValue(address, out UnityEngine.Object cached) && cached != null)
            return cached as T;

        var handle = Addressables.LoadAssetAsync<T>(address);
        T asset = handle.WaitForCompletion();
        s_cache[address] = asset;

        if (asset == null)
            Debug.LogWarning($"[AssetResolver] Address '{address}' did not resolve to a {typeof(T).Name}.");
        return asset;
    }

    public static Sprite LoadSprite(string address)               => Load<Sprite>(address);
    public static Texture2D LoadTexture(string address)           => Load<Texture2D>(address);
    public static TextAsset LoadText(string address)              => Load<TextAsset>(address);
    public static AudioClip LoadAudio(string address)             => Load<AudioClip>(address);
    public static ParticleEffect LoadParticleEffect(string effectId) => Load<ParticleEffect>(effectId);

    /// <summary>
    /// Kicks async loads for the given addresses without blocking. Completed
    /// results land in the cache; a later Load() for an address that finished
    /// hits the cache, one still in flight is internally deduplicated by
    /// Addressables. Used for warmup so sync calls resolve instantly.
    /// </summary>
    public static void Preload(IEnumerable<string> addresses)
    {
        foreach (string address in addresses)
        {
            if (string.IsNullOrEmpty(address) || s_cache.TryGetValue(address, out var c) && c != null)
                continue;

            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
            string captured = address;
            handle.Completed += op => s_cache[captured] = op.Result;
        }
    }
}
