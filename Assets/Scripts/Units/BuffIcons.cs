using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
//  BuffIcons.cs — port of buff_debuff_icon.gd + unit_buffs.gd icon cycling.
//  Enum order == BuffAtom.BuffMst in buff_object.gd, so enum value == spritesheet cell index.
//  DO NOT reorder the enums.
// ─────────────────────────────────────────────────────────────

public enum BuffMst
{
        DESC_BUFF_ATKUP,
        DESC_BUFF_ATKDOWN,
        DESC_BUFF_DEFUP,
        DESC_BUFF_DEFDOWN,
        DESC_BUFF_RECUP,
        DESC_BUFF_RECDOWN,
        DESC_BUFF_CRTRATEUP,
        DESC_BUFF_HPREC,
        DESC_BUFF_HCDROP,
        DESC_BUFF_BCDROP,
        DESC_BUFF_ITEMDROP,
        DESC_BUFF_KOBLK,
        DESC_BUFF_FIREATKUP,
        DESC_BUFF_FIREDEFUP,
        DESC_BUFF_FIRERECUP,
        DESC_BUFF_WATERATKUP,
        DESC_BUFF_WATERDEFUP,
        DESC_BUFF_WATERRECUP,
        DESC_BUFF_EARTHATKUP,
        DESC_BUFF_EARTHDEFUP,
        DESC_BUFF_EARTHRECUP,
        DESC_BUFF_THUNDERATKUP,
        DESC_BUFF_THUNDERDEFUP,
        DESC_BUFF_THUNDERRECUP,
        DESC_BUFF_LIGHTATKUP,
        DESC_BUFF_LIGHTDEFUP,
        DESC_BUFF_LIGHTRECUP,
        DESC_BUFF_DARKATKUP,
        DESC_BUFF_DARKDEFUP,
        DESC_BUFF_DARKRECUP,
        DESC_BUFF_FIREATKDOWN,
        DESC_BUFF_FIREDEFDOWN,
        DESC_BUFF_FIRERECDOWN,
        DESC_BUFF_WATERATKDOWN,
        DESC_BUFF_WATERDEFDOWN,
        DESC_BUFF_WATERRECDOWN,
        DESC_BUFF_EARTHATKDOWN,
        DESC_BUFF_EARTHDEFDOWN,
        DESC_BUFF_EARTHRECDOWN,
        DESC_BUFF_THUNDERATKDOWN,
        DESC_BUFF_THUNDERDEFDOWN,
        DESC_BUFF_THUNDERRECDOWN,
        DESC_BUFF_LIGHTATKDOWN,
        DESC_BUFF_LIGHTDEFDOWN,
        DESC_BUFF_LIGHTRECDOWN,
        DESC_BUFF_DARKATKDOWN,
        DESC_BUFF_DARKDEFDOWN,
        DESC_BUFF_DARKRECDOWN,
        DESC_BUFF_FIREDMGDOWN,
        DESC_BUFF_WATERDMGDOWN,
        DESC_BUFF_EARTHDMGDOWN,
        DESC_BUFF_THUNDERDMGDOWN,
        DESC_BUFF_LIGHDMGDOWN,
        DESC_BUFF_DARKDMGDOWN,
        DESC_BUFF_POISONBLK,
        DESC_BUFF_WEAKBLK,
        DESC_BUFF_SICKBLK,
        DESC_BUFF_INJURYBLK,
        DESC_BUFF_CURSEBLK,
        DESC_BUFF_PARALYSISBLK,
        DESC_BUFF_BBREC,
        DESC_BUFF_DAMAGEBB,
        DESC_BUFF_GETENEATT,
        DESC_BUFF_REPENEATT,
        DESC_BUFF_IGNOREDEF,
        DESC_BUFF_DBLSTRIKE,
        DESC_BUFF_HITUP,
        DESC_BUFF_ADDFIRE,
        DESC_BUFF_ADDWATER,
        DESC_BUFF_ADDEARTH,
        DESC_BUFF_ADDTHUNDER,
        DESC_BUFF_ADDLIGHT,
        DESC_BUFF_ADDDARK,
        DESC_BUFF_DAMAGECUT,
        DESC_BUFF_SPARKUP,
        DESC_BUFF_SPARKHC,
        DESC_BUFF_SPARKBC,
        DESC_BUFF_SPARKITEM,
        DESC_BUFF_DISABLELS,
        DESC_BUFF_RAIDATKUP,
        DESC_BUFF_RAIDDEFUP,
        DESC_BUFF_RAIDRECUP,
        DESC_BUFF_RAIDCRTUP,
        DESC_BUFF_RAIDDMGDOWN,
        DESC_BUFF_ATKDOWNLOCK,
        DESC_BUFF_DEFDOWNLOCK,
        DESC_BUFF_RECDOWNLOCK,
        DESC_BUFF_ADDPOISON,
        DESC_BUFF_ADDWEAK,
        DESC_BUFF_ADDSICK,
        DESC_BUFF_ADDINJURY,
        DESC_BUFF_ADDCURSE,
        DESC_BUFF_ADDPARA,
        DESC_BUFF_SPARKABILITY,
        DESC_BUFF_ACTIVEOD,
        DESC_BUFF_TURNDMG,
        DESC_BUFF_BBATKUP,
        DESC_BUFF_COUNTERDAMAGE,
        DESC_BUFF_ADDATKDOWN,
        DESC_BUFF_ADDDEFDOWN,
        DESC_BUFF_ADDRECDOWN,
        DESC_BUFF_BBFILL,
        DESC_BUFF_CRTUP,
        DESC_BUFF_FIREDMGUP,
        DESC_BUFF_WATERMDGUP,
        DESC_BUFF_EARTHDMGUP,
        DESC_BUFF_THUNDERDMGUP,
        DESC_BUFF_LIGHTDMGUP,
        DESC_BUFF_DARKDMGUP,
        DESC_BUFF_POISIONCOUNTER,
        DESC_BUFF_WEAKCOUNTER,
        DESC_BUFF_SICKCOUNTER,
        DESC_BUFF_INJCONTER,
        DESC_BUFF_CURSECOUNTER,
        DESC_BUFF_PARALYCOUNTER,
        DESC_BUFF_KOBLOCK,
        DESC_BUFF_HCDOWN,
        DESC_BUFF_BCDOWN,
        DESC_BUFF_SPARKDMGUP,
        DESC_BUFF_BBATKDOWN,
        DESC_BUFF_FIRESHIELD,
        DESC_BUFF_WATERSHIELD,
        DESC_BUFF_EARTHSHIELD,
        DESC_BUFF_THUNDERSHIELD,
        DESC_BUFF_LIGHTSHIELD,
        DESC_BUFF_DARKSHIELD,
        DESC_BUFF_AILDMGUP,
        DESC_BUFF_SPARKBBUP,
        DESC_BUFF_GUARDCUT,
        DESC_BUFF_GUARDBBUP,
        DESC_BUFF_GUARDPARAMUP,
        DESC_BUFF_BBFILLDOWN,
        DESC_BUFF_SPARKATKUP,
        DESC_BUFF_SPARKDEFUP,
        DESC_BUFF_SPARKRECUP,
        DESC_BUFF_SPARKCRTUP,
        DESC_BUFF_RESISTATKDOWN,
        DESC_BUFF_RESISTDEFDOWN,
        DESC_BUFF_RESISTRECDOWN,
        DESC_BUFF_ATKUP2,
        DESC_BUFF_DEFUP2,
        DESC_BUFF_RECUP2,
        DESC_BUFF_SPARKCRTACTIVATED,
        DESC_BUFF_OVERDRIVEUP,
        DESC_BUFF_BEENATK_HPREC,
        DESC_BUFF_HPABS,
        DESC_BUFF_SPARK_HPREC,
        DESC_BUFF_ATKUP3,
        DESC_BUFF_DEFUP3,
        DESC_BUFF_RECUP3,
        DESC_BUFF_CRTRATEUP2,
        DESC_BUFF_SPARKDMGUP2,
        DESC_BUFF_HPREC2,
        DESC_BUFF_BBFILL2,
        DESC_BUFF_ATKUP4,
        DESC_BUFF_DEFUP4,
        DESC_BUFF_RECUP4,
        DESC_BUFF_CRTDOWN,
        DESC_BUFF_ELEMENTDOWN,
        DESC_BUFF_SPARKDMGDOWN,
        DESC_BUFF_AOEATK,
        DESC_BUFF_NULLSPHERE,
        DESC_BUFF_NULLES,
        DESC_BUFF_BBREDUC,
        DESC_BUFF_ODFILLBOOST,
        DESC_BUFF_ATKREDUC,
        DESC_BUFF_TARGETED,
        DESC_BUFF_PROB_ATKREDUC,
        DESC_BUFF_PROB_DEFREDUC,
        DESC_BUFF_PROB_RECREDUC,
        DESC_BUFF_CRITDMG_VUL,
        DESC_BUFF_ELEDMG_VUL,
        DESC_BUFF_TAUNT,
        DESC_BUFF_STEALTH,
        DESC_BUFF_ALL,
        DESC_BUFF_FIRE,
        DESC_BUFF_WATER,
        DESC_BUFF_EARTH,
        DESC_BUFF_THUNDER,
        DESC_BUFF_LIGHT,
        DESC_BUFF_DARK,
        DESC_BUFF_SPHERE_DISABLE,
        DESC_BUFF_DMG_IMMUNINTY,
        DESC_BUFF_SKIP_TURN,
        DESC_BUFF_TIME_STOP,
        DESC_BUFF_EVASION,
        DESC_BUFF_CRIT_DMG_DOWN,
        DESC_BUFF_PIERCE_STACK_FIRE_MITIGATION,
        DESC_BUFF_PIERCE_STACK_WATER_MITIGATION,
        DESC_BUFF_PIERCE_STACK_EARTH_MITIGATION,
        DESC_BUFF_PIERCE_STACK_THUNDER_MITIGATION,
        DESC_BUFF_PIERCE_STACK_LIGHT_MITIGATION,
        DESC_BUFF_PIERCE_STACK_DARK_MITIGATION,
        DESC_BUFF_PIERCE_WEAK_MITIGATION,
        DESC_BUFF_PIERCE_MITIGATION,
        DESC_BUFF_ATK_EU,
        DESC_BUFF_DBLBB_EU,
        DESC_BUFF_REC_EU,
        DESC_SPARK_UP_DMG_ELEMENT_FIRE,
        DESC_SPARK_UP_DMG_ELEMENT_WATER,
        DESC_SPARK_UP_DMG_ELEMENT_DARK,
        DESC_SPARK_UP_DMG_ELEMENT_TREE,
        DESC_SPARK_UP_DMG_ELEMENT_THUNDER,
        DESC_SPARK_UP_DMG_ELEMENT_LIGHT,
        DESC_CRIT_DMG_ELEMENT_FIRE,
        DESC_CRIT_DMG_ELEMENT_WATER,
        DESC_CRIT_DMG_ELEMENT_DARK,
        DESC_CRIT_DMG_ELEMENT_TREE,
        DESC_CRIT_DMG_ELEMENT_THUNDER,
        DESC_CRIT_DMG_ELEMENT_LIGHT,
        DESC_MAX_HP_DOWN,
        DESC_ACTIVE_HEAL_REDUCE,
        DESC_PASSIVE_HEAL_REDUCE,
        DESC_HC_HEAL_REDUCE,
        DESC_BUFF_NEGATEANGEL,
        DESC_BUFF_DOOM,
        DESC_BUFF_FIRE_MITIGATION_DOWN,
        DESC_BUFF_WATER_MITIGATION_DOWN,
        DESC_BUFF_EARTH_MITIGATION_DOWN,
        DESC_BUFF_THUNDER_MITIGATION_DOWN,
        DESC_BUFF_LIGHT_MITIGATION_DOWN,
        DESC_BUFF_DARK_MITIGATION_DOWN,
        DESC_BUFF_MAXHPDOT,
        DESC_BUFF_HPBOOST,
        DESC_BUFF_NEGATE_LOCK,
        DESC_BUFF_NEGATE_HP_REDUCTION,
        DESC_BUFF_NEGATE_HEAL_REDUCTION,
        DESC_BUFF_NEGATE_PURGE,
        DESC_BUFF_NEGATE_DRAIN,
        DESC_BUFF_NEGATE_SKIPTURN,
        DESC_BUFF_NEGATE_DOOM,
        DESC_BUFF_DBB_USED,
        DESC_BUFF_AMNESIA,
        DESC_BUFF_NEGATE_AMNESIA,
        DESC_BUFF_BBCOST_REDUCTION,
        DESC_BUFF_MARTYR,
        DESC_BUFF_ENNUI,
        DESC_BUFF_NEGATE_ENNUI,
        DESC_BUFF_FINALCHARGE,
        DESC_BUFF_HPDMGRESIST,
        DESC_BUFF_AOEDMGRESIST,
        DESC_BUFF_TGTDMGRESIST,
        DESC_BUFF_DOTRESIST,
        DESC_BUFF_NATKRESIST
}

