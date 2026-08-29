// Ability.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Ability", menuName = "Combat/Ability")]
public class Ability : ScriptableObject
{
    [Header("Info")]
    public string abilityId;
    public string abilityName;
    public string abilityDesc;

    [Header("Targeting")]
    public TargetType targetType;
    public TargetArea targetArea;

    [Header("Animation")]
    public List<HitFrame> hitFrames;
    public List<EffectFrame> effectFrames;
    public int dropChecksPerHit;

    [Header("Levels")]
    public List<AbilityLevel> levels;

    public AbilityLevel MaxLevel => (levels != null && levels.Count > 0)
        ? levels[levels.Count - 1] : null;
}

// ─────────────────────────────────────────
//  ABILITY LEVEL
// ─────────────────────────────────────────

[System.Serializable]
public class AbilityLevel
{
    public int bcCost;
    public int damageRate;
    public List<Effect> effects;
}

// ─────────────────────────────────────────
//  HIT / EFFECT FRAMES
// ─────────────────────────────────────────

[System.Serializable]
public class HitFrame
{
    public int frame;
    public int damagePercent;
}

[System.Serializable]
public class EffectFrame
{
    public int frame;               // absolute frame within the ability (skillFrame + group sub-frame)
    public string battleEffectGroupId; // kept for debugging/re-export

    public string particleEffectId; // raw particle id from the group data (e.g. "600540")
    public ParticleEffect particleEffect;  // resolved SO — may be null if not found in the index

    // Best-effort guesses — unverified against real footage, tune ComputeEffectPosition to match.
    // placementType: 1 = per-target, 2 = center/screen, 3 = ground/formation ("陣地")
    public int   placementType;
    // xAnchor/yAnchor: 1/2/3 = left-center-right / top-center-bottom nudge around the base point.
    // 5/5 together = "xy固定" (fixed xy) — ignore anchor, offsetX/offsetY IS the absolute position.
    public int   xAnchor;
    public int   yAnchor;
    public float offsetX;
    public float offsetY;

    public AudioClip audioClip; // loaded by importer, if a sound shares this frame
}

// ─────────────────────────────────────────
//  EFFECT  — procId is the type identifier
//  All sub-structs are always populated;
//  use procId + isPassive at runtime to
//  decide which fields are meaningful.
// ─────────────────────────────────────────

[System.Serializable]
public class Effect
{
    // Identity — use these at runtime instead of an EffectType enum
    public string procId;      // Used for active ability effects (BB/SBB/UBB)
    public string passiveId;   // Used for passive effects (leader/extra abilities)
    public bool   isPassive;   // True if this is a passive effect
    public string procParam;   // Raw param for "unknown proc/passive id" entries the parser can't type — item data only

    // Shared targeting / timing
    public TargetType targetType;
    public TargetArea targetArea;
    public string     effectDelayFrame;

    // Conditions that gate this effect
    public List<EffectCondition> conditions;

    // ── Sub-effect data (all parsed; only relevant ones are non-default) ──
    public AttackEffect          attack;
    public StatBuffEffect        statBuff;
    public BBAtkBuffEffect       bbAtkBuff;
    public HealEffect            heal;
    public GradualHealEffect     gradualHeal;
    public StatDebuffEffect      statDebuff;
    public StatusEffect          status;
    public DamageBuffEffect      damageBuff;
    public BCFillEffect          bcFill;
    public DotEffect             dot;
    public ShieldEffect          shield;
    public ReviveEffect          revive;
    public ConditionalBuffEffect conditionalBuff;
    public AilmentInflictEffect  ailmentInflict;
    public AilmentResistEffect   ailmentResist;
    public ExtraActionEffect     extraAction;
}

// ─────────────────────────────────────────
//  SUB-EFFECT STRUCTS
// ─────────────────────────────────────────

[System.Serializable]
public class AttackEffect
{
    public int   bbAtkPercent;
    public int  hitCount;
    public int   bbFlatAtk;
    public int   bbCritPercent;
    public int   bbBCPercent;
    public int   bbHCPercent;
    public bool  randomTarget;
    public int   fixedDamage;
    public int   hpDamageChance;
    public int   hpDamageHigh;
    public int   hpDamageLow;
    public int   hpDrainHigh;
    public int   hpDrainLow;
    public bool  ignoresDef;
    public int   ignoreDefPercent;
    public List<ElementalType> bbElements;
}

