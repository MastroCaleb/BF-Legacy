using System.Collections.Generic;
using UnityEngine;

public static class UnitRegistry
{
    private static Dictionary<string, Unit> _units;

    public static Unit GetUnitById(string unitId)
    {
        if (_units == null)
        {
            _units = new Dictionary<string, Unit>();
        }

        if (_units.TryGetValue(unitId, out Unit unit))
        {
            return unit;
        }

        unit = Resources.Load<Unit>($"Units/unit_{unitId}/unit_{unitId}");

        if (unit == null)
        {
            Debug.LogError($"Unit {unitId} not found.");
            return null;
        }

        _units[unitId] = unit;
        return unit;
    }
}