public enum DebuffMst
{
    DESC_BADSTATE_POISON, DESC_BADSTATE_WEAK, DESC_BADSTATE_SICK,
    DESC_BADSTATE_INJURY, DESC_BADSTATE_CURSE, DESC_BADSTATE_PARA
}

// ─────────────────────────────────────────────────────────────
//  Spritesheet slicing (Godot region_rect → Unity Sprite.Create)
// ─────────────────────────────────────────────────────────────
public static class BuffIconSheets
{
    public const int   Step       = 32;     // BUFF_ICON_POSITION_OFFSET_PX
    public const int   PerRow     = 15;     // BUFF_ICONS_PER_ROW
    public const int   DebuffPerRow = 6;
    public const float IconSize   = 31f;    // Godot uses 31.15; whole pixels avoid bleeding in Unity

    // Assign these from a bootstrap script/inspector, or leave null to load from Resources.
    public static Texture2D buffSheet, sgSheet, debuffSheet;
    public static Sprite    ubbOverlay;
    public const string BuffPath = "BuffIcons/battle_buff_icon", SgPath = "BuffIcons/battle_buff_sg_icon",
                        DebuffPath = "BuffIcons/battle_bad_icons", UbbPath = "BuffIcons/nuff_ubb_icon";

    static readonly Dictionary<int, Sprite> buffCache   = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite> sgCache     = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite> debuffCache = new Dictionary<int, Sprite>();

