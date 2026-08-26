using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class PlayerUnitInventoryDatabase
{
    private static string SavePath => Application.persistentDataPath + "/unitinventory.json";

    // Wrapper to persist the auto-incrementing key alongside the dictionary
    private class SaveData
    {
        public int nextKey = 0;
        public Dictionary<int, UnitInventoryData> playerUnits = new Dictionary<int, UnitInventoryData>();
    }

    public static int _nextKey = 0;
    public static Dictionary<int, UnitInventoryData> playerUnits = new Dictionary<int, UnitInventoryData>();

    // ─── Persistence ────────────────────────────────────────────────────────────

    public static void SaveToJson()
    {
        var saveData = new SaveData { nextKey = _nextKey, playerUnits = playerUnits };
        string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static void LoadFromJson()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        var saveData = JsonConvert.DeserializeObject<SaveData>(json);

        playerUnits = saveData.playerUnits;
        _nextKey = saveData.nextKey;

        // Re-link Unit references from your unit registry
        foreach (var kvp in playerUnits)
        {
            kvp.Value.unit = UnitRegistry.GetUnitById(kvp.Value.unitId);
        }
    }

    // ─── Add / Remove ────────────────────────────────────────────────────────────

    public static int AddUnit(Unit unit, UnitType? type = null, int level = 1, bool saveAfterAdd = true, bool isNew = false)
    {
        if(!PlayerData.unitDex.Contains(unit.unitId)) 
            PlayerData.unitDex.Add(unit.unitId);

        UnitInventoryData newUnitData = new UnitInventoryData
        {
            unit = unit,
            unitId = unit?.unitId,
            type = type ?? GetRandomUnitType(),
            currentLevel = 1,
            currentExperience = 0,
            currentBBLevel = 1,
            currentSBBLevel = 1,
            hpImpBonus = 0,
            atkImpBonus = 0,
            defImpBonus = 0,
            recImpBonus = 0,
            isNew = isNew
        };
        playerUnits.Add(_nextKey++, newUnitData);
        UnitInventoryData addedUnit = GetUnitByKey(_nextKey - 1);
        ModifyStats(addedUnit, level);
        if (saveAfterAdd) SaveToJson();
        return _nextKey - 1;
     }

    public static void RemoveUnit(int unitKey)
    {
        // Keys are permanent unique IDs — no rearranging needed, gaps are intentional
        playerUnits.Remove(unitKey);
        SaveToJson();
    }

    // ─── Queries ─────────────────────────────────────────────────────────────────

    public static UnitInventoryData GetUnitByKey(int unitKey)
    {
        return playerUnits.TryGetValue(unitKey, out var unit) ? unit : null;
    }

    public static int GetKeyByUnit(UnitInventoryData targetUnit)
    {
        return playerUnits
            .FirstOrDefault(kvp => kvp.Value == targetUnit)
            .Key;
    }

    public static UnitInventoryData GetUnitWithId(string unitId)
    {
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.unit.unitId == unitId)
                return kvp.Value;
        }
        return null;
    }

    public static int GetUnitKeyWithId(string unitId)
    {
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.unit.unitId == unitId)
                return kvp.Key;
        }
        return -1;
    }

    public static List<UnitInventoryData> GetAllUnitsWithId(string unitId)
    {
        List<UnitInventoryData> units = new List<UnitInventoryData>();
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.unit.unitId == unitId)
                units.Add(kvp.Value);
        }
        return units;
    }

    public static List<int> GetAllUnitKeysWithId(string unitId)
    {
        List<int> units = new List<int>();
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.unit.unitId == unitId)
                units.Add(kvp.Key);
        }
        return units;
    }

    public static int GetUnitCountWithId(string unitId)
    {
        int count = 0;
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.unit.unitId == unitId)
                count++;
        }
        return count;
    }

    public static List<UnitInventoryData> GetUnitsByType(UnitType type)
    {
        List<UnitInventoryData> units = new List<UnitInventoryData>();
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.type == type)
                units.Add(kvp.Value);
        }
        return units;
    }

    // ─── Gameplay Logic ───────────────────────────────────────────────────────────

    public static void EvolveUnit(int baseUnitKey)
    {
        UnitInventoryData baseUnit = playerUnits[baseUnitKey];

        Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
        foreach(string unitId in baseUnit.unit.evoMats)
        {
            requiredCounts[unitId] = requiredCounts.TryGetValue(unitId, out int c) ? c + 1 : 1;
        }

        foreach(var kvp in requiredCounts)
        {
            List<int> keysToConsume = GetUnitKeysWithId(kvp.Key, kvp.Value);
            foreach(int key in keysToConsume)
            {
                MainUI.inventoryRenderer.DestroySlot(key);
                RemoveUnit(key);
            }
        }

        PlayerData.zel -= baseUnit.unit.evoZelCost;

        SwapWithEvolveUnit(baseUnitKey, UnitRegistry.GetUnitById(baseUnit.unit.evoInto));
    }

    public static List<int> GetUnitKeysWithId(string unitId, int count)
    {
        List<int> keys = new List<int>();
        foreach (var kvp in playerUnits)
        {
            if (keys.Count >= count) break;
            if (kvp.Value.unit.unitId == unitId)
                keys.Add(kvp.Key);
        }
        return keys;
    }

    public static bool CanEvolve(int baseUnitKey)
    {
        UnitInventoryData baseUnit = playerUnits[baseUnitKey];

        if(baseUnit.currentLevel != baseUnit.unit.maxLevel) return false;
        if(baseUnit.unit.evoZelCost >= PlayerData.zel) return false;

        Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
        foreach(string unitId in baseUnit.unit.evoMats)
        {
            requiredCounts[unitId] = requiredCounts.TryGetValue(unitId, out int c) ? c + 1 : 1;
        }

        foreach(var kvp in requiredCounts)
        {
            if(GetUnitCountWithId(kvp.Key) < kvp.Value) return false;
        }

        return true;
    }

    public static void SellUnits(List<int> sellUnits)
    {
        foreach (int u in sellUnits)
        {
            int sellPrice = GetUnitByKey(u).unit.sellPrice;
            PlayerData.zel += sellPrice < 0 ? sellPrice * -1 : sellPrice;
            MainUI.inventoryRenderer.DestroySlot(u);
            RemoveUnit(u);
        }

        PlayerData.SaveDataToJson();
        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
    }

    public static void FuseUnits(int baseUnitKey, List<int> fodderUnitKeys, SuccessType success)
    {
        if (!playerUnits.ContainsKey(baseUnitKey)) return;

        UnitInventoryData baseUnit = playerUnits[baseUnitKey];
        int totalExp = 0;
        int totalZelCost = 0;

        foreach (int fodderKey in fodderUnitKeys)
        {
            if (playerUnits.ContainsKey(fodderKey))
            {
                UnitInventoryData fodderUnit = playerUnits[fodderKey];
                totalExp += CalculateTotalEXPFromUnit(baseUnit, fodderUnit);
                totalZelCost += ZelFusionCost(baseUnitKey, fodderKey);
                BBLevelUp(baseUnitKey, fodderKey);
                MainUI.inventoryRenderer.DestroySlot(fodderKey);
                RemoveUnit(fodderKey);
            }
        }

        float successMultiplier = success switch
        {
            SuccessType.GreatSuccess => 1.5f,
            SuccessType.SuperSuccess => 2.0f,
            _ => 1.0f
        };

        Debug.Log($"Fusing units into {baseUnit.unit.unitName}: Total EXP = {totalExp}, Total Zel Cost = {totalZelCost}, Multiplier = {successMultiplier}");

        baseUnit.currentExperience += (int)(totalExp * successMultiplier);
        PlayerData.zel -= totalZelCost;

        while (baseUnit.currentLevel < baseUnit.unit.maxLevel)
        {
            long cumulativeToNext = ExperienceTable.GetCumulativeExp(baseUnit.currentLevel + 1, baseUnit.unit.baseExp);
            if (baseUnit.currentExperience < cumulativeToNext) break;
            baseUnit.currentLevel++;
            ModifyStats(baseUnit);
        }

        PlayerData.SaveDataToJson();
        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
    }

    public static void ModifyStats(UnitInventoryData unit, int levels = 1)
    {
        Unit curUnit = unit.unit;
        int countedLevel = unit.currentLevel;
        //Reduces redundant extra typing
        int maxLevel = curUnit.maxLevel;
        //Total levels that gain stats
        int gainLimit = maxLevel - 1;

        UnitStats s = curUnit.statsLord;
        int hpMax  = s.hp  - curUnit.maxHealth;
        int atkMax = s.atk - curUnit.atk;
        int defMax = s.def - curUnit.def;
        int recMax = s.rec - curUnit.rec;

        switch (unit.type)
        {
            case UnitType.Anima:
            {
                hpMax = (s.hp + (10*gainLimit)) - curUnit.maxHealth;
                recMax = (s.rec - (3*gainLimit)) - curUnit.rec;
                unit.hpLevelUpBonus  += Random.Range(5, 11);
                unit.recLevelUpBonus -= Random.Range(1, 4);
                break;
            }
            case UnitType.Breaker:
            {
                atkMax = (s.atk + (3*gainLimit)) - curUnit.atk;
                defMax = (s.def - (3*gainLimit)) - curUnit.def;
                unit.atkLevelUpBonus += Random.Range(1, 4);
                unit.defLevelUpBonus -= Random.Range(1, 4);
                break;
            }
            case UnitType.Guardian:
            {
                defMax = (s.def + (3*gainLimit)) - curUnit.def;
                recMax = (s.rec - (2*gainLimit)) - curUnit.rec;
                unit.defLevelUpBonus += Random.Range(1, 4);
                unit.recLevelUpBonus -= Random.Range(0, 3);
                break;
            }
            case UnitType.Oracle:
            {
                recMax = (s.rec + (4*gainLimit)) - curUnit.rec;
                defMax = (s.def - (2*gainLimit)) - curUnit.def;
                unit.recLevelUpBonus += Random.Range(2, 5);
                unit.defLevelUpBonus -= Random.Range(0, 3);
                break;
            }
            case UnitType.Rex:
            {
                hpMax = (s.hp + (15*gainLimit)) - curUnit.maxHealth;
                atkMax = (s.atk + (2*gainLimit)) - curUnit.atk;
                defMax = (s.def + (2*gainLimit)) - curUnit.def;
                recMax = (s.rec + (2*gainLimit)) - curUnit.rec;
                unit.hpLevelUpBonus  += Random.Range(10, 16);
                unit.atkLevelUpBonus += Random.Range(1, 3);
                unit.defLevelUpBonus += Random.Range(1, 3);
                unit.recLevelUpBonus += Random.Range(1, 3);
                break;
            }
        }
        
        for (var i = unit.currentLevel+1; i <= unit.currentLevel + levels; i++) {
            if (i > maxLevel) break;
        
            // t / tPrev represent the fraction of the curve reached at this level vs the previous level.
            // We add only the DELTA between them each call, so repeated ModifyStats calls (one per level-up)
            // accumulate correctly instead of either double-counting the full curve or wiping prior random rolls.
            float t     = (float)i / maxLevel;
            float tPrev = (float)(i - 1) / maxLevel;

            unit.hpLevelUpBonus  += (int)Mathf.Lerp(0, hpMax,  t) - (int)Mathf.Lerp(0, hpMax,  tPrev);
            unit.atkLevelUpBonus += (int)Mathf.Lerp(0, atkMax, t) - (int)Mathf.Lerp(0, atkMax, tPrev);
            unit.defLevelUpBonus += (int)Mathf.Lerp(0, defMax, t) - (int)Mathf.Lerp(0, defMax, tPrev);
            unit.recLevelUpBonus += (int)Mathf.Lerp(0, recMax, t) - (int)Mathf.Lerp(0, recMax, tPrev);
        }

        unit.hpLevelUpBonus  = Mathf.Min(unit.hpLevelUpBonus,  hpMax);
        unit.atkLevelUpBonus = Mathf.Min(unit.atkLevelUpBonus, atkMax);
        unit.defLevelUpBonus = Mathf.Min(unit.defLevelUpBonus, defMax);
        unit.recLevelUpBonus = Mathf.Min(unit.recLevelUpBonus, recMax);
    }

    public static bool BBLevelUp(int baseUnitKey, int materialUnitKey)
    {
        UnitInventoryData baseUnit = GetUnitByKey(baseUnitKey);
        UnitInventoryData materialUnit = GetUnitByKey(materialUnitKey);

        // Fully maxed
        if (baseUnit.currentBBLevel >= 10 && baseUnit.currentSBBLevel >= 10)
            return false;

        // Helper to add levels with overflow
        void AddLevels(int amount)
        {
            // Fill BB first
            if (baseUnit.currentBBLevel < 10)
            {
                int space = 10 - baseUnit.currentBBLevel;
                int toBB = Mathf.Min(space, amount);

                baseUnit.currentBBLevel += toBB;
                amount -= toBB;
            }

            // Overflow goes to SBB
            if (baseUnit.unit.sbbAbility != null && amount > 0 && baseUnit.currentSBBLevel < 10)
            {
                int space = 10 - baseUnit.currentSBBLevel;
                int toSBB = Mathf.Min(space, amount);

                baseUnit.currentSBBLevel += toSBB;
            }
        }

        // Guaranteed materials
        if (materialUnit.unitId == "10312")
        {
            AddLevels(1);
            return true;
        }
        else if (materialUnit.unitId == "10313")
        {
            AddLevels(5);
            return true;
        }
        else if (materialUnit.unitId == "750004")
        {
            AddLevels(20);
            return true;
        }

        if(UnitIsInEvoLine(baseUnit, materialUnit.unit))
        {
            if(Random.Range(0, 100) <= 80)
            {
                AddLevels(1);
                return true;
            }
            return false;
        }

        int[] chances = new int[] { 15, 13, 11, 9, 7, 5, 3, 2, 1 };

        bool isSBB = baseUnit.currentBBLevel >= 10;

        int level = isSBB ? baseUnit.currentSBBLevel : baseUnit.currentBBLevel;
        int index = Mathf.Clamp((level - 1) % chances.Length, 0, chances.Length - 1);

        if (Random.Range(0, 100) <= chances[index])
        {
            AddLevels(1);

            if (isSBB)
                Debug.Log($"SBB Level Up Success! New SBB Level: {baseUnit.currentSBBLevel}");
            else
                Debug.Log($"BB Level Up Success! New BB Level: {baseUnit.currentBBLevel}");

            return true;
        }

        return false;
    }

    public static BBLevelUpProbability ShouldBBLevelUp(UnitInventoryData baseUnit, UnitInventoryData materialUnit)
    {
        if(baseUnit.currentBBLevel >= 10) return BBLevelUpProbability.None; //Need to add SBB level up logic
        
        string[] certainLevelUpUnitIds = new string[] {"10312", "10313", "750004"};

        if (certainLevelUpUnitIds.Contains(materialUnit.unitId))
        {
            return BBLevelUpProbability.Certain;
        }
        
        if(baseUnit.unit.bbType == materialUnit.unit.bbType)
        {
            return BBLevelUpProbability.Chance;
        }
        
        return BBLevelUpProbability.None;
    }

    public static bool UnitIsInEvoLine(UnitInventoryData unitData, Unit materialUnit)
    {
        if (unitData.unit.evoInto == null) return false;

        List<string> unitIdsInEvoLine = new List<string> { unitData.unit.unitId };
        Unit currentEvo = UnitRegistry.GetUnitById(unitData.unit.evoInto);
        while (currentEvo != null)
        {
            unitIdsInEvoLine.Add(currentEvo.unitId);
            currentEvo = UnitRegistry.GetUnitById(currentEvo.evoInto);
        }

        currentEvo = UnitRegistry.GetUnitById(unitData.unit.evoFrom);
        while (currentEvo != null)
        {
            unitIdsInEvoLine.Add(currentEvo.unitId);
            currentEvo = UnitRegistry.GetUnitById(currentEvo.evoFrom);
        }

        return unitIdsInEvoLine.Contains(materialUnit.unitId);
    }

    public static int CalculateTotalEXPFromUnit(UnitInventoryData baseUnit, UnitInventoryData material)
    {
        float elementMultiplier = (material.unit.element == baseUnit.unit.element) ? 1.5f : 1.0f;
        return (int)(MaterialExpGiven(material) * elementMultiplier);
    }

    public static int CalculateTotalEXPFromUnit(int baseUnitKey, int materialKey)
    {
        UnitInventoryData baseUnit = GetUnitByKey(baseUnitKey);
        UnitInventoryData material = GetUnitByKey(materialKey);

        float elementMultiplier = (material.unit.element == baseUnit.unit.element) ? 1.5f : 1.0f;
        return (int)(MaterialExpGiven(materialKey) * elementMultiplier);
    }

    public static int MaterialExpGiven(UnitInventoryData material)
    {
        switch(material.unit.unitId)
        {
            case "10202":
            case "20202":
            case "30202":
            case "40202":
            case "50202":
            case "60132":
                return 1506;
            
            case "10203":
            case "20203":
            case "30203":
            case "40203":
            case "50203":
            case "60133":
                return 11012;

            case "10204":
            case "20204":
            case "30204":
            case "40204":
            case "50204":
            case "60134":
                return 51518;

            case "10344":
            case "20334":
            case "30324":
            case "40324":
            case "50364":
            case "60334":
                return 151524;

            
            default: return material.unit.baseExp + material.currentExperience;
        }
    }

    public static int MaterialExpGiven(int materialKey)
    {
        UnitInventoryData material = GetUnitByKey(materialKey);

        switch(material.unit.unitId)
        {
            case "10202":
            case "20202":
            case "30202":
            case "40202":
            case "50202":
            case "60132":
                return 1506;
            
            case "10203":
            case "20203":
            case "30203":
            case "40203":
            case "50203":
            case "60133":
                return 11012;

            case "10204":
            case "20204":
            case "30204":
            case "40204":
            case "50204":
            case "60134":
                return 51518;

            case "10344":
            case "20344":
            case "30344":
            case "40344":
            case "50344":
            case "60344":
                return 151524;

            
            default: return material.unit.baseExp + material.currentExperience;
        }
    }

    public static void UpdateUnitLevel(int unitKey, int newLevel)
    {
        if (playerUnits.ContainsKey(unitKey))
        {
            playerUnits[unitKey].currentLevel = newLevel;
            SaveToJson();
        }
    }

    public static int ZelFusionCost(UnitInventoryData baseUnit, UnitInventoryData material)
    {
        int total = 0;
        int zelCost =  baseUnit.currentLevel == baseUnit.unit.maxLevel ? 25 : 100;

        total += (baseUnit.currentLevel + material.currentLevel) * zelCost;
        Debug.Log($"Calculating Zel Cost for fusing {material.unit.unitName} into {baseUnit.unit.unitName}: Total Cost = {total}");
        return total;
    }

    public static int ZelFusionCost(int baseUnitKey, int materialKey)
    {
        UnitInventoryData baseUnit = GetUnitByKey(baseUnitKey);
        UnitInventoryData material = GetUnitByKey(materialKey);

        int total = 0;
        int zelCost =  baseUnit.currentLevel == baseUnit.unit.maxLevel ? 25 : 100;

        total += (baseUnit.currentLevel + material.currentLevel) * zelCost;
        Debug.Log($"Calculating Zel Cost for fusing {material.unit.unitName} into {baseUnit.unit.unitName}: Total Cost = {total}");
        return total;
    }

    public static int ZelFusionCostByRarity(UnitRarity rarity)
    {
        return 250 + ((int)rarity * 500);
    }

    public static void SwapWithEvolveUnit(int key, Unit evolvedUnit)
    {
        if (!playerUnits.ContainsKey(key)) return;

        UnitType type = playerUnits[key].type;
        UnitInventoryData old = playerUnits[key];

        playerUnits[key] = new UnitInventoryData
        {
            unit = evolvedUnit,
            unitId = evolvedUnit?.unitId,
            type = type,
            currentLevel = 1,
            currentExperience = 0,
            currentBBLevel = Mathf.Max(1, old.currentBBLevel / 2),
            currentSBBLevel = 1,
            hpLevelUpBonus = old.hpLevelUpBonus,
            atkLevelUpBonus = old.atkLevelUpBonus,
            defLevelUpBonus = old.defLevelUpBonus,
            recLevelUpBonus = old.recLevelUpBonus,
            hpImpBonus = old.hpImpBonus,
            atkImpBonus = old.atkImpBonus,
            defImpBonus = old.defImpBonus,
            recImpBonus = old.recImpBonus,
            isInParty = old.isInParty,
            isFavorite = old.isFavorite
        };

        MainUI.inventoryRenderer.renderedSlots[key].UpdateView();

        PlayerData.SaveDataToJson();
        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
    }

    public static bool ToggleFavorite(int unitKey, bool saveAfterToggle = true)
    {
        if (!playerUnits.TryGetValue(unitKey, out var data)) return false;

        data.isFavorite = !data.isFavorite;
        if (saveAfterToggle) SaveToJson();
        return data.isFavorite;
    }

    public static void SetFavorite(int unitKey, bool isFavorite, bool saveAfterSet = true)
    {
        if (!playerUnits.TryGetValue(unitKey, out var data)) return;

        data.isFavorite = isFavorite;
        if (saveAfterSet) SaveToJson();
    }

    public static List<int> GetFavoriteUnitKeys()
    {
        List<int> keys = new List<int>();
        foreach (var kvp in playerUnits)
        {
            if (kvp.Value.isFavorite)
                keys.Add(kvp.Key);
        }
        return keys;
    }