[System.Serializable]
public class StatBuffEffect
{
    public int    atkBuff;
    public int    defBuff;
    public int    recBuff;
    public int    hpBuff;
    public int    critBuff;
    public int    buffTurns;
    public string elementBuffed;
    public List<string> elementsBuffed;
    public int    fireResist;
    public int    waterResist;
    public int    earthResist;
    public int    thunderResist;
    public int    lightResist;
    public int    darkResist;
}

[System.Serializable]
public class BBAtkBuffEffect
{
    public int bbAtkBuff;
    public int sbbAtkBuff;
    public int ubbAtkBuff;
    public int buffTurns;
    // proc 64 extras
    public int bbBaseAtk;
    public int bbAtkIncPerUse;
    public int bbAtkMaxInc;
    public int bbBCPercent;
    public int bbCritPercent;
    public int bbFlatAtk;
}

[System.Serializable]
public class HealEffect
{
    public int healHigh;
    public int healLow;
    public int recAddedPercent;
}

[System.Serializable]
public class GradualHealEffect
{
    public int healHigh;
    public int healLow;
    public int turns;
    public int recAddedPercent;
}

[System.Serializable]
public class StatDebuffEffect
{
    // proc 9 — probabilistic ATK/DEF down
    public StatDebuffEntry buff1;
    public StatDebuffEntry buff2;
    public int    buffTurns;
    public string elementBuffed;
    // proc 51 — inflict stat % debuffs
    public int inflictAtkDebuff;
    public int inflictAtkDebuffChance;
    public int inflictDefDebuff;
    public int inflictDefDebuffChance;
    public int inflictRecDebuff;
    public int inflictRecDebuffChance;
    public int statDebuffTurns;
}

[System.Serializable]
public class StatDebuffEntry
{
    public int   atkBuffPercent;
    public int   defBuffPercent;
    public float procChance;
}

[System.Serializable]
public class StatusEffect
{
    // Inflict chances (proc 11 / 40)
    public int poisonChance;
    public int weakenChance;
    public int sickChance;
    public int injuryChance;
    public int curseChance;
    public int paralysisChance;
    public int buffTurns;
    // Cure (proc 38)
    public bool         removeAll;
    public List<string> ailmentsCured;
    // Resist (proc 17)
    public int poisonResist;
    public int weakenResist;
    public int sickResist;
    public int injuryResist;
    public int curseResist;
    public int paralysisResist;
    public int resistTurns;
}

[System.Serializable]
public class DamageBuffEffect
{
    // Spark (proc 23 / 83)
    public int   sparkDmgBuff;
    public int   sparkDmgBuffTurns;
    public int   sparkDmgIncChance;
    // Crit (proc 54)
    public int   critMultiplier;
    public int   critBuffTurns;
    // Elemental weakness (proc 55 / passive 50)
    public float elementalWeaknessMultiplier;
    public int   elementalWeaknessTurns;
    public bool  fireDoesExtraElementalDmg;
    public bool  waterDoesExtraElementalDmg;
    public bool  earthDoesExtraElementalDmg;
    public bool  thunderDoesExtraElementalDmg;
    public bool  lightDoesExtraElementalDmg;
    public bool  darkDoesExtraElementalDmg;
    // Sub-buff carried by proc 55
    public bool               hasSubBuff;
    public bool               hpBelowActivation;
    public int                hpBelowThreshold;
    public AngelIdolSubBuff   angelIdolSubBuff;
    public DmgReductionSubBuff dmgReductionSubBuff;
    public GradualHealSubBuff gradualHealSubBuff;
    // Mitigation (proc 16 / 18 / 39)
    public int  dmgMitigationPercent;
    public int  dmgMitigationTurns;
    public bool mitigateFire;
    public bool mitigateWater;
    public bool mitigateEarth;
    public bool mitigateThunder;
    public bool mitigateLight;
    public bool mitigateDark;
    // Defense ignore (proc 22)
    public int defenseIgnorePercent;
    public int defenseIgnoreTurns;
}

[System.Serializable] public class AngelIdolSubBuff   { public int buffTurns; public float recoverHpPercent; }
[System.Serializable] public class DmgReductionSubBuff { public int buffTurns; public float dmgReductionPercent; }
[System.Serializable] public class GradualHealSubBuff  { public int buffTurns; public int healHigh; public int healLow; }