    static Texture2D Tex(ref Texture2D field, string path)
    {
        if (field == null) field = Resources.Load<Texture2D>(path);
        return field;
    }

    static Sprite Cut(Texture2D tex, int cell, int perRow, Dictionary<int, Sprite> cache)
    {
        if (tex == null || cell < 0) return null;
        if (cache.TryGetValue(cell, out var cached) && cached != null) return cached;
        float x = Step * (cell % perRow);
        float yTop = Step * (cell / perRow);
        float y = tex.height - yTop - IconSize;            // Unity's origin is bottom-left, Godot's is top-left
        if (x + IconSize > tex.width || y < 0) return null; // cell outside the sheet
        var s = Sprite.Create(tex, new Rect(x, y, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
        cache[cell] = s;
        return s;
    }

    public static Sprite Buff(int buffMstIndex)
    {
        int taunt = (int)BuffMst.DESC_BUFF_TAUNT;
        if (buffMstIndex < taunt) return Cut(Tex(ref buffSheet, BuffPath), buffMstIndex, PerRow, buffCache);
        return Cut(Tex(ref sgSheet, SgPath), buffMstIndex - taunt, PerRow, sgCache);   // SG icons follow the normal ones in the enum
    }

    public static Sprite Debuff(int debuffIndex)
    {
        if (debuffIndex < 0 || debuffIndex > 5) return null;
        return Cut(Tex(ref debuffSheet, DebuffPath), debuffIndex, DebuffPerRow, debuffCache);
    }

    public static Sprite Ubb()
    {
        if (ubbOverlay == null) ubbOverlay = Resources.Load<Sprite>(UbbPath);
        return ubbOverlay;
    }
}

// ─────────────────────────────────────────────────────────────
//  proc id → icon list (generated from ACTIVE_PROCS) + per-effect filtering
//  (Godot filters the list by which params the decoder found; this does the same from Effect fields)
// ─────────────────────────────────────────────────────────────
public static class BuffIconTable
{
    static readonly Dictionary<string, int[]> procIcons = new Dictionary<string, int[]>
    {
        { "3", new[] { (int)BuffMst.DESC_BUFF_HPREC } },
        { "5", new[] { (int)BuffMst.DESC_BUFF_ATKUP, (int)BuffMst.DESC_BUFF_DEFUP, (int)BuffMst.DESC_BUFF_RECUP, (int)BuffMst.DESC_BUFF_CRTRATEUP } },
        { "6", new[] { (int)BuffMst.DESC_BUFF_BCDROP, (int)BuffMst.DESC_BUFF_HCDROP, (int)BuffMst.DESC_BUFF_ITEMDROP } },
        { "7", new[] { (int)BuffMst.DESC_BUFF_KOBLK } },
        { "8", new[] { (int)BuffMst.DESC_BUFF_HPBOOST } },
        { "9", new[] { (int)BuffMst.DESC_BUFF_ATKDOWN, (int)BuffMst.DESC_BUFF_DEFDOWN, (int)BuffMst.DESC_BUFF_RECDOWN, (int)BuffMst.DESC_BUFF_CRTDOWN } },
        { "16", new[] { (int)BuffMst.DESC_BUFF_FIREDMGDOWN, (int)BuffMst.DESC_BUFF_WATERDMGDOWN, (int)BuffMst.DESC_BUFF_EARTHDMGDOWN, (int)BuffMst.DESC_BUFF_THUNDERDMGDOWN, (int)BuffMst.DESC_BUFF_LIGHDMGDOWN, (int)BuffMst.DESC_BUFF_DARKDMGDOWN } },
        { "17", new[] { (int)BuffMst.DESC_BUFF_POISONBLK, (int)BuffMst.DESC_BUFF_WEAKBLK, (int)BuffMst.DESC_BUFF_SICKBLK, (int)BuffMst.DESC_BUFF_INJURYBLK, (int)BuffMst.DESC_BUFF_CURSEBLK, (int)BuffMst.DESC_BUFF_PARALYSISBLK } },
        { "18", new[] { (int)BuffMst.DESC_BUFF_DAMAGECUT } },
        { "19", new[] { (int)BuffMst.DESC_BUFF_BBREC } },
        { "20", new[] { (int)BuffMst.DESC_BUFF_DAMAGEBB } },
        { "22", new[] { (int)BuffMst.DESC_BUFF_IGNOREDEF } },
        { "23", new[] { (int)BuffMst.DESC_BUFF_SPARKUP, (int)BuffMst.DESC_BUFF_SPARKHC, (int)BuffMst.DESC_BUFF_SPARKBC, (int)BuffMst.DESC_BUFF_SPARKITEM } },
        { "24", new[] { (int)BuffMst.DESC_BUFF_ATKUP3, (int)BuffMst.DESC_BUFF_DEFUP3, (int)BuffMst.DESC_BUFF_RECUP3 } },
        { "26", new[] { (int)BuffMst.DESC_BUFF_HITUP } },
        { "30", new[] { (int)BuffMst.DESC_BUFF_ADDFIRE, (int)BuffMst.DESC_BUFF_ADDWATER, (int)BuffMst.DESC_BUFF_ADDEARTH, (int)BuffMst.DESC_BUFF_ADDTHUNDER, (int)BuffMst.DESC_BUFF_ADDLIGHT, (int)BuffMst.DESC_BUFF_ADDDARK } },
        { "36", new[] { (int)BuffMst.DESC_BUFF_DISABLELS } },
        { "39", new[] { (int)BuffMst.DESC_BUFF_FIREDMGDOWN, (int)BuffMst.DESC_BUFF_WATERDMGDOWN, (int)BuffMst.DESC_BUFF_EARTHDMGDOWN, (int)BuffMst.DESC_BUFF_THUNDERDMGDOWN, (int)BuffMst.DESC_BUFF_LIGHDMGDOWN, (int)BuffMst.DESC_BUFF_DARKDMGDOWN } },
        { "40", new[] { (int)BuffMst.DESC_BUFF_ADDPOISON, (int)BuffMst.DESC_BUFF_ADDWEAK, (int)BuffMst.DESC_BUFF_ADDSICK, (int)BuffMst.DESC_BUFF_ADDINJURY, (int)BuffMst.DESC_BUFF_ADDCURSE, (int)BuffMst.DESC_BUFF_ADDPARA } },
        { "44", new[] { (int)BuffMst.DESC_BUFF_TURNDMG } },
        { "45", new[] { (int)BuffMst.DESC_BUFF_BBATKUP } },
        { "50", new[] { (int)BuffMst.DESC_BUFF_COUNTERDAMAGE } },
        { "52", new[] { (int)BuffMst.DESC_BUFF_BBFILL } },
        { "53", new[] { (int)BuffMst.DESC_BUFF_POISIONCOUNTER, (int)BuffMst.DESC_BUFF_WEAKCOUNTER, (int)BuffMst.DESC_BUFF_SICKCOUNTER, (int)BuffMst.DESC_BUFF_INJCONTER, (int)BuffMst.DESC_BUFF_CURSECOUNTER, (int)BuffMst.DESC_BUFF_PARALYCOUNTER } },
        { "54", new[] { (int)BuffMst.DESC_BUFF_CRTUP } },
        { "55", new[] { (int)BuffMst.DESC_BUFF_FIREDMGUP, (int)BuffMst.DESC_BUFF_WATERMDGUP, (int)BuffMst.DESC_BUFF_EARTHDMGUP, (int)BuffMst.DESC_BUFF_THUNDERDMGUP, (int)BuffMst.DESC_BUFF_LIGHTDMGUP, (int)BuffMst.DESC_BUFF_DARKDMGUP } },
        { "56", new[] { (int)BuffMst.DESC_BUFF_KOBLOCK } },
        { "57", new[] { (int)BuffMst.DESC_BUFF_BCDOWN, (int)BuffMst.DESC_BUFF_HCDOWN } },
        { "58", new[] { (int)BuffMst.DESC_BUFF_SPARKDMGUP } },
        { "59", new[] { (int)BuffMst.DESC_BUFF_BBATKDOWN } },
        { "62", new[] { (int)BuffMst.DESC_BUFF_FIRESHIELD, (int)BuffMst.DESC_BUFF_WATERSHIELD, (int)BuffMst.DESC_BUFF_EARTHSHIELD, (int)BuffMst.DESC_BUFF_THUNDERSHIELD, (int)BuffMst.DESC_BUFF_LIGHTSHIELD, (int)BuffMst.DESC_BUFF_DARKSHIELD } },
        { "65", new[] { (int)BuffMst.DESC_BUFF_AILDMGUP } },
        { "67", new[] { (int)BuffMst.DESC_BUFF_SPARKBBUP } },
        { "68", new[] { (int)BuffMst.DESC_BUFF_GUARDCUT } },
        { "69", new[] { (int)BuffMst.DESC_BUFF_GUARDBBUP } },
        { "71", new[] { (int)BuffMst.DESC_BUFF_BBFILLDOWN } },
        { "73", new[] { (int)BuffMst.DESC_BUFF_RESISTATKDOWN, (int)BuffMst.DESC_BUFF_RESISTDEFDOWN, (int)BuffMst.DESC_BUFF_RESISTRECDOWN } },
        { "76", new[] { (int)BuffMst.DESC_BUFF_DBLSTRIKE } },
        { "78", new[] { (int)BuffMst.DESC_BUFF_ATKUP2, (int)BuffMst.DESC_BUFF_DEFUP2, (int)BuffMst.DESC_BUFF_RECUP2, (int)BuffMst.DESC_BUFF_CRTRATEUP2 } },
        { "83", new[] { (int)BuffMst.DESC_BUFF_SPARKCRTACTIVATED } },
        { "84", new[] { (int)BuffMst.DESC_BUFF_OVERDRIVEUP } },
        { "85", new[] { (int)BuffMst.DESC_BUFF_BEENATK_HPREC } },
        { "86", new[] { (int)BuffMst.DESC_BUFF_HPABS } },
        { "87", new[] { (int)BuffMst.DESC_BUFF_SPARK_HPREC } },
        { "88", new[] { (int)BuffMst.DESC_BUFF_SPARKDMGUP2 } },
        { "89", new[] { (int)BuffMst.DESC_BUFF_ATKUP4, (int)BuffMst.DESC_BUFF_DEFUP4, (int)BuffMst.DESC_BUFF_RECUP4 } },
        { "93", new[] { (int)BuffMst.DESC_BUFF_CRIT_DMG_DOWN, (int)BuffMst.DESC_BUFF_ELEMENTDOWN, (int)BuffMst.DESC_BUFF_SPARKDMGDOWN, (int)BuffMst.DESC_BUFF_HPDMGRESIST, (int)BuffMst.DESC_BUFF_AOEDMGRESIST, (int)BuffMst.DESC_BUFF_TGTDMGRESIST, (int)BuffMst.DESC_BUFF_DOTRESIST, (int)BuffMst.DESC_BUFF_NATKRESIST } },
        { "94", new[] { (int)BuffMst.DESC_BUFF_AOEATK } },
        { "95", new[] { (int)BuffMst.DESC_BUFF_SPHERE_DISABLE } },
        { "96", new[] { (int)BuffMst.DESC_BUFF_NULLES } },
        { "113", new[] { (int)BuffMst.DESC_BUFF_ODFILLBOOST } },
        { "119", new[] { (int)BuffMst.DESC_BUFF_BBREDUC } },
        { "126", new[] { (int)BuffMst.DESC_BUFF_ATKREDUC } },
        { "127", new[] { (int)BuffMst.DESC_BUFF_TARGETED } },
        { "130", new[] { (int)BuffMst.DESC_BUFF_PROB_ATKREDUC, (int)BuffMst.DESC_BUFF_PROB_DEFREDUC, (int)BuffMst.DESC_BUFF_PROB_RECREDUC } },
        { "132", new[] { (int)BuffMst.DESC_BUFF_CRITDMG_VUL, (int)BuffMst.DESC_BUFF_ELEDMG_VUL } },
        { "133", new[] { (int)BuffMst.DESC_BUFF_ACTIVEOD } },
        { "902", new[] { (int)BuffMst.DESC_BUFF_RAIDATKUP, (int)BuffMst.DESC_BUFF_RAIDDEFUP, (int)BuffMst.DESC_BUFF_RAIDRECUP, (int)BuffMst.DESC_BUFF_RAIDCRTUP } },
        { "907", new[] { (int)BuffMst.DESC_BUFF_RAIDDMGDOWN } },
        { "10000", new[] { (int)BuffMst.DESC_BUFF_TAUNT } },
        { "10001", new[] { (int)BuffMst.DESC_BUFF_STEALTH } },
        { "10004", new[] { (int)BuffMst.DESC_BUFF_DMG_IMMUNINTY } },
        { "10005", new[] { (int)BuffMst.DESC_BUFF_SKIP_TURN } },
        { "10006", new[] { (int)BuffMst.DESC_BUFF_TIME_STOP } },
        { "10007", new[] { (int)BuffMst.DESC_BUFF_EVASION } },
        { "10017", new[] { (int)BuffMst.DESC_BUFF_ALL, (int)BuffMst.DESC_BUFF_FIRE, (int)BuffMst.DESC_BUFF_WATER, (int)BuffMst.DESC_BUFF_EARTH, (int)BuffMst.DESC_BUFF_THUNDER, (int)BuffMst.DESC_BUFF_LIGHT, (int)BuffMst.DESC_BUFF_DARK } },
        { "10015", new[] { (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_FIRE, (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_WATER, (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_TREE, (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_THUNDER, (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_LIGHT, (int)BuffMst.DESC_SPARK_UP_DMG_ELEMENT_DARK } },
        { "10016", new[] { (int)BuffMst.DESC_CRIT_DMG_ELEMENT_FIRE, (int)BuffMst.DESC_CRIT_DMG_ELEMENT_WATER, (int)BuffMst.DESC_CRIT_DMG_ELEMENT_TREE, (int)BuffMst.DESC_CRIT_DMG_ELEMENT_THUNDER, (int)BuffMst.DESC_CRIT_DMG_ELEMENT_LIGHT, (int)BuffMst.DESC_CRIT_DMG_ELEMENT_DARK } },
        { "10018", new[] { (int)BuffMst.DESC_MAX_HP_DOWN } },
        { "10021", new[] { (int)BuffMst.DESC_ACTIVE_HEAL_REDUCE } },
        { "10022", new[] { (int)BuffMst.DESC_PASSIVE_HEAL_REDUCE } },
        { "10023", new[] { (int)BuffMst.DESC_HC_HEAL_REDUCE } },
        { "10025", new[] { (int)BuffMst.DESC_BUFF_NEGATEANGEL } },
        { "10026", new[] { (int)BuffMst.DESC_BUFF_DOOM } },
        { "10027", new[] { (int)BuffMst.DESC_BUFF_AMNESIA } },
        { "10028", new[] { (int)BuffMst.DESC_BUFF_MARTYR } },
        { "10031", new[] { (int)BuffMst.DESC_BUFF_ENNUI } },
        { "10032", new[] { (int)BuffMst.DESC_BUFF_FINALCHARGE } },
        { "10034", new[] { (int)BuffMst.DESC_BUFF_CRIT_DMG_DOWN, (int)BuffMst.DESC_BUFF_ELEMENTDOWN, (int)BuffMst.DESC_BUFF_SPARKDMGDOWN, (int)BuffMst.DESC_BUFF_HPDMGRESIST, (int)BuffMst.DESC_BUFF_AOEDMGRESIST, (int)BuffMst.DESC_BUFF_TGTDMGRESIST, (int)BuffMst.DESC_BUFF_DOTRESIST, (int)BuffMst.DESC_BUFF_NATKRESIST } },
        { "11001", new[] { (int)BuffMst.DESC_BUFF_MAXHPDOT } },
        { "11003", new[] { (int)BuffMst.DESC_BUFF_NEGATE_LOCK, (int)BuffMst.DESC_BUFF_NEGATE_HP_REDUCTION, (int)BuffMst.DESC_BUFF_NEGATE_HEAL_REDUCTION, (int)BuffMst.DESC_BUFF_NEGATE_PURGE, (int)BuffMst.DESC_BUFF_NEGATE_DRAIN, (int)BuffMst.DESC_BUFF_NEGATE_SKIPTURN, (int)BuffMst.DESC_BUFF_NEGATE_DOOM, (int)BuffMst.DESC_BUFF_NEGATE_AMNESIA, (int)BuffMst.DESC_BUFF_NEGATE_ENNUI } },
        { "11004", new[] { (int)BuffMst.DESC_BUFF_NEGATE_LOCK, (int)BuffMst.DESC_BUFF_NEGATE_HP_REDUCTION, (int)BuffMst.DESC_BUFF_NEGATE_HEAL_REDUCTION, (int)BuffMst.DESC_BUFF_NEGATE_PURGE, (int)BuffMst.DESC_BUFF_NEGATE_DRAIN, (int)BuffMst.DESC_BUFF_NEGATE_SKIPTURN, (int)BuffMst.DESC_BUFF_NEGATE_DOOM, (int)BuffMst.DESC_BUFF_NEGATE_AMNESIA, (int)BuffMst.DESC_BUFF_NEGATE_ENNUI } },
        { "11005", new[] { (int)BuffMst.DESC_BUFF_BBCOST_REDUCTION } },
        { "70001", new[] { (int)BuffMst.DESC_BUFF_REC_EU } },
        { "70002", new[] { (int)BuffMst.DESC_BUFF_DBLBB_EU } },
    };

    static int ElemIdx(ElementalType e) => (int)e - 1; // Fire..Dark → 0..5

    static ElementalType ParseElement(string s)
    {
        if (string.IsNullOrEmpty(s)) return ElementalType.None;
        s = s.ToLowerInvariant();
        if (s.Contains("fire"))    return ElementalType.Fire;
        if (s.Contains("water"))   return ElementalType.Water;
        if (s.Contains("earth") || s.Contains("tree")) return ElementalType.Earth;
        if (s.Contains("thunder")) return ElementalType.Thunder;
        if (s.Contains("light"))   return ElementalType.Light;
        if (s.Contains("dark"))    return ElementalType.Dark;
        return ElementalType.None;
    }

    /// Returns the BuffMst indices to display for an active effect (empty = no icon).
    public static void IconsFor(Effect e, List<int> outIcons)
    {
        if (e == null) return;
        if (e.isPassive) { PassiveIcons(e, outIcons); return; }   // passive ids are a different id space than ACTIVE_PROCS
        string id = e.procId;
        if (id == null || !procIcons.TryGetValue(id, out var all)) return;
        int before = outIcons.Count;
        Filtered(e, id, all, outIcons);
        if (outIcons.Count == before) outIcons.Add(all[0]);   // a filter found no matching param: still show the proc's main icon
    }

    /// Passives (leader / extra skills...) have no entry in ACTIVE_PROCS, so icons come from which params the effect carries.
    /// (Godot shows no icons for passives; this is deliberately an addition.)
    static void PassiveIcons(Effect e, List<int> o)
    {
        int[] stat   = procIcons["5"];
        Filtered(e, "5",  stat,               o);   // atk / def / rec / crit (elemental variants too)
        if (e.statBuff != null && e.statBuff.hpBuff > 0) o.Add((int)BuffMst.DESC_BUFF_HPBOOST);
        Filtered(e, "6",  procIcons["6"],     o);   // bc / hc / item drop
        Filtered(e, "24", procIcons["24"],    o);   // parameter conversion
        Filtered(e, "16", procIcons["16"],     o);   // elemental mitigation
        Filtered(e, "55", procIcons["55"],    o);   // elemental damage up
        Filtered(e, "17", procIcons["17"],    o);   // ailment resist
        var d = e.damageBuff;
        if (d != null)
        {
            if (d.dmgMitigationPercent > 0 && !(d.mitigateFire || d.mitigateWater || d.mitigateEarth || d.mitigateThunder || d.mitigateLight || d.mitigateDark))
                o.Add((int)BuffMst.DESC_BUFF_DAMAGECUT);
            if (d.sparkDmgBuff > 0)          o.Add((int)BuffMst.DESC_BUFF_SPARKUP);
            if (d.critMultiplier > 0)        o.Add((int)BuffMst.DESC_BUFF_CRTUP);
            if (d.defenseIgnorePercent > 0)  o.Add((int)BuffMst.DESC_BUFF_IGNOREDEF);
        }
        var f = e.bcFill;
        if (f != null && (f.bcFillPerTurn > 0 || f.bbBCFill > 0 || f.bbBCFillPercent > 0f || f.bcFillOnSparkHigh > 0 || f.bcFillWhenAttackedHigh > 0))
            o.Add((int)BuffMst.DESC_BUFF_BBFILL);
        if (e.gradualHeal != null && e.gradualHeal.healHigh > 0) o.Add((int)BuffMst.DESC_BUFF_HPREC);
    }

    static void Filtered(Effect e, string id, int[] all, List<int> outIcons)
    {

        switch (id)
        {
            case "5": case "78": case "902":
            {
                var s = e.statBuff; if (s == null) break;
                var el = ParseElement(s.elementBuffed);
                if (el == ElementalType.None && s.elementsBuffed != null && s.elementsBuffed.Count > 0)
                    el = ParseElement(s.elementsBuffed[0]);
                if (el != ElementalType.None && id == "5")
                {
                    int b = (int)BuffMst.DESC_BUFF_FIREATKUP + ElemIdx(el) * 3; // atk, def, rec per element
                    if (s.atkBuff > 0) outIcons.Add(b);
                    if (s.defBuff > 0) outIcons.Add(b + 1);
                    if (s.recBuff > 0) outIcons.Add(b + 2);
                }
                else
                {
                    if (s.atkBuff  > 0) outIcons.Add(all[0]);
                    if (s.defBuff  > 0) outIcons.Add(all[1]);
                    if (s.recBuff  > 0) outIcons.Add(all[2]);
                }
                if (s.critBuff > 0 && all.Length > 3) outIcons.Add(all[3]);
                return;
            }
            case "6":
            {
                var d = e.dropRate; if (d == null) break;
                if (d.bcDropRate   > 0) outIcons.Add(all[0]);
                if (d.hcDropRate   > 0) outIcons.Add(all[1]);
                if (d.itemDropRate > 0) outIcons.Add(all[2]);
                return;
            }
            case "9":
            {
                var d = e.statDebuff; if (d == null) break;
                bool atk = (d.buff1 != null && d.buff1.atkBuffPercent != 0) || (d.buff2 != null && d.buff2.atkBuffPercent != 0);
                bool def = (d.buff1 != null && d.buff1.defBuffPercent != 0) || (d.buff2 != null && d.buff2.defBuffPercent != 0);
                if (atk) outIcons.Add(all[0]);
                if (def) outIcons.Add(all[1]);
                if (atk || def) return;
                break;
            }
            case "16": case "39":
            {
                var d = e.damageBuff; if (d == null) break;
                bool[] f = { d.mitigateFire, d.mitigateWater, d.mitigateEarth, d.mitigateThunder, d.mitigateLight, d.mitigateDark };
                bool any = false;
                for (int i = 0; i < 6; i++) if (f[i]) { outIcons.Add(all[i]); any = true; }
                if (any) return;
                break;
            }
            case "55":
            {
                var d = e.damageBuff; if (d == null) break;
                bool[] f = { d.fireDoesExtraElementalDmg, d.waterDoesExtraElementalDmg, d.earthDoesExtraElementalDmg,
                             d.thunderDoesExtraElementalDmg, d.lightDoesExtraElementalDmg, d.darkDoesExtraElementalDmg };
                bool any = false;
                for (int i = 0; i < 6; i++) if (f[i]) { outIcons.Add(all[i]); any = true; }
                if (any) return;
                break;
            }
            case "24": case "89":
            {
                var c = e.conversion; if (c == null) break;
                if (c.atkConvertPercent > 0) outIcons.Add(all[0]);
                if (c.defConvertPercent > 0) outIcons.Add(all[1]);
                if (c.recConvertPercent > 0) outIcons.Add(all[2]);
                return;
            }
            case "23":
            {
                var d = e.damageBuff; var r = e.dropRate;
                if (d != null && d.sparkDmgBuff > 0)      outIcons.Add(all[0]);
                if (r != null && r.sparkHcDropRate > 0)   outIcons.Add(all[1]);
                if (r != null && r.sparkBcDropRate > 0)   outIcons.Add(all[2]);
                if (r != null && r.sparkItemDropRate > 0) outIcons.Add(all[3]);
                return;
            }
            case "17":
            {
                var s = e.ailmentResist; var st = e.status;
                if (s == null && st == null) break;
                int[] r = s != null
                    ? new[] { s.poisonResist, s.weakenResist, s.sickResist, s.injuryResist, s.curseResist, s.paralysisResist }
                    : new[] { st.poisonResist, st.weakenResist, st.sickResist, st.injuryResist, st.curseResist, st.paralysisResist };
                bool any = false;
                for (int i = 0; i < 6; i++) if (r[i] > 0) { outIcons.Add(all[i]); any = true; }
                if (any) return;
                break;
            }
            case "62":
            {
                var sh = e.shield;
                if (sh != null && sh.barrierElement != ElementalType.None) { outIcons.Add(all[ElemIdx(sh.barrierElement)]); return; }
                break;
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────
//  Per-unit display. Add to the unit's GameObject (UnitBehaviour.Start does this).
//  Polls the unit's buffs/ailments, rebuilds the entry list when they change, and cycles one icon at a time
//  (unit_buffs.gd _change_displayed_buff on the displayed_buff timer).
// ─────────────────────────────────────────────────────────────
public class BuffIconDisplay : MonoBehaviour
{
    public float cycleSeconds = 1f;
    public Vector3 worldOffset = new Vector3(0f, 3.25f, 0f);
    public float iconScale = 1f;

    struct Entry { public bool debuff; public int index; public bool ubb; }

    UnitBehaviour unit;
    readonly List<Entry> entries = new List<Entry>();
    readonly List<int> scratch = new List<int>();
    string signature = "";
    int current = -1;
    float timer;
    bool visible = true, wasDead;

    RectTransform root;
    Image icon, ubbMod;

    public void Init(UnitBehaviour u) { unit = u; }

    void Awake() { if (unit == null) unit = GetComponent<UnitBehaviour>(); }

    void Build()
    {
        var go = new GameObject("BuffIcon", typeof(RectTransform));
        root = go.GetComponent<RectTransform>();
        root.SetParent(BattleUI.popupLayer, false);
        root.localScale = Vector3.one * iconScale;
        root.sizeDelta = new Vector2(BuffIconSheets.IconSize, BuffIconSheets.IconSize);

        icon = new GameObject("icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.rectTransform.SetParent(root, false);
        icon.rectTransform.sizeDelta = root.sizeDelta;
        icon.raycastTarget = false;

        ubbMod = new GameObject("ubb_modifier", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ubbMod.rectTransform.SetParent(root, false);
        ubbMod.rectTransform.sizeDelta = root.sizeDelta;
        ubbMod.raycastTarget = false;
        ubbMod.sprite = BuffIconSheets.Ubb();
        ubbMod.enabled = false;
        root.gameObject.SetActive(false);
    }

    static int AilmentIndex(string a)
    {
        switch (a)
        {
            case "Poison": return (int)DebuffMst.DESC_BADSTATE_POISON;
            case "Weaken": case "Weak": return (int)DebuffMst.DESC_BADSTATE_WEAK;
            case "Sick": case "Sickness": return (int)DebuffMst.DESC_BADSTATE_SICK;
            case "Injury": return (int)DebuffMst.DESC_BADSTATE_INJURY;
            case "Curse": return (int)DebuffMst.DESC_BADSTATE_CURSE;
            case "Paralysis": case "Para": return (int)DebuffMst.DESC_BADSTATE_PARA;
        }
        return -1;
    }

    void Rebuild()
    {
        entries.Clear();
        var seen = new HashSet<long>();
        foreach (var b in unit.buffs.active)
        {
            if (b.effect == null) continue;   // passives show icons too (derived from their params, see PassiveIcons)
            scratch.Clear();
            BuffIconTable.IconsFor(b.effect, scratch);
            foreach (int idx in scratch)
                if (seen.Add(idx)) entries.Add(new Entry { debuff = false, index = idx, ubb = b.source == BuffSource.UBB });
        }
        foreach (var a in unit.activeAilments)
        {
            int idx = AilmentIndex(a);
            if (idx >= 0 && seen.Add(1000 + idx)) entries.Add(new Entry { debuff = true, index = idx, ubb = false });
        }
    }

    string Signature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in unit.buffs.active) sb.Append(b.Id).Append(b.source).Append(',');
        sb.Append('|');
        foreach (var a in unit.activeAilments) sb.Append(a).Append(',');
        return sb.ToString();
    }

    void Show(int i)
    {
        if (entries.Count == 0) { if (root) root.gameObject.SetActive(false); current = -1; return; }
        for (int tries = 0; tries < entries.Count; tries++)
        {
            current = (((i + tries) % entries.Count) + entries.Count) % entries.Count;
            var e = entries[current];
            var sp = e.debuff ? BuffIconSheets.Debuff(e.index) : BuffIconSheets.Buff(e.index);
            if (sp == null)
            {
                Debug.LogWarning($"[BuffIcons] no sprite for {(e.debuff ? "debuff" : "buff")} index {e.index} (sheet missing/not in Resources, or cell outside the texture)");
                continue;   // "no icon defined" → try the next entry
            }
            icon.sprite = sp;
            ubbMod.enabled = e.ubb && ubbMod.sprite != null;
            root.gameObject.SetActive(visible);
            return;
        }
        root.gameObject.SetActive(false);
    }

    public void SetVisible(bool v)   // hide_all_buffs / show_all_buffs
    {
        visible = v;
        if (root) root.gameObject.SetActive(v && entries.Count > 0);
    }

    void LateUpdate()
    {
        if (unit == null) return;
        if (unit.currentState == UnitState.Dead)
        {
            if (root) root.gameObject.SetActive(false);
            wasDead = true;
            return;
        }
        if (root == null) { Build(); signature = null; }   // root can be destroyed with popupLayer's children: rebuild AND re-show
        if (wasDead) { wasDead = false; signature = null; }   // revived: force a rebuild

        string sig = Signature();
        if (sig != signature)
        {
            signature = sig;
            Rebuild();
            Show(current >= 0 && current < entries.Count ? current : 0);
        }

        timer += Time.deltaTime;
        if (timer >= cycleSeconds)
        {
            timer = 0f;
            if (entries.Count > 1) Show(current + 1);
        }

        root.position = transform.position + worldOffset;
    }

    void OnDestroy() { if (root) Destroy(root.gameObject); }
}