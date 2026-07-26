using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Unit", menuName = "Units/Unit")]
public class Unit : ScriptableObject
{
    [Header("Unit Details")]
    //Unit Info
    public string unitName;
    public string unitId;
    public string unitNumber;
    public string description;
    public string summonDesc;
    public string fusionDesc;
    public string evoDesc;
    public ElementalType element;
    public UnitRarity rarity;

    [Header("Unit Visuals")]
    //Unit Visuals
    public Sprite unitSlotIcon;
    public Sprite unitIcon;
    public Sprite unitFullArt;
    public AnimationType animationType;

    //CGG AND CGS
    public Texture2D[] spriteSheets;
    public TextAsset cggFile;
    public TextAsset idleCgsFile;
    public TextAsset moveCgsFile;
    public TextAsset attackCgsFile;

    //Unit Display Values
    public UnitDisplay unitDisplayHomePosition;
    public UnitDisplay unitDisplayDetailPosition;
    public UnitDisplay unitDisplayConfirmImagePosition;
    public UnitDisplay unitDisplayCutInImagePosition;
    public UnitDisplay unitDisplaySummonPosition;
    public Vector2 unitDisplayHpPosition;
    public Vector2 unitDisplayCursorPosition;

    //SAM
    public TextAsset samFile;

    [Header("Unit Statistics")]
    //Unit Statistics
    public int aiLevel;
    public int maxLevel;
    public int maxHealth;
    public int atk;
    public int def;
    public int rec;
    public int summonCost;
    public int baseExp; // 10, 15, or 21

    [Header("Type Statistics")]
    public UnitStats statsBase;
    public UnitStats statsLord;
    public UnitStats statsAnima;
    public UnitStats statsBreaker;
    public UnitStats statsGuardian;
    public UnitStats statsOracle;
    

    [Header("Unit Abilities")]
    //Unit Abilities
    public Ability basicAbility;
    public Ability bbAbility;
    public Ability sbbAbility;
    public Ability ubbAbility;
    public Ability leaderAbility;
    public Ability extraAbility;
    public string bbType;

    [Header("Movement")]
    public float moveSpeedAttack;
    public float moveSpeedSkill;
    public int   speedTypeAttack;
    public int   speedTypeSkill;
    public int   moveTypeAttack;
    public int   moveTypeSkill;

    [Header("Evolution")]
    public string       evoFrom;
    public string       evoInto;      // ADD this — was missing
    public List<string> evoMats;
    public string       evoItem;
    public int          evoZelCost;

    [Header("Sell")]
    public int sellPrice;
    public bool sellCaution;
}
public class ImpBonuses
{
    public int healthBonus;
    public int atkBonus;
    public int defBonus;
    public int recBonus;
}
public enum UnitRarity
{
    ONE,
    TWO,
    THREE,
    FOUR,
    FIVE,
    SIX,
    SEVEN,
    OMNI
}
public enum Gender
{
    M,
    F,
    NONE
}
public enum AnimationType
{
    CGG,
    SAM
}
[System.Serializable]
public class UnitDisplay
{
    public float x;
    public float y;
    public float width;
    public float height;
    public float other;
}
[System.Serializable]
public class UnitStats
{
    public int hp;
    public int hpMin;
    public int hpMax;
    public int atk;
    public int atkMin;
    public int atkMax;
    public int def;
    public int defMin;
    public int defMax;
    public int rec;
    public int recMin;
    public int recMax;
}