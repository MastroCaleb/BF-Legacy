using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class SummonGate : MonoBehaviour
{
    public SummonBanner summonBanner;

    public (Unit pulled, Unit evolved, bool isSurprise, bool isNewUnit) Summon()
    {
        Unit pulledUnit = PullUnit();
        Unit evolvedUnit = pulledUnit;
        if(pulledUnit.evoInto != "") evolvedUnit = EvolveUnitAtSummon(pulledUnit);

        bool isSurprise = false;
        
        if (pulledUnit.unitId == evolvedUnit.unitId)
        {
            isSurprise = Random.Range(0f, 100f) <= summonBanner.surpriseDoorBreakChance;
        }

        bool isNew = !PlayerData.unitDex.Contains(evolvedUnit.unitId);

        MainUI.inventoryRenderer.AddUnit(evolvedUnit.unitId);
        return (pulledUnit, evolvedUnit, isSurprise, isNew);
    }

    public Unit PullUnit()
    {
        float randomValue = Random.Range(0f, 100f);
        string pulledUnitKey;

        if(randomValue <= summonBanner.featuredPullChance)
        {
            int poolIndex = Random.Range(0, summonBanner.featuredSummonPools.Count);
            var pool = summonBanner.featuredSummonPools[poolIndex];
            pulledUnitKey = pool.poolUnitKeys[Random.Range(0, pool.poolUnitKeys.Count)];
        }
        else
        {
            int poolIndex = Random.Range(0, summonBanner.baseSummonPools.Count);
            var pool = summonBanner.baseSummonPools[poolIndex];
            pulledUnitKey = pool.poolUnitKeys[Random.Range(0, pool.poolUnitKeys.Count)];
        }

        return UnitRegistry.GetUnitById(pulledUnitKey);
    }

    public Unit EvolveUnitAtSummon(Unit unit)
    {
        Unit evolvedUnit = unit;

        Unit evo = UnitRegistry.GetUnitById(unit.evoInto);
        if(evo == null)
        {
            return unit;
        }

        int i = 1;
        while (evolvedUnit.evoInto != "" && 
            evolvedUnit.rarity != UnitRarity.FIVE &&
            evolvedUnit.rarity != UnitRarity.SIX &&
            evolvedUnit.rarity != UnitRarity.SEVEN &&
            evolvedUnit.rarity != UnitRarity.OMNI)
        {
            Unit nextEvo = UnitRegistry.GetUnitById(evolvedUnit.evoInto);
            if(Random.Range(0f, 100f) <= summonBanner.evoChance/i && nextEvo != null)
            {
                evolvedUnit = nextEvo;
            }
            else
            {
                break;
            }
            i++;
        }

        return evolvedUnit;
    }
}