public static List<UnitInventoryData> GetFavoriteUnits()
{
    List<UnitInventoryData> units = new List<UnitInventoryData>();
    foreach (var kvp in playerUnits)
    {
        if (kvp.Value.isFavorite)
            units.Add(kvp.Value);
    }
    return units;
}

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    public static UnitType GetRandomUnitType()
    {
        System.Array types = System.Enum.GetValues(typeof(UnitType));
        return (UnitType)types.GetValue(Random.Range(0, types.Length - 1));
    }
}

// ─── Enums & Data Classes ─────────────────────────────────────────────────────

public enum SuccessType
{
    Success,
    GreatSuccess,
    SuperSuccess
}
public enum BBLevelUpProbability
{
    None,
    Chance,
    Certain
}

public enum UnitType
{
    Lord,
    Anima,
    Breaker,
    Guardian,
    Oracle,
    Rex
}

public class UnitInventoryData
{
    public string unitId;
    public UnitType type;
    public int currentLevel;
    public int currentExperience;
    public int currentBBLevel;
    public int currentSBBLevel;
    public int hpLevelUpBonus;
    public int atkLevelUpBonus;
    public int defLevelUpBonus;
    public int recLevelUpBonus;
    public int hpImpBonus;
    public int atkImpBonus;
    public int defImpBonus;
    public int recImpBonus;
    public bool isFavorite;

    [JsonIgnore] public Unit unit;   // Runtime-only, not serialized
    [JsonIgnore] public bool isNew;
    [JsonIgnore] public bool isInParty;

    public UnitInventoryData Clone()
    {
        return new UnitInventoryData
        {
            unit             = this.unit,
            currentLevel     = this.currentLevel,
            currentExperience= this.currentExperience,
            currentBBLevel   = this.currentBBLevel,
            hpLevelUpBonus   = this.hpLevelUpBonus,
            atkLevelUpBonus  = this.atkLevelUpBonus,
            defLevelUpBonus  = this.defLevelUpBonus,
            recLevelUpBonus  = this.recLevelUpBonus,
            hpImpBonus       = this.hpImpBonus,
            atkImpBonus      = this.atkImpBonus,
            defImpBonus      = this.defImpBonus,
            recImpBonus      = this.recImpBonus,
            type             = this.type,
            isFavorite       = this.isFavorite,
        };
    }
}
