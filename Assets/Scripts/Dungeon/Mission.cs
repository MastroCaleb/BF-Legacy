using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Mission", menuName = "Dungeon/Mission")]
public class Mission : ScriptableObject
{
    public string missionName;
    public int energyCost;
    public int experienceReward;
    public int zelReward;
    public List<Round> rounds;
    public string missionId;
    public string description;
    public string areaName;
    public string landName;
    public int difficulty;
    public int battleCount;
    public bool canContinue;
    public int karmaReward;
    public string requiresMissionId;
}

[System.Serializable]
public class Round
{
    public List<Enemy> enemies;
}

[System.Serializable]
public class Enemy
{
    public string unitId;
    public ActCounts actCounts;
    public List<Action> actions;
    public int health;
    public int atk;
    public int def;
    public int aiType;
    public float karmaDropCount;
    public float karmaMaxDrop;
    public float zelDropCount;
    public float zelMaxDrop;
    public ConditionResist conditionResist;
    public List<ItemDrop> itemDrops;
    public TreasureDrop treasureDrop;
    public UnitDrop unitDrop;
}

[System.Serializable]
public class Action
{
    public int priority;
    public ActionType actionType;
    public string actionParameters;
    public float percentChance;
    public ActionTargetType targetType;       // 1=Self, 2=EnemyParty, 3=AllyParty
    public TargetCondition targetCondition;   // who specifically to pick within that pool
    public List<AiCondition> selfConditions;
    public List<AiCondition> partyConditions;
}

[System.Serializable]
public class AiCondition
{
    public string type;
    public string parameters;
}

public enum ActionType
{
    Attack,
    Skill,
    Wait,
    Guard,
    TurnEnd
}

// JSON target type: 1 = self, 2 = enemy party (player squad), 3 = ally party (other enemies)
public enum ActionTargetType
{
    Self      = 1,
    EnemyParty = 2,
    AllyParty  = 3
}

public enum TargetCondition
{
    Random,
    HpMin,
    HpMax,
    HpOver25,
    HpUnder25,
    HpOver50,
    HpUnder50,
    HpOver75,
    HpUnder75,
    AtkMin,
    AtkMax,
    DefMin,
    DefMax,
    AtkDown,
    DefDown,
    HealDown,
    StatDebuffed,
    StatBuffed,
    BadStatus,
    Poison,
    Paralysis,
    Guard,
    Weakness,
    BbFull,
    BbOver50,
    BbUnder50,
    BbAttack,
    BbHeal,
    BbSupport,
    ElemFire,
    ElemWater,
    ElemEarth,
    ElemThunder,
    ElemLight,
    ElemDark,
    Non
}

[System.Serializable]
public class ConditionResist
{
    public float curse;
    public float injury;
    public float paralysis;
    public float poison;
    public float sick;
    public float weaken;
}

[System.Serializable]
public class ItemDrop
{
    public string itemName;
    public float dropChance;
    public float dropRest;
}

[System.Serializable]
public class TreasureDrop
{
    public float chestDropChance;
    public float bcOrHcAmount;
    public float bcOrHcChance;
    public string itemName;
    public float itemDropChance;
    public float karmaAmount;
    public float karmaChance;
    public float zelAmount;
    public float zelChance;
}

[System.Serializable]
public class UnitDrop
{
    public string unitId;
    public float dropChance;
    public float level;
    public string type;
    public string bonusUnitId;
    public float bonusDropChance;
    public float bonusLevel;
    public string bonusType;
}

[System.Serializable]
public class ActCounts
{
    public int min;
    public int max;
}