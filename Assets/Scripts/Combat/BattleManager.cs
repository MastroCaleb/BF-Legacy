using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class BattleManager : MonoBehaviour
{
    public CutInAnimation cutInAnimator;
    public NextRoundTransition nextRoundTransition;
    public PauseBattleButton pauseBattleButton;
    public TextMeshProUGUI turnText;
    public static DungeonLevel dungeonLevelData;
    public static Mission missionData;

    // Prefabs / layout
    public RectTransform unitCanvas;
    public GameObject unitPrefab;
    public List<RectTransform> playerUnitPositions;
    public List<RectTransform> enemyUnitPositions;
    public RectTransform centerArenaPos;
    // Combat flags
    public static bool isCombatAutomatic = true;

    // Private state
    public static UnitBehaviour selectedEnemyUnit;
    private int currentTurnIndex = 1;
    public static TeamBehaviour playerTeam;
    public static TeamBehaviour enemyTeam;
    private List<UnitBehaviour> unitsThatMustAct = new List<UnitBehaviour>();
    private List<UnitBehaviour> unitsThatActed   = new List<UnitBehaviour>();
    public static List<TreasureChestDropBehaviour> chests = new List<TreasureChestDropBehaviour>();
    private bool playerTurnWaitingForInput = false;

    //Sounds
    public AudioClip battleWin;
    public AudioClip bossBattleMusic;
    public AudioClip bossWarning;
    public AudioClip mimicWarning;

    //UBB
    public static int ubbCurrentMaxGauge;
    public static int ubbCount;

    //Rewards
    public static bool isVortex;
    public static int totalZelReward;
    public static int totalKarmaReward;
    public static int totalGemReward;
    public static List<UnitDropData> unitDrops = new List<UnitDropData>();
    public static List<int> newUnits = new List<int>();

    //Battle Stats
    public static int totalSparkCount;
    public static int totalCriticalHits;
    public static int totalBcDropCount;
    public static int totalHcDropCount;
    public static int xpObtained;
    public static int oldLevel;
    public static int oldExperience;
    public static bool obtainedGemsForMission;

    private Coroutine autoAttackCoroutine;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        totalZelReward = 0;
        totalKarmaReward = 0;
        totalGemReward = 0;
        totalSparkCount = 0;
        totalCriticalHits = 0;
        totalBcDropCount = 0;
        totalHcDropCount = 0;
        ubbCurrentMaxGauge = 10000;
        ubbCount = 0;
        obtainedGemsForMission = false;
        unitDrops.Clear();
        newUnits.Clear();
        chests.Clear();
        playerTeam = new TeamBehaviour();
        enemyTeam  = new TeamBehaviour();
        selectedEnemyUnit = null;

        SoundManager.Instance.PlayMusicLoop(dungeonLevelData.bgm);

        StartCoroutine(StartBattle());
    }

    void Update()
    {
        turnText.text = $"TURN: {currentTurnIndex}";

        if (EnemyHealthUI.enemyUnit == null && !enemyTeam.IsDefeated())
            EnemyHealthUI.enemyUnit = enemyTeam.units.Find(u => u.currentState != UnitState.Dead);

        SortUnitsByY(playerTeam.units);
        SortUnitsByY(enemyTeam.units);

        SelectEnemyUnit();
    }

    // ─────────────────────────────────────────────────────────────
    //  Setup
    // ─────────────────────────────────────────────────────────────

    IEnumerator StartBattle()
    {
        pauseBattleButton.UpdateState(dungeonLevelData.levelName, missionData.missionName, 1, missionData.rounds.Count);
        SummonPlayerUnits();
        yield return new WaitForSeconds(2f);
        StartCoroutine(BattleLoop());
    }

    void SummonPlayerUnits()
    {
        PartyData party = PartyDatabase.GetParty(0);
        int i = 0;

        for (int slot = 0; slot < PartyDatabase.MaxPartySize; slot++)
        {
            int unitKey = party.GetUnitAt(slot);
            if (unitKey == -1) continue;

            UnitInventoryData inventoryData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
            if (inventoryData == null) continue;

            UnitBehaviour u = Instantiate(unitPrefab, playerUnitPositions[i].position, Quaternion.identity)
                                .GetComponent<UnitBehaviour>();
            u.transform.SetParent(BattleUI.playerUnitsLayer.transform);
            u.unitData         = inventoryData.unit;
            u.inventoryData    = inventoryData;       // set inventory reference
            u.unitCanvas       = unitCanvas;
            u.originalPosition = playerUnitPositions[i];
            BattleUI.unitSlots[i].unit          = u;
            BattleUI.unitSlots[i].battleManager = this;
            u.unitSlotUI = BattleUI.unitSlots[i];
            playerTeam.units.Add(u);
            i++;
        }

        playerTeam.SetLeader();
        SummonEnemyUnitsForRound(0);
    }

    public void SummonEnemyUnitsForRound(int round)
    {
        enemyTeam.units.Clear();
        int i = 0;
        foreach (var enemy in missionData.rounds[round].enemies)
        {
            RectTransform spawnPoint = enemyUnitPositions[i];
            bool isMelee = UnitRegistry.GetUnitById(enemy.unitId).moveTypeAttack == 1;

            UnitBehaviour u = Instantiate(unitPrefab, Vector3.zero, Quaternion.identity)
                                .GetComponent<UnitBehaviour>();

            u.isEnemyUnit      = true;
            u.enemyData        = enemy;
            u.transform.SetParent(BattleUI.enemyUnitsLayer.transform, false);
            u.unitData         = UnitRegistry.GetUnitById(enemy.unitId);
            u.unitCanvas       = unitCanvas;
            u.originalPosition = spawnPoint;
            u.isBoss           = round == missionData.rounds.Count - 1;

            RectTransform uRect = u.GetComponent<RectTransform>();
            Vector2 spawnAnchoredPos = spawnPoint.anchoredPosition - (isMelee ? new Vector2(300, 0) : Vector2.zero);
            uRect.anchoredPosition = spawnAnchoredPos;

            enemyTeam.units.Add(u);
            AppearAnimation(u, spawnPoint.anchoredPosition, 300f);
            i++;
        }
        enemyTeam.SetLeader();
        EnemyHealthUI.SetEnemyNames(enemyTeam);
    }

    public void AppearAnimation(UnitBehaviour unit, Vector2 targetAnchoredPosition, float duration)
    {
        if (unit.unitData.moveTypeAttack == 1)
            StartCoroutine(MoveUnitToPosition(unit, targetAnchoredPosition, duration));
        else
            StartCoroutine(unit.TransparentToWhite(2));
    }

    public IEnumerator MoveUnitToPosition(UnitBehaviour unit, Vector2 targetAnchoredPosition, float speed)
    {
        yield return new WaitForSeconds(0.1f);
        unit.SetAnimation("Move");
        RectTransform rt = unit.GetComponent<RectTransform>();
        while (rt.anchoredPosition != targetAnchoredPosition)
        {
            rt.anchoredPosition = Vector2.MoveTowards(rt.anchoredPosition, targetAnchoredPosition, speed * Time.deltaTime);
            yield return null;
        }
        unit.SetAnimation("Idle");
    }

    // ─────────────────────────────────────────────────────────────
    //  Battle loop
    // ─────────────────────────────────────────────────────────────

    IEnumerator BattleLoop()
    {
        nextRoundTransition.Initialize(dungeonLevelData.levelName, missionData.missionName, missionData.rounds.Count);
        yield return new WaitForSeconds(2f);

        // Leader ability fires once at battle start (permanent)
        if(playerTeam.leaderUnit.unitData.leaderAbility != null){
            playerTeam.ActivateLeaderAbility(
                GetTargets(enemyTeam.units, playerTeam.leaderUnit, playerTeam.leaderUnit.unitData.leaderAbility)
            );
        }
       

        for (int round = 1; round <= missionData.rounds.Count; round++)
        {
            pauseBattleButton.UpdateState(dungeonLevelData.levelName, missionData.missionName, round,missionData.rounds.Count);
            currentTurnIndex = 1;
            bool isBattleOver = false;

            while (!isBattleOver)
            {
                Debug.Log($"Turn {currentTurnIndex} started");

                if (playerTeam.IsDefeated())
                {
                    TitleTextInstantiate("Lose");
                    yield return new WaitForSeconds(3f);
                    Time.timeScale = 1f;
                    StartCoroutine(BattleUI.FadeAndLoad("MainMenuScene"));
                    yield break;
                }

                if(selectedEnemyUnit != null)
                    BattleUI.enemySelect.SetActive(true);

                // ── Player turn ──────────────────────────────────
                if (!isCombatAutomatic)
                {
                    PlayerTurn();
                    while (playerTurnWaitingForInput)
                    {
                        if (isCombatAutomatic)
                        {
                            EndManualPlayerTurn();
                            RefreshFocusFireState();
                            autoAttackCoroutine = StartCoroutine(DelayedAutoAttack());
                            break;
                        }
                        yield return null;
                    }
                    while (!playerTeam.GetEndTurn()) yield return null;
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    PlayerTurn();
                    while (!playerTeam.GetEndTurn())
                    {
                        if (!isCombatAutomatic)
                        {
                            if (autoAttackCoroutine != null)
                            {
                                StopCoroutine(autoAttackCoroutine);
                                autoAttackCoroutine = null;
                            }

                            unitsThatMustAct.Clear();
                            foreach (var unit in playerTeam.units)
                                if (unit.currentState != UnitState.Dead && !unitsThatActed.Contains(unit))
                                    unitsThatMustAct.Add(unit);

                            if (unitsThatMustAct.Count == 0)
                            {
                                while (!playerTeam.GetEndTurn()) yield return null;
                                break;
                            }

                            playerTurnWaitingForInput = true;
                            foreach (var slot in BattleUI.unitSlots)
                                if (slot.unit != null && slot.unit.currentState != UnitState.Dead
                                    && !unitsThatActed.Contains(slot.unit))
                                    slot.canBeClicked = true;

                            while (playerTurnWaitingForInput)
                            {
                                if (enemyTeam.IsDefeated()) { EndManualPlayerTurn(); break; }
                                yield return null;
                            }
                            break;
                        }
                        yield return null;
                    }
                    yield return new WaitForSeconds(1f);
                }

                // ── Tick effects after player phase ──────────────
                TickAllEffects();

                DropMoveManager.canMove = true;

                while (enemyTeam.units.Exists(u => u.currentState == UnitState.Dead && !u.deathSequenceComplete))
                    yield return null;

                BattleUI.enemySelect.SetActive(false);
                enemyTeam.DestroyDeadUnits();

                while (DropMoveManager.activeDrops.Count > 0)
                    yield return null;

                yield return new WaitForSeconds(2f);

                DropMoveManager.canMove = false;

                if (enemyTeam.IsDefeated())
                {
                    isBattleOver = true;
                    yield return new WaitForSeconds(1f);
                    if (round != missionData.rounds.Count) TitleTextInstantiate("Win");
                    yield return new WaitForSeconds(2f);

                    TreasureChestDropBehaviour.interactable = true;

                    if (isCombatAutomatic)
                    {
                        foreach(TreasureChestDropBehaviour chest in chests)
                        {
                            chest.Open();
                        }
                    }
                    
                    //Wait until all chests are opened and removed from the list
                    while(chests.Count > 0)
                    {
                        yield return null;
                    }
                    TreasureChestDropBehaviour.interactable = false;

                    DropMoveManager.canMove = true;
                    while (DropMoveManager.activeDrops.Count > 0)
                        yield return null;

                    yield return new WaitForSeconds(2f);
                    DropMoveManager.canMove = false;

                    break;
                }

                // ── Enemy turn ────────────────────────────────────
                EnemyTurn();
                yield return new WaitForSeconds(0.5f);
                while (!enemyTeam.GetEndTurn()) yield return null;

                // ── Tick effects after enemy phase ────────────────
                TickAllEffects();

                if (!isBattleOver) currentTurnIndex++;
                yield return new WaitForSeconds(0.5f);
            }

            if (round == missionData.rounds.Count)
            {
                SoundManager.Instance.StopMusic();
                SoundManager.Instance.PlaySound(battleWin);
                SamAnimator[] anims = TitleTextInstantiate("Congratulation");
                if(!PlayerData.completedMissionDex.Contains(missionData.missionId))
                {
                    PlayerData.completedMissionDex.Add(missionData.missionId);
                    if (!isVortex)
                    {
                        obtainedGemsForMission = true;
                        PlayerData.gems += 2;
                    }
                    PlayerData.SaveDataToJson();
                }
                yield return new WaitForSeconds(2f);
                foreach (var a in anims) a.SetAnimation("delete", false);
                yield return new WaitForSeconds(1f);
                Time.timeScale = 1f;
                ReceiveRewards();
                StartCoroutine(BattleUI.FadeAndLoad("MainMenuScene"));
                yield break;
            }

            yield return StartCoroutine(nextRoundTransition.PlayTransition(round));

            if (round == missionData.rounds.Count - 1)
            {
                SoundManager.Instance.StopMusic();
                SoundManager.Instance.PlayLoopingSound(bossWarning);
                TitleTextInstantiate("Boss");
                yield return new WaitForSeconds(4f);
                SoundManager.Instance.StopLoopingSound();
                SoundManager.Instance.PlayMusicLoop(bossBattleMusic);
            }

            if (round < missionData.rounds.Count)
                SummonEnemyUnitsForRound(round);

            yield return new WaitForSeconds(2f);
        }
    }

    public void ReceiveRewards()
    {
        HeaderPlayerData.openRewardsScreen = true;
        PlayerData.zel += totalZelReward;
        PlayerData.karma += totalKarmaReward;
        oldLevel = PlayerData.level;
        oldExperience = PlayerData.experience;
        xpObtained = missionData.experienceReward;

        PlayerData.AddExperience(missionData.experienceReward);

        HashSet<string> unitIdsAlreadyMarkedNew = new HashSet<string>();

        foreach (UnitDropData u in unitDrops)
        {
            int key = PlayerUnitInventoryDatabase._nextKey;
            if (!PlayerData.unitDex.Contains(u.unitId) && !unitIdsAlreadyMarkedNew.Contains(u.unitId))
            {
                newUnits.Add(key);
                unitIdsAlreadyMarkedNew.Add(u.unitId);
            }
            PlayerUnitInventoryDatabase.AddUnit(UnitRegistry.GetUnitById(u.unitId), u.type, u.unitLevel);
        }

        PlayerData.SaveDataToJson();
    }

    // ─────────────────────────────────────────────────────────────
    //  Effect ticking
    // ─────────────────────────────────────────────────────────────

    /// Decrements all timed effects on every living unit.
    /// Call once per turn (we call it twice — after player phase and after enemy phase,
    /// so each team's buffs tick on both halves of the turn, matching the original game).
    private void TickAllEffects()
    {
        foreach (var u in playerTeam.units)
            if (u.currentState != UnitState.Dead) u.TickEffects();

        foreach (var u in enemyTeam.units)
            if (u.currentState != UnitState.Dead) u.TickEffects();
    }

    // ─────────────────────────────────────────────────────────────
    //  Player turn helpers
    // ─────────────────────────────────────────────────────────────

    SamAnimator[] TitleTextInstantiate(string text)
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(text));
        obj.GetComponent<RectTransform>().SetParent(BattleUI.titleTextLayer, false);
        return obj.GetComponentsInChildren<SamAnimator>();
    }

    void PlayerTurn()
    {
        Debug.Log("Player Turn Started");
        UpdateUnitLayering(true);

        unitsThatMustAct.Clear();
        unitsThatActed.Clear();
        RefreshFocusFireState();

        if (isCombatAutomatic)
        {
            autoAttackCoroutine = StartCoroutine(DelayedAutoAttack());
        }
        else
        {
            foreach (var unit in playerTeam.units)
                if (unit.currentState != UnitState.Dead)
                    unitsThatMustAct.Add(unit);

            playerTurnWaitingForInput = true;
            foreach (var slot in BattleUI.unitSlots)
                if (slot.unit != null && slot.unit.currentState != UnitState.Dead)
                    slot.canBeClicked = true;
        }

        enemyTeam.CheckDeadUnits();
    }

    IEnumerator DelayedAutoAttack()
    {
        foreach (var player in playerTeam.units.ToList())
        {
            if (player.currentState == UnitState.Dead || player.currentState == UnitState.Attacking) continue;
            if (unitsThatActed.Contains(player)) continue;

            unitsThatActed.Add(player);
            Ability ability = player.GetAbilityToUse(cutInAnimator);

            var targets = ResolveAttackTargets(player, ability);

            if (targets.Count > 0)
            {
                if (IsAttackAbility(ability)) StartCoroutine(UnitAttackCoroutine(player, ability, targets));
                else player.UseAbility(ability, targets);
            }

            yield return null;
        }
    }

    public void PlayerIssuedCommand(UnitBehaviour unit)
    {
        if (enemyTeam.IsDefeated()) { EndManualPlayerTurn(); return; }
        if (unitsThatActed.Contains(unit) || unit.currentState == UnitState.Attacking) return;

        Ability ability = unit.GetBaseAbility();
        var targets = ResolveAttackTargets(unit, ability);

        if (IsAttackAbility(ability)) StartCoroutine(UnitAttackAndCheckCoroutine(unit, ability, targets));
        else unit.UseAbility(ability, targets);

        unitsThatActed.Add(unit);
        unit.unitSlotUI.canBeClicked = false;
        if (unitsThatActed.Count == unitsThatMustAct.Count) EndManualPlayerTurn();
    }

    public void PlayerSlideCommand(UnitBehaviour unit, Vector2 slideDirection, Ability abilityToUse, CutInType cutInType)
    {
        if (unitsThatActed.Contains(unit) || unit.currentState == UnitState.Attacking) return;
        if (slideDirection.y >= 0f)
        {
            var targets = ResolveAttackTargets(unit, abilityToUse);

            if (!unit.isEnemyUnit) cutInAnimator.PlayCutIn(unit.unitData.unitFullArt, abilityToUse.abilityName, cutInType);

            if (IsAttackAbility(abilityToUse)) StartCoroutine(UnitAttackAndCheckCoroutine(unit, abilityToUse, targets));
            else unit.UseAbility(abilityToUse, targets);

            unitsThatActed.Add(unit);
            unit.unitSlotUI.canBeClicked = false;
            if (unitsThatActed.Count == unitsThatMustAct.Count) EndManualPlayerTurn();
        }
    }
    
    private IEnumerator UnitAttackAndCheckCoroutine(UnitBehaviour unit, Ability ability, List<UnitBehaviour> targets)
    {
        yield return StartCoroutine(UnitAttackCoroutine(unit, ability, targets));

        if (enemyTeam.IsDefeated() && playerTurnWaitingForInput)
            EndManualPlayerTurn();
    }

    public void PlayerGuardCommand(UnitBehaviour unit, Vector2 slideDirection)
    {
        if (unitsThatActed.Contains(unit)) return;
        if (slideDirection.y <= 0f)
        {
            // Slide DOWN -- guard
            unit.isGuarding = true;
            unitsThatActed.Add(unit);
            unit.unitSlotUI.canBeClicked = false;
            if (unitsThatActed.Count == unitsThatMustAct.Count) EndManualPlayerTurn();
        }
    }

    private void EndManualPlayerTurn()
    {
        playerTurnWaitingForInput = false;
        foreach (var slot in BattleUI.unitSlots) slot.canBeClicked = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  Attack ability detection
    // ─────────────────────────────────────────────────────────────

    private bool IsAttackAbility(Ability ability)
    {
        // If an ability has hitFrames, it's an attack ability
        return ability.hitFrames != null && ability.hitFrames.Count > 0;
    }

    // ─────────────────────────────────────────────────────────────
    //  Target selection
    // ─────────────────────────────────────────────────────────────

    // New instance fields (replace the local vars that used to live inside DelayedAutoAttack)
    private List<UnitBehaviour> availableEnemies = new List<UnitBehaviour>();
    private List<HealthPrediction> predictions = new List<HealthPrediction>();

    // Call this once whenever a player phase (re)starts targeting from scratch
    private void RefreshFocusFireState()
    {
        availableEnemies = enemyTeam.units.FindAll(u => u.currentState != UnitState.Dead);
        predictions = availableEnemies.Select(e => new HealthPrediction(e, e.currentHealth)).ToList();
    }

    // Shared by auto AND manual — this is the single source of truth for "who gets hit"
    private List<UnitBehaviour> ResolveAttackTargets(UnitBehaviour actor, Ability ability)
    {
        bool hasLockedTarget = selectedEnemyUnit != null;

        if (hasLockedTarget && ability.targetType == TargetType.SingleEnemy)
        {
            // Locked: every single-target attack goes here, full stop.
            // No liveness check — if it's already dying from earlier hits
            // this turn, later attackers still commit to it instead of
            // hopping to a fresh enemy.
            return new List<UnitBehaviour> { selectedEnemyUnit };
        }

        var pool = availableEnemies.Count > 0
            ? availableEnemies
            : enemyTeam.units.FindAll(u => u.currentState != UnitState.Dead);

        var targets = GetTargets(pool, actor, ability);
        targets.RemoveAll(t => t == null);

        foreach (var target in targets)
        {
            var pred = predictions.Find(p => p.unit == target);
            if (pred == null) continue;
            pred.predictedHealth -= Mathf.CeilToInt(actor.GetDamage(ability, target));
            if (pred.predictedHealth <= 0) availableEnemies.Remove(target);
        }

        return targets;
    }

    List<UnitBehaviour> GetTargets(List<UnitBehaviour> availableUnits, UnitBehaviour unit, Ability ability)
    {
        var targets     = new List<UnitBehaviour>();
        var allies      = unit.isEnemyUnit
                            ? enemyTeam.units.FindAll(u => u.currentState != UnitState.Dead)
                            : playerTeam.units;

        switch (ability.targetType)
        {
            case TargetType.SingleEnemy: targets.Add(GetUnitWithLowestHealth(availableUnits));  break;
            case TargetType.AllEnemies:  targets = new List<UnitBehaviour>(availableUnits);     break;
            case TargetType.SingleAlly:  targets.Add(GetUnitWithLowestHealth(allies));          break;
            case TargetType.AllAllies:   targets = new List<UnitBehaviour>(allies);             break;
            case TargetType.Self:        targets.Add(unit);                                     break;
        }
        return targets;
    }

    List<UnitBehaviour> GetTargets(List<UnitBehaviour> availableUnits, UnitBehaviour unit, Action action)
    {
        // Resolve the candidate pool from ActionTargetType
        List<UnitBehaviour> pool;
        switch (action.targetType)
        {
            case ActionTargetType.Self:
                return new List<UnitBehaviour> { unit };

            case ActionTargetType.AllyParty:
                pool = unit.isEnemyUnit
                    ? enemyTeam.units.FindAll(u => u.currentState != UnitState.Dead)
                    : playerTeam.units.FindAll(u => u.currentState != UnitState.Dead);
                break;

            case ActionTargetType.EnemyParty:
            default:
                pool = unit.isEnemyUnit
                    ? playerTeam.units.FindAll(u => u.currentState != UnitState.Dead)
                    : enemyTeam.units.FindAll(u => u.currentState != UnitState.Dead);
                break;
        }

        if (pool.Count == 0) return new List<UnitBehaviour>();

        // Pick from pool using TargetCondition
        switch (action.targetCondition)
        {
            case TargetCondition.Random:
                return new List<UnitBehaviour> { pool[Random.Range(0, pool.Count)] };

            case TargetCondition.HpMin:
                return new List<UnitBehaviour> { pool.OrderBy(u => u.currentHealth).First() };

            case TargetCondition.HpMax:
                return new List<UnitBehaviour> { pool.OrderByDescending(u => u.currentHealth).First() };

            case TargetCondition.HpUnder25:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth < 0.25f), pool) };

            case TargetCondition.HpOver25:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth >= 0.25f), pool) };

            case TargetCondition.HpUnder50:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth < 0.5f), pool) };

            case TargetCondition.HpOver50:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth >= 0.5f), pool) };

            case TargetCondition.HpUnder75:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth < 0.75f), pool) };

            case TargetCondition.HpOver75:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => (float)u.currentHealth / u.unitData.maxHealth >= 0.75f), pool) };

            case TargetCondition.AtkMin:
                return new List<UnitBehaviour> { pool.OrderBy(u => u.unitData.atk).First() };

            case TargetCondition.AtkMax:
                return new List<UnitBehaviour> { pool.OrderByDescending(u => u.unitData.atk).First() };

            case TargetCondition.DefMin:
                return new List<UnitBehaviour> { pool.OrderBy(u => u.unitData.def).First() };

            case TargetCondition.DefMax:
                return new List<UnitBehaviour> { pool.OrderByDescending(u => u.unitData.def).First() };

            case TargetCondition.Poison:
                //return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => u.HasStatusEffect(StatusEffect.Poison)), pool) };

            case TargetCondition.Paralysis:
                //return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => u.HasStatusEffect(StatusEffect.Paralysis)), pool) };

            case TargetCondition.BadStatus:
                //return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => u.HasAnyStatusEffect()), pool) };

            case TargetCondition.Guard:
                //return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => u.currentState == UnitState.Guard), pool) };

            case TargetCondition.Weakness:
                //return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element.IsWeakAgainst(u.unitData.element)), pool) };

            // Conditions we can't evaluate without BB gauge data — fall back to random
            case TargetCondition.BbFull:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.bcCount >= unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost), pool) };
            case TargetCondition.BbOver50:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.bcCount >= unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost/2), pool) };
            case TargetCondition.BbUnder50:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.bcCount < unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost/2), pool) };
            case TargetCondition.BbAttack: //use bb type from unit
            case TargetCondition.BbHeal:
            case TargetCondition.BbSupport:
            case TargetCondition.AtkDown:
            case TargetCondition.DefDown:
            case TargetCondition.HealDown:
            case TargetCondition.StatDebuffed:
            case TargetCondition.StatBuffed:
            case TargetCondition.ElemFire:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Fire), pool) };
            case TargetCondition.ElemWater:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Water), pool) };
            case TargetCondition.ElemEarth:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Earth), pool) };
            case TargetCondition.ElemThunder:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Thunder), pool) };
            case TargetCondition.ElemLight:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Light), pool) };
            case TargetCondition.ElemDark:
                return new List<UnitBehaviour> { PickOrRandom(pool.FindAll(u => unit.unitData.element == ElementalType.Dark), pool) };
            case TargetCondition.Non:
            default:
                return new List<UnitBehaviour> { pool[Random.Range(0, pool.Count)] };
        }
    }

    // Returns a random unit from candidates if any exist, otherwise falls back to the full pool.
    private UnitBehaviour PickOrRandom(List<UnitBehaviour> candidates, List<UnitBehaviour> fallback)
    {
        var source = candidates.Count > 0 ? candidates : fallback;
        return source[Random.Range(0, source.Count)];
    }

    // ─────────────────────────────────────────────────────────────
    //  Enemy turn
    // ─────────────────────────────────────────────────────────────

    void EnemyTurn()
    {
        Debug.Log("Enemy Turn Started");
        UpdateUnitLayering(false);

        foreach (var enemy in enemyTeam.units)
        {
            if (enemy.currentState == UnitState.Dead) continue;
            if (playerTeam.IsDefeated()) break;

            List<Action> actions = enemy.GetEnemyActions();
            foreach(var action in actions){
                var targets     = GetTargets(playerTeam.units.FindAll(u => u.currentState != UnitState.Dead), enemy, action);

                if (action.actionType == ActionType.Attack) StartCoroutine(UnitAttackCoroutine(enemy, enemy.GetBaseAbility(), targets));
                else                          enemy.UseAbility(enemy.GetBaseAbility(), targets);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Utilities
    // ─────────────────────────────────────────────────────────────

    public UnitBehaviour GetUnitWithLowestHealth(List<UnitBehaviour> units)
    {
        if (units.Count == 0) return null;

        UnitBehaviour lowest = null;
        int lowestHP = int.MaxValue;

        foreach (var u in units)
        {
            if (u.currentState == UnitState.Dead) continue;
            if (u.currentHealth < lowestHP) { lowestHP = u.currentHealth; lowest = u; }
        }

        if (lowest != null) return lowest;

        var alive = units.FindAll(u => u.currentState != UnitState.Dead);
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }

    // ─── Frame-delay table for moving units (type 1) ───────────────────────

    // Visual travel speeds (units/sec) per speedType 1–5
    // Higher speedType = faster unit, tune these to taste
    private static readonly float[] MoveSpeedByType = { 3f, 4f, 5.5f, 7.5f, 11f };

    private IEnumerator MoveUnitToPositionSafe(Transform unitTransform, Vector3 targetPosition, float speed, float stopDistance = 0.05f, float maxDuration = 8f)
    {
        float elapsed = 0f;

        while (Vector3.Distance(unitTransform.position, targetPosition) > stopDistance)
        {
            if (elapsed >= maxDuration)
            {
                unitTransform.position = targetPosition;
                break;
            }

            float dt = Time.deltaTime;
            unitTransform.position = Vector3.MoveTowards(unitTransform.position, targetPosition, speed * dt);
            elapsed += dt;
            yield return null;
        }

        unitTransform.position = targetPosition;
    }

    IEnumerator UnitAttackCoroutine(UnitBehaviour attacker, Ability ability, List<UnitBehaviour> targets)
    {
        UnitBehaviour target = targets[0];
        attacker.currentState = UnitState.Attacking;

        int   moveType  = attacker.unitData.moveTypeAttack;
        int   speedType = attacker.unitData.speedTypeAttack;
        float moveSpeedRaw = attacker.unitData.moveSpeedAttack;

        Vector3 attackPos;

        if (moveType == 1 || moveType == 2)
        {
            bool hitsAll = ability.targetType == TargetType.AllEnemies || ability.targetType == TargetType.AllAllies;

            if (hitsAll)
            {
                float xOffset = attacker.isEnemyUnit ? -1f : 1f;
                Vector3 singleAttackPos = target.transform.position + new Vector3(xOffset, 0f, 0f);
                attackPos = targets.Count > 1 ? centerArenaPos.position : singleAttackPos;
            }
            else
            {
                float xOffset = attacker.isEnemyUnit ? -1f : 1f;
                attackPos = target.transform.position + new Vector3(xOffset, 0f, 0f);
            }
        }
        else
        {
            attackPos = attacker.transform.position; // stationary
        }

        // ── 1. Movement phase ─────────────────────────────────────────
        if (moveType == 1) // Moving — walk across the screen
        {
            attacker.SetAnimation("Move");
            float speed = MoveSpeedByType[Mathf.Clamp(speedType - 1, 0, 4)] * 2f;

            yield return StartCoroutine(MoveUnitToPositionSafe(
                attacker.transform,
                attackPos,
                speed * 2f,
                0.1f,
                8f));
        }
        else if (moveType == 2) // Teleporting — brief flash delay then snap
        {
            attacker.SetAnimation("Move");
            float teleportDelay = moveSpeedRaw / 60f;
            yield return new WaitForSeconds(teleportDelay);
            attacker.transform.position = attackPos;
        }
        else // Stationary — no movement, just in-place delay
        {
            float stationaryDelay = moveSpeedRaw / 60f;
            yield return new WaitForSeconds(stationaryDelay);
        }

        attacker.transform.position = attackPos;

        // ── 2. Effect delay + attack (hit frames drive actual damage) ─
        yield return new WaitForSeconds(1f / 60f);

        attacker.SetAnimation("Attack");
        attacker.UseAbility(ability, targets);

        // Wait for the full hit animation to complete
        float waitTime = 0f;
        for (int i = 0; i < ability.hitFrames.Count; i++)
        {
            int prevFrame = i == 0 ? 0 : ability.hitFrames[i - 1].frame;
            waitTime += ability.hitFrames[i].frame - prevFrame;
        }
        yield return new WaitForSeconds(waitTime / 60f + 1f);

        // ── 3. Return movement ────────────────────────────────────────
        if (moveType == 1 || moveType == 2)
        {
            attacker.SetAnimation("Move");
            attacker.FlipImage(true);

            float returnSpeed = MoveSpeedByType[Mathf.Clamp(speedType - 1, 0, 4)] * 2f;
            yield return StartCoroutine(MoveUnitToPositionSafe(
                attacker.transform,
                attacker.originalPosition.position,
                returnSpeed,
                0.1f,
                8f));
        }

        attacker.transform.position = attacker.originalPosition.position;
        attacker.SetAnimation("Idle");
        attacker.FlipImage(false);
        attacker.currentState = UnitState.Idle;
    }

    // Returns the slot index (0–5) of a unit based on playerUnitPositions
    private int GetSlotIndex(UnitBehaviour unit)
    {
        for (int i = 0; i < playerUnitPositions.Count; i++)
            if (playerUnitPositions[i].position == unit.originalPosition.position)
                return i;
        return 0;
    }

    void SelectEnemyUnit()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var unit = hit.collider.GetComponent<UnitBehaviour>();
            if (unit != null)
            {
                selectedEnemyUnit = unit;
                Debug.Log($"Selected: {selectedEnemyUnit.unitData.unitName}");
            }
        }
    }

    void UpdateUnitLayering(bool isPlayerTurn)
    {
        if (isPlayerTurn)
        {
            BattleUI.playerUnitsLayer.transform.SetAsLastSibling();
            BattleUI.enemyUnitsLayer.transform.SetAsFirstSibling();
        }
        else
        {
            BattleUI.enemyUnitsLayer.transform.SetAsLastSibling();
            BattleUI.playerUnitsLayer.transform.SetAsFirstSibling();
        }
        SortUnitsByY(playerTeam.units);
        SortUnitsByY(enemyTeam.units);
    }

    void SortUnitsByY(List<UnitBehaviour> units)
    {
        if (units.Count == 0) return;
        units.RemoveAll(u => u == null);
        units.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        for (int i = 0; i < units.Count; i++)
            units[i].transform.SetSiblingIndex(i);
    }

    //UBB

    public static void AddUbbCount(int amount)
    {
        ubbCount += amount;
        if (ubbCount > ubbCurrentMaxGauge) ubbCount = ubbCurrentMaxGauge;
    }

    public static void MaxUBBGaugeUpdate()
    {
        ubbCurrentMaxGauge = ubbCurrentMaxGauge + 5000;
    }

    private class HealthPrediction
    {
        public UnitBehaviour unit;
        public int predictedHealth;
        public HealthPrediction(UnitBehaviour u, int hp) { unit = u; predictedHealth = hp; }
    }
}
public struct UnitDropData
{
    public string unitId;
    public int unitLevel;
    public UnitType type;
}