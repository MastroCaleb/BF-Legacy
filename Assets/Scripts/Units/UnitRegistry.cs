using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables-backed unit ScriptableObject cache. Address scheme equals the
/// old Resources path: "Units/unit_{id}/unit_{id}". Sync facade (WaitForCompletion
/// on local bundles) + Warmup() so battles can preload their roster during the
/// battle-start delay. Units stay loaded for the whole session (v1 policy —
/// PlayerUnitInventoryDatabase holds live Unit references).
/// </summary>
public static class UnitRegistry
{
    static readonly Dictionary<string, Unit> s_units = new Dictionary<string, Unit>();
    static readonly Dictionary<string, AsyncOperationHandle<Unit>> s_handles = new Dictionary<string, AsyncOperationHandle<Unit>>();

    /// <summary>
    /// Migration toggle: when true, units load from the data-driven JSON
    /// pipeline (UnitDataRegistry → DataDrivenUnitFactory) instead of baked
    /// Unit SOs. Same GetUnitById surface, so consumers re-link via id
    /// unchanged. Toggle from Tools/Data-Driven Units.
    /// </summary>
    public static bool UseDataDriven = true;

    public static Unit GetUnitById(string unitId)
    {
        if (UseDataDriven)
            return DataDrivenUnitFactory.GetOrBuildUnit(unitId);

        if (s_units.TryGetValue(unitId, out Unit unit))
        {
            return unit;
        }

        if (!s_handles.TryGetValue(unitId, out var handle))
        {
            handle = Addressables.LoadAssetAsync<Unit>(AssetPath(unitId));
            s_handles[unitId] = handle;
        }

        unit = handle.WaitForCompletion();

        if (unit == null)
        {
            Debug.LogError($"Unit {unitId} not found.");
            return null;
        }

        s_units[unitId] = unit;
        return unit;
    }

    /// <summary>
    /// Kicks async loads for the given unit ids (skips cached ones). Yield on
    /// the returned handles, then call SamTextureProvider.PreloadUnitSam for
    /// each loaded unit to also warm the animation sheets.
    /// </summary>
    public static List<AsyncOperationHandle<Unit>> Warmup(IEnumerable<string> unitIds)
    {
        if (UseDataDriven)
        {
            // Sync build + SAM texture preload, mirroring the baked warmup
            // semantics (empty handle list — callers just skip the yield).
            DataDrivenUnitFactory.Warmup(unitIds);
            return new List<AsyncOperationHandle<Unit>>();
        }

        var handles = new List<AsyncOperationHandle<Unit>>();
        foreach (string unitId in unitIds)
        {
            if (string.IsNullOrEmpty(unitId) || s_units.ContainsKey(unitId) || s_handles.ContainsKey(unitId))
                continue;

            var handle = Addressables.LoadAssetAsync<Unit>(AssetPath(unitId));
            s_handles[unitId] = handle;
            handles.Add(handle);
        }
        return handles;
    }

    public static bool TryGetLoadedUnit(string unitId, out Unit unit)
    {
        return s_units.TryGetValue(unitId, out unit);
    }

    static string AssetPath(string unitId)
    {
        return $"Units/unit_{unitId}/unit_{unitId}";
    }
}
