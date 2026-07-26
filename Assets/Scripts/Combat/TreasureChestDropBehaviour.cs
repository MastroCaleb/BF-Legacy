using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TreasureChestDropBehaviour : MonoBehaviour, IPointerClickHandler
{
    public Enemy enemyData;
    public AudioClip openChest;
    public SamAnimator touch;
    BraveFrontierFrameAnimator animator;
    RectTransform rect;
    public static bool interactable = false;
    bool isOpen = false;

    void Awake()
    {
        BattleManager.chests.Add(this);
        rect = GetComponent<RectTransform>();
        animator = GetComponent<BraveFrontierFrameAnimator>();

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
        isOpen = true;
        SoundManager.Instance.PlaySound(openChest);
        touch.gameObject.SetActive(false);
        animator.SetAnimation("Open");
        animator.loop = false;

        // Items not implemented yet, so the roll is split between Zel, Karma, and BC/HC.
        // BC/HC is weighted higher since it's usually guaranteed to drop at least one of each.
        float roll = Random.Range(25f, 100f);

        if(roll > 0)
        {
            DropBattleCrystals();
            DropHeartCrystals();
        }

        if(roll > 25f)
            DropZelCoins();

        if(roll > 50f)
            DropKarmaOrbs();

        //Add items later

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

            GameObject c = Instantiate(Resources.Load<GameObject>("ZelCoin"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
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

            GameObject c = Instantiate(Resources.Load<GameObject>("KarmaOrb"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.karmaPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = orbValue;
        }
    }

    public void DropBattleCrystals()
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Instantiate(Resources.Load<GameObject>("BattleCrystal"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = GetBattleCrystalTarget().gameObject;
            BattleManager.totalBcDropCount++;
        }
    }

    public void DropHeartCrystals()
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Instantiate(Resources.Load<GameObject>("HeartCrystal"), transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = GetHeartCrystalTarget().gameObject;
            BattleManager.totalHcDropCount++;
        }
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