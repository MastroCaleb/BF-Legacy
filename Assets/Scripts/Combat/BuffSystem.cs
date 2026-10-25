// BuffSystem.cs
// C# port of the Godot buff system (buff_object.gd / buff_manager.gd).
// Owns: buff sources, id groups, per-unit buff storage with Godot's override/merge rules,
// and the stat / damage / mitigation / status formulas. UnitBehaviour delegates to this.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BuffSource
{
    BB, SBB, UBB, DBB,
    LeaderSkill, FriendSkill, ExtraSkill, Elgif, SpEnhancement, Sphere,
    OD, Item, None
}

public static class BuffIds
{
    public const string Overdrive = "133";

    // ids that deal damage / apply instantly (Godot DAMAGE_BUFFS / INSTANT_BUFFS)
    public static readonly HashSet<string> Damage = new HashSet<string>
    { "1","13","14","27","28","29","46","47","48","61","64","75","97","10020","11000" };
    public static readonly HashSet<string> Instant = new HashSet<string>
    { "2","4","10","12","31","38","43","49","66","10019","10029","10033" };

    // buffs that stack per element instead of overriding each other (Godot STACKABLE_BUFFS)
    public static readonly HashSet<string> Stackable = new HashSet<string>
    { "5","9","16","24","39","10015","10016" };

    // ids whose statBuff atk/def/rec/hp/crit values count towards the unit's stats.
    // Deliberately excludes passive 10 (HC efficacy), which reuses statBuff.recBuff.
    public static readonly HashSet<string> StatBoost = new HashSet<string>
    { "1","2","3","5","11","42","10000","10001", Overdrive };

    public static readonly HashSet<string> Mitigation = new HashSet<string>
    { "8","16","18","39","93" };

    public static readonly HashSet<string> SparkBoost = new HashSet<string>
    { "23","31","83","88" };

    public static bool IsPassiveSource(BuffSource s) => s switch
    {
        BuffSource.LeaderSkill or BuffSource.FriendSkill or BuffSource.ExtraSkill
            or BuffSource.Elgif or BuffSource.SpEnhancement or BuffSource.Sphere => true,
        _ => false
    };
}

public class ActiveBuff
{
    public Effect effect;
    public BuffSource source;
    public int turnsLeft; // -1 = permanent (passives)

    public string Id => effect.isPassive ? effect.passiveId : effect.procId;
    public bool IsPassive => BuffIds.IsPassiveSource(source);
}

// ─────────────────────────────────────────────────────────────
//  Per-unit buff storage. Mirrors unit.buffs.active_buffs + _override_or_merge_similar_buffs.
// ─────────────────────────────────────────────────────────────
public class UnitBuffs
{
    public readonly List<ActiveBuff> active = new List<ActiveBuff>();

    public List<Effect> Effects() => active.Select(b => b.effect).ToList();

    public bool Has(string id) => active.Any(b => b.Id == id);

    // BB/SBB share a group and UBB/DBB share a group, everything else is its own group
    static BuffSource[] Group(BuffSource s) => s switch
    {
        BuffSource.BB or BuffSource.SBB   => new[] { BuffSource.BB, BuffSource.SBB },
        BuffSource.UBB or BuffSource.DBB  => new[] { BuffSource.UBB, BuffSource.DBB },
        _                                 => new[] { s }
    };

    /// Adds a buff. A buff with the same id from the same source group replaces the old one with
    /// the highest value of every numeric param (and the longest duration). Stackable ids only
    /// merge when their element matches. Passives are always permanent.
    public ActiveBuff Add(Effect effect, BuffSource source, int turns)
    {
        if (effect == null) return null;
        if (BuffIds.IsPassiveSource(source)) turns = -1;

        var incoming = new ActiveBuff { effect = Clone(effect), source = source, turnsLeft = turns };
        string id = incoming.Id;
        var group = Group(source);

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var existing = active[i];
            if (existing.Id != id || Array.IndexOf(group, existing.source) < 0) continue;
            if (BuffIds.Stackable.Contains(id) && ElementKey(existing.effect) != ElementKey(incoming.effect)) continue;

            MergeMax(existing.effect, incoming.effect);
            incoming.turnsLeft = (existing.turnsLeft < 0 || turns < 0) ? -1 : Math.Max(existing.turnsLeft, turns);
            active.RemoveAt(i);
        }

