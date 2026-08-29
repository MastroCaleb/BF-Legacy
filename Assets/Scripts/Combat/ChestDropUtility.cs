using UnityEngine;
using UnityEngine.UI;

public static class ChestDropUtility
{
    // Same roll table TreasureChestDropBehaviour.Open() used to do inline.
    public static void OpenDrops(Enemy enemyData, Vector3 dropOrigin)
    {
        float roll = Random.Range(25f, 100f);

        if (roll > 0f)
        {
            DropBattleCrystals(enemyData, dropOrigin);
            DropHeartCrystals(enemyData, dropOrigin);
        }
        if (roll > 25f) DropZelCoins(enemyData, dropOrigin);
        if (roll > 50f) DropKarmaOrbs(enemyData, dropOrigin);
        if (roll > 75f) DropItems(enemyData, dropOrigin);
    }

    public static void DropZelCoins(Enemy enemyData, Vector3 dropOrigin)
    {
        int total = (int)enemyData.zelMaxDrop;
        int count = (int)enemyData.zelDropCount;
        if (count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int coinValue = baseValue + (i < remainder ? 1 : 0);
            GameObject c = Object.Instantiate(PrefabCache.Get("ZelCoin"), dropOrigin + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.zelPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = coinValue;
        }
    }

    public static void DropKarmaOrbs(Enemy enemyData, Vector3 dropOrigin)
    {
        int total = (int)enemyData.karmaMaxDrop;
        int count = (int)enemyData.karmaDropCount;
        if (count == 0) return;

        int baseValue = total / count;
        int remainder = total % count;

        for (int i = 0; i < count; i++)
        {
            int orbValue = baseValue + (i < remainder ? 1 : 0);
            GameObject c = Object.Instantiate(PrefabCache.Get("KarmaOrb"), dropOrigin + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            c.GetComponent<DropBehaviour>().target = BattleUI.karmaPoint.gameObject;
            c.GetComponent<DropBehaviour>().valueOfDrop = orbValue;
        }
    }

    public static void DropBattleCrystals(Enemy enemyData, Vector3 dropOrigin)
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Object.Instantiate(PrefabCache.Get("BattleCrystal"), dropOrigin + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            var target = GetBattleCrystalTarget();
            if (target != null) c.GetComponent<DropBehaviour>().target = target.gameObject;
            BattleManager.totalBcDropCount++;
        }
    }

    public static void DropHeartCrystals(Enemy enemyData, Vector3 dropOrigin)
    {
        for (int i = 0; i < enemyData.treasureDrop.bcOrHcAmount / 2; i++)
        {
            GameObject c = Object.Instantiate(PrefabCache.Get("HeartCrystal"), dropOrigin + new Vector3(0, 1f, 0), Quaternion.identity);
            c.transform.SetParent(BattleUI.dropsLayer);
            var target = GetHeartCrystalTarget();
            if (target != null) c.GetComponent<DropBehaviour>().target = target.gameObject;
            BattleManager.totalHcDropCount++;
        }
    }

    public static void DropItems(Enemy enemyData, Vector3 dropOrigin)
    {
        ItemDropData dropData = new ItemDropData
        {
            itemName = enemyData.treasureDrop.itemName,
            itemCount = 1
        };

        GameObject c = Object.Instantiate(PrefabCache.Get("ItemDrop"));
        c.transform.SetParent(BattleUI.dropsLayer);
        c.transform.position = dropOrigin + new Vector3(0, 1f, 0);
        c.GetComponent<Image>().sprite = ItemDatabase.GetItemByName(dropData.itemName).thumbnailSprite;
        c.GetComponent<DropBehaviour>().itemDropData = dropData;
        c.GetComponent<DropBehaviour>().target = BattleUI.unitPoint.gameObject;
    }

    static UnitBehaviour GetBattleCrystalTarget()
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

        var notFull = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead && u.bcCount < GetMaxBC(u));
        if (notFull.Count > 0) return notFull[Random.Range(0, notFull.Count)];

        var alive = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead);
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }

    static UnitBehaviour GetHeartCrystalTarget()
    {
        int GetMaxHP(UnitBehaviour u) => u.unitData.maxHealth + u.inventoryData.hpLevelUpBonus + u.inventoryData.hpImpBonus;

        var notFull = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead && u.currentHealth < GetMaxHP(u));
        if (notFull.Count > 0) return notFull[Random.Range(0, notFull.Count)];

        var alive = BattleManager.playerTeam.units.FindAll(u => u.currentState != UnitState.Dead);
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }
}