using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class UnitRegistry
{
    public static bool UseDataDriven = true;

    public static Unit GetUnitById(string unitId)
        => DataDrivenUnitFactory.GetOrBuildUnit(unitId);

    public static List<AsyncOperationHandle<Unit>> Warmup(IEnumerable<string> unitIds)
    {
        DataDrivenUnitFactory.Warmup(unitIds);
        return new List<AsyncOperationHandle<Unit>>();
    }

    public static bool TryGetLoadedUnit(string unitId, out Unit unit)
        => DataDrivenUnitFactory.TryGetLoadedUnit(unitId, out unit);
}