        active.Add(incoming);
        return incoming;
    }

    public int RemoveWhere(Predicate<ActiveBuff> match) => active.RemoveAll(match);

    /// End of turn: decrement non-passive buffs, drop the ones that ran out and return them.
    public List<ActiveBuff> Decrement()
    {
        var expired = new List<ActiveBuff>();
        foreach (var b in active.ToList())
        {
            if (b.turnsLeft < 0) continue;
            b.turnsLeft--;
            if (b.turnsLeft <= 0) { active.Remove(b); expired.Add(b); }
        }
        return expired;
    }

    static Effect Clone(Effect e) => JsonUtility.FromJson<Effect>(JsonUtility.ToJson(e));

    static string ElementKey(Effect e)
    {
        var s = e.statBuff; var d = e.damageBuff;
        string elems = s == null ? "" : (s.elementBuffed ?? "") + "|" + string.Join(",", s.elementsBuffed ?? new List<string>());
        string flags = d == null ? "" : $"{d.mitigateFire}{d.mitigateWater}{d.mitigateEarth}{d.mitigateThunder}{d.mitigateLight}{d.mitigateDark}";
        return elems + "|" + flags;
    }

    // takes the higher of every int/float field across the sub-effect structs, writing into 'incoming'
    static void MergeMax(Effect existing, Effect incoming)
    {
        foreach (var sf in typeof(Effect).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var t = sf.FieldType;
            if (!t.IsClass || t == typeof(string) || typeof(System.Collections.IList).IsAssignableFrom(t)) continue;
            object a = sf.GetValue(existing), b = sf.GetValue(incoming);
            if (a == null || b == null) continue;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(int))
                    f.SetValue(b, Math.Max((int)f.GetValue(a), (int)f.GetValue(b)));
                else if (f.FieldType == typeof(float))
                    f.SetValue(b, Mathf.Max((float)f.GetValue(a), (float)f.GetValue(b)));
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────
//  Formulas (buff_manager.gd: get_total_stat_for_unit, get_total_hit_damage, mitigation, ...)
// ─────────────────────────────────────────────────────────────
public static class BuffCalc
{
    public const float BaseCritRate     = 10f;
    public const float BaseSparkBonus   = 0.5f;  // Godot: enemy base spark resistance term (no per-enemy field here yet)
    public const float BaseElemAdvBonus = 1.5f;
    public const float PoisonPercent    = 10f;   // % of max HP per turn
    public const int   MaxStat          = 99999;

    // ── sources ──
    public static BuffSource SourceFor(UnitBehaviour u, Ability a, bool permanent)
    {
        if (permanent) return a == u.unitData.leaderAbility ? BuffSource.LeaderSkill : BuffSource.ExtraSkill;
        if (a == null) return BuffSource.None;
        if (a == u.unitData.bbAbility)  return BuffSource.BB;
        if (a == u.unitData.sbbAbility) return BuffSource.SBB;
        if (a == u.unitData.ubbAbility) return BuffSource.UBB;
        return BuffSource.None;
    }

    public static Effect MakeOverdriveEffect() => new Effect
    {
        procId = BuffIds.Overdrive,
        passiveId = "",
        isPassive = false,
        targetType = TargetType.Self,
        targetArea = TargetArea.Single,
        conditions = new List<EffectCondition>(),
        statBuff = new StatBuffEffect { atkBuff = 100, defBuff = 100, recBuff = 100, buffTurns = 4 }
    };

    // ── element helpers ──
    public static ElementalType ElementOf(UnitBehaviour u) => u.unitData.element;

    static bool BuffAppliesToElement(ActiveBuff b, ElementalType unitElement)
    {
        var s = b.effect.statBuff;
        if (s == null) return true;
        var elements = new List<string>();
        if (!string.IsNullOrEmpty(s.elementBuffed)) elements.Add(s.elementBuffed);
        if (s.elementsBuffed != null) elements.AddRange(s.elementsBuffed);
        if (elements.Count == 0) return true;
        string mine = unitElement.ToString().ToLowerInvariant();
        foreach (var e in elements)
        {
            string l = (e ?? "").ToLowerInvariant();
            if (l == "" || l == "all" || l == "base" || l == "none" || l == mine) return true;
        }
        return false;
    }

    /// +1 attacker has advantage, -1 attacker is resisted, 0 neutral. Light/Dark are mutually weak (no resist).
    public static int ElementAdvantage(ElementalType atk, ElementalType def)
    {
        bool Beats(ElementalType a, ElementalType d) =>
            (a == ElementalType.Fire    && d == ElementalType.Earth)   ||
            (a == ElementalType.Earth   && d == ElementalType.Thunder) ||
            (a == ElementalType.Thunder && d == ElementalType.Water)   ||
            (a == ElementalType.Water   && d == ElementalType.Fire)    ||
            (a == ElementalType.Light   && d == ElementalType.Dark)    ||
            (a == ElementalType.Dark    && d == ElementalType.Light);
        if (Beats(atk, def)) return 1;
        bool lightDark = atk == ElementalType.Light || atk == ElementalType.Dark;
        if (!lightDark && Beats(def, atk)) return -1;
        return 0;
    }

    // ── stats ──
    public static int BaseStat(UnitBehaviour u, string stat)
    {
        if (u.isEnemyUnit)
        {
            switch (stat)
            {
                case "atk": return (int)u.enemyData.atk;
                case "def": return (int)u.enemyData.def;
                case "hp":  return (int)u.enemyData.health;
                default:    return 0;
            }
        }
        var d = u.unitData; var inv = u.inventoryData;
        switch (stat)
        {
            case "atk": return d.atk + inv.atkImpBonus + inv.atkLevelUpBonus;
            case "def": return d.def + inv.defImpBonus + inv.defLevelUpBonus;
            case "rec": return d.rec + inv.recImpBonus + inv.recLevelUpBonus;
            case "hp":  return d.maxHealth + inv.hpLevelUpBonus + inv.hpImpBonus;
            default:    return 0;
        }
    }

    // Godot: injury halves ATK, weakness halves DEF, sickness halves REC
    static float StatusModifier(UnitBehaviour u, string stat)
    {
        if (stat == "atk" && u.activeAilments.Contains("Injury")) return 0.5f;
        if (stat == "def" && u.activeAilments.Contains("Weaken")) return 0.5f;
        if (stat == "rec" && u.activeAilments.Contains("Sick"))   return 0.5f;
        return 1f;
    }

    static float StatBuffSum(UnitBehaviour u, string stat, bool includeBbAtk, BuffSource cast)
    {
        float sum = 0f;
        var element = ElementOf(u);
        foreach (var b in u.buffs.active)
        {
            if (!BuffAppliesToElement(b, element)) continue;
            var e = b.effect; string id = b.Id;

            if (BuffIds.StatBoost.Contains(id) && e.statBuff != null)
            {
                switch (stat)
                {
                    case "atk": sum += e.statBuff.atkBuff; break;
                    case "def": sum += e.statBuff.defBuff; break;
                    case "rec": sum += e.statBuff.recBuff; break;
                    case "hp":  sum += e.statBuff.hpBuff;  break;
                }
            }

            // bb atk buffs only count while a bb/sbb/ubb modifier is part of the hit
            if (stat == "atk" && includeBbAtk && e.bbAtkBuff != null && (id == "45" || (b.IsPassive && id == "64")))
            {
                sum += cast == BuffSource.SBB ? e.bbAtkBuff.sbbAtkBuff
                     : cast == BuffSource.UBB ? e.bbAtkBuff.ubbAtkBuff
                     : e.bbAtkBuff.bbAtkBuff;
            }
        }
        return sum;
    }

    /// total = base * (1 + (buffs + modifier%) / 100) * status, capped. 'modifier' is the attack's damage %.
    public static int GetStat(UnitBehaviour u, string stat, int flat = 0, float modifier = 0f, BuffSource cast = BuffSource.None)
    {
        float sum = StatBuffSum(u, stat, modifier > 0f, cast);
        if (stat == "atk") sum += modifier;
        float total = (BaseStat(u, stat) + flat) * (1f + sum / 100f) * StatusModifier(u, stat);
        return (int)Mathf.Min(total, MaxStat);
    }

    /// Percentage form (100 = no buff) for callers that still want a rate.
    public static int StatRate(UnitBehaviour u, string stat)
        => Mathf.Max(Mathf.RoundToInt((100f + StatBuffSum(u, stat, false, BuffSource.None)) * StatusModifier(u, stat)), 1);

    // ── crit ──
    public static float CritRate(UnitBehaviour u, float procCrit)
    {
        float rate = BaseCritRate + procCrit;
        var element = ElementOf(u);
        foreach (var b in u.buffs.active)
        {
            if (!BuffAppliesToElement(b, element)) continue;
            if (BuffIds.StatBoost.Contains(b.Id) && b.effect.statBuff != null) rate += b.effect.statBuff.critBuff;
        }
        return Mathf.Clamp(rate, 0f, 100f);
    }

    public static bool RollCrit(UnitBehaviour u, float procCrit) => Random.Range(0, 100) < CritRate(u, procCrit);

    /// 1 + random 50-109% base + crit damage buffs (proc 54)
    public static float CritMultiplier(UnitBehaviour u)
    {
        float bonus = 0f;
        foreach (var b in u.buffs.active)
            if (b.Id == "54" && b.effect.damageBuff != null) bonus += b.effect.damageBuff.critMultiplier;
        return 1f + Random.Range(50, 110) / 100f + bonus / 100f;
    }

    // ── defense / elemental / spark / mitigation / guard ──
    public static float DefReduction(UnitBehaviour atk, UnitBehaviour tgt)
    {
        foreach (var b in atk.buffs.active)
            if (b.Id == "22" && b.effect.damageBuff != null && Random.Range(0, 100) < b.effect.damageBuff.defenseIgnorePercent)
                return 0f;
        return (GetStat(tgt, "def") + ConversionBonus(tgt, "def")) / 3f;
    }

    public static float ElementalBonus(UnitBehaviour atk)
    {
        float bonus = 0f;
        foreach (var b in atk.buffs.active)
            if (b.Id == "55" && b.effect.damageBuff != null) bonus += b.effect.damageBuff.elementalWeaknessMultiplier;
        return BaseElemAdvBonus + Mathf.Max(bonus / 100f, 0f);
    }

    public static float SparkMultiplier(UnitBehaviour atk)
    {
        float bonus = 0f;
        if (atk != null)
            foreach (var b in atk.buffs.active)
                if (BuffIds.SparkBoost.Contains(b.Id) && b.effect.damageBuff != null) bonus += b.effect.damageBuff.sparkDmgBuff;
        return 1f + BaseSparkBonus + Mathf.Max(bonus / 100f, 0f);
    }

    static bool MitigatesElement(DamageBuffEffect d, ElementalType e) => e switch
    {
        ElementalType.Fire    => d.mitigateFire,
        ElementalType.Water   => d.mitigateWater,
        ElementalType.Earth   => d.mitigateEarth,
        ElementalType.Thunder => d.mitigateThunder,
        ElementalType.Light   => d.mitigateLight,
        ElementalType.Dark    => d.mitigateDark,
        _ => false
    };

    /// Multiplier on incoming damage (1 = none). BB/SBB, UBB/DBB and passive mitigation multiply together,
    /// elemental mitigation is a second set that only counts against the attacker's element.
    public static float MitigationFactor(UnitBehaviour target, ElementalType attackerElement)
    {
        float bb = 0, ubb = 0, pas = 0, ebb = 0, eubb = 0, epas = 0;
        foreach (var b in target.buffs.active)
        {
            var d = b.effect.damageBuff;
            if (d == null || !BuffIds.Mitigation.Contains(b.Id) || d.dmgMitigationPercent <= 0) continue;
            bool elemental = d.mitigateFire || d.mitigateWater || d.mitigateEarth || d.mitigateThunder || d.mitigateLight || d.mitigateDark;
            if (elemental && !MitigatesElement(d, attackerElement)) continue;
            float v = d.dmgMitigationPercent;
            switch (b.source)
            {
                case BuffSource.BB: case BuffSource.SBB:  if (elemental) ebb += v;  else bb += v;  break;
                case BuffSource.UBB: case BuffSource.DBB: if (elemental) eubb += v; else ubb += v; break;
                default:                                  if (elemental) epas += v; else pas += v; break;
            }
        }
        float F(float p) => Mathf.Max(1f - p / 100f, 0f);
        return F(bb) * F(ubb) * F(pas) * F(ebb) * F(eubb) * F(epas);
    }

    public static float GuardModifier(UnitBehaviour target)
    {
        float m = 0.5f;
        foreach (var b in target.buffs.active)
            if (b.Id == "68" && b.effect.damageBuff != null) m -= b.effect.damageBuff.dmgMitigationPercent / 100f;
        return Mathf.Max(m, 0f);
    }

    public static AttackEffect FindAttackEffect(Ability ability, int level)
    {
        if (ability?.levels == null || ability.levels.Count == 0) return null;
        var effects = ability.levels[Mathf.Clamp(level, 0, ability.levels.Count - 1)].effects;
        if (effects == null) return null;
        foreach (var e in effects)
            if (e.attack != null && !e.isPassive && e.attack.bbAtkPercent != 0) return e.attack;
        return null;
    }

    /// Per-hit damage before spark (spark is applied in TakeDamage from the sparkCount).
    /// dmg = (atk + modifier - def/3) * crit * element * mitigation * guard * hitRate, min 1
    public static float HitDamage(UnitBehaviour atk, UnitBehaviour tgt, float hitRate, int abilityModifier, BuffSource cast, bool crit)
    {
        float dmg = GetStat(atk, "atk", 0, abilityModifier, cast) + ConversionBonus(atk, "atk") - DefReduction(atk, tgt);
        if (crit) dmg *= CritMultiplier(atk);

        int adv = ElementAdvantage(ElementOf(atk), ElementOf(tgt));
        if (adv > 0)      dmg *= ElementalBonus(atk);
        else if (adv < 0) dmg *= 0.5f;

        dmg *= MitigationFactor(tgt, ElementOf(atk));
        if (tgt.isGuarding) dmg *= GuardModifier(tgt);
        dmg *= hitRate;
        return Mathf.Max(dmg, 1f);
    }

    // ── parameter conversion (procs 24 / 89) ──
    static string NormalizeAttribute(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        s = s.ToLowerInvariant();
        if (s.Contains("atk")) return "atk";
        if (s.Contains("def")) return "def";
        if (s.Contains("rec")) return "rec";
        if (s.Contains("hp"))  return "hp";
        return null;
    }

    /// Flat bonus to 'stat' from conversion buffs: pct% of the converted attribute's buffed total.
    /// Added on top of the stat in damage / defense calcs, never fed back into GetStat (no recursion).
    public static int ConversionBonus(UnitBehaviour u, string stat)
    {
        float bonus = 0f;
        foreach (var b in u.buffs.active)
        {
            var c = b.effect.conversion;
            if (c == null) continue;

            int pct = stat == "atk" ? c.atkConvertPercent
                    : stat == "def" ? c.defConvertPercent
                    : stat == "rec" ? c.recConvertPercent : 0;
            if (pct <= 0) continue;

            string source = NormalizeAttribute(c.convertedAttribute);
            if (source == null) continue;

            bonus += GetStat(u, source) * pct / 100f;
        }
        return (int)bonus;
    }

    // ── drops ──
    public enum DropType { Crystal, Heart, Zell, Karma, Item }

    public const float BaseCrystalDropPercent  = 35f;
    public const float BaseHeartDropPercent    = 10f;
    public const float OverkillDropMultiplier  = 2f;

    /// Sum of the unit's drop-rate buffs for one drop type (percentage points). Spark variants count only on sparked hits.
    public static float DropBonus(UnitBehaviour u, DropType type, bool sparked)
    {
        float sum = 0f;
        foreach (var b in u.buffs.active)
        {
            var d = b.effect.dropRate;
            if (d == null) continue;
            switch (type)
            {
                case DropType.Crystal: sum += d.bcDropRate    + (sparked ? d.sparkBcDropRate    : 0); break;
                case DropType.Heart:   sum += d.hcDropRate    + (sparked ? d.sparkHcDropRate    : 0); break;
                case DropType.Item:    sum += d.itemDropRate  + (sparked ? d.sparkItemDropRate  : 0); break;
                case DropType.Zell:    sum += d.zelDropRate   + (sparked ? d.sparkZelDropRate   : 0); break;
                case DropType.Karma:   sum += d.karmaDropRate + (sparked ? d.sparkKarmaDropRate : 0); break;
            }
        }
        return sum;
    }

    /// Per-check BC / HC drop chance in percent:
    /// (base + proc's own bc%/hc% + unit drop buffs) * overkill (x2 when the hit killed the target).
    public static float CrystalDropChance(UnitBehaviour attacker, UnitBehaviour victim, DropType type, int procBonus, bool sparked)
    {
        float baseChance = type == DropType.Heart ? BaseHeartDropPercent : BaseCrystalDropPercent;
        float chance = baseChance + procBonus + DropBonus(attacker, type, sparked);
        if (victim.currentHealth <= 0) chance *= OverkillDropMultiplier;
        return Mathf.Max(chance, 0f);
    }

    // ── healing / bc ──
    /// RandomBetween(low, high) + target REC + healer REC * addedPercent/100
    public static int BurstHealAmount(UnitBehaviour healer, UnitBehaviour target, HealEffect h)
    {
        float baseHeal = Random.Range(h.healLow, h.healHigh + 1);
        float total = baseHeal + GetStat(target, "rec") + GetStat(healer, "rec") * (h.recAddedPercent / 100f);
        return Mathf.CeilToInt(total);
    }

    /// Flat fill if set, otherwise a percentage of the unit's max BC.
    public static int BcFillAmount(UnitBehaviour u, BCFillEffect fill)
    {
        if (fill.bbBCFill > 0) return fill.bbBCFill;
        if (fill.bbBCFillPercent > 0) return Mathf.FloorToInt(u.GetMaxBC() * (fill.bbBCFillPercent / 100f));
        return fill.bbBCFill;
    }

    // ── status ──
    /// All ailments last 3 turns, paralysis / curse last 1 turn on enemies.
    public static int StatusTurns(UnitBehaviour target, string ailment)
        => (ailment == "Paralysis" || ailment == "Curse") && target.isEnemyUnit ? 1 : 3;
}