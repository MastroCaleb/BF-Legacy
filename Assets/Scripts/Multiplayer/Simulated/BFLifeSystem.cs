using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

/*
 * BFLifeSystem.cs
 * 
 * This class is work in progress, right now it can only generate random bot data, in future versions this code will also be
 * responsible for simulating bot progress, team building, and other bot-related features.
 */
public class BFLifeSystem
{
    static readonly Random rng = new Random();

    public static BotData GenerateBot(int minLevel = 1, int maxLevel = 200, int unitCount = 5)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);

        BotData bot = new BotData
        {
            botId = id,
            botName = BotNameSystem.GetRandomBotName(),
            botLevel = rng.Next(minLevel, maxLevel + 1),
            party = new BotPartyData
            {
                mainUnitIndex = 0,
                units = new List<BotUnitData>()
            }
        };

        for (int i = 0; i < unitCount; i++)
        {
            bot.party.units.Add(GenerateBotUnit());
        }

        return bot;
    }

    public static BotUnitData GenerateBotUnit()
    {
        string unitId = PickRandomUnitId();
        return new BotUnitData
        {
            unitId = unitId,
            unitLevel = rng.Next(1, UnitRegistry.GetUnitById(unitId).maxLevel),
            bbLevel = UnityEngine.Random.Range(1, 11),
            sbbLevel = UnityEngine.Random.Range(1, 11),
            itemId = "",
            itemId2 = ""
        };
    }

    public static List<BotData> GenerateBots(int count, int minLevel = 1, int maxLevel = 100, int unitCount = 5)
    {
        List<BotData> bots = new List<BotData>(count);
        for (int i = 0; i < count; i++)
            bots.Add(GenerateBot(minLevel, maxLevel, unitCount));
        return bots;
    }

    static string PickRandomUnitId()
    {
        return UnitDataDb.GetRandomUnitRow()?.UnitId ?? "10011" ; // fallback to a default unit ID if none found
    }

    public static void ModifyStats(UnitInventoryData unit, int levels = 1)
    {
        Unit curUnit = unit.unit;
        int countedLevel = unit.currentLevel;
        //Reduces redundant extra typing
        int maxLevel = curUnit.maxLevel;
        //Total levels that gain stats
        int gainLimit = maxLevel - 1;

        UnitStats s = curUnit.statsLord;
        int hpMax  = s.hp  - curUnit.maxHealth;
        int atkMax = s.atk - curUnit.atk;
        int defMax = s.def - curUnit.def;
        int recMax = s.rec - curUnit.rec;

        switch (unit.type)
        {
            case UnitType.Anima:
            {
                hpMax = (s.hp + (10*gainLimit)) - curUnit.maxHealth;
                recMax = (s.rec - (3*gainLimit)) - curUnit.rec;
                unit.hpLevelUpBonus  += UnityEngine.Random.Range(5, 11);
                unit.recLevelUpBonus -= UnityEngine.Random.Range(1, 4);
                break;
            }
            case UnitType.Breaker:
            {
                atkMax = (s.atk + (3*gainLimit)) - curUnit.atk;
                defMax = (s.def - (3*gainLimit)) - curUnit.def;
                unit.atkLevelUpBonus += UnityEngine.Random.Range(1, 4);
                unit.defLevelUpBonus -= UnityEngine.Random.Range(1, 4);
                break;
            }
            case UnitType.Guardian:
            {
                defMax = (s.def + (3*gainLimit)) - curUnit.def;
                recMax = (s.rec - (2*gainLimit)) - curUnit.rec;
                unit.defLevelUpBonus += UnityEngine.Random.Range(1, 4);
                unit.recLevelUpBonus -= UnityEngine.Random.Range(0, 3);
                break;
            }
            case UnitType.Oracle:
            {
                recMax = (s.rec + (4*gainLimit)) - curUnit.rec;
                defMax = (s.def - (2*gainLimit)) - curUnit.def;
                unit.recLevelUpBonus += UnityEngine.Random.Range(2, 5);
                unit.defLevelUpBonus -= UnityEngine.Random.Range(0, 3);
                break;
            }
            case UnitType.Rex:
            {
                hpMax = (s.hp + (15*gainLimit)) - curUnit.maxHealth;
                atkMax = (s.atk + (2*gainLimit)) - curUnit.atk;
                defMax = (s.def + (2*gainLimit)) - curUnit.def;
                recMax = (s.rec + (2*gainLimit)) - curUnit.rec;
                unit.hpLevelUpBonus  += UnityEngine.Random.Range(10, 16);
                unit.atkLevelUpBonus += UnityEngine.Random.Range(1, 3);
                unit.defLevelUpBonus += UnityEngine.Random.Range(1, 3);
                unit.recLevelUpBonus += UnityEngine.Random.Range(1, 3);
                break;
            }
        }
        
        for (var i = unit.currentLevel+1; i <= unit.currentLevel + levels; i++) {
            if (i > maxLevel) break;
        
            // t / tPrev represent the fraction of the curve reached at this level vs the previous level.
            // We add only the DELTA between them each call, so repeated ModifyStats calls (one per level-up)
            // accumulate correctly instead of either double-counting the full curve or wiping prior random rolls.
            float t     = (float)i / maxLevel;
            float tPrev = (float)(i - 1) / maxLevel;

            unit.hpLevelUpBonus  += (int)Mathf.Lerp(0, hpMax,  t) - (int)Mathf.Lerp(0, hpMax,  tPrev);
            unit.atkLevelUpBonus += (int)Mathf.Lerp(0, atkMax, t) - (int)Mathf.Lerp(0, atkMax, tPrev);
            unit.defLevelUpBonus += (int)Mathf.Lerp(0, defMax, t) - (int)Mathf.Lerp(0, defMax, tPrev);
            unit.recLevelUpBonus += (int)Mathf.Lerp(0, recMax, t) - (int)Mathf.Lerp(0, recMax, tPrev);
        }

        unit.hpLevelUpBonus  = Mathf.Min(unit.hpLevelUpBonus,  hpMax);
        unit.atkLevelUpBonus = Mathf.Min(unit.atkLevelUpBonus, atkMax);
        unit.defLevelUpBonus = Mathf.Min(unit.defLevelUpBonus, defMax);
        unit.recLevelUpBonus = Mathf.Min(unit.recLevelUpBonus, recMax);
    }

}
[Serializable]
public class BotData
{
    public string botId;
    public string botName;
    public int botLevel;
    public BotPartyData party;

    public bool IsNull => string.IsNullOrEmpty(botId);
}
[Serializable]
public class BotUnitData
{
    public string unitId;
    public int unitLevel;
    public int bbLevel;
    public int sbbLevel;
    public string itemId;
    public string itemId2;
}
[Serializable]
public class BotPartyData
{
    public int mainUnitIndex;
    public List<BotUnitData> units;
}