using System.Collections.Generic;
using UnityEngine;

public class TeamBehaviour : MonoBehaviour
{
    public List<UnitBehaviour> units = new List<UnitBehaviour>();
    public UnitBehaviour leaderUnit;
    public float waitTime;

    public bool GetEndTurn()
    {
        foreach(var unit in units)
        {
            if(unit.currentState == UnitState.Attacking) return false;
        }

        return true;
    }

    public void SetLeader()
    {
        leaderUnit = units[0];
    }

    public void ActivateLeaderAbility(List<UnitBehaviour> targets)
    {
        if (leaderUnit != null)
        {
            leaderUnit.ActivateLeaderAbility(targets);
        }
    }

    public bool IsDefeated()
    {
        foreach (var unit in units)
        {
            if (unit.currentState != UnitState.Dead)
                return false;
        }
        return true;
    }

    public void CheckDeadUnits()
    {
        units.RemoveAll(unit => unit.currentState == UnitState.Dead);
    }

    public void DestroyDeadUnits()
    {
        foreach (var unit in units)
        {
            if (unit.currentState == UnitState.Dead)
            {
                if(BattleManager.selectedEnemyUnit == unit) 
                    BattleManager.selectedEnemyUnit = null;
                    
                Destroy(unit.gameObject);
            }
        }
    }
}
