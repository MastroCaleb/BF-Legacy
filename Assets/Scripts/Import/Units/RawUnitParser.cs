// RawUnitParser.cs
// Shared raw-data → DTO parser for the data-driven units pipeline. Ports
// JsonToSOUnit's resolution logic into a reusable form consumed by both
// emitters:
//   JsonToUnitData      — writes the DTOs as typed JSON + Addressables entries
//   UnitDatabaseBuilder — packs the DTOs straight into units.db
// Input: raw unit JSON (default JsonData/Units/*.json) plus the raw asset
// folders (cgg/cgs/sprites/sam/sound) and the SKILL_MST / EFFECT_GROUP_MST
// editor tables. Output: one UnitData per file plus deduplicated AbilityData
// per skill (EmittedAbilities). UUID keys from the source JSON are consumed as
// the source of truth for display positions, but the output contains typed
// fields only. Every referenced asset (sprites with __v2..__v9 fallback,
// cgg/cgs/sam, audio, ParticleEffect ids) is verified to exist; missing refs
// are counted and logged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class RawUnitParser
{
    // ── Source folders (relative to Assets/ unless absolute) ──
    public string jsonFolderPath        = "JsonData/Units";
    public string cggFolderPath         = "BF_Assets/content/unit/cgg";
    public string cgsFolderPath         = "BF_Assets/content/unit/cgs";
    public string spriteFolderPath      = "BF_Assets/content/unit/img";
    public string samRootFolderPath     = "AddressableContent/Sams/Unit_SAMS/unit_sam/";
    public string soundFolderPath       = "BF_Assets/content/sound"; // relative to Assets/
    public string effectGroupMst1       = "EditorData/F_EFFECT_GROUP_MST_1";
    public string effectGroupMst2       = "EditorData/F_EFFECT_GROUP_MST_2";
    public string skillMstFolder        = "EditorData/SkillMSTs";
    public string particleEffectFolder  = "Particles";

    // ── run state ──
    readonly HashSet<string> _particleIds = new();          // verification index
    readonly Dictionary<string, string> _audioCache = new(); // clip file name → address
    readonly Dictionary<string, AbilityData> _emittedAbilities = new(); // dedup across units
    readonly HashSet<string> _emittedUnitIds = new();

    public int MissingSprites    { get; private set; }
    public int MissingParticles  { get; private set; }
    public int MissingAudio      { get; private set; }
    public int MissingAnimations { get; private set; }

    /// <summary>Unique abilities emitted so far, by ability id (dedup across units).</summary>
    public IReadOnlyDictionary<string, AbilityData> EmittedAbilities => _emittedAbilities;

    /// <summary>Ids of the units successfully parsed so far.</summary>
    public IReadOnlyCollection<string> EmittedUnitIds => _emittedUnitIds;

    /// <summary>Clears per-run caches and counters (call before starting a fresh run).</summary>
    public void ResetState()
    {
        _audioCache.Clear();
        _emittedAbilities.Clear();
        _emittedUnitIds.Clear();
        MissingSprites = MissingParticles = MissingAudio = MissingAnimations = 0;
    }

    // ─────────────────────────────────────────────────────────────
    //  RAW FILE ACCESS + TABLE / INDEX LOADING
    // ─────────────────────────────────────────────────────────────

    /// <summary>Sorted *.json paths under the raw unit JSON folder, or false (with an error log) when it doesn't exist.</summary>
    public bool TryGetRawUnitFiles(out string[] files)
    {
        files = null;
        string absPath = Application.dataPath + "/" + jsonFolderPath;
        if (!Directory.Exists(absPath))
        {
            Debug.LogError($"[RawUnitParser] Raw unit JSON folder not found: {absPath}");
            return false;
        }
        files = Directory.GetFiles(absPath, "*.json").OrderBy(f => f).ToArray();
        return true;
    }

    public void LoadTables()
    {
        var skillTable = new SkillMstTable();
        string abs = Application.dataPath + "/" + skillMstFolder;
        if (Directory.Exists(abs))
        {
            foreach (string file in Directory.GetFiles(abs, "*.json"))
                skillTable.AddFromText(File.ReadAllText(file));
        }
        else
        {
            Debug.LogWarning($"[SkillMST] Folder not found: {abs}");
        }

        var groupTable = new EffectGroupMstTable();
        foreach (string rel in new[] { effectGroupMst1, effectGroupMst2 })
        {
            string path = Application.dataPath + "/" + rel + ".json";
            if (File.Exists(path)) groupTable.AddFromText(File.ReadAllText(path));
            else Debug.LogWarning($"[EffectGroup] Not found: {path}");
        }

        EffectFrameResolver.SetTables(skillTable, groupTable);
        Debug.Log($"[RawUnitParser] Tables: {skillTable.EntryCount} skill entries, {groupTable.GroupCount} effect groups.");
    }

    public void LoadParticleIdIndex()
    {
        _particleIds.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ParticleEffect", new[] { $"Assets/{particleEffectFolder}" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ParticleEffect pe = AssetDatabase.LoadAssetAtPath<ParticleEffect>(path);
            if (pe == null || string.IsNullOrEmpty(pe.effectId)) continue;
            _particleIds.Add(pe.effectId);
        }
        Debug.Log($"[RawUnitParser] Indexed {_particleIds.Count} ParticleEffect ids for verification.");
    }

    // ─────────────────────────────────────────────────────────────
    //  UNIT PARSING
    // ─────────────────────────────────────────────────────────────

    /// <summary>Parses one raw unit JSON file into a UnitData (emitting its abilities). Returns null when skipped.</summary>
    public UnitData ParseUnitFile(string filePath) => ParseUnit(JObject.Parse(File.ReadAllText(filePath)));

    public UnitData ParseUnit(JObject data)
    {
        string unitId = data["id"]?.ToString();
        if (string.IsNullOrEmpty(unitId)) { Debug.LogWarning("[RawUnitParser] Skipped unit: missing id"); return null; }

        UnitData unit = new UnitData();

        // ── Core Info ────────────────────────────────────────────
        unit.unitId      = unitId;
        unit.unitName    = data["unitName"]?.ToString();
        unit.unitNumber  = data["no"]?.ToString();
        unit.description = data["description"]?.ToString();
        unit.summonDesc  = data["summon"]?.ToString();
        unit.fusionDesc  = data["fusion"]?.ToString();
        unit.evoDesc     = data["evolution"]?.ToString();
        unit.summonCost  = ParseInt(data["cost"]);
        unit.element     = ParseElement(data["element"]?.ToString());
        unit.rarity      = ParseRarity(data["rarity"]?.ToString());
        unit.bbType      = data["bbtype"]?.ToString();

        // ── Statistics ───────────────────────────────────────────
        unit.maxLevel  = ParseInt(data["maxlv"]);
        unit.aiLevel   = ParseInt(data["ai"]);
        unit.maxHealth = ParseInt(data["hp_base"]);
        unit.atk       = ParseInt(data["atk_base"]);
        unit.def       = ParseInt(data["def_base"]);
        unit.rec       = ParseInt(data["rec_base"]);
        unit.baseExp   = ParseInt(data["basexp"]);

        // ── Movement ─────────────────────────────────────────────
        unit.moveSpeedAttack = ParseFloat(data["movespeed_attack"]);
        unit.moveSpeedSkill  = ParseFloat(data["movespeed_skill"]);
        unit.speedTypeAttack = ParseInt(data["speedtype_attack"]);
        unit.speedTypeSkill  = ParseInt(data["speedtype_skill"]);
        unit.moveTypeAttack  = ParseInt(data["movetype_attack"]);
        unit.moveTypeSkill   = ParseInt(data["movetype_skill"]);

        // ── Evolution ────────────────────────────────────────────
        unit.evoFrom    = data["evofrom"]?.ToString();
        unit.evoInto    = data["evointo"]?.ToString();
        unit.evoItem    = data["evoitem"]?.ToString();
        unit.evoZelCost = ParseInt(data["evozelcost"]);
        unit.evoMats    = new List<string>();
        for (int m = 1; m <= 5; m++)
        {
            string mat = data[$"evomats{m}"]?.ToString();
            if (!string.IsNullOrEmpty(mat))
                unit.evoMats.Add(mat);
        }

        // ── Sell ─────────────────────────────────────────────────
        unit.sellPrice   = ParseInt(data["sellPrice"]);
        unit.sellCaution = data["sellCaution"]?.Type == JTokenType.Boolean
            ? data["sellCaution"].Value<bool>()
            : ParseInt(data["sellCaution"]) != 0;

        // ── Type Statistics ──────────────────────────────────────
        JObject stats = data["stats"] as JObject;
        if (stats != null)
        {
            unit.statsBase = ParseUnitStats(stats["_base"] as JObject);
            unit.statsLord = ParseUnitStats(stats["_lord"] as JObject);
        }

        // ── Display Positions (UUID keys are the source of truth) ──
        unit.unitDisplayHomePosition         = ParseUnitDisplay(data["unitDisplayHomePosition_1W9CxaFK"]?.ToString());
        unit.unitDisplayDetailPosition       = ParseUnitDisplay(data["unitDisplayDetailPosition_6z54rgb3"]?.ToString());
        unit.unitDisplayConfirmImagePosition = ParseUnitDisplay(data["unitDisplayConfirmImagePosition_MYK1fq6c"]?.ToString());
        unit.unitDisplayCutInImagePosition   = ParseUnitDisplay(data["unitDisplayCutInImagePosition_7hLR6pDN"]?.ToString());
        unit.unitDisplaySummonPosition       = ParseUnitDisplay(data["unitDisplaySummonPosition_KC3Jk8Br"]?.ToString());
        unit.unitDisplayHpPosition           = ParseVector2(data["unitDisplayHpPosition_3BpHN6VD"]?.ToString());
        unit.unitDisplayCursorPosition       = ParseVector2(data["unitDisplayCursorPosition"]?.ToString());

        // ── Visuals & Animation (resolved to addressable paths) ──
        ResolveAnimationData(unit, unitId);

        // ── Abilities (emitted as separate deduplicated DTOs) ────
        unit.basicAbilityId  = EmitAbility(BuildNormalAbility(data, unitId, unit.unitName));
        unit.bbAbilityId     = EmitAbility(BuildLeveledAbility(data["bb"]  as JObject, data, "bbmultiplier",  "bbdc",  $"unknown_{unitId}_bb"));
        unit.sbbAbilityId    = EmitAbility(BuildLeveledAbility(data["sbb"] as JObject, data, "sbbmultiplier", "sbbdc", $"unknown_{unitId}_sbb"));
        unit.ubbAbilityId    = EmitAbility(BuildLeveledAbility(data["ubb"] as JObject, data, "ubbmultiplier", "ubbdc", $"unknown_{unitId}_ubb"));
        unit.leaderAbilityId = EmitAbility(BuildFlatAbility(data["leader_skill"] as JObject, $"unknown_{unitId}_leader"));
        unit.extraAbilityId  = EmitAbility(BuildFlatAbility(data["extra_skill"]  as JObject, $"unknown_{unitId}_extra"));

        _emittedUnitIds.Add(unitId);
        return unit;
    }

    // ─────────────────────────────────────────────────────────────
    //  ABILITY BUILDERS
    // ─────────────────────────────────────────────────────────────

    private AbilityData BuildNormalAbility(JObject data, string unitId, string unitName)
    {
        // Normal attacks have no real skill id, so they can't go through the
        // SKILL_MST chain that BB/SBB/UBB use — their effectFrames live on the
        // unit's own F_UNIT_MST entry as a flat "frame:particleId:placement"
        // list. Keyed "normal_{unitId}" so they can never collide with real
        // skill ids (unit ids and bbIds have been observed to overlap).
        var a = new AbilityData
        {
            abilityId        = $"normal_{unitId}",
            abilityName      = $"{unitName} Normal Attack",
            targetType       = TargetType.SingleEnemy,
            targetArea       = TargetArea.Single,
            dropChecksPerHit = ParseInt(data["normaldc"]),
            hitFrames        = ParseHitFramesFromString(
                                 data["normal_frames"]?.ToString(),
                                 data["normal_distribute"]?.ToString()),
            effectFrames     = EffectFrameResolver.ResolveNormalFrames(
                                 data["effectFrames"]?.ToString(), ResolveAudioAddress),
            levels = new List<AbilityLevel>
            {
                new AbilityLevel { bcCost = 0, damageRate = 100, effects = new List<Effect>() }
            }
        };
        VerifyParticleRefs(a, unitId);
        return a;
    }

    private AbilityData BuildLeveledAbility(JObject skillObj, JObject unitData,
                                            string dmgKey, string dcKey, string fallbackId)
    {
        if (skillObj == null) return null;

        string name = skillObj["name"]?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return null;

        string abilityId = skillObj["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(abilityId)) abilityId = fallbackId;

        int    dc      = ParseInt(unitData[dcKey]);
        int    dmgRate = ParseInt(unitData[dmgKey]);

        var a = new AbilityData
        {
            abilityId        = abilityId,
            abilityName      = name,
            abilityDesc      = skillObj["desc"]?.ToString(),
            dropChecksPerHit = dc
        };

        JArray damageFrames = skillObj["damage frames"] as JArray;
        if (damageFrames?.Count > 0)
        {
            JObject first = damageFrames[0] as JObject;
            a.hitFrames = ParseHitFramesFromArrays(
                first?["frame times"]           as JArray,
                first?["hit dmg% distribution"] as JArray);
        }

        // Effect frames via the SKILL_MST → EFFECT_GROUP_MST chain
        if (EffectFrameResolver.SkillMst?.TryGetEffectFrames(GetAnimationId(abilityId) ?? "", out string efRaw) == true)
            a.effectFrames = EffectFrameResolver.ResolveSkillFrames(efRaw, ResolveAudioAddress);
        else
            a.effectFrames = new List<EffectFrameData>();

        a.levels = new List<AbilityLevel>();
        JArray levels = skillObj["levels"] as JArray;
        if (levels != null)
        {
            foreach (JToken lvl in levels)
            {
                a.levels.Add(new AbilityLevel
                {
                    bcCost     = ParseInt(lvl["bc cost"]),
                    damageRate = dmgRate,
                    effects    = ParseEffects(lvl["effects"] as JArray)
                });
            }
        }

        Effect firstEffect = a.levels.LastOrDefault()?.effects?.FirstOrDefault();
        if (firstEffect != null)
        {
            a.targetType = firstEffect.targetType;
            a.targetArea = firstEffect.targetArea;
        }

        VerifyParticleRefs(a, abilityId);
        return a;
    }

    private AbilityData BuildFlatAbility(JObject skillObj, string fallbackId)
    {
        if (skillObj == null) return null;

        string name = skillObj["name"]?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return null;

        string abilityId = skillObj["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(abilityId)) abilityId = fallbackId;

        return new AbilityData
        {
            abilityId   = abilityId,
            abilityName = name,
            abilityDesc = skillObj["desc"]?.ToString(),
            targetType  = TargetType.AllAllies,
            targetArea  = TargetArea.AOE,
            levels = new List<AbilityLevel>
            {
                new AbilityLevel
                {
                    bcCost     = 0,
                    damageRate = 0,
                    effects    = ParseEffects(skillObj["effects"] as JArray)
                }
            }
        };
    }

    /// <summary>Deduplicates across units (abilities are shared); returns the ability id, or null.</summary>
    private string EmitAbility(AbilityData ability)
    {
        if (ability == null) return null;

        if (_emittedAbilities.TryGetValue(ability.abilityId, out AbilityData existing))
        {
            if (existing.abilityName != ability.abilityName)
                Debug.LogWarning($"[RawUnitParser] Ability id '{ability.abilityId}' reused by different names: " +
                                 $"'{existing.abilityName}' vs '{ability.abilityName}' — keeping the first.");
            return ability.abilityId;
        }

        _emittedAbilities[ability.abilityId] = ability;
        return ability.abilityId;
    }

    /// <summary>Verifies every particle id in an ability exists in the Particles index.</summary>
    private void VerifyParticleRefs(AbilityData ability, string ownerId)
    {
        if (ability.effectFrames == null) return;
        foreach (EffectFrameData ef in ability.effectFrames)
        {
            if (string.IsNullOrEmpty(ef.particleEffectId)) continue;
            if (!_particleIds.Contains(ef.particleEffectId))
            {
                MissingParticles++;
                Debug.LogWarning($"[RawUnitParser {ownerId}] No ParticleEffect found for id '{ef.particleEffectId}' " +
                                 $"(ability '{ability.abilityName}').");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  ANIMATION / SPRITE RESOLUTION (paths, existence-verified)
    // ─────────────────────────────────────────────────────────────

    private void ResolveAnimationData(UnitData unit, string unitId)
    {
        string animId = GetAnimationId(unitId);

        string cggAbs = $"{Application.dataPath}/{cggFolderPath}/unit_cgg_{animId}.csv";
        if (File.Exists(cggAbs))
        {
            unit.animationType = AnimationType.CGG;
            unit.cggPath = DataDrivenAddresses.Cgg(animId);
            if (File.Exists($"{Application.dataPath}/{cgsFolderPath}/unit_idle_cgs_{animId}.csv"))
                unit.idleCgsPath = DataDrivenAddresses.Cgs("idle", animId);
            if (File.Exists($"{Application.dataPath}/{cgsFolderPath}/unit_move_cgs_{animId}.csv"))
                unit.moveCgsPath = DataDrivenAddresses.Cgs("move", animId);
            if (File.Exists($"{Application.dataPath}/{cgsFolderPath}/unit_atk_cgs_{animId}.csv"))
                unit.attackCgsPath = DataDrivenAddresses.Cgs("atk", animId);

            if (File.Exists($"{Application.dataPath}/{spriteFolderPath}/unit_anime_{unitId}.png"))
                unit.spriteSheetPaths = new List<string> { DataDrivenAddresses.SpriteSheet(unitId) };
        }
        else
        {
            string samRel = $"{samRootFolderPath}/unit_{unitId}/unit_anime_{unitId}.json";
            if (File.Exists($"{Application.dataPath}/{samRel}"))
            {
                unit.animationType = AnimationType.SAM;
                unit.samPath = DataDrivenAddresses.Sam(unitId);
            }
            else
            {
                MissingAnimations++;
                Debug.LogWarning($"[RawUnitParser {unitId}] No animation data found.");
                LogMissingAnimation(unitId, unit.unitName);
            }
        }

        // Illustrations: try the plain filename first, then __v2..__v9 —
        // some units ship re-releases/alt versions of their art under a
        // versioned suffix instead of the base name.
        unit.slotIconPath = ResolveSpriteWithVersions($"unit_ills_thum_{unitId}");
        unit.iconPath     = ResolveSpriteWithVersions($"unit_ills_battle_{unitId}");
        unit.fullArtPath  = ResolveSpriteWithVersions($"unit_ills_full_{unitId}");
    }

    private string ResolveSpriteWithVersions(string baseName)
    {
        if (File.Exists($"{Application.dataPath}/{spriteFolderPath}/{baseName}.png"))
            return DataDrivenAddresses.Sprite(baseName);

        for (int v = 2; v <= 9; v++)
        {
            string versioned = $"{baseName}__v{v}";
            if (File.Exists($"{Application.dataPath}/{spriteFolderPath}/{versioned}.png"))
                return DataDrivenAddresses.Sprite(versioned);
        }

        MissingSprites++;
        return null;
    }

    /// <summary>
    /// "clip.mp3" → addressable audio address. Tries .mp3/.wav/.ogg like the
    /// old importer; result cached per clip name.
    /// </summary>
    private string ResolveAudioAddress(string clipFileName)
    {
        if (string.IsNullOrEmpty(clipFileName)) return null;
        if (_audioCache.TryGetValue(clipFileName, out string cached)) return cached;

        string name = Path.GetFileNameWithoutExtension(clipFileName);
        string address = null;
        foreach (string ext in new[] { ".mp3", ".wav", ".ogg" })
        {
            if (File.Exists($"{Application.dataPath}/{soundFolderPath}/{name}{ext}"))
            {
                address = DataDrivenAddresses.Sound(name);
                break;
            }
        }

        if (address == null)
        {
            MissingAudio++;
            Debug.LogWarning($"[RawUnitParser] No audio clip found for '{clipFileName}'.");
        }

        return _audioCache[clipFileName] = address;
    }

    private void LogMissingAnimation(string unitId, string unitName)
    {
        const string missingFile = "Assets/Temp/MissingUnitAnimations.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(missingFile)!);
        File.AppendAllText(missingFile, $"{unitId}\t{unitName}{Environment.NewLine}");
    }

    // ─────────────────────────────────────────────────────────────
    //  EFFECT PAYLOAD PARSING (ported 1:1 from JsonToSOUnit)
    // ─────────────────────────────────────────────────────────────

    private static List<Effect> ParseEffects(JArray arr)
    {
        List<Effect> list = new();
        if (arr == null) return list;
        foreach (JToken t in arr)
            if (t is JObject obj) list.Add(ParseEffect(obj));
        return list;
    }

    private static Effect ParseEffect(JObject obj)
    {
        bool isPassive = obj["passive id"] != null;

        return new Effect
        {
            procId           = obj["proc id"]?.ToString() ?? "",
            passiveId        = obj["passive id"]?.ToString() ?? "",
            isPassive        = isPassive,
            targetType       = ParseTargetType(obj["target type"]?.ToString()),
            targetArea       = ParseTargetArea(obj["target area"]?.ToString()),
            effectDelayFrame = obj["effect delay time(ms)/frame"]?.ToString(),
            conditions       = ParseConditions(obj["conditions"] as JArray),

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
            conditionalBuff = ParseConditionalBuff(obj, isPassive ? obj["passive id"]?.ToString() : obj["proc id"]?.ToString()),
            ailmentInflict  = ParseAilmentInflict(obj),
            ailmentResist   = ParseAilmentResist(obj),
            extraAction     = ParseExtraAction(obj)
        };
    }

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
        atkBuff       = ParseInt(o["atk% buff (1)"]) + ParseInt(o["atk% buff"]),
        defBuff       = ParseInt(o["def% buff (3)"]) + ParseInt(o["def% buff"]),
        recBuff       = ParseInt(o["rec% buff (5)"]) + ParseInt(o["rec% buff"]),
        hpBuff        = ParseInt(o["hp% buff"]),
        critBuff      = ParseInt(o["crit% buff (7)"]) + ParseInt(o["crit% buff"]),
        buffTurns     = ParseInt(o["buff turns"]),
        elementBuffed = o["element buffed"]?.ToString(),
        elementsBuffed= (o["elements buffed"] as JArray)
                            ?.Select(t => t.ToString()).ToList() ?? new List<string>(),
        fireResist    = ParseInt(o["fire resist%"]),
        waterResist   = ParseInt(o["water resist%"]),
        earthResist   = ParseInt(o["earth resist%"]),
        thunderResist = ParseInt(o["thunder resist%"]),
        lightResist   = ParseInt(o["light resist%"]),
        darkResist    = ParseInt(o["dark resist%"])
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
            buffTurns             = ParseInt(o["buff turns"]),
            elementBuffed         = o["element buffed"]?.ToString(),
            inflictAtkDebuff      = ParseInt(o["inflict atk% debuff (2)"]),
            inflictAtkDebuffChance= ParseInt(o["inflict atk% debuff chance% (74)"]),
            inflictDefDebuff      = ParseInt(o["inflict def% debuff (4)"]),
            inflictDefDebuffChance= ParseInt(o["inflict def% debuff chance% (75)"]),
            inflictRecDebuff      = ParseInt(o["inflict rec% debuff (6)"]),
            inflictRecDebuffChance= ParseInt(o["inflict rec% debuff chance% (76)"]),
            statDebuffTurns       = ParseInt(o["stat% debuff turns"])
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
            sparkDmgBuff                = ParseInt(o["spark dmg% buff (40)"])
                                        + ParseInt(o["spark dmg inc% buff"]),
            sparkDmgBuffTurns           = ParseInt(o["buff turns"])
                                        + ParseInt(o["spark dmg inc buff turns (131)"]),
            sparkDmgIncChance           = ParseInt(o["spark dmg inc chance%"]),
            critMultiplier              = ParseInt(o["crit multiplier%"]),
            critBuffTurns               = ParseInt(o["buff turns (84)"]),
            elementalWeaknessMultiplier = ParseFloat(o["elemental weakness multiplier%"]),
            elementalWeaknessTurns      = ParseInt(o["elemental weakness buff turns"]),
            fireDoesExtraElementalDmg   = ParseBool(o["fire units do extra elemental weakness dmg"]),
            waterDoesExtraElementalDmg  = ParseBool(o["water units do extra elemental weakness dmg"]),
            earthDoesExtraElementalDmg  = ParseBool(o["earth units do extra elemental weakness dmg"]),
            thunderDoesExtraElementalDmg= ParseBool(o["thunder units do extra elemental weakness dmg"]),
            lightDoesExtraElementalDmg  = ParseBool(o["light units do extra elemental weakness dmg"]),
            darkDoesExtraElementalDmg   = ParseBool(o["dark units do extra elemental weakness dmg"]),
            dmgMitigationPercent        = ParseInt(o["dmg% mitigation"])
                                        + ParseInt(o["dmg% reduction"]),
            dmgMitigationTurns          = ParseInt(o["dmg% reduction turns (36)"]),
            mitigateFire                = o["mitigate fire attacks (21)"] != null
                                       || o["mitigate fire attacks"] != null,
            mitigateWater               = o["mitigate water attacks (22)"] != null
                                       || o["mitigate water attacks"] != null,
            mitigateEarth               = o["mitigate earth attacks"] != null,
            mitigateThunder             = o["mitigate thunder attacks"] != null,
            mitigateLight               = o["mitigate light attacks (25)"] != null
                                       || o["mitigate light attacks"] != null,
            mitigateDark                = o["mitigate dark attacks (26)"] != null
                                       || o["mitigate dark attacks"] != null,
            defenseIgnorePercent        = ParseInt(o["defense% ignore"]),
            defenseIgnoreTurns          = ParseInt(o["defense% ignore turns (39)"])
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
            sub.atkBuff             = ParseInt(buff["atk% buff (1)"]);
            sub.defBuff             = ParseInt(buff["def% buff (3)"]);
            sub.bbAtkBuff           = ParseInt(buff["bb atk% buff"]);
            sub.sbbAtkBuff          = ParseInt(buff["sbb atk% buff"]);
            sub.ubbAtkBuff          = ParseInt(buff["ubb atk% buff"]);
            sub.sparkDmgBuff        = ParseInt(buff["spark dmg% buff"]);
            sub.dmgReductionBuff    = ParseInt(buff["dmg reduction% buff"]);
            sub.gradualHealHigh     = ParseInt(buff["gradual heal high"]);
            sub.gradualHealLow      = ParseInt(buff["gradual heal low"]);
            sub.odFillRate          = ParseInt(buff["od fill rate% buff"]);
            sub.hpDrainChance       = ParseInt(buff["hp drain chance%"]);
            sub.hpDrainHigh         = ParseInt(buff["hp drain% high"]);
            sub.hpDrainLow          = ParseInt(buff["hp drain% low"]);
            sub.sparkDmgInc         = ParseInt(buff["spark dmg inc%"]);
            sub.bcFillOnSparkHigh   = ParseInt(buff["bc fill on spark high"]);
            sub.bcFillOnSparkLow    = ParseInt(buff["bc fill on spark low"]);
            sub.bcFillOnSparkPercent= ParseFloat(buff["bc fill on spark%"]);
            sub.gradualBcFill       = ParseInt(buff["increase bb gauge gradual buff"]);
            sub.elementBuffed       = buff["element buffed"]?.ToString();
            sub.buffTurns           = ParseInt(buff["buff turns (1)"])
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

    private static List<EffectCondition> ParseConditions(JArray arr)
    {
        List<EffectCondition> list = new();
        if (arr == null) return list;

        foreach (JToken t in arr)
        {
            if (t is not JObject o) continue;

            EffectCondition c = new();

            if (o["hp above % buff requirement"] != null)
            {
                c.conditionType      = ConditionType.HPAbove;
                c.hpThresholdPercent = ParseFloat(o["hp above % buff requirement"]);
            }
            else if (o["hp below % buff requirement"] != null)
            {
                c.conditionType      = ConditionType.HPBelow;
                c.hpThresholdPercent = ParseFloat(o["hp below % buff requirement"]);
            }
            else if (o["item required"] is JArray items)
            {
                c.conditionType = ConditionType.ItemRequired;
                c.itemsRequired = items.Select(i => i.ToString()).ToList();
            }
            else if (o["elements required"] is JArray elems)
            {
                c.conditionType    = ConditionType.ElementRequired;
                c.elementsRequired = elems.Select(e => e.ToString()).ToList();
            }
            else if (o["gender required"] != null)
            {
                c.conditionType  = ConditionType.GenderRequired;
                c.genderRequired = o["gender required"].ToString();
            }
            else if (o["bb gauge above % buff requirement"] != null)
            {
                c.conditionType    = ConditionType.BBGaugeAbove;
                c.bbGaugeThreshold = ParseInt(o["bb gauge above % buff requirement"]);
            }
            else
            {
                c.conditionType = ConditionType.Always;
            }

            list.Add(c);
        }

        return list;
    }

    // ─────────────────────────────────────────────────────────────
    //  VALUE HELPERS (ported 1:1 from JsonToSOUnit)
    // ─────────────────────────────────────────────────────────────

    private static UnitDisplay ParseUnitDisplay(string raw)
    {
        var result = new UnitDisplay();
        if (string.IsNullOrEmpty(raw)) return result;

        string[] parts = raw.Split(',');
        if (parts.Length > 0) float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.x);
        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.y);
        if (parts.Length > 2) float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.width);
        if (parts.Length > 3) float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.height);
        if (parts.Length > 4) float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result.other);
        return result;
    }

    private static UnitStats ParseUnitStats(JObject o)
    {
        if (o == null) return new UnitStats();
        return new UnitStats
        {
            hp     = ParseInt(o["hp"]),
            hpMin  = ParseInt(o["hp min"]),
            hpMax  = ParseInt(o["hp max"]),
            atk    = ParseInt(o["atk"]),
            atkMin = ParseInt(o["atk min"]),
            atkMax = ParseInt(o["atk max"]),
            def    = ParseInt(o["def"]),
            defMin = ParseInt(o["def min"]),
            defMax = ParseInt(o["def max"]),
            rec    = ParseInt(o["rec"]),
            recMin = ParseInt(o["rec min"]),
            recMax = ParseInt(o["rec max"]),
        };
    }

    private static Vector2 ParseVector2(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return Vector2.zero;

        string[] parts = raw.Split(',');
        float x = 0f, y = 0f;
        if (parts.Length > 0) float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
        return new Vector2(x, y);
    }

    private static List<HitFrame> ParseHitFramesFromString(string frames, string dist)
    {
        List<HitFrame> list = new();
        if (string.IsNullOrEmpty(frames) || string.IsNullOrEmpty(dist)) return list;
        string[] f = frames.Split(',');
        string[] d = dist.Split(',');
        for (int i = 0; i < Mathf.Min(f.Length, d.Length); i++)
            list.Add(new HitFrame { frame = ParseInt(f[i].Trim()), damagePercent = ParseInt(d[i].Trim()) });
        return list;
    }

    private static List<HitFrame> ParseHitFramesFromArrays(JArray frames, JArray dist)
    {
        List<HitFrame> list = new();
        if (frames == null || dist == null) return list;
        int count = Mathf.Min(frames.Count, dist.Count);
        for (int i = 0; i < count; i++)
            list.Add(new HitFrame { frame = ParseInt(frames[i]), damagePercent = ParseInt(dist[i]) });
        return list;
    }

    private static int ParseInt(JToken t)
    {
        if (t == null) return 0;
        string s = t.ToString().Trim();
        if (int.TryParse(s, out int i))     return i;
        if (float.TryParse(s, out float f)) return Mathf.RoundToInt(f);
        return 0;
    }

    private static float ParseFloat(JToken t)
    {
        if (t == null) return 0f;
        float.TryParse(t.ToString().Trim(), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float f);
        return f;
    }

    private static bool ParseBool(JToken t)
    {
        if (t == null) return false;
        if (t.Type == JTokenType.Boolean) return t.Value<bool>();
        return t.ToString().Trim().ToLowerInvariant() == "true";
    }

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

    private static UnitRarity ParseRarity(string r) => r switch
    {
        "Omni"      => UnitRarity.OMNI,
        "★★★★★★★" => UnitRarity.SEVEN,
        "★★★★★★"  => UnitRarity.SIX,
        "★★★★★"   => UnitRarity.FIVE,
        "★★★★"    => UnitRarity.FOUR,
        "★★★"     => UnitRarity.THREE,
        "★★"      => UnitRarity.TWO,
        "★"       => UnitRarity.ONE,
        _           => UnitRarity.ONE
    };

    private static string GetAnimationId(string unitId) => unitId switch
    {
        "10202" or "20202" or "30202" or "40202" or "50202" => "60132",
        "10203" or "20203" or "30203" or "40203" or "50203" => "60133",
        "10204" or "20204" or "30204" or "40204" or "50204" => "60134",
        _ => unitId
    };
}
