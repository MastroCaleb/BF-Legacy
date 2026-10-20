// Unit.cs — classe normale, stessa superficie pubblica del vecchio SO
using System.Collections.Generic;
using UnityEngine;

public class Unit
{
    readonly UnitData d;
    public Unit(UnitData data) { d = data; }

    public string name => $"unit_{d.unitId}";

    // ── Dati leggeri: letti direttamente dal DTO ──
    public string unitId => d.unitId;
    public string unitName => d.unitName;
    public string unitNumber => d.unitNumber;
    public string description => d.description;
    public string summonDesc => d.summonDesc;
    public string fusionDesc => d.fusionDesc;
    public string evoDesc => d.evoDesc;
    public ElementalType element => d.element;
    public UnitRarity rarity => d.rarity;
    public AnimationType animationType => d.animationType;

    public UnitDisplay unitDisplayHomePosition => d.unitDisplayHomePosition;
    public UnitDisplay unitDisplayDetailPosition => d.unitDisplayDetailPosition;
    public UnitDisplay unitDisplayConfirmImagePosition => d.unitDisplayConfirmImagePosition;
    public UnitDisplay unitDisplayCutInImagePosition => d.unitDisplayCutInImagePosition;
    public UnitDisplay unitDisplaySummonPosition => d.unitDisplaySummonPosition;
    public Vector2 unitDisplayHpPosition => d.unitDisplayHpPosition;
    public Vector2 unitDisplayCursorPosition => d.unitDisplayCursorPosition;

    public int aiLevel => d.aiLevel;
    public int maxLevel => d.maxLevel;
    public int maxHealth => d.maxHealth;
    public int atk => d.atk;
    public int def => d.def;
    public int rec => d.rec;
    public int summonCost => d.summonCost;
    public int baseExp => d.baseExp;
    public UnitStats statsBase => d.statsBase;
    public UnitStats statsLord => d.statsLord;
    public string bbType => d.bbType;

    public float moveSpeedAttack => d.moveSpeedAttack;
    public float moveSpeedSkill => d.moveSpeedSkill;
    public int speedTypeAttack => d.speedTypeAttack;
    public int speedTypeSkill => d.speedTypeSkill;
    public int moveTypeAttack => d.moveTypeAttack;
    public int moveTypeSkill => d.moveTypeSkill;

    public string evoFrom => d.evoFrom;
    public string evoInto => d.evoInto;
    public List<string> evoMats => d.evoMats;
    public string evoItem => d.evoItem;
    public int evoZelCost => d.evoZelCost;
    public int sellPrice => d.sellPrice;
    public bool sellCaution => d.sellCaution;

    // ── Ability: costruite al primo accesso ──
    Ability _basic, _bb, _sbb, _ubb, _leader, _extra;
    bool _basicD, _bbD, _sbbD, _ubbD, _leaderD, _extraD;
    public Ability basicAbility  => Lazy(ref _basic,  ref _basicD,  d.basicAbilityId);
    public Ability bbAbility     => Lazy(ref _bb,     ref _bbD,     d.bbAbilityId);
    public Ability sbbAbility    => Lazy(ref _sbb,    ref _sbbD,    d.sbbAbilityId);
    public Ability ubbAbility    => Lazy(ref _ubb,    ref _ubbD,    d.ubbAbilityId);
    public Ability leaderAbility => Lazy(ref _leader, ref _leaderD, d.leaderAbilityId);
    public Ability extraAbility  => Lazy(ref _extra,  ref _extraD,  d.extraAbilityId);

    static Ability Lazy(ref Ability cache, ref bool done, string id)
    {
        if (!done) { cache = AbilityRegistry.GetAbilityById(id); done = true; }
        return cache;
    }

    // ── Asset: stesse chiamate AssetResolver di prima, ma su richiesta ──
    Sprite _slotIcon, _icon, _fullArt;
    Texture2D[] _sheets; bool _sheetsD;
    TextAsset _cgg, _idle, _move, _attack, _sam;

    public Sprite unitSlotIcon => _slotIcon ??= AssetResolver.LoadSprite(d.slotIconPath);
    public Sprite unitIcon     => _icon     ??= AssetResolver.LoadSprite(d.iconPath);
    public Sprite unitFullArt  => _fullArt  ??= AssetResolver.LoadSprite(d.fullArtPath);

    public Texture2D[] spriteSheets
    {
        get
        {
            if (!_sheetsD) { _sheets = LoadTextures(d.spriteSheetPaths); _sheetsD = true; }
            return _sheets;
        }
    }
    public TextAsset cggFile       => _cgg    ??= AssetResolver.LoadText(d.cggPath);
    public TextAsset idleCgsFile   => _idle   ??= AssetResolver.LoadText(d.idleCgsPath);
    public TextAsset moveCgsFile   => _move   ??= AssetResolver.LoadText(d.moveCgsPath);
    public TextAsset attackCgsFile => _attack ??= AssetResolver.LoadText(d.attackCgsPath);
    public TextAsset samFile       => _sam    ??= AssetResolver.LoadText(d.samPath);

    /// Scarta i riferimenti agli asset; si ricaricano da soli al prossimo accesso.
    public void ReleaseAssets()
    {
        _slotIcon = _icon = _fullArt = null;
        _sheets = null; _sheetsD = false;
        _cgg = _idle = _move = _attack = _sam = null;
    }

    static Texture2D[] LoadTextures(List<string> addresses)
    {
        if (addresses == null || addresses.Count == 0) return null;
        var sheets = new List<Texture2D>(addresses.Count);
        foreach (string a in addresses)
        {
            Texture2D t = AssetResolver.LoadTexture(a);
            if (t != null) sheets.Add(t);
        }
        return sheets.Count > 0 ? sheets.ToArray() : null;
    }
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
