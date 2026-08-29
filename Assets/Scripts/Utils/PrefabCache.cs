using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables-backed replacement for Resources.Load&lt;GameObject&gt; prefab
/// loads. Addresses equal the old Resources names (prefabs used to sit in the
/// Resources root). Preload() is called once at startup; Get() falls back to a
/// synchronous load for anything not preloaded. Prefabs are kept for the whole
/// session (they are tiny) and never released.
/// </summary>
public static class PrefabCache
{
    public static readonly string[] StartupPrefabs =
    {
        // battle drops
        "BattleCrystal", "HeartCrystal", "ZelCoin", "KarmaOrb", "GemCrystal",
        "ChestDrop", "UnitDropCommon", "UnitDropRare", "UnitDropSuperRare", "UnitDropUltraRare", "ItemDrop",
        // popups
        "DamagePopUp", "CriticalPopUp", "SparkPopUp",
        // battle title SAM banners
        "Win", "Lose", "Boss", "Mimic", "Congratulation",
        // menu UI
        "DungeonButton", "SummonBanner", "MissionSlot",
    };

    static readonly Dictionary<string, GameObject> s_prefabs = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, AsyncOperationHandle<GameObject>> s_handles = new Dictionary<string, AsyncOperationHandle<GameObject>>();

    public static void Preload()
    {
        foreach (string name in StartupPrefabs)
        {
            if (s_prefabs.ContainsKey(name) || s_handles.ContainsKey(name)) continue;
            s_handles[name] = Addressables.LoadAssetAsync<GameObject>(name);
        }
    }

    public static GameObject Get(string name)
    {
        if (s_prefabs.TryGetValue(name, out var prefab)) return prefab;

        if (!s_handles.TryGetValue(name, out var handle))
        {
            handle = Addressables.LoadAssetAsync<GameObject>(name);
            s_handles[name] = handle;
        }

        prefab = handle.WaitForCompletion();
        s_prefabs[name] = prefab;
        return prefab;
    }
}
