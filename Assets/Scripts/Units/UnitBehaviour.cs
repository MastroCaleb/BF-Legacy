using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UnitBehaviour : MonoBehaviour
{
    public Unit unitData;
    public UnitInventoryData inventoryData;

    //UBB
    public bool isInOverdrive = false;

    //Sounds
    public AudioClip action;
    public AudioClip skillAction;
    public AudioClip deathSound;
    public AudioClip bossDeathSound;
    public AudioClip criticalSound;
    public AudioClip sparkSound;

    public UnitState currentState = UnitState.Idle;
    public RectTransform originalPosition;
    public int currentHealth;
    public bool isLeader;
    public BraveFrontierFrameAnimator animatorCGG;
    public SamAnimator animatorSAM;
    public UnitSlotUI unitSlotUI;
    public bool isGuarding;

    //Enemy-specific
    public bool isEnemyUnit = false;
    public bool isBoss = false;
    public bool isMimicChest = false;
    public Enemy enemyData;
    public Enemy mimicSourceEnemyData;
    public bool deathSequenceComplete = false;

    // Spark
    public int sparkCount = 0;
    public bool lastHitSparked = false; // set by TakeDamage, read by the attacker's drop rolls
    public static int sparkInstances = 0;
    private const int MAX_SPARK_SAMS = 25;

    public UnitBuffs buffs = new UnitBuffs();
    public List<Effect> activeEffects => buffs.Effects();   // read-only view for existing readers
    private BuffSource castSource = BuffSource.None;
    
    [Header("Particle Effects")]
    [Tooltip("Parent for spawned battle particle effects. Falls back to BattleUI.dropsLayer if unassigned.")]
    public Transform effectsLayer;

    // Ailments — tracked separately from timedEffects because procs 11/40 can
    // inflict several distinct ailments from the SAME procId, which would
    // otherwise collide under the single-entry-per-procId AddTimedEffect scheme.
    public HashSet<string> activeAilments = new HashSet<string>();
    private Dictionary<string, int> ailmentTurns = new Dictionary<string, int>();

    // Standard Brave Frontier ailment magnitudes. These aren't encoded anywhere
    // in the data model, so they're hardcoded here — tune freely.
    private const int AILMENT_STAT_DEBUFF_PERCENT = 50; // Weaken/Injury/Sick reduce the relevant stat by 50%
    private const float POISON_DAMAGE_PERCENT = 25f;     // Poison deals 25% max HP per turn

    // UI
    public RectTransform unitCanvas;
    public List<GameObject> icons = new List<GameObject>();

    // Bars
    public Slider healthBar;
    public Slider superBar;
    public int bcCount;

    // Stat Icons
    public GameObject statIconPrefab;
    public float distance = 0.5f;
    public Sprite statUpSprite;
    public Sprite statDownSprite;

    // Unit Slot UI
    bool checkSlotUI = true;

    //Death
    int deathHitCount = 0;

    // Extra action (proc 76) — BattleManager should check and consume this
    // after a unit's turn resolves to grant an additional action.
    public bool extraActionPending = false;

    // ─────────────────────────────────────────────────────────────
    //  Timed effect wrapper — tracks remaining turns without
    //  mutating the Effect ScriptableObject.
    // ─────────────────────────────────────────────────────────────
    private class ActiveEffectEntry
    {
        public Effect effect;
        public int    remainingTurns; // -1 = permanent (leader / passive)

        public ActiveEffectEntry(Effect e, int turns)
        {
            effect         = e;
            remainingTurns = turns;
        }
    }

    private List<ActiveEffectEntry> timedEffects = new List<ActiveEffectEntry>();

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (GetComponent<BuffIconDisplay>() == null) gameObject.AddComponent<BuffIconDisplay>().Init(this);
        if (unitData != null)
        {
            currentHealth = isEnemyUnit ? enemyData.health == 0 ? unitData.maxHealth : enemyData.health : unitData.maxHealth + inventoryData.hpLevelUpBonus + inventoryData.hpImpBonus;
            ComposeUnit();
        }
        else
        {
            Debug.LogError("Unit data not assigned!");
        }
        StartCoroutine(SparkCounter());
    }

    void Update()
    {
        if (unitSlotUI != null && !isEnemyUnit && checkSlotUI)
        {
            currentHealth = unitData.maxHealth + inventoryData.hpLevelUpBonus + inventoryData.hpImpBonus;
            unitSlotUI.UpdateUI();
            checkSlotUI = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(!isEnemyUnit) return;

        if(BattleManager.selectedEnemyUnit == this)
        {
            BattleManager.selectedEnemyUnit = null;
            BattleUI.enemySelect.SetActive(false);
        }
        else
        {
            BattleManager.selectedEnemyUnit = this;
            BattleUI.enemySelect.SetActive(true);
            BattleUI.enemySelect.GetComponent<RectTransform>().position = GetComponent<RectTransform>().position + new Vector3(0, 0.5f, 0);
        }

        Debug.Log($"Selected {unitData.unitName}");
    }

    public void ComposeUnit()
    {
        if (unitData.animationType == AnimationType.CGG)
        {
            GetComponent<SamAnimator>().enabled = false;
            animatorCGG = GetComponent<BraveFrontierFrameAnimator>();
            animatorCGG.spriteSheets  = unitData.spriteSheets;
            animatorCGG.cggFile       = unitData.cggFile;
            animatorCGG.idleCgsFile   = unitData.idleCgsFile;
            animatorCGG.moveCgsFile   = unitData.moveCgsFile;
            animatorCGG.attackCgsFile = unitData.attackCgsFile;
            animatorCGG.InitializeAnimator();
        }
        else
        {
            GetComponent<BraveFrontierFrameAnimator>().enabled = false;
            GetComponent<Image>().enabled = false;
            animatorSAM          = GetComponent<SamAnimator>();
            animatorSAM.enabled  = true;
            animatorSAM.jsonFile = unitData.samFile;
            animatorSAM.InitializeAnimator();
        }

        currentHealth = isEnemyUnit ? enemyData.health == 0 ? unitData.maxHealth : enemyData.health : unitData.maxHealth + inventoryData.hpLevelUpBonus + inventoryData.hpImpBonus;

        SetupClickZone();

        FlipImage(false);
    }

    private void SetupClickZone()
    {
        Image img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;

        GameObject clickZoneGO = new GameObject("ClickZone");
        clickZoneGO.transform.SetParent(transform, false);

        RectTransform rt = clickZoneGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(50f, 50f);
        rt.anchoredPosition = new Vector2(0, -30);

        NonDrawingGraphic ndg = clickZoneGO.AddComponent<NonDrawingGraphic>();
        ndg.raycastTarget = true;

        // Sposta il listener click qui
        ClickReceiver receiver = clickZoneGO.AddComponent<ClickReceiver>();
        receiver.owner = this;
    }

    IEnumerator SparkCounter()
    {
        yield return new WaitForSeconds(1f / 60f);
        sparkCount = 0;
        StartCoroutine(SparkCounter());
    }

    // ─────────────────────────────────────────────────────────────
    //  Ability selection
    // ─────────────────────────────────────────────────────────────

    public void ActivateLeaderAbility(List<UnitBehaviour> targets)
    {
        UseAbility(unitData.leaderAbility, targets, permanent: true);
    }

    public Ability GetAbilityToUse(CutInAnimation cutInAnimator)
    {
        int level = inventoryData.currentBBLevel-1;

        if (unitData.bbAbility != null && unitData.bbAbility.abilityName != "Unnamed" && bcCount >= unitData.bbAbility.levels[level].bcCost)
        {
            if (!isEnemyUnit) cutInAnimator.PlayCutIn(unitData.unitFullArt, unitData.bbAbility.abilityName, CutInType.Normal);
            SoundManager.Instance.PlaySound(skillAction);
            return unitData.bbAbility;
        }
        if (unitData.sbbAbility != null && unitData.sbbAbility.abilityName != "Unnamed" && bcCount >= (unitData.bbAbility != null ? unitData.bbAbility.levels[inventoryData.currentBBLevel - 1].bcCost : 0) + unitData.sbbAbility.levels[inventoryData.currentSBBLevel - 1].bcCost)
        {
            if (!isEnemyUnit) cutInAnimator.PlayCutIn(unitData.unitFullArt, unitData.sbbAbility.abilityName, CutInType.SBB);
            SoundManager.Instance.PlaySound(skillAction);
            return unitData.sbbAbility;
        }
        if (unitData.ubbAbility != null && isInOverdrive && unitData.ubbAbility.abilityName != "Unnamed" && bcCount >= unitData.ubbAbility.levels[0].bcCost)
        {
            if (!isEnemyUnit) cutInAnimator.PlayCutIn(unitData.unitFullArt, unitData.ubbAbility.abilityName, CutInType.Normal);
            SoundManager.Instance.PlaySound(skillAction);
            return unitData.ubbAbility;
        }
        SoundManager.Instance.PlaySound(action);
        return unitData.basicAbility;
    }

    public Ability GetBaseAbility() => unitData.basicAbility;

    public (Ability, CutInType) GetBBAbilityToUse()
    {
        if (unitData.bbAbility  != null && unitData.bbAbility.abilityName != "Unnamed" && bcCount >= unitData.bbAbility.levels[inventoryData.currentBBLevel - 1].bcCost)return (unitData.bbAbility, CutInType.Normal); 
        if (unitData.sbbAbility != null && unitData.sbbAbility.abilityName != "Unnamed" && bcCount >= unitData.sbbAbility.levels[inventoryData.currentSBBLevel - 1].bcCost)return (unitData.sbbAbility, CutInType.SBB);
        if (unitData.ubbAbility != null && unitData.ubbAbility.abilityName != "Unnamed" && bcCount >= unitData.ubbAbility.levels[0].bcCost) return (unitData.ubbAbility, CutInType.Normal);
        return (unitData.basicAbility, CutInType.Normal);
    }

    public List<Action> GetEnemyActions()
    {
        List<Action> actions = new List<Action>();
        int randomActionCount = Random.Range(enemyData.actCounts.min, enemyData.actCounts.max+1);

        for(int i=0; i<randomActionCount; i++)
        {
            int randomAction = Random.Range(0, enemyData.actions.Count);

            //actions.Add(enemyData.actions[randomAction]); do this when Action and Skill system is fully in the game
            actions.Add(new Action()
            {
                priority = 1,
                actionType = ActionType.Attack,
                percentChance = 100f,
                targetType = ActionTargetType.EnemyParty,
                targetCondition = TargetCondition.Random
            });
        }

        return actions;
    }

    // ─────────────────────────────────────────────────────────────
    //  UseAbility
    //  Iterates MaxLevel effects and dispatches per procId.
    //  Attack damage is handled separately via AttackAtHitFrames.
    //  Pass permanent = true for leader abilities (no expiry).
    // ─────────────────────────────────────────────────────────────

    public void UseAbility(Ability ability, List<UnitBehaviour> targets, bool permanent = false)
    {
        Debug.Log($"[UseAbility] {unitData.unitName} uses {ability.abilityName} on {string.Join(", ", targets.ConvertAll(t => t.unitData.unitName))}");

        StartCoroutine(PlayAbilityEffectFrames(ability, targets));

        castSource = BuffCalc.SourceFor(this, ability, permanent);

        if(ability.abilityName.Contains("Normal Attack"))
        {
            foreach (var t in targets) StartCoroutine(AttackAtHitFrames(ability, t));
            return;
        }

        if (ability == unitData.bbAbility || ability == unitData.sbbAbility || ability == unitData.ubbAbility) bcCount = 0;

        if (ability?.levels[0]?.effects == null)
        {
            if (!isEnemyUnit) unitSlotUI?.UpdateUI();
            return;
        }

        foreach (var effect in ability.levels[0].effects)
        {
            // Create composite key: "ID_PASSIVE/ACTIVE" to distinguish same ID used for different effects
            string effectId = effect.isPassive ? effect.passiveId : effect.procId;
            if (string.IsNullOrEmpty(effectId)) continue;

            string effectKey = $"{effectId}_{(effect.isPassive ? "P" : "A")}";

            switch (effectKey)
            {
                // ═══════════════════════════════════════════════════════════
                //  PROC 1: Regular Damage (Active) vs Parameter Boost (Passive)
                // ═══════════════════════════════════════════════════════════
                case "1_A":
                    // Non-passive proc 1: Regular Damage
                    if (effect.attack != null)
                    {
                        foreach (var t in targets) StartCoroutine(AttackAtHitFrames(ability, t));
                    }
                    break;

                case "5_A":
                case "1_P":
                    // Passive proc 1: Stat buff (atk/crit/def/hp/rec)
                    if (effect.statBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.statBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 2: Burst Healing (Active)
                // ═══════════════════════════════════════════════════════════
                case "2_A":
                    foreach (var t in targets) t.BurstHeal(ability, this);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 3: Gradual heal (HoT)
                // ═══════════════════════════════════════════════════════════
                case "3_A":
                case "33_P":
                    if (effect.gradualHeal != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.gradualHeal.turns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 4: BB Gauge Refill (Active) vs Ailment resistance (Passive)
                // ═══════════════════════════════════════════════════════════
                case "4_A":
                    if (effect.bcFill != null)
                    {
                        foreach (var t in targets)
                        {
                            if (!t.CanGainBC()) continue; // Curse blocks BC fill
                            t.bcCount += BuffCalc.BcFillAmount(t, effect.bcFill);
                            t.bcCount = Mathf.Max(t.bcCount, 0);
                            Debug.Log($"[BC Fill] {t.unitData.unitName} BC → {t.bcCount}");
                        }
                    }
                    break;
                case "4_P":
                    // Passive proc 4: Ailment resistance — uses dedicated ailmentResist struct
                    if (effect.ailmentResist != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.ailmentResist.immunityBuffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                //Elemental resistance
                case "5_P":
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 6: Drop-rate buff (Active only)
                // ═══════════════════════════════════════════════════════════
                case "6_A":
                case "19_P":
                    if (effect.dropRate != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.dropRate.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 7: Guaranteed KO Resistance (Active only)
                // ═══════════════════════════════════════════════════════════
                case "7_A":
                case "69_P":
                    if (effect.damageBuff?.angelIdolSubBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.angelIdolSubBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 8: Max HP Boost / Shield (Active) vs DMG mitigation (Passive)
                // ═══════════════════════════════════════════════════════════
                case "8_A":
                    if (effect.shield != null)
                    {
                        foreach (var t in targets)
                        {
                            int hpGain = Mathf.CeilToInt(t.unitData.maxHealth * PercentageToNumber(effect.shield.maxHPIncreasePercent));
                            t.currentHealth += hpGain;
                            Debug.Log($"[HP Up] {t.unitData.unitName} gained {hpGain} max HP");
                        }
                    }
                    break;


                case "8_P":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.dmgMitigationTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 9: Parameter Reduction (Active) vs BC fill per turn (Passive)
                // ═══════════════════════════════════════════════════════════
                case "9_A":
                    if (effect.statDebuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.statDebuff.buffTurns, 1);
                        // buff1 and buff2 are independent rolls (e.g. ATK down / DEF down).
                        // Both use AddTimedEffect keyed by procId, so only one can be tracked
                        // per target at a time under the current single-entry-per-procId scheme —
                        // buff1 takes priority if both succeed. Flag this if you need both to
                        // stack simultaneously; that needs a keying change in AddTimedEffect.
                        bool applied = false;
                        if (effect.statDebuff.buff1 != null && Random.Range(0f, 100f) <= effect.statDebuff.buff1.procChance)
                        {
                            foreach (var t in targets) AddTimedEffect(t, effect, turns);
                            applied = true;
                        }
                        if (!applied && effect.statDebuff.buff2 != null && Random.Range(0f, 100f) <= effect.statDebuff.buff2.procChance)
                        {
                            foreach (var t in targets) AddTimedEffect(t, effect, turns);
                        }
                    }
                    break;
                case "9_P":
                    // Passive proc 9: BC fill per turn
                    if (effect.bcFill != null)
                    {
                        int turns = permanent ? -1 : 3;
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 10: Status Cleanse (Active) vs HC effectiveness (Passive)
                // ═══════════════════════════════════════════════════════════
                case "10_A":
                    if (effect.status != null)
                        foreach (var t in targets) t.CureAilments(effect.status);
                    break;

                    
                case "10_P":
                    // Passive proc 10: HC effectiveness — treated like a stat/heal buff, reuses statBuff.recBuff
                    if (effect.statBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.statBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 11 / 40: Status infliction (Active) vs Ailment (Passive)
                // ═══════════════════════════════════════════════════════════
                case "11_A":
                case "40_A":
                case "20_P":
                    if (effect.ailmentInflict != null)
                    {
                        int turns = permanent ? -1 : 3;
                        //foreach (var t in targets) InflictAilment(t, effect.ailmentInflict, turns);
                    }
                    break;
                case "11_P":
                    // Passive proc 11: Stat buff
                    if (effect.statBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.statBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 12: Revive (Active — instant, distinct from passive proc 66 auto-revive)
                // ═══════════════════════════════════════════════════════════
                case "12_A":
                    if (effect.revive != null)
                    {
                        foreach (var t in targets)
                        {
                            if (t.currentState != UnitState.Dead) continue;
                            int hp = Mathf.CeilToInt(t.unitData.maxHealth * PercentageToNumber(effect.revive.reviveHPPercent));
                            t.currentHealth = Mathf.Max(hp, 1);
                            t.currentState  = UnitState.Idle;
                            //Should also undo death shake and xy dimensions and restore unit animations to idle
                            Debug.Log($"[Revive] {t.unitData.unitName} revived with {t.currentHealth} HP");
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 13: Random Target Damage (Active) vs BC fill on defeat (Passive)
                // ═══════════════════════════════════════════════════════════
                case "13_A":
                    if (effect.attack != null)
                    {
                        int hitCount = Mathf.Max(effect.attack.hitCount, 1);
                        StartCoroutine(RepeatRandomAttack(ability, targets, hitCount));
                    }
                    break;


                case "13_P":
                    // Passive proc 13: BC fill on defeating an enemy
                    if (effect.bcFill != null)
                    {
                        int turns = permanent ? -1 : 1;
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 14: Lifesteal Damage (Active) vs DMG reduction chance (Passive)
                // ═══════════════════════════════════════════════════════════
                case "14_A":
                    // Runs the normal hit-frame timeline; TryHPDrainOnHit (called from Attack(),
                    // which AttackAtHitFrames invokes per hit frame) applies the hpDrainHigh/Low
                    // lifesteal automatically once damage lands.
                    if (effect.attack != null)
                        foreach (var t in targets) StartCoroutine(AttackAtHitFrames(ability, t));
                    break;
                case "14_P":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.dmgMitigationTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 16 Elemental Mitigation / 18 Normal Mitigation / 39: Damage mitigation (Active)
                // ═══════════════════════════════════════════════════════════
                case "16_A":
                case "18_A":
                case "39_A":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.dmgMitigationTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 17: Status Negation (Active) — uses dedicated ailmentResist struct
                // ═══════════════════════════════════════════════════════════
                case "17_A":
                    if (effect.ailmentResist != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.ailmentResist.immunityBuffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 19: Gradual BB gauge fill (Active) vs Drop rate buff (Passive)
                // ═══════════════════════════════════════════════════════════
                case "19_A":
                    if (effect.bcFill != null)
                    {
                        int turns = (int)(permanent ? -1 : Mathf.Max(effect.bcFill.bbGaugeFillRate, 1));
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;
                // ═══════════════════════════════════════════════════════════
                //  PROC 20: BC fill when attacked (Active) vs Status ailments (Passive)
                // ═══════════════════════════════════════════════════════════
                case "20_A":
                    if (effect.bcFill != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.bcFill.bcFillWhenAttackedTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 22: Ignore Defense (Active only)
                // ═══════════════════════════════════════════════════════════
                case "22_A":
                case "29_P":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.defenseIgnoreTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 23: Spark Boost (Active only)
                // ═══════════════════════════════════════════════════════════
                case "23_A": // spark drop boosts travel in effect.dropRate, summed by BuffCalc.DropBonus
                case "31_P":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.sparkDmgBuffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 24: Parameter Conversion — X% of one attribute added to
                //  ATK / DEF / REC (effect.conversion, read by BuffCalc.ConversionBonus).
                // ═══════════════════════════════════════════════════════════
                case "24_A":
                case "40_P":
                    if (effect.conversion != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.conversion.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 26: Hit Count Boost — no dedicated field exists for
                //  "additional hit count"; hitFrames on the Ability itself is
                //  what currently drives hit count, so this can't be applied
                //  as a standalone buff without a new field (e.g. an
                //  "additionalHits" int). Leaving unimplemented rather than
                //  guessing a field name.
                // ═══════════════════════════════════════════════════════════
                case "26_A":
                case "37_P":
                    Debug.LogWarning("[UseAbility] Proc 26 (Hit Count Boost) needs a dedicated field — not implemented.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 27: Proportional Damage — % of target's current/max HP.
                //  Reusing AttackEffect.hpDamageHigh/hpDamageLow/hpDamageChance,
                //  the only fields shaped like "percent HP damage".
                // ═══════════════════════════════════════════════════════════
                case "27_A":
                    if (effect.attack != null)
                        foreach (var t in targets) ProportionalDamage(t, effect.attack, allowLethal: true);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 28: Fixed Damage (Active only)
                // ═══════════════════════════════════════════════════════════
                case "28_A":
                    if (effect.attack != null)
                    {
                        foreach (var t in targets)
                        {
                            int damage = effect.attack.fixedDamage;
                            t.TakeDamage(damage, false, 0, this);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 29: Multi-element Damage — deals damage checked against
                //  each element in attack.bbElements. Elemental weakness/resist
                //  multipliers aren't modeled anywhere else in this codebase yet
                //  (no per-unit element field surfaced here), so this applies
                //  the base attack once per listed element at full damage.
                // ═══════════════════════════════════════════════════════════
                case "29_A":
                    // NOTE: AttackAtHitFrames plays the ability's full hit-frame timeline once
                    // per call, so calling it once per element (as below) replays the whole
                    // animation per element rather than splitting a single timeline's damage
                    // across elements. If that's not the intended visual, this needs the
                    // per-element damage split done inside AttackAtHitFrames itself instead.
                    if (effect.attack != null && effect.attack.bbElements != null)
                        foreach (var t in targets)
                            foreach (var _ in effect.attack.bbElements)
                                StartCoroutine(AttackAtHitFrames(ability, t));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 30: Added Elements (Active) — grants the attacker extra
                //  elements for the duration of the buff, handled as a timed
                //  effect on the caster (targets should be self here).
                // ═══════════════════════════════════════════════════════════
                case "30_A":
                    if (effect.attack != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 31: Burst BB Gauge Fill (party-wide instant fill)
                // ═══════════════════════════════════════════════════════════
                case "31_A":
                    if (effect.bcFill != null)
                    {
                        foreach (var t in targets)
                        {
                            if (!t.CanGainBC()) continue;
                            t.bcCount += effect.bcFill.bbBCFill;
                            t.bcCount = Mathf.Max(t.bcCount, 0);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 32: Element Shift (self element change) — timed buff
                // ═══════════════════════════════════════════════════════════
                case "32_A":
                    foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 33: Buff Removal (strip enemy buffs)
                // ═══════════════════════════════════════════════════════════
                case "33_A":
                    foreach (var t in targets) t.RemoveAllBuffs();
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 34: BB Gauge Reduction (enemy)
                // ═══════════════════════════════════════════════════════════
                case "34_A":
                    if (effect.bcFill != null)
                    {
                        foreach (var t in targets)
                        {
                            t.bcCount -= effect.bcFill.bbBCFill;
                            t.bcCount = Mathf.Max(t.bcCount, 0);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 36: Leader Skill Lock — requires the enemy-side
                //  leader-ability-application code path (in BattleManager) to
                //  check a per-unit "leaderSkillLocked" flag before applying
                //  leader effects. Exposing the flag here; BattleManager needs
                //  to consult it — no such check currently exists.
                // ═══════════════════════════════════════════════════════════
                case "36_A":
                    foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 38: Status Cure (Active only)
                // ═══════════════════════════════════════════════════════════
                case "38_A":
                    if (effect.status != null)
                        foreach (var t in targets) t.CureAilments(effect.status);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 43: Instant OD Fill — no Overdrive gauge system exists
                //  anywhere in this codebase yet (no odGauge field on Unit /
                //  UnitBehaviour). Needs that system built first.
                // ═══════════════════════════════════════════════════════════
                case "43_A":
                    Debug.LogWarning("[UseAbility] Proc 43 (OD Gauge Fill) needs an Overdrive gauge system — not implemented.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 44: Damage-over-time / DOT (Active only)
                // ═══════════════════════════════════════════════════════════
                case "44_A":
                    if (effect.dot != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.dot.turns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 45: BB ATK Boost (Active) vs Crit resist (Passive)
                // ═══════════════════════════════════════════════════════════
                case "45_A":
                    if (effect.bbAtkBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.bbAtkBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;
                case "45_P":
                    if (effect.damageBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.damageBuff.critBuffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 46: Non-Lethal Proportional Damage — same as 27 but
                //  clamped so the target survives with at least 1 HP.
                // ═══════════════════════════════════════════════════════════
                case "46_A":
                    if (effect.attack != null)
                        foreach (var t in targets) ProportionalDamage(t, effect.attack, allowLethal: false);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 47: HP-scaled Damage — damage scales with the
                //  ATTACKER's current HP ratio. bbAtkPercent doubles as the
                //  scaling coefficient here since there's no dedicated field.
                // ═══════════════════════════════════════════════════════════
                case "47_A":
                    if (effect.attack != null)
                    {
                        int maxHp = isEnemyUnit ? enemyData.health : unitData.maxHealth + inventoryData.hpLevelUpBonus + inventoryData.hpImpBonus;
                        float hpRatio = maxHp > 0 ? (float)currentHealth / maxHp : 0f;
                        foreach (var t in targets)
                        {
                            int dmg = Mathf.CeilToInt(hpRatio * PercentageToNumber(effect.attack.bbAtkPercent) * (isEnemyUnit ? enemyData.atk : unitData.atk));
                            t.TakeDamage(dmg, false, 0, this);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 48: Piercing Proportional Damage — % of HP, ignores DEF.
                // ═══════════════════════════════════════════════════════════
                case "48_A":
                    if (effect.attack != null)
                        foreach (var t in targets) ProportionalDamage(t, effect.attack, allowLethal: true);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 49: Instant Death — hpDamageChance doubles as KO chance.
                // ═══════════════════════════════════════════════════════════
                case "49_A":
                    if (effect.attack != null)
                    {
                        foreach (var t in targets)
                        {
                            if (Random.Range(0f, 100f) <= effect.attack.hpDamageChance)
                            {
                                t.currentHealth = 0;
                                t.Die();
                                Debug.Log($"[Instant Death] {t.unitData.unitName} was KO'd");
                            }
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 50: Damage Counter (reflect) — no reflect-percent field
                //  exists on any struct here, and TakeDamage now has an
                //  attacker reference so the hook point exists, but the actual
                //  reflect percentage/chance needs a new field to be correct
                //  rather than guessed. See ProcessReactiveEffects().
                // ═══════════════════════════════════════════════════════════
                case "50_A":
                case "50_P":
                    foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    Debug.LogWarning("[UseAbility] Proc 50 (Damage Counter) needs a reflect-percent field to actually reflect damage.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 51: Parameter Reduction Added to Attack (Active only)
                // ═══════════════════════════════════════════════════════════
                case "51_A":
                    if (effect.statDebuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.statDebuff.statDebuffTurns, 1);
                        foreach (var t in targets)
                        {
                            if (Random.Range(0f, 100f) <= effect.statDebuff.inflictAtkDebuffChance) AddTimedEffect(t, effect, turns);
                            if (Random.Range(0f, 100f) <= effect.statDebuff.inflictDefDebuffChance) AddTimedEffect(t, effect, turns);
                            if (Random.Range(0f, 100f) <= effect.statDebuff.inflictRecDebuffChance) AddTimedEffect(t, effect, turns);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 52: BC Efficacy (Active only)
                // ═══════════════════════════════════════════════════════════
                case "52_A":
                    if (effect.bcFill != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.bcFill.gradualBCFillTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 53: Status Counter (inflict ailment when attacked) —
                //  stored as a timed passive; the actual roll/infliction
                //  happens reactively in ProcessReactiveEffects() from TakeDamage.
                // ═══════════════════════════════════════════════════════════
                case "53_A":
                case "53_P":
                    if (effect.ailmentInflict != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 54: Critical Damage Boost (Active) vs Crit resist (Passive)
                // ═══════════════════════════════════════════════════════════
                case "54_A":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.critBuffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 55: Elemental Damage Boost (Active) vs Complex buff (Passive)
                // ═══════════════════════════════════════════════════════════
                case "55_A":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.elementalWeaknessTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;
                case "55_P":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.elementalWeaknessTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 56: Chance KO Resistance (Active only)
                // ═══════════════════════════════════════════════════════════
                case "56_A":
                    if (effect.damageBuff?.angelIdolSubBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.angelIdolSubBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 57: Drop Rate Reduction (enemy) — reuses bcFill as the
                //  only drop-rate-shaped struct available.
                // ═══════════════════════════════════════════════════════════
                case "57_A":
                    if (effect.bcFill != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 58: Spark Vulnerability (enemy takes more spark dmg)
                // ═══════════════════════════════════════════════════════════
                case "58_A":
                    if (effect.damageBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.damageBuff.sparkDmgBuffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 59: BB ATK Reduction (enemy)
                // ═══════════════════════════════════════════════════════════
                case "59_A":
                    if (effect.bbAtkBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.bbAtkBuff.buffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 61: BB-scaled Damage — damage scales with attacker's
                //  BB gauge (bcCount). No explicit coefficient field exists;
                //  bbAtkPercent is reused as the scaling coefficient.
                // ═══════════════════════════════════════════════════════════
                case "61_A":
                    if (effect.attack != null)
                    {
                        foreach (var t in targets)
                        {
                            int dmg = Mathf.CeilToInt(bcCount * PercentageToNumber(effect.attack.bbAtkPercent));
                            t.TakeDamage(dmg, false, 0, this);
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 62: Barrier
                // ═══════════════════════════════════════════════════════════
                case "62_A":
                    if (effect.shield != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 64: Consecutive Damage (Active) vs BB ATK% buff (Passive)
                // ═══════════════════════════════════════════════════════════
                case "64_A":
                case "64_P":
                    if (effect.bbAtkBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.bbAtkBuff.buffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 65: Attack Boost on Status Afflicted Foes — applied at
                //  damage-calc time in GetDamage() by checking the target's
                //  activeAilments; stored here as a timed self-buff marker.
                // ═══════════════════════════════════════════════════════════
                case "65_A":
                    foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 66: Revive (Active) vs Conditional trigger (Passive)
                // ═══════════════════════════════════════════════════════════
                case "66_A":
                    if (effect.revive != null)
                    {
                        foreach (var t in targets)
                        {
                            if (t.currentState != UnitState.Dead) continue;
                            if (Random.Range(0f, 100f) <= effect.revive.reviveChance)
                            {
                                int hp = Mathf.CeilToInt(t.unitData.maxHealth * PercentageToNumber(effect.revive.reviveHPPercent));

                                t.Heal(Mathf.Max(hp, 1), true);
                                t.currentState  = UnitState.Idle;
                                Debug.Log($"[Revive] {t.unitData.unitName} revived with {hp} HP");
                            }
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 67: BC Fill on Spark — reactive, resolved in TakeDamage
                //  via ProcessReactiveEffects(); stored here as a timed marker.
                // ═══════════════════════════════════════════════════════════
                case "67_A":
                case "67_P":
                    if (effect.bcFill != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 68: Guard Mitigation — applied in Attack() when the
                //  target isGuarding; stored here as a timed marker.
                // ═══════════════════════════════════════════════════════════
                case "68_A":
                case "68_P":
                    if (effect.damageBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.damageBuff.dmgMitigationTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 69: BC Fill on Guard — reactive, resolved in TakeDamage.
                // ═══════════════════════════════════════════════════════════
                case "69_A":
                    if (effect.bcFill != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 71: BC Efficacy Reduction (enemy)
                // ═══════════════════════════════════════════════════════════
                case "71_A":
                    if (effect.bcFill != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 73: Parameter Reduction Negation — grants immunity to
                //  further ATK/DEF/REC down; checked via GetAilmentResist-style
                //  lookup wherever stat debuffs would otherwise be applied.
                // ═══════════════════════════════════════════════════════════
                case "73_A":
                    if (effect.ailmentResist != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.ailmentResist.immunityBuffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 75: Element Squad-scaled Damage — damage scales with
                //  count of matching-element allies. No roster/element lookup
                //  is exposed on UnitBehaviour/Unit here, so this can't be
                //  computed correctly without that reference — not implemented.
                // ═══════════════════════════════════════════════════════════
                case "75_A":
                    Debug.LogWarning("[UseAbility] Proc 75 (Element Squad-scaled Damage) needs party/element lookup — not implemented.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 76: Extra Action — sets a flag for BattleManager's turn
                //  queue to check and consume after this unit's turn resolves.
                // ═══════════════════════════════════════════════════════════
                case "76_A":
                case "76_P":
                    if (effect.extraAction != null)
                    {
                        foreach (var t in targets)
                        {
                            if (Random.Range(0f, 100f) <= effect.extraAction.chance)
                                t.extraActionPending = true;
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 78: Self Parameter Boost (conditional) — reactive,
                //  stored as timed marker; trigger check happens wherever the
                //  matching condition (damage received / dealt / etc.) occurs.
                // ═══════════════════════════════════════════════════════════
                case "78_A":
                case "78_P":
                    if (effect.conditionalBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.conditionalBuff.buff?.buffTurns ?? 3, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 79: Player EXP Boost — meta-progression, no hook exists
                //  in this battle-scoped file. Needs to live in whatever system
                //  awards EXP after battle (not UnitBehaviour).
                // ═══════════════════════════════════════════════════════════
                case "79_A":
                case "79_P":
                    Debug.LogWarning("[UseAbility] Proc 79 (Player EXP Boost) belongs in the post-battle reward system, not UnitBehaviour.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 83 / 88: Spark Critical (Active)
                // ═══════════════════════════════════════════════════════════
                case "83_A":
                case "88_A":
                    if (effect.damageBuff != null)
                    {
                        int turns = permanent ? -1 : Mathf.Max(effect.damageBuff.sparkDmgBuffTurns, 1);
                        foreach (var t in targets) AddTimedEffect(t, effect, turns);
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 84: OD Gauge Fill Rate — same OD-system gap as proc 43.
                // ═══════════════════════════════════════════════════════════
                case "84_A":
                case "84_P":
                    Debug.LogWarning("[UseAbility] Proc 84 (OD Gauge Fill Rate) needs an Overdrive gauge system — not implemented.");
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 85: Heal when attacked — reactive, resolved from TakeDamage.
                // ═══════════════════════════════════════════════════════════
                case "85_A":
                case "85_P":
                    if (effect.conditionalBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 86: HP Absorption — covered automatically by
                //  TryHPDrainOnHit whenever effect.attack.hpDrainHigh/Low > 0
                //  on any landed hit, so no extra dispatch needed here beyond
                //  making sure the effect participates in a real Attack().
                // ═══════════════════════════════════════════════════════════
                case "86_A":
                    if (effect.attack != null)
                        foreach (var t in targets) StartCoroutine(AttackAtHitFrames(ability, t));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 87: Heal on Spark — reactive, resolved from TakeDamage.
                // ═══════════════════════════════════════════════════════════
                case "87_A":
                case "87_P":
                    if (effect.conditionalBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : 3);
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 89: Self Parameter Conversion (effect.conversion, same as proc 24)
                // ═══════════════════════════════════════════════════════════
                case "89_A":
                case "89_P":
                    if (effect.conversion != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.conversion.buffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 92: Self Max HP Boost
                // ═══════════════════════════════════════════════════════════
                case "92_A":
                case "92_P":
                    if (effect.shield != null)
                    {
                        foreach (var t in targets)
                        {
                            int hpGain = Mathf.CeilToInt(t.unitData.maxHealth * PercentageToNumber(effect.shield.maxHPIncreasePercent));
                            t.currentHealth += hpGain;
                        }
                    }
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 93: Damage Resistance — folded into GetDefenseBuffRate()
                //  via the "93" procId check, stored here as a timed marker.
                // ═══════════════════════════════════════════════════════════
                case "93_A":
                case "93_P":
                    if (effect.damageBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.damageBuff.dmgMitigationTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  PROC 10000: Taunt (Active) / PROC 10001: Stealth (Active)
                // ═══════════════════════════════════════════════════════════
                case "10000_A":
                case "10001_A":
                    if (effect.statBuff != null)
                        foreach (var t in targets) AddTimedEffect(t, effect, permanent ? -1 : Mathf.Max(effect.statBuff.buffTurns, 1));
                    break;

                // ═══════════════════════════════════════════════════════════
                //  Default: Log unhandled effect
                // ═══════════════════════════════════════════════════════════
                default:
                    if (!isEnemyUnit)
                        Debug.LogWarning($"Unhandled effect - ID: {effectId}, Type: {(effect.isPassive ? "PASSIVE" : "ACTIVE")}, procId: {effect.procId}, passiveId: {effect.passiveId}");
                    break;
            }
        }

        if (!isEnemyUnit) unitSlotUI?.UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    //  Effect helpers
    // ─────────────────────────────────────────────────────────────

    /// Adds a timed effect to the target, refreshing if one with the same procId already exists.
    private void AddTimedEffect(UnitBehaviour target, Effect effect, int turns)
    => target.buffs.Add(effect, castSource, turns);

    /// Helper to get the correct ID for an effect based on isPassive flag
    private string GetEffectId(Effect effect) => effect.isPassive ? effect.passiveId : effect.procId;

    /// Removes ALL timed effects and stat/damage buffs (proc 33).
    private void RemoveAllBuffs()
    {
        buffs.RemoveWhere(b => !IsAilmentProc(b.Id) && !b.IsPassive);
        Debug.Log($"[Buff Removal] {unitData.unitName} lost all active buffs");
    }

    /// Remove ailments via proc 38 (StatusEffect.removeAll or explicit list).
    private void CureAilments(StatusEffect status)
    {
        if (status.removeAll)
        {
            buffs.RemoveWhere(b => IsAilmentProc(b.Id));
            activeAilments.Clear();
            ailmentTurns.Clear();
            return;
        }
        if (status.ailmentsCured == null) return;
        foreach (var id in status.ailmentsCured)
        {
            buffs.RemoveWhere(b => b.Id == id);
            activeAilments.Remove(id);
            ailmentTurns.Remove(id);
        }
    }

    private static bool IsAilmentProc(string id) => id == "11" || id == "40";
    
    private void InflictAilment(string ailment)
    {
        if (activeAilments.Contains(ailment)) return;
        activeAilments.Add(ailment);
        ailmentTurns[ailment] = BuffCalc.StatusTurns(this, ailment);
    }

    private void TryInflictOne(UnitBehaviour target, string ailment, int chance, int turns)
    {
        if (chance <= 0) return;
        float finalChance = chance * (1f - target.GetAilmentResist(ailment) / 100f);
        if (Random.Range(0f, 100f) >= finalChance) return;
        target.InflictAilment(ailment);
    }

    private void InflictAilment(string ailment, int turns)
    {
        activeAilments.Add(ailment);
        ailmentTurns[ailment] = turns;
        Debug.Log($"[Ailment] {unitData.unitName} afflicted with {ailment} ({turns}t)");
    }

    public int GetAilmentResist(string ailment)
    {
        int resist = 0;
        foreach (var b in buffs.active)
        {
            var r = b.effect.ailmentResist;
            if (r == null) continue;
            switch (ailment)
            {
                case "Poison":    resist += r.poisonResist; break;
                case "Weaken":    resist += r.weakenResist; break;
                case "Sick":      resist += r.sickResist; break;
                case "Injury":    resist += r.injuryResist; break;
                case "Curse":     resist += r.curseResist; break;
                case "Paralysis": resist += r.paralysisResist; break;
            }
        }
        return Mathf.Clamp(resist, 0, 100);
    }

    public bool CanGainBC() => !activeAilments.Contains("Curse");
    public bool IsParalyzed() => activeAilments.Contains("Paralysis"); // BattleManager's turn queue should check this before letting the unit act

    /// Deals % of target max HP as damage (procs 27/46/48), ignoring normal DEF calc.
    private void ProportionalDamage(UnitBehaviour target, AttackEffect atk, bool allowLethal)
    {
        int maxHp = target.isEnemyUnit ? target.enemyData.health : target.unitData.maxHealth + target.inventoryData.hpLevelUpBonus + target.inventoryData.hpImpBonus;
        float pct = Random.Range(atk.hpDamageLow, atk.hpDamageHigh + 1);
        int dmg = Mathf.CeilToInt(maxHp * PercentageToNumber(pct));

        if (!allowLethal)
            dmg = Mathf.Min(dmg, target.currentHealth - 1);

        dmg = Mathf.Max(dmg, allowLethal ? 1 : 0);
        target.TakeDamage(dmg, false, 0, this);
    }

    // ─────────────────────────────────────────────────────────────
    //  TickEffects — call once per turn end for each living unit
    // ─────────────────────────────────────────────────────────────

    public void TickEffects()
    {
        // ailments: poison first
        if (activeAilments.Contains("Poison"))
        {
            int maxHp = BuffCalc.BaseStat(this, "hp");
            int dmg = Mathf.CeilToInt(maxHp * PercentageToNumber(BuffCalc.PoisonPercent));
            TakeDamage(dmg, false, 0);
        }

        foreach (var b in buffs.active.ToList())
        {
            if (b.turnsLeft < 0) continue; // passive

            if (b.Id == "44" && b.effect.dot != null)
            {
                var dot = b.effect.dot;
                int dmg = dot.flatAtk + Mathf.CeilToInt(BuffCalc.BaseStat(this, "atk") * PercentageToNumber(dot.atkPercent));
                TakeDamage(dmg, false, 0);
            }
        }
        foreach (var b in buffs.active.ToList())
        {
            if (b.turnsLeft < 0) continue;
            if (b.Id == "3" && b.effect.gradualHeal != null)
            {
                var gh = b.effect.gradualHeal;
                int baseAmt = Random.Range(gh.healLow, gh.healHigh + 1);
                int recAmt = Mathf.CeilToInt(BuffCalc.GetStat(this, "rec") * PercentageToNumber(gh.recAddedPercent));
                Heal(baseAmt + recAmt);
            }
        }

        foreach (var expired in buffs.Decrement())
        {
            if (expired.Id == BuffIds.Overdrive) isInOverdrive = false;
            Debug.Log($"[Effect] {expired.Id} expired on {unitData.unitName}");
        }

        // ailment timers
        var expiredAilments = new List<string>();
        foreach (var ailment in activeAilments)
        {
            if (ailmentTurns.TryGetValue(ailment, out int remaining))
            {
                remaining--;
                ailmentTurns[ailment] = remaining;
                if (remaining <= 0) expiredAilments.Add(ailment);
            }
        }
        foreach (var a in expiredAilments) { activeAilments.Remove(a); ailmentTurns.Remove(a); }

        if (!isEnemyUnit) unitSlotUI?.UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    //  Combat
    // ─────────────────────────────────────────────────────────────

    private IEnumerator RepeatRandomAttack(Ability ability, List<UnitBehaviour> targets, int hitCount)
    {
        SetAnimation("Attack");
        for (int i = 0; i < hitCount; i++)
        {
            var alive = targets.FindAll(t => t.currentState != UnitState.Dead);
            if (alive.Count == 0) yield break;

            var randomTarget = alive[Random.Range(0, alive.Count)];
            Attack(ability, randomTarget, PercentageToNumber(100/hitCount), i);
            float seconds = (ability.hitFrames[0].frame - 0) / 60f;
            yield return new WaitForSeconds(seconds);
        }
    }

    public IEnumerator AttackAtHitFrames(Ability ability, UnitBehaviour target)
    {
        SetAnimation("Attack");

        if (ability.hitFrames == null || ability.hitFrames.Count == 0) yield break;

        var hits = ability.hitFrames.OrderBy(hf => hf.frame).ToList();

        int lastFrame = 0;
        foreach (var hf in hits)
        {
            float seconds = (hf.frame - lastFrame) / 60f;
            if (seconds > 0) yield return new WaitForSeconds(seconds);
            lastFrame = hf.frame;

            Attack(ability, target, PercentageToNumber(hf.damagePercent), ability.hitFrames.IndexOf(hf));
        }
    }

    public void Attack(Ability ability, UnitBehaviour target, float damageRate, int hitNum)
    {
        if (target == null) return;

        var cast = BuffCalc.SourceFor(this, ability, false);
        int lvl = isEnemyUnit ? 0 : Mathf.Clamp(inventoryData.currentBBLevel - 1, 0, ability.levels.Count - 1);
        int abilityMod = ability.levels[ability.levels.Count > 1 ? lvl : 0].damageRate;
        bool isCritical = BuffCalc.RollCrit(this, BuffCalc.FindAttackEffect(ability, lvl)?.bbCritPercent ?? 0);

        // guard halving / proc 68 now applied on the TARGET inside HitDamage
        float damage = BuffCalc.HitDamage(this, target, damageRate, abilityMod, cast, isCritical);

        if (ability.levels.Count > 1)
        {
            foreach (var e in ability.levels[lvl].effects)
                if (GetEffectId(e) == "13_A") damage /= e.attack.hitCount;
        }

        if (isCritical) Debug.Log($"{unitData.unitName} CRITICAL HIT!");

        TryHPDrainOnHit(ability, damage);
        target.TakeDamage(Mathf.CeilToInt(damage), isCritical, hitNum, this);
        DropsOnDamage(target, ability); // after the hit: spark and overkill are only known now
    }

    public void DropsOnDamage(UnitBehaviour target, Ability ability)
    {
        // 'this' is the attacker: its buffs decide the drop rates, the victim only supplies position / overkill
        bool sparked = target.lastHitSparked;
        target.DropBattleCrystals(this, ability, sparked);
        target.DropHeartCrystals(this, ability, sparked);
        target.DropItemsOnHit(this, sparked);
    }

    private void TryHPDrainOnHit(Ability ability, float rawDamage)
    {
        if (ability?.MaxLevel?.effects == null) return;
        foreach (var effect in ability.MaxLevel.effects)
        {
            if (effect.attack == null) continue;
            if (effect.attack.hpDrainHigh <= 0 && effect.attack.hpDrainLow <= 0) continue;

            float pct     = Random.Range(effect.attack.hpDrainLow, effect.attack.hpDrainHigh + 1f);
            int   drainAmt = Mathf.CeilToInt(rawDamage * PercentageToNumber(pct));
            Heal(drainAmt);
            Debug.Log($"[Drain] {unitData.unitName} drained {drainAmt} HP");
        }
    }

    public float GetDamage(Ability ability, UnitBehaviour target)
    {
        float atk     = isEnemyUnit ? enemyData.atk : (unitData.atk + inventoryData.atkImpBonus + inventoryData.atkLevelUpBonus) * PercentageToNumber(GetAttackBuffRate());
        float abilMod = PercentageToNumber(100 + ability.levels[ability.levels.Count > 1 ? inventoryData.currentBBLevel-1 : 0].damageRate);

        // Proc 65: bonus damage vs status-afflicted targets
        foreach (var e in activeEffects)
        {
            if (GetEffectId(e) == "65" && target.activeAilments.Count > 0 && e.statBuff != null)
                atk *= PercentageToNumber(100 + e.statBuff.atkBuff);
        }

        // Proc 22: ignore defense entirely while active
        bool ignoreDef = false;
        foreach (var e in activeEffects)
            if (GetEffectId(e) == "22") ignoreDef = true;

        return ignoreDef ? atk * abilMod : (atk * abilMod) - target.GetDefense();
    }

    // ─────────────────────────────────────────────────────────────
    //  Plays an ability's effectFrames (sounds/particles) on their
    //  own timeline, independent of hitFrames. Runs exactly once per
    //  UseAbility() call so it fires for every proc type — including
    //  heals/buffs/BC-fill/etc. that never touch AttackAtHitFrames —
    //  and so AoE abilities don't refire the same cue once per target.
    // ─────────────────────────────────────────────────────────────
    private IEnumerator PlayAbilityEffectFrames(Ability ability, List<UnitBehaviour> targets)
    {
        if (ability?.effectFrames == null || ability.effectFrames.Count == 0) yield break;

        // Snapshot: the caller's list may be mutated/reused while we wait between frames.
        var snapshot = targets != null ? new List<UnitBehaviour>(targets) : new List<UnitBehaviour>();
        var frames = ability.effectFrames.OrderBy(ef => ef.frame).ToList();

        int lastFrame = 0;
        foreach (var ef in frames)
        {
            float seconds = (ef.frame - lastFrame) / 60f;
            if (seconds > 0) yield return new WaitForSeconds(seconds);
            lastFrame = ef.frame;

            PlayEffectFrame(ef, snapshot);
        }
    }

    private void PlayEffectFrame(EffectFrame ef, List<UnitBehaviour> targets)
    {
        if (ef.audioClip != null)
            SoundManager.Instance.PlaySound(ef.audioClip); // once per frame, not per target

        if (!Options.Instance.GetVfxEnabled() || ef.particleEffect == null) return;

        bool fixedPos  = ef.xAnchor == 5 && ef.yAnchor == 5;
        bool perTarget = ef.placementType != 2 && !fixedPos;

        // Drop dead targets so AoE effects don't play on corpses (remove if you want them).
        var alive = targets.FindAll(t => t != null && t.currentState != UnitState.Dead);

        if (!perTarget || alive.Count == 0)
        {
            SpawnEffect(ef, alive.Count > 0 ? alive[0] : (targets.Count > 0 ? targets[0] : this));
            return;
        }

        foreach (var t in alive) SpawnEffect(ef, t);
    }

    private void SpawnEffect(EffectFrame ef, UnitBehaviour target)
    {
        RectTransform layer = BattleUI.uiEffectLayer != null
            ? BattleUI.uiEffectLayer.GetComponent<RectTransform>()
            : BattleUI.dropsLayer;

        GameObject go = new GameObject($"FX_{ef.particleEffect.effectId}", typeof(RectTransform));
        go.transform.SetParent(layer, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = ComputeEffectPosition(ef, target);

        float lifetime = 2f;

        switch (ef.particleEffect.particleType)
        {
            case ParticleType.PLIST:
            {
                var fx = go.AddComponent<CocosParticleEffect>();
                fx.raycastTarget = false; // otherwise the particle quad eats clicks
                if (ef.particleEffect.plistJson != null)
                    fx.LoadFromJson(ef.particleEffect.plistJson.text);
                lifetime = fx.GetTotalLifetime();
                break;
            }
            case ParticleType.CGG:
            {
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                var fx = go.AddComponent<BraveFrontierFrameAnimator>();
                fx.cggFile       = ef.particleEffect.cggJson;
                fx.idleCgsFile   = ef.particleEffect.cgsJson;
                fx.attackCgsFile = ef.particleEffect.cgsJson;
                fx.spriteSheets  = ef.particleEffect.spriteSheet != null
                    ? new[] { ef.particleEffect.spriteSheet.texture }
                    : new Texture2D[0];
                fx.animationName = "Attack";
                fx.loop          = false;
                fx.playOnStart   = true;
                fx.initOnStart   = false;
                fx.InitializeAnimator();
                lifetime = Mathf.Max(fx.GetTotalDurationFrames("Attack") / 60f, 0.1f);
                break;
            }
            case ParticleType.SAM:
            {
                var fx = go.AddComponent<SamAnimator>();
                fx.jsonFile    = ef.particleEffect.samJson;
                fx.isEffect    = true;
                fx.loop        = false;
                fx.playOnStart = false;
                fx.SetAnimation("start", false);
                fx.InitializeAnimator();
                lifetime = Mathf.Max(fx.GetTotalDurationFrames("start") / 60f, 0.1f);
                break;
            }
        }

        Destroy(go, lifetime);
    }

    /// <summary>
    /// Best-effort placement from the group data — UNVERIFIED against real
    /// footage. placementType: 1 = at target, 2 = shared "center" point,
    /// 3 = anchored near the ground under the target ("陣地" formation effects).
    /// xAnchor/yAnchor 1/2/3 nudge left-center-right / top-center-bottom around
    /// that base point; 5/5 together means offsetX/offsetY IS the absolute
    /// position (matches the "xy固定" groups). Tune anchorNudge and the ground
    /// offset once you can compare against actual gameplay video.
    /// </summary>
    private Vector2 ComputeEffectPosition(EffectFrame ef, UnitBehaviour target)
    {
        if (ef.xAnchor == 5 && ef.yAnchor == 5)
            return new Vector2(ef.offsetX, ef.offsetY);

        Vector2 basePos;
        switch (ef.placementType)
        {
            case 2: // center / screen
                basePos = new Vector2(0, 261); // assumes effectsLayer's origin sits at battle-center
                break;
            case 3: // ground / formation
                basePos = (Vector2)target.transform.localPosition;
                break;
            default: // 1: per-target
                basePos = target.transform.localPosition;
                break;
        }

        const float anchorNudge = 20f; // px per anchor step away from center — tune to taste
        basePos.x += (ef.xAnchor - 2) * anchorNudge;
        basePos.y += (ef.yAnchor - 2) * anchorNudge;

        return basePos + new Vector2(ef.offsetX, ef.offsetY);
    }

    // ─────────────────────────────────────────────────────────────
    //  Buff rate calculators — field names match Ability.cs structs
    // ─────────────────────────────────────────────────────────────
    
    public float GetDefense()          => BuffCalc.GetStat(this, "def");
    public int   GetAttackBuffRate()   => BuffCalc.StatRate(this, "atk");
    public int   GetDefenseBuffRate()  => BuffCalc.StatRate(this, "def");
    public int   GetRecoveryBuffRate() => BuffCalc.StatRate(this, "rec");
    public int   GetCritBuffRate()     => Mathf.RoundToInt(BuffCalc.CritRate(this, 0));

    /// BC / HC drop rate as a percentage of the base chance (100 = no buff).
    /// The real roll is BuffCalc.CrystalDropChance, these are kept for callers that still read a rate.
    public int GetBattleCrystalBuffRate() => 100 + Mathf.RoundToInt(BuffCalc.DropBonus(this, BuffCalc.DropType.Crystal, false));
    public int GetHeartCrystalBuffRate()  => 100 + Mathf.RoundToInt(BuffCalc.DropBonus(this, BuffCalc.DropType.Heart, false));

    // ─────────────────────────────────────────────────────────────
    //  Damage / Heal / Death
    // ─────────────────────────────────────────────────────────────

    // Called on the VICTIM with the attacker, so the attacker's buffs are what boost the drop.
    public void DropBattleCrystals(UnitBehaviour attacker, Ability ability, bool sparked = false)
    {
        if (attacker.isEnemyUnit) return;

        int procBonus = BuffCalc.FindAttackEffect(ability, attacker.inventoryData.currentBBLevel - 1)?.bbBCPercent ?? 0;
        float dropChance = BuffCalc.CrystalDropChance(attacker, this, BuffCalc.DropType.Crystal, procBonus, sparked) / 100f;

        for (int i = 0; i < ability.dropChecksPerHit / ability.hitFrames.Count; i++)
        {
            if (Random.Range(0f, 1f) <= dropChance)
            {
                GameObject bc = Instantiate(PrefabCache.Get("BattleCrystal"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
                bc.transform.SetParent(BattleUI.dropsLayer);
                bc.GetComponent<DropBehaviour>().target = GetBattleCrystalTarget().gameObject;
                BattleManager.totalBcDropCount++;
            }
        }
    }

    public void DropHeartCrystals(UnitBehaviour attacker, Ability ability, bool sparked = false)
    {
        if (attacker.isEnemyUnit) return;

        int procBonus = BuffCalc.FindAttackEffect(ability, attacker.inventoryData.currentBBLevel - 1)?.bbHCPercent ?? 0;
        float dropChance = BuffCalc.CrystalDropChance(attacker, this, BuffCalc.DropType.Heart, procBonus, sparked) / 100f;

        for (int i = 0; i < ability.dropChecksPerHit / ability.hitFrames.Count; i++)
        {
            if (Random.Range(0f, 1f) <= dropChance)
            {
                GameObject hc = Instantiate(PrefabCache.Get("HeartCrystal"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
                hc.transform.SetParent(BattleUI.dropsLayer);
                hc.GetComponent<DropBehaviour>().target = GetHeartCrystalTarget().gameObject;
                BattleManager.totalHcDropCount++;
            }
        }
    }

    public int GetMaxBC()
    {
        if (isInOverdrive && unitData.ubbAbility != null) return Mathf.Max(unitData.ubbAbility.levels[0].bcCost, 1);
        int max = 0;
        if (unitData.bbAbility != null && unitData.bbAbility.abilityName != "Unnamed")
            max += unitData.bbAbility.levels[inventoryData.currentBBLevel - 1].bcCost;
        if (unitData.sbbAbility != null && unitData.sbbAbility.abilityName != "Unnamed")
            max += unitData.sbbAbility.levels[inventoryData.currentSBBLevel - 1].bcCost;
        return Mathf.Max(max, 1);
    }

   UnitBehaviour GetBattleCrystalTarget()
    {

        var notFull = BattleManager.playerTeam.units.FindAll(u =>
            u.currentState != UnitState.Dead && u.bcCount < u.GetMaxBC());

        if (notFull.Count > 0)
            return notFull[Random.Range(0, notFull.Count)];

        var alive = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead);
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }

    UnitBehaviour GetHeartCrystalTarget()
    {
        int GetMaxHP(UnitBehaviour u) =>
            u.unitData.maxHealth + u.inventoryData.hpLevelUpBonus + u.inventoryData.hpImpBonus;

        var notFull = BattleManager.playerTeam.units.FindAll(u =>
            u.currentState != UnitState.Dead && u.currentHealth < GetMaxHP(u));

        if (notFull.Count > 0)
            return notFull[Random.Range(0, notFull.Count)];

        var alive = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead);
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }

    public void DropZelCoinsOnDeath()
    {
        if (!isEnemyUnit) return;

        int total = (int)enemyData.zelMaxDrop;
        int count = (int)enemyData.zelDropCount;

        if(count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int coinValue = baseValue + (i < remainder ? 1 : 0);

            GameObject c = Instantiate(PrefabCache.Get("ZelCoin"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.zelPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = coinValue;
        }
    }

    public void DropKarmaOrbsOnDeath()
    {
        if (!isEnemyUnit) return;

        int total = (int)enemyData.karmaMaxDrop;
        int count = (int)enemyData.karmaDropCount;

        if(count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int orbValue = baseValue + (i < remainder ? 1 : 0);

            GameObject c = Instantiate(PrefabCache.Get("KarmaOrb"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.karmaPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = orbValue;
        }
    }

    public void DropItemsOnHit(UnitBehaviour attacker = null, bool sparked = false)
    {
        if (!isEnemyUnit) return;

        float itemBonus = attacker != null ? BuffCalc.DropBonus(attacker, BuffCalc.DropType.Item, sparked) : 0f;

        foreach(ItemDrop drop in enemyData.itemDrops){

            if(Random.Range(0f, 100f) > drop.dropChance + itemBonus) return;
            
            int count = (int)drop.dropRest;

            if(count == 0) return;

            ItemDropData dropData = new ItemDropData
            {
                itemName = drop.itemName,
                itemCount = 1
            };

            for (int i = 0; i < count; i++)
            {
                GameObject c = Instantiate(PrefabCache.Get("ItemDrop"));
                c.transform.SetParent(BattleUI.dropsLayer);
                c.transform.position = transform.position + new Vector3(0, 0.5f, 0);
                c.GetComponent<Image>().sprite = ItemDatabase.GetItemByName(dropData.itemName).thumbnailSprite;
                c.GetComponent<DropBehaviour>().itemDropData = dropData;
                c.GetComponent<DropBehaviour>().target = BattleUI.unitPoint.gameObject;
            }
        }
    }

    public void DropGemCrystalsOnDeath()
    {
        if (!isEnemyUnit) return;

        int total = 1; //(int)enemyData.gemCrystalMaxDrop;
        int count = 1; //(int)enemyData.gemCrystalDropCount;

        if(count == 0) return;

        if(this.enemyData != null && this.enemyData.unitId == "750006")
        {
            int baseValue = total / count;
            int remainder = total % count;

            for (int i = 0; i < count; i++)
            {
                int gemValue = baseValue + (i < remainder ? 1 : 0);

                GameObject c = Instantiate(PrefabCache.Get("GemCrystal"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
                c.transform.SetParent(BattleUI.dropsLayer);
                c.GetComponent<DropBehaviour>().target = BattleUI.unitPoint.gameObject;
                c.GetComponent<DropBehaviour>().valueOfDrop = gemValue;
            }
        }
    }

    public void DropUnitOnDeath()
    {
        if (!isEnemyUnit) return;

        if(Random.Range(0f, 100f) > enemyData.unitDrop.dropChance) return;

        bool isBonus = Random.Range(0f, 100f) <= enemyData.unitDrop.bonusDropChance;
        string unitId = isBonus ? enemyData.unitDrop.bonusUnitId : enemyData.unitDrop.unitId;

        Unit unit = UnitRegistry.GetUnitById(unitId);

        if(unit == null) return;

        Debug.Log("Trying to drop: " + unit.name + " " + unit.element);

        int level = isBonus ? (int)enemyData.unitDrop.bonusLevel : (int)enemyData.unitDrop.level;
        level = Math.Min(unit.maxLevel, level);

        UnitType unitType;

        if(enemyData.unitDrop.bonusType == "random")
        {
            unitType = PlayerUnitInventoryDatabase.GetRandomUnitType();
        }
        else
        {
            Enum.TryParse(enemyData.unitDrop.bonusType, out UnitType bonusResult);
            Enum.TryParse(enemyData.unitDrop.type, out UnitType result);

            unitType = isBonus ? bonusResult : result;
        }

        string dropPrefab = "UnitDropCommon";

        switch (unit.rarity)
        {
            case UnitRarity.ONE or UnitRarity.TWO:
            dropPrefab = "UnitDropCommon";
            break;
            case UnitRarity.THREE:
            dropPrefab = "UnitDropRare";
            break;
            case UnitRarity.FOUR:
            dropPrefab = "UnitDropSuperRare";
            break;
            case UnitRarity.FIVE or UnitRarity.SIX or UnitRarity.SEVEN or UnitRarity.OMNI:
            dropPrefab = "UnitDropUltraRare";
            break;
        }

        UnitDropData dropData = new UnitDropData
        {
            unitId = unitId,
            unitLevel = level,
            type = unitType
        };

        GameObject c = Instantiate(PrefabCache.Get(dropPrefab), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
        c.transform.SetParent(BattleUI.dropsLayer);
        c.GetComponent<DropBehaviour>().target = BattleUI.unitPoint.gameObject;
        c.GetComponent<DropBehaviour>().unitDropData = dropData;
    }

    public void DropTreasureChestOnDeath()
    {
        if (!isEnemyUnit) return;

        if(Random.Range(0f, 100f) > enemyData.treasureDrop.chestDropChance) return;

        GameObject c = Instantiate(PrefabCache.Get("ChestDrop"), transform.position, Quaternion.identity);
        c.transform.SetParent(BattleUI.dropsLayer);
        c.GetComponent<TreasureChestDropBehaviour>().enemyData = enemyData;
    }

    /// Backward-compatible overload for existing callers that don't pass an attacker.
    public void TakeDamage(int damage, bool isCritical, int hitNum) => TakeDamage(damage, isCritical, hitNum, null);

    public void TakeDamage(int damage, bool isCritical, int hitNum, UnitBehaviour attacker)
    {
        if(!isEnemyUnit) StartCoroutine(unitSlotUI.ShakeIcon(0.5f, 3f));

        int effectiveDamage = Mathf.Max(damage, 1);

        if (effectiveDamage > 0)
        {
            sparkCount++;
        }

        PopUpText(-effectiveDamage, isCritical, hitNum);

        if (isCritical && effectiveDamage > 0)
        {
            SoundManager.Instance.PlaySound(criticalSound);
            PopUpText("CriticalPopUp");
            if (isEnemyUnit) BattleManager.totalCriticalHits++;
        } 
        
        float sparkBonus = (sparkCount >= 2 && effectiveDamage > 0) ? BuffCalc.SparkMultiplier(attacker) : 1f;
        lastHitSparked = sparkBonus > 1f;
        if (sparkBonus > 1f) {
            SoundManager.Instance.PlaySound(sparkSound);
            PopUpText("SparkPopUp");
            if (isEnemyUnit) BattleManager.totalSparkCount++;
        }

        if (isEnemyUnit) EnemyHealthUI.enemyUnit = this;

        currentHealth -= Mathf.CeilToInt(effectiveDamage * sparkBonus);
        currentHealth = Mathf.Max(currentHealth, 0);
        Debug.Log($"{unitData.unitName} took {effectiveDamage} dmg (Spark: {sparkBonus}x). HP: {currentHealth}");

        // Reactive procs that depend on "this unit just took damage" (20, 53, 67, 69, 85)
        ProcessReactiveEffects(attacker, sparkBonus > 1f);

        if (currentHealth <= 0 && !TryAngelIdol())
        {
            Die();
            DropsOnLastHit();
        }
            

        if (currentState == UnitState.Dead)
            deathHitCount++;

        if (!isEnemyUnit) unitSlotUI.UpdateUI();
    }

    /// Handles procs whose behavior only fires reactively when this unit is hit.
    private void ProcessReactiveEffects(UnitBehaviour attacker, bool wasSpark)
    {
        foreach (var e in activeEffects)
        {
            string id = GetEffectId(e);

            // Proc 20: BC fill when attacked
            if (id == "20" && e.bcFill != null && CanGainBC())
            {
                int fill = Random.Range(e.bcFill.bcFillWhenAttackedLow, e.bcFill.bcFillWhenAttackedHigh + 1);
                bcCount = Mathf.Max(bcCount + fill, 0);
            }

            // Proc 53: inflict an ailment back on the attacker
            if (id == "53" && e.ailmentInflict != null && attacker != null)
                //TryInflictAilments(attacker, e.ailmentInflict, 3);

            // Proc 67: BC fill on landing/receiving a spark
            if (id == "67" && wasSpark && e.bcFill != null && CanGainBC())
            {
                int fill = Random.Range(e.bcFill.bcFillOnSparkLow, e.bcFill.bcFillOnSparkHigh + 1);
                bcCount = Mathf.Max(bcCount + fill, 0);
            }

            // Proc 69: BC fill while guarding
            if (id == "69" && isGuarding && e.bcFill != null && CanGainBC())
            {
                int fill = Random.Range(e.bcFill.bcFillWhenAttackedLow, e.bcFill.bcFillWhenAttackedHigh + 1);
                bcCount = Mathf.Max(bcCount + fill, 0);
            }

            // Proc 85: heal a flat amount when attacked
            if (id == "85" && e.conditionalBuff?.buff != null)
            {
                int healAmt = Random.Range(e.conditionalBuff.buff.gradualHealLow, e.conditionalBuff.buff.gradualHealHigh + 1);
                if (Random.Range(0f, 100f) <= e.conditionalBuff.activationChance)
                {
                    Heal(healAmt);
                }
            }

            // Proc 87: heal on spark
            if (id == "87" && wasSpark && e.conditionalBuff?.buff != null)
            {
                int healAmt = Random.Range(e.conditionalBuff.buff.gradualHealLow, e.conditionalBuff.buff.gradualHealHigh + 1);
                Heal(healAmt);
            }

            // NOTE: Proc 50 (damage counter/reflect) intentionally not handled here —
            // no reflect-percent field exists on any struct, so there's nothing
            // correct to compute. Add e.g. a `reflectPercent` field to DamageBuffEffect
            // (or a dedicated struct) and this is a one-line addition.
        }
    }

    private bool TryAngelIdol()
    {
        for (int i = buffs.active.Count - 1; i >= 0; i--)
        {
            var b = buffs.active[i];
            if (b.Id != "7" || b.effect.damageBuff?.angelIdolSubBuff == null) continue;
            float pct = b.effect.damageBuff.angelIdolSubBuff.recoverHpPercent;
            currentHealth = Mathf.Max(Mathf.CeilToInt(BuffCalc.BaseStat(this, "hp") * PercentageToNumber(pct)), 1);
            buffs.active.RemoveAt(i);
            Debug.Log($"[Angel Idol] {unitData.unitName} survived! HP: {currentHealth}");
            return true;
        }
        return false;
    }

    public void BurstHeal(Ability ability, UnitBehaviour healer)
    {
        if(currentState == UnitState.Dead) return;
        if (ability?.MaxLevel?.effects == null) return;

        float total = 0f;
        foreach (var effect in ability.MaxLevel.effects)
        {
            // proc 2 → HealEffect
            if (effect.procId == "2" && effect.heal != null)
                total += BuffCalc.BurstHealAmount(healer, this, effect.heal);
        }

        int healAmount = Mathf.CeilToInt(total);
        Debug.Log($"{healer.unitData.unitName} healed {unitData.unitName} for {healAmount}. HP: {currentHealth}");
        Heal(healAmount);

        if (!isEnemyUnit) unitSlotUI.UpdateUI();
    }

    void Heal(int healAmount, bool isRevive = false)
    {
        if(currentState == UnitState.Dead && !isRevive)
            return;

        currentHealth = Mathf.Min(currentHealth + healAmount, isEnemyUnit ? enemyData.health : unitData.maxHealth + inventoryData.hpLevelUpBonus + inventoryData.hpImpBonus);

        PopUpText(healAmount, false, 0);

        if (!isEnemyUnit) unitSlotUI.UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    //  Misc
    // ─────────────────────────────────────────────────────────────

    void PopUpText(int value, bool isCritical, int hitNum)
    {
        string popupText = "" + (value * -1);
        ImageToFont popup = Instantiate(
            PrefabCache.Get("DamagePopUp"),
            transform.position + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f) + 0.5f + (0.6f * hitNum)),
            Quaternion.identity
        ).GetComponentInChildren<ImageToFont>();

        popup.transform.SetParent(BattleUI.popupLayer);
        popup.text  = popupText;
        popup.color = value >= 0 ? Color.green : Color.white;
        popup.transform.localScale = isCritical ? new Vector3(2f, 2f, 1f) : Vector3.one;
        popup.transform.localScale = popup.transform.parent.localScale * ((0.1f * hitNum) + 1);

        var rt = popup.GetComponent<RectTransform>();
        popup.GetComponent<ShakeDmgText>().PlayEffect(rt.localPosition, rt.localScale, popup.color);
    }

    void PopUpText(string text)
    {
        if (text == "SparkPopUp" && sparkInstances >= MAX_SPARK_SAMS) return;
        if (text == "SparkPopUp") sparkInstances++;
        GameObject popup = Instantiate(
            PrefabCache.Get(text),
            transform.position + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f) + 0.5f),
            Quaternion.identity
        );
        popup.transform.SetParent(BattleUI.popupLayer);
        popup.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    private void Die()
    {
        if(currentState == UnitState.Dead) return;
        
        if(BattleManager.selectedEnemyUnit == this) BattleUI.enemySelect.SetActive(false);
        Debug.Log($"{unitData.unitName} has been defeated!");
        currentState = UnitState.Dead;
        StartCoroutine(DisappearIfNoHit());

        if (currentState == UnitState.Dead && isMimicChest && !BattleManager.mimicDropsHandled.Contains(this))
        {
            BattleManager.mimicDropsHandled.Add(this);
            ChestDropUtility.OpenDrops(enemyData, transform.position);
        }
    }

    public void Overdrive()
    {
        if (currentState == UnitState.Dead) return;
        isInOverdrive = true;
        buffs.Add(BuffCalc.MakeOverdriveEffect(), BuffSource.OD, 4);
    }

    public void StopOverdrive()
    {
        if (currentState == UnitState.Dead) return;
        isInOverdrive = false;
        buffs.RemoveWhere(b => b.source == BuffSource.OD);
    }

    void DropsOnLastHit()
    {
        if(deathHitCount != 0) return;
        DropZelCoinsOnDeath();
        DropKarmaOrbsOnDeath();
        DropGemCrystalsOnDeath();
    }

    void DropsOnDeath()
    {
        if(!isEnemyUnit) return;
        DropUnitOnDeath();
        DropTreasureChestOnDeath();
    }

    IEnumerator DisappearIfNoHit(){
        deathHitCount = 0;
        yield return new WaitForSeconds(1f);
        if (deathHitCount > 0)
        {
            deathHitCount = 0;
            StartCoroutine(DisappearIfNoHit());
        }
        else
        {
            StopAnimations();
            SoundManager.Instance.PlaySound(isBoss ? bossDeathSound : deathSound);
            StartCoroutine(ShakeEffect());
            StartCoroutine(ScaleEffect());
        }
    }

    void DarkenSprite()
    {
        if (unitData.animationType == AnimationType.CGG)
            GetComponent<Image>().color = Color.gray;
        else
            foreach (var sr in GetComponentsInChildren<SamUIQuadGraphic>())
                sr.color = Color.gray;
    }

    public IEnumerator TransparentToWhite(float duration)
    {
        float elapsed = 0f;

        if (unitData.animationType == AnimationType.CGG)
        {
            Image img = GetComponent<Image>();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                img.color = Color.Lerp(Color.clear, Color.white, elapsed / duration);
                yield return null;
            }
            img.color = Color.white;
        }
        else
        {
            SamUIQuadGraphic[] graphics = GetComponentsInChildren<SamUIQuadGraphic>();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Color c = Color.Lerp(Color.clear, Color.white, elapsed / duration);
                foreach (var sr in graphics)
                    sr.color = c;
                yield return null;
            }
            foreach (var sr in graphics)
                sr.color = Color.white;
        }
    }

    void StopAnimations()
    {
        if (unitData.animationType == AnimationType.CGG)
            animatorCGG.Pause();
        else
            animatorSAM.Stop();
    }

    public IEnumerator ShakeEffect()
    {
        float shakeTime = 1.5f;
        float totalTime = isBoss ? shakeTime * 2f : shakeTime;

        RectTransform rt     = GetComponent<RectTransform>();
        Vector3 originalPos  = rt.localPosition;

        // Fixed offsets to snap between — tweak magnitude to taste
        float magnitude = 4f;
        float[] offsets = { -magnitude, magnitude };

        float elapsed       = 0f;
        float stepDuration  = 1f / 24f; // snap rate: ~24 "ticks" per second
        float stepElapsed   = 0f;
        int   offsetIndex   = 0;

        while (elapsed < totalTime)
        {
            rt.localPosition = new Vector3(originalPos.x + offsets[offsetIndex], originalPos.y, originalPos.z);

            stepElapsed += Time.deltaTime;
            elapsed     += Time.deltaTime;

            if (stepElapsed >= stepDuration)
            {
                stepElapsed = 0f;
                offsetIndex = (offsetIndex + 1) % offsets.Length;
            }

            yield return null;
        }

        rt.localPosition = originalPos;
    }
    
    public IEnumerator ScaleEffect(float duration = 1.2f, float targetScaleX = 0f, float targetScaleY = 1.5f)
    {
        RectTransform rt = GetComponent<RectTransform>();
        Vector3 originalScale = rt.localScale;
        float startScaleX = originalScale.x;
        float startScaleY = originalScale.y;
        float elapsed = 0f;
        float actualDuration = isBoss ? duration * 2 : duration;

        while (elapsed < actualDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / actualDuration);
            float newScaleX = Mathf.Lerp(startScaleX, startScaleX * targetScaleX, t);
            float newScaleY = Mathf.Lerp(startScaleY, startScaleY * targetScaleY, t);
            rt.localScale = new Vector3(newScaleX, newScaleY, originalScale.z);
            yield return null;
        }

        yield return new WaitForSeconds(1);
        DropsOnDeath();
        deathSequenceComplete = true;
    }

    public void HealByCrystal()
    {
        float amountHealed = 0;

        amountHealed = (unitData.rec + inventoryData.recLevelUpBonus + inventoryData.recImpBonus) * (1 + 0);
        amountHealed = amountHealed / Random.Range(3.0f, 4.2f);

        int healAmount = Mathf.CeilToInt(amountHealed);
        Heal(healAmount);
    }

    float PercentageToNumber(float percentage) => percentage / 100f;

    public void FlipImage(bool flipX)
    {
        float size = 1.75f;
        Image sr = GetComponent<Image>();
        sr.rectTransform.localScale = isEnemyUnit
            ? new Vector3(flipX ?  size : -size, size, size)
            : new Vector3(flipX ? -size :  size, size, size);
    }

    public void SetAnimation(string name)
    {
        if (unitData.animationType == AnimationType.CGG)
            animatorCGG.SetAnimation(name);
        else
            animatorSAM.SetAnimation(name);
    }
}

public enum UnitState { Idle, Attacking, Dead }