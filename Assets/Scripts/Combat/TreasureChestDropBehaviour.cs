using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TreasureChestDropBehaviour : MonoBehaviour, IPointerClickHandler
{
    public Enemy enemyData;
    public bool isMimic = false;
    public AudioClip openChest;
    public SamAnimator touch;
    BraveFrontierFrameAnimator animator;
    RectTransform rect;
    public static bool interactable = false;
    public bool isOpen = false;

    void Awake()
    {
        BattleManager.chests.Add(this);
        rect = GetComponent<RectTransform>();
        animator = GetComponent<BraveFrontierFrameAnimator>();

        isMimic = Random.Range(0f, 100f) < BattleManager.dungeonLevelData.mimicChance;

        string anim = isMimic ? "Vibe" : "Idle";
        animator.SetAnimation(anim);

        animator.InitializeAnimator();
    }

    void Start()
    {
        rect.localScale = Vector3.one;
        rect.SetParent(BattleUI.dropsLayer, false);
    }

    void Update()
    {
        if(interactable && !touch.isActiveAndEnabled && !isOpen)
        {
            touch.gameObject.SetActive(true);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(interactable && !isOpen)
            Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        if (isMimic)
        {
            RevealMimic();
            return;
        }

        SoundManager.Instance.PlaySound(openChest);
        touch.gameObject.SetActive(false);
        animator.SetAnimation("Open");
        animator.loop = false;

        ChestDropUtility.OpenDrops(enemyData, transform.position);

        StartCoroutine(RemoveChest());
    }

    void RevealMimic()
    {
        touch.gameObject.SetActive(false);
        animator.SetAnimation("Open"); // swap for a dedicated "reveal/chomp" clip if you make one
        animator.loop = false;

        // NOTE: gameObject is deliberately NOT destroyed here — BattleManager
        // destroys it once it converts this chest into a real enemy unit.
        BattleManager.QueueMimicChest(this);

        StartCoroutine(RemoveChest());
    }

    IEnumerator RemoveChest()
    {
        yield return new WaitForSeconds(1);
        StartCoroutine(FadeToClear(1));
        
        yield return new WaitForSeconds(1f);
        BattleManager.chests.Remove(this);
        Destroy(gameObject);
    }

    public IEnumerator FadeToClear(float duration)
    {
        float elapsed = 0f;

        Image img = GetComponent<Image>();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            img.color = Color.Lerp(Color.white, Color.clear, elapsed / duration);
            yield return null;
        }
        img.color = Color.clear;
    }

    public void DropZelCoins()
    {
        int total = (int)enemyData.zelMaxDrop;
        int count = (int)enemyData.zelDropCount;

        if(count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int coinValue = baseValue + (i < remainder ? 1 : 0);

            GameObject c = Instantiate(PrefabCache.Get("ZelCoin"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.zelPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = coinValue;
        }
    }

    public void DropKarmaOrbs()
    {
        int total = (int)enemyData.karmaMaxDrop;
        int count = (int)enemyData.karmaDropCount;

        if(count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int orbValue = baseValue + (i < remainder ? 1 : 0);

            GameObject c = Instantiate(PrefabCache.Get("KarmaOrb"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.karmaPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = orbValue;
        }
    }

    public void DropBattleCrystals()
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Instantiate(PrefabCache.Get("BattleCrystal"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = GetBattleCrystalTarget().gameObject;
            BattleManager.totalBcDropCount++;
        }
    }

    public void DropHeartCrystals()
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Instantiate(PrefabCache.Get("HeartCrystal"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = GetHeartCrystalTarget().gameObject;
            BattleManager.totalHcDropCount++;
        }
    }

    public void DropItems()
    {
        ItemDropData dropData = new ItemDropData
        {
            itemName = enemyData.treasureDrop.itemName,
            itemCount = 1
        };

        GameObject c = Instantiate(PrefabCache.Get("ItemDrop"));
        c.transform.SetParent(BattleUI.dropsLayer);
        c.transform.position = transform.position + new Vector3(0, 1f, 0);
        c.GetComponent<Image>().sprite = ItemDatabase.GetItemByName(dropData.itemName).thumbnailSprite;
        c.GetComponent<DropBehaviour>().itemDropData = dropData;
        c.GetComponent<DropBehaviour>().target = BattleUI.unitPoint.gameObject;
    }

    UnitBehaviour GetBattleCrystalTarget()
    {
        int GetMaxBC(UnitBehaviour u)
        {
            int max = 0;
            int level = u.inventoryData.currentBBLevel - 1; 
            if (u.unitData.bbAbility != null && u.unitData.bbAbility.abilityName != "Unnamed")
                max += u.unitData.bbAbility.levels[level].bcCost;
            if (u.unitData.sbbAbility != null && u.unitData.sbbAbility.abilityName != "Unnamed")
                max += u.unitData.sbbAbility.levels[level].bcCost;
            return Mathf.Max(max, 1);
        }

        var notFull = BattleManager.playerTeam.units.FindAll(u =>
            u.currentState != UnitState.Dead && u.bcCount < GetMaxBC(u));

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
}