[System.Serializable]
public class BCFillEffect
{
    public int   bbBCFill;
    public float bbBCFillPercent;
    public int   bcFillPerTurn;
    public int   bcFillOnSparkHigh;
    public int   bcFillOnSparkLow;
    public float bcFillOnSparkPercent;
    public int   bcFillWhenAttackedHigh;
    public int   bcFillWhenAttackedLow;
    public float bcFillWhenAttackedPercent;
    public int   bcFillWhenAttackedTurns;
    public int   bcFillWhenAttackingHigh;
    public int   bcFillWhenAttackingLow;
    public float bcFillWhenAttackingPercent;
    public int   bcFillOnEnemyDefeatHigh;
    public int   bcFillOnEnemyDefeatLow;
    public float bcFillOnEnemyDefeatPercent;
    public int   gradualBCFillTurns;
    public float bbGaugeFillRate;
}

[System.Serializable]
public class DotEffect
{
    public int          atkPercent;
    public int          flatAtk;
    public ElementalType element;
    public int          turns;
    public int          unitIndex;
}

[System.Serializable]
public class ShieldEffect
{
    // proc 8
    public int dmgMitigationPercent;
    public int maxHPIncreasePercent;
    // proc 62
    public int          barrierHP;
    public int          barrierDef;
    public ElementalType barrierElement;
    public float        barrierAbsorbPercent;
    public int          dmgMitigationForElementalAttacks;
}

[System.Serializable]
public class ReviveEffect
{
    public float reviveChance;
    public float reviveHPPercent;
    public bool  triggerOnBB;
    public bool  triggerOnSBB;
    public bool  triggerOnUBB;
}

[System.Serializable]
public class ConditionalBuffEffect
{
    // proc 78 / 80 / 82 / 84 / 86 / 88 / 89
    // triggerType is implied by procId at runtime — stored here for convenience
    public string triggerType;   // "damage_received" / "damage_dealt" / "bc_count" / "hc_count" / "spark_count" / "on_guard" / "on_crit"
    public float  activationChance;
    public ConditionalSubBuff buff;
}

[System.Serializable]
public class ConditionalSubBuff
{
    public int    atkBuff;
    public int    defBuff;
    public int    bbAtkBuff;
    public int    sbbAtkBuff;
    public int    ubbAtkBuff;
    public int    sparkDmgBuff;
    public int    dmgReductionBuff;
    public int    gradualHealHigh;
    public int    gradualHealLow;
    public int    odFillRate;
    public int    hpDrainChance;
    public int    hpDrainHigh;
    public int    hpDrainLow;
    public int    sparkDmgInc;
    public int    bcFillOnSparkHigh;
    public int    bcFillOnSparkLow;
    public float  bcFillOnSparkPercent;
    public int    gradualBcFill;
    public string elementBuffed;
    public int    buffTurns;
}

[System.Serializable]
public class AilmentInflictEffect
{
    public int poisonChance;
    public int weakenChance;
    public int sickChance;
    public int injuryChance;
    public int curseChance;
    public int paralysisChance;
}

[System.Serializable]
public class AilmentResistEffect
{
    public int poisonResist;
    public int weakenResist;
    public int sickResist;
    public int injuryResist;
    public int curseResist;
    public int paralysisResist;
    public int atkDownResist;
    public int defDownResist;
    public int recDownResist;
    public int immunityBuffTurns;
}

[System.Serializable]
public class ExtraActionEffect
{
    public float chance;
    public int   maxExtraActions;
    public int   buffTurns;
}

// ─────────────────────────────────────────
//  CONDITIONS
// ─────────────────────────────────────────

[System.Serializable]
public class EffectCondition
{
    public ConditionType conditionType;
    public float         hpThresholdPercent;
    public List<string>  itemsRequired;
    public List<string>  elementsRequired;
    public string        genderRequired;
    public int           bbGaugeThreshold;
}

public enum ConditionType
{
    Always,
    HPAbove,
    HPBelow,
    ItemRequired,
    ElementRequired,
    GenderRequired,
    BBGaugeAbove,
    BBGaugeBelow
}

// ─────────────────────────────────────────
//  ENUMS
// ─────────────────────────────────────────

public enum TargetType
{
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies,
    Self
}

public enum TargetArea
{
    Single,
    AOE,
    Random
}

public enum ElementalType
{
    None,
    Fire,
    Water,
    Earth,
    Thunder,
    Light,
    Dark
}