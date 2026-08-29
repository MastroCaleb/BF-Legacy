// EffectParser.cs
//
// Runtime-safe port of the Effect-parsing logic from JsonToSOUnit.cs.
// No UNITY_EDITOR guard — this can run in builds.
//
// JsonToSOUnit.cs bakes Abilities into ScriptableObjects at editor import
// time, where "effects" is always an array of objects and conditions are
// always nested under a "conditions" array. Item data (items.json) reuses
// the same key vocabulary but with two shape differences handled here:
//
//   1. Sphere / ls_sphere: "effect" (singular) is a flat array, same as
//      Ability, but any condition ("hp above % buff requirement", "gender
//      required", etc.) is flattened directly onto the effect object
//      instead of living under a nested "conditions" array.
//
//   2. Consumable / summoner_consumable: "effect" is an object wrapping an
//      inner "effect" array plus item-level "target_type"/"target_area"
//      (underscored, and shared by every entry in the array rather than
//      repeated per-entry).
//
//   3. A handful of item effects use "unknown proc id"/"unknown passive id"
//      + "unknown proc param"/"unknown passive params" instead of "proc id"/
//      "passive id" — these are reverse-engineering gaps in the source data,
//      not a different format. Handled with a plain fallback.
//
// This file intentionally duplicates the small ParseInt/ParseFloat/ParseBool/
// ParseElement/ParseTargetType/ParseTargetArea helpers and all of the
// sub-effect parsers from JsonToSOUnit.cs rather than reaching into that
// class, since JsonToSOUnit.cs is compiled out of player builds (UNITY_EDITOR
// only). If the duplication ever becomes annoying to keep in sync, both call
// sites can be pointed at this file instead — not done here to keep this a
// self-contained, additive change.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class EffectParser
{
    // ─────────────────────────────────────────────────────────────
    //  PUBLIC ENTRY POINTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses an ability-style effects array: each entry carries its own
    /// "target type"/"target area" and conditions live under a nested
    /// "conditions" array.
    /// </summary>
    public static List<Effect> ParseAbilityStyleEffects(JArray arr)
    {
        List<Effect> list = new();
        if (arr == null) return list;
        foreach (JToken t in arr)
            if (t is JObject obj) list.Add(ParseEffect(obj, null, null));
        return list;
    }

    /// <summary>
    /// Parses an item-style effects array (sphere/ls_sphere "effect", or the
    /// inner "effect" array of a wrapped consumable/summoner_consumable
    /// "effect" object). Conditions are read directly off each effect object
    /// rather than a nested array. targetType/targetArea are optional
    /// wrapper-level overrides (consumables carry these once, outside the
    /// array; spheres pass null since passives don't target).
    /// </summary>
    public static List<Effect> ParseItemStyleEffects(JArray arr, string targetType = null, string targetArea = null)
    {
        List<Effect> list = new();
        if (arr == null) return list;
        foreach (JToken t in arr)
            if (t is JObject obj) list.Add(ParseEffect(obj, targetType, targetArea));
        return list;
    }

    // ─────────────────────────────────────────────────────────────
    //  CORE EFFECT PARSE
    // ─────────────────────────────────────────────────────────────

    private static Effect ParseEffect(JObject obj, string targetTypeOverride, string targetAreaOverride)
    {
        string procId    = obj["proc id"]?.ToString()    ?? obj["unknown proc id"]?.ToString()    ?? "";
        string passiveId = obj["passive id"]?.ToString() ?? obj["unknown passive id"]?.ToString() ?? "";
        bool   isPassive = !string.IsNullOrEmpty(passiveId);

        Effect e = new Effect
        {
            procId     = procId,
            passiveId  = passiveId,
            isPassive  = isPassive,
            procParam  = obj["unknown proc param"]?.ToString() ?? obj["unknown passive params"]?.ToString(),

            targetType = obj["target type"] != null
                            ? ParseTargetType(obj["target type"].ToString())
                            : ParseTargetType(targetTypeOverride),
            targetArea = obj["target area"] != null
                            ? ParseTargetArea(obj["target area"].ToString())
                            : ParseTargetArea(targetAreaOverride),

            effectDelayFrame = obj["effect delay time(ms)/frame"]?.ToString(),

            // Ability shape: nested "conditions" array.
            // Item shape: condition fields flattened onto this same object.
            conditions = obj["conditions"] is JArray condArr
                            ? ParseConditions(condArr)
                            : ParseInlineConditions(obj),

            attack          = ParseAttack(obj),
            statBuff        = ParseStatBuff(obj),
            bbAtkBuff       = ParseBBAtkBuff(obj),
            heal            = ParseHeal(obj),
            gradualHeal     = ParseGradualHeal(obj),
            statDebuff      = ParseStatDebuff(obj),
            status          = ParseStatus(obj),
            damageBuff      = ParseDamageBuff(obj),
            bcFill          = ParseBCFill(obj),
            dot             = ParseDOT(obj),
            shield          = ParseShield(obj),
            revive          = ParseRevive(obj),
            conditionalBuff = ParseConditionalBuff(obj, isPassive ? passiveId : procId),
            ailmentInflict  = ParseAilmentInflict(obj),
            ailmentResist   = ParseAilmentResist(obj),
            extraAction     = ParseExtraAction(obj)
        };

        return e;
    }

    // ─────────────────────────────────────────────────────────────
    //  CONDITIONS
    // ─────────────────────────────────────────────────────────────

    // Ability shape — array of standalone condition objects.
    private static List<EffectCondition> ParseConditions(JArray arr)
    {
        List<EffectCondition> list = new();
        if (arr == null) return list;
        foreach (JToken t in arr)
            if (t is JObject o && ParseSingleCondition(o) is { } c)
                list.Add(c);
        return list;
    }

    // Item shape — the condition (at most one, in practice) lives directly
    // on the effect object alongside its numeric fields.
    private static List<EffectCondition> ParseInlineConditions(JObject o)
    {
        List<EffectCondition> list = new();
        if (ParseSingleCondition(o) is { } c) list.Add(c);
        return list;
    }

    // Shared field-sniffing logic — works whether the fields live on their
    // own object (ability "conditions" entries) or on the effect object
    // itself (item data).
    private static EffectCondition ParseSingleCondition(JObject o)
    {
        if (o["hp above % buff requirement"] != null)
            return new EffectCondition
            {
                conditionType      = ConditionType.HPAbove,
                hpThresholdPercent = ParseFloat(o["hp above % buff requirement"])
            };

        if (o["hp below % buff requirement"] != null || o["hp below % passive requirement"] != null)
            return new EffectCondition
            {
                conditionType      = ConditionType.HPBelow,
                hpThresholdPercent = ParseFloat(o["hp below % buff requirement"] ?? o["hp below % passive requirement"])
            };

        if (o["item required"] is JArray items)
            return new EffectCondition
            {
                conditionType = ConditionType.ItemRequired,
                itemsRequired = items.Select(i => i.ToString()).ToList()
            };

        if (o["elements required"] is JArray elems)
            return new EffectCondition
            {
                conditionType    = ConditionType.ElementRequired,
                elementsRequired = elems.Select(el => el.ToString()).ToList()
            };

        // "unique elements required" is a variant seen on item data — same
        // meaning (a set of elements the party must cover), so it's folded
        // into the same condition type rather than adding a new one.
        if (o["unique elements required"] is JArray uniqueElems)
            return new EffectCondition
            {
                conditionType    = ConditionType.ElementRequired,
                elementsRequired = uniqueElems.Select(el => el.ToString()).ToList()
            };

        if (o["gender required"] != null)
            return new EffectCondition
            {
                conditionType   = ConditionType.GenderRequired,
                genderRequired  = o["gender required"].ToString()
            };

        if (o["bb gauge above % buff requirement"] != null)
            return new EffectCondition
            {
                conditionType    = ConditionType.BBGaugeAbove,
                bbGaugeThreshold = ParseInt(o["bb gauge above % buff requirement"])
            };

        if (o["bb gauge below % buff requirement"] != null)
            return new EffectCondition
            {
                conditionType    = ConditionType.BBGaugeBelow,
                bbGaugeThreshold = ParseInt(o["bb gauge below % buff requirement"])
            };

        return null;
    }

    // ─────────────────────────────────────────────────────────────
    //  SUB-EFFECT PARSERS
    //  (verbatim port from JsonToSOUnit.cs — keep in sync if the source
    //   key vocabulary changes there)
    // ─────────────────────────────────────────────────────────────

    private static AttackEffect ParseAttack(JObject o) => new()
    {
        bbAtkPercent     = ParseInt(o["bb atk%"]),
        hitCount         = ParseInt(o["hits"]),
        bbFlatAtk        = ParseInt(o["bb flat atk"]),
        bbCritPercent    = ParseInt(o["bb crit%"]),
        bbBCPercent      = ParseInt(o["bb bc%"]),
        bbHCPercent      = ParseInt(o["bb hc%"]),
        randomTarget     = ParseBool(o["random attack"]),
        fixedDamage      = ParseInt(o["fixed damage"]),
        hpDamageChance   = ParseInt(o["hp% damage chance%"]),
        hpDamageHigh     = ParseInt(o["hp% damage high"]),
        hpDamageLow      = ParseInt(o["hp% damage low"]),
        hpDrainHigh      = ParseInt(o["hp drain% high"]),
        hpDrainLow       = ParseInt(o["hp drain% low"]),
        ignoresDef       = o["ignore def%"] != null,
        ignoreDefPercent = ParseInt(o["ignore def%"]),
        bbElements       = (o["bb elements"] as JArray)
                               ?.Select(t => ParseElement(t.ToString()))
                               .ToList() ?? new List<ElementalType>()
    };

    private static StatBuffEffect ParseStatBuff(JObject o) => new()
    {
        atkBuff        = ParseInt(o["atk% buff (1)"]) + ParseInt(o["atk% buff"]),
        defBuff        = ParseInt(o["def% buff (3)"]) + ParseInt(o["def% buff"]),
        recBuff        = ParseInt(o["rec% buff (5)"]) + ParseInt(o["rec% buff"]),
        hpBuff         = ParseInt(o["hp% buff"]),
        critBuff       = ParseInt(o["crit% buff (7)"]) + ParseInt(o["crit% buff"]),
        buffTurns      = ParseInt(o["buff turns"]),
        elementBuffed  = o["element buffed"]?.ToString(),
        elementsBuffed = (o["elements buffed"] as JArray)
                            ?.Select(t => t.ToString()).ToList() ?? new List<string>(),
        fireResist     = ParseInt(o["fire resist%"]),
        waterResist    = ParseInt(o["water resist%"]),
        earthResist    = ParseInt(o["earth resist%"]),
        thunderResist  = ParseInt(o["thunder resist%"]),
        lightResist    = ParseInt(o["light resist%"]),
        darkResist     = ParseInt(o["dark resist%"])
    };

    private static BBAtkBuffEffect ParseBBAtkBuff(JObject o) => new()
    {
        bbAtkBuff      = ParseInt(o["bb atk% buff"]),
        sbbAtkBuff     = ParseInt(o["sbb atk% buff"]),
        ubbAtkBuff     = ParseInt(o["ubb atk% buff"]),
        buffTurns      = ParseInt(o["buff turns (72)"]),
        bbBaseAtk      = ParseInt(o["bb base atk%"]),
        bbAtkIncPerUse = ParseInt(o["bb atk% inc per use"]),
        bbAtkMaxInc    = ParseInt(o["bb atk% max number of inc"]),
        bbBCPercent    = ParseInt(o["bb bc%"]),
        bbCritPercent  = ParseInt(o["bb crit%"]),
        bbFlatAtk      = ParseInt(o["bb flat atk"])
    };

    private static HealEffect ParseHeal(JObject o) => new()
    {
        healHigh        = ParseInt(o["heal high"]),
        healLow         = ParseInt(o["heal low"]),
        recAddedPercent = ParseInt(o["rec added% (from healer)"])
                        + ParseInt(o["angel idol recover hp%"])
    };

    private static GradualHealEffect ParseGradualHeal(JObject o) => new()
    {
        healHigh        = ParseInt(o["gradual heal high"]),
        healLow         = ParseInt(o["gradual heal low"]),
        turns           = ParseInt(o["gradual heal turns (8)"]),
        recAddedPercent = ParseInt(o["rec added% (from target)"])
    };

    private static StatDebuffEffect ParseStatDebuff(JObject o)
    {
        StatDebuffEffect d = new()
        {
            buffTurns              = ParseInt(o["buff turns"]),
            elementBuffed          = o["element buffed"]?.ToString(),
            inflictAtkDebuff       = ParseInt(o["inflict atk% debuff (2)"]),
            inflictAtkDebuffChance = ParseInt(o["inflict atk% debuff chance% (74)"]),
            inflictDefDebuff       = ParseInt(o["inflict def% debuff (4)"]),
            inflictDefDebuffChance = ParseInt(o["inflict def% debuff chance% (75)"]),
            inflictRecDebuff       = ParseInt(o["inflict rec% debuff (6)"]),
            inflictRecDebuffChance = ParseInt(o["inflict rec% debuff chance% (76)"]),
            statDebuffTurns        = ParseInt(o["stat% debuff turns"])
        };

        if (o["buff #1"] is JObject b1)
            d.buff1 = new StatDebuffEntry
            {
                atkBuffPercent = ParseInt(b1["atk% buff (2)"]),
                defBuffPercent = ParseInt(b1["def% buff (4)"]),
                procChance     = ParseFloat(b1["proc chance%"])
            };

        if (o["buff #2"] is JObject b2)
            d.buff2 = new StatDebuffEntry
            {
                atkBuffPercent = ParseInt(b2["atk% buff (2)"]),
                defBuffPercent = ParseInt(b2["def% buff (4)"]),
                procChance     = ParseFloat(b2["proc chance%"])
            };

        return d;
    }

    private static StatusEffect ParseStatus(JObject o) => new()
    {
        poisonChance    = ParseInt(o["poison%"])    + ParseInt(o["poison% buff"]),
        weakenChance    = ParseInt(o["weaken%"])    + ParseInt(o["weaken% buff"]),
        sickChance      = ParseInt(o["sick%"])      + ParseInt(o["sick% buff"]),
        injuryChance    = ParseInt(o["injury%"])    + ParseInt(o["injury% buff"]),
        curseChance     = ParseInt(o["curse%"])     + ParseInt(o["curse% buff"]),
        paralysisChance = ParseInt(o["paralysis%"]) + ParseInt(o["paralysis% buff"]),
        buffTurns       = ParseInt(o["buff turns"]),
        removeAll       = ParseBool(o["remove all status ailments"]),
        ailmentsCured   = (o["ailments cured"] as JArray)
                              ?.Select(t => t.ToString()).ToList() ?? new List<string>(),
        poisonResist    = ParseInt(o["resist poison% (30)"]),
        weakenResist    = ParseInt(o["resist weaken% (31)"]),
        sickResist      = ParseInt(o["resist sick% (32)"]),
        injuryResist    = ParseInt(o["resist injury% (33)"]),
        curseResist     = ParseInt(o["resist curse% (34)"]),
        paralysisResist = ParseInt(o["resist paralysis% (35)"]),
        resistTurns     = ParseInt(o["resist status ails turns"])
    };

    private static DamageBuffEffect ParseDamageBuff(JObject o)
    {
        DamageBuffEffect d = new()
        {
            sparkDmgBuff                 = ParseInt(o["spark dmg% buff (40)"])
                                         + ParseInt(o["spark dmg inc% buff"]),
            sparkDmgBuffTurns            = ParseInt(o["buff turns"])
                                         + ParseInt(o["spark dmg inc buff turns (131)"]),
            sparkDmgIncChance            = ParseInt(o["spark dmg inc chance%"]),
            critMultiplier               = ParseInt(o["crit multiplier%"]),
            critBuffTurns                = ParseInt(o["buff turns (84)"]),
            elementalWeaknessMultiplier  = ParseFloat(o["elemental weakness multiplier%"]),
            elementalWeaknessTurns       = ParseInt(o["elemental weakness buff turns"]),
            fireDoesExtraElementalDmg    = ParseBool(o["fire units do extra elemental weakness dmg"]),
            waterDoesExtraElementalDmg   = ParseBool(o["water units do extra elemental weakness dmg"]),
            earthDoesExtraElementalDmg   = ParseBool(o["earth units do extra elemental weakness dmg"]),
            thunderDoesExtraElementalDmg = ParseBool(o["thunder units do extra elemental weakness dmg"]),
            lightDoesExtraElementalDmg   = ParseBool(o["light units do extra elemental weakness dmg"]),
            darkDoesExtraElementalDmg    = ParseBool(o["dark units do extra elemental weakness dmg"]),
            dmgMitigationPercent         = ParseInt(o["dmg% mitigation"])
                                         + ParseInt(o["dmg% reduction"]),
            dmgMitigationTurns           = ParseInt(o["dmg% reduction turns (36)"]),
            mitigateFire                 = o["mitigate fire attacks (21)"] != null
                                        || o["mitigate fire attacks"] != null,
            mitigateWater                = o["mitigate water attacks (22)"] != null
                                        || o["mitigate water attacks"] != null,
            mitigateEarth                = o["mitigate earth attacks"] != null,
            mitigateThunder              = o["mitigate thunder attacks"] != null,
            mitigateLight                = o["mitigate light attacks (25)"] != null
                                        || o["mitigate light attacks"] != null,
            mitigateDark                 = o["mitigate dark attacks (26)"] != null
                                        || o["mitigate dark attacks"] != null,
            defenseIgnorePercent         = ParseInt(o["defense% ignore"]),
            defenseIgnoreTurns           = ParseInt(o["defense% ignore turns (39)"])
        };

        if (o["buff"] is JObject sub)
        {
            d.hasSubBuff        = true;
            d.hpBelowActivation = o["hp below % buff activation"] != null;
            d.hpBelowThreshold  = ParseInt(o["hp below % buff activation"]);

            if (sub["angel idol buff (12)"] != null)
                d.angelIdolSubBuff = new AngelIdolSubBuff
                {
                    buffTurns        = ParseInt(sub["buff turns (12)"]),
                    recoverHpPercent = ParseFloat(sub["angel idol recover hp%"])
                };

            if (sub["dmg reduction% buff"] != null)
                d.dmgReductionSubBuff = new DmgReductionSubBuff
                {
                    buffTurns           = ParseInt(sub["buff turns (36)"]),
                    dmgReductionPercent = ParseFloat(sub["dmg reduction% buff"])
                };

            if (sub["gradual heal high"] != null)
                d.gradualHealSubBuff = new GradualHealSubBuff
                {
                    buffTurns = ParseInt(sub["buff turns (8)"]),
                    healHigh  = ParseInt(sub["gradual heal high"]),
                    healLow   = ParseInt(sub["gradual heal low"])
                };
        }

        return d;
    }

    private static BCFillEffect ParseBCFill(JObject o) => new()
    {
        bbBCFill                   = ParseInt(o["bb bc fill"]),
        bbBCFillPercent            = ParseFloat(o["bb bc fill%"]),
        bcFillPerTurn              = ParseInt(o["bc fill per turn"]),
        bcFillOnSparkHigh          = ParseInt(o["bc fill on spark high"]),
        bcFillOnSparkLow           = ParseInt(o["bc fill on spark low"]),
        bcFillOnSparkPercent       = ParseFloat(o["bc fill on spark%"]),
        bcFillWhenAttackedHigh     = ParseInt(o["bc fill when attacked high"]),
        bcFillWhenAttackedLow      = ParseInt(o["bc fill when attacked low"]),
        bcFillWhenAttackedPercent  = ParseFloat(o["bc fill when attacked%"]),
        bcFillWhenAttackedTurns    = ParseInt(o["bc fill when attacked turns (38)"]),
        bcFillWhenAttackingHigh    = ParseInt(o["bc fill when attacking high"]),
        bcFillWhenAttackingLow     = ParseInt(o["bc fill when attacking low"]),
        bcFillWhenAttackingPercent = ParseFloat(o["bc fill when attacking%"]),
        bcFillOnEnemyDefeatHigh    = ParseInt(o["bc fill on enemy defeat high"]),
        bcFillOnEnemyDefeatLow     = ParseInt(o["bc fill on enemy defeat low"]),
        bcFillOnEnemyDefeatPercent = ParseFloat(o["bc fill on enemy defeat%"]),
        gradualBCFillTurns         = ParseInt(o["increase bb gauge gradual turns (37)"]),
        bbGaugeFillRate            = ParseFloat(o["bb gauge fill rate%"])
    };

    private static DotEffect ParseDOT(JObject o) => new()
    {
        atkPercent = ParseInt(o["dot atk%"]),
        flatAtk    = ParseInt(o["dot flat atk"]),
        element    = ParseElement(o["dot element affected"]?.ToString()),
        turns      = ParseInt(o["dot turns (71)"]),
        unitIndex  = ParseInt(o["dot unit index"])
    };

    private static ShieldEffect ParseShield(JObject o) => new()
    {
        dmgMitigationPercent             = ParseInt(o["dmg% mitigation"]),
        maxHPIncreasePercent             = ParseInt(o["max hp% increase"]),
        barrierHP                        = ParseInt(o["elemental barrier hp"]),
        barrierDef                       = ParseInt(o["elemental barrier def"]),
        barrierElement                   = ParseElement(o["elemental barrier element"]?.ToString()),
        barrierAbsorbPercent             = ParseFloat(o["elemental barrier absorb dmg%"]),
        dmgMitigationForElementalAttacks = ParseInt(o["dmg% mitigation for elemental attacks"])
    };

    private static ReviveEffect ParseRevive(JObject o) => new()
    {
        reviveChance    = ParseFloat(o["revive unit chance%"]),
        reviveHPPercent = ParseFloat(o["revive unit hp%"]),
        triggerOnBB     = ParseBool(o["trigger on bb"]),
        triggerOnSBB    = ParseBool(o["trigger on sbb"]),
        triggerOnUBB    = ParseBool(o["trigger on ubb"])
    };

    private static ConditionalBuffEffect ParseConditionalBuff(JObject o, string id)
    {
        string triggerType = id switch
        {
            "78" => "damage_received",
            "80" => "damage_dealt",
            "82" => "bc_count",
            "84" => "hc_count",
            "86" => "spark_count",
            "88" => "on_guard",
            "89" => "on_crit",
            _    => "unknown"
        };

        ConditionalSubBuff sub = new();
        if (o["buff"] is JObject buff)
        {
            sub.atkBuff              = ParseInt(buff["atk% buff (1)"]);
            sub.defBuff              = ParseInt(buff["def% buff (3)"]);
            sub.bbAtkBuff            = ParseInt(buff["bb atk% buff"]);
            sub.sbbAtkBuff           = ParseInt(buff["sbb atk% buff"]);
            sub.ubbAtkBuff           = ParseInt(buff["ubb atk% buff"]);
            sub.sparkDmgBuff         = ParseInt(buff["spark dmg% buff"]);
            sub.dmgReductionBuff     = ParseInt(buff["dmg reduction% buff"]);
            sub.gradualHealHigh      = ParseInt(buff["gradual heal high"]);
            sub.gradualHealLow       = ParseInt(buff["gradual heal low"]);
            sub.odFillRate           = ParseInt(buff["od fill rate% buff"]);
            sub.hpDrainChance        = ParseInt(buff["hp drain chance%"]);
            sub.hpDrainHigh          = ParseInt(buff["hp drain% high"]);
            sub.hpDrainLow           = ParseInt(buff["hp drain% low"]);
            sub.sparkDmgInc          = ParseInt(buff["spark dmg inc%"]);
            sub.bcFillOnSparkHigh    = ParseInt(buff["bc fill on spark high"]);
            sub.bcFillOnSparkLow     = ParseInt(buff["bc fill on spark low"]);
            sub.bcFillOnSparkPercent = ParseFloat(buff["bc fill on spark%"]);
            sub.gradualBcFill        = ParseInt(buff["increase bb gauge gradual buff"]);
            sub.elementBuffed        = buff["element buffed"]?.ToString();
            sub.buffTurns            = ParseInt(buff["buff turns (1)"])
                                     + ParseInt(buff["buff turns (3)"])
                                     + ParseInt(buff["buff turns (36)"])
                                     + ParseInt(buff["buff turns (40)"])
                                     + ParseInt(buff["buff turns (72)"]);
        }

        return new ConditionalBuffEffect
        {
            triggerType      = triggerType,
            activationChance = ParseFloat(o["on guard activation chance%"])
                             + ParseFloat(o["on crit activation chance%"]),
            buff             = sub
        };
    }

    private static AilmentInflictEffect ParseAilmentInflict(JObject o) => new()
    {
        poisonChance    = ParseInt(o["inflict poison%"]),
        weakenChance    = ParseInt(o["inflict weaken%"]),
        sickChance      = ParseInt(o["inflict sick%"]),
        injuryChance    = ParseInt(o["inflict injury%"]),
        curseChance     = ParseInt(o["inflict curse%"]),
        paralysisChance = ParseInt(o["inflict paralysis%"])
    };

    private static AilmentResistEffect ParseAilmentResist(JObject o) => new()
    {
        poisonResist      = ParseInt(o["poison resist%"]),
        weakenResist      = ParseInt(o["weaken resist%"]),
        sickResist        = ParseInt(o["sick resist%"]),
        injuryResist      = ParseInt(o["injury resist%"]),
        curseResist       = ParseInt(o["curse resist%"]),
        paralysisResist   = ParseInt(o["paralysis resist%"]),
        atkDownResist     = ParseInt(o["atk down resist% (120)"])
                          + ParseInt(o["atk down resist%"]),
        defDownResist     = ParseInt(o["def down resist% (121)"])
                          + ParseInt(o["def down resist%"]),
        recDownResist     = ParseInt(o["rec down resist% (122)"])
                          + ParseInt(o["rec down resist%"]),
        immunityBuffTurns = ParseInt(o["stat down immunity buff turns"])
    };

    private static ExtraActionEffect ParseExtraAction(JObject o) => new()
    {
        chance          = ParseFloat(o["chance% for extra action"]),
        maxExtraActions = ParseInt(o["max number of extra actions"]),
        buffTurns       = ParseInt(o["extra action buff turns (123)"])
    };

    // ─────────────────────────────────────────────────────────────
    //  PRIMITIVE HELPERS
    // ─────────────────────────────────────────────────────────────

    // internal (not private): ItemDatabase reuses these for the plain
    // scalar item fields (rarity, sellPrice, etc.) instead of duplicating them.
    internal static int ParseInt(JToken t)
    {
        if (t == null) return 0;
        string s = t.ToString().Trim();
        if (int.TryParse(s, out int i))     return i;
        if (float.TryParse(s, out float f)) return Mathf.RoundToInt(f);
        return 0;
    }

    internal static float ParseFloat(JToken t)
    {
        if (t == null) return 0f;
        float.TryParse(t.ToString().Trim(), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float f);
        return f;
    }

    internal static bool ParseBool(JToken t)
    {
        if (t == null) return false;
        if (t.Type == JTokenType.Boolean) return t.Value<bool>();
        return t.ToString().Trim().ToLowerInvariant() == "true";
    }

    private static ElementalType ParseElement(string s) => s?.ToLower() switch
    {
        "fire"    => ElementalType.Fire,
        "water"   => ElementalType.Water,
        "earth"   => ElementalType.Earth,
        "thunder" => ElementalType.Thunder,
        "light"   => ElementalType.Light,
        "dark"    => ElementalType.Dark,
        _         => ElementalType.None
    };

    private static TargetType ParseTargetType(string s) => s switch
    {
        "enemy" => TargetType.AllEnemies,
        "party" => TargetType.AllAllies,
        "self"  => TargetType.Self,
        _       => TargetType.SingleEnemy
    };

    private static TargetArea ParseTargetArea(string s) => s switch
    {
        "aoe"    => TargetArea.AOE,
        "single" => TargetArea.Single,
        "random" => TargetArea.Random,
        _        => TargetArea.AOE
    };
}
