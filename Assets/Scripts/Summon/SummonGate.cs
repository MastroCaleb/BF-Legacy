using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class SummonGate : MonoBehaviour
{
    public SummonBanner summonBanner;
    public RectTransform content;

    [Range(0f, 100f)]
    public float surpriseDoorBreakChance = 25f;

    [Range(0f, 100f)]
    public float featuredPullChance = 20f;

    [Header("Rates UI")]
    public TMP_FontAsset ratesFont;
    public int separatorFontSize = 22;
    public int unitFontSize = 16;

    public (Unit pulled, Unit evolved, bool isSurprise, bool isNewUnit) Summon()
    {
        Unit pulledUnit = PullUnit();
        Unit evolvedUnit = pulledUnit;
        if(pulledUnit.evoInto != "") evolvedUnit = EvolveUnitAtSummon(pulledUnit);

        bool isSurprise = false;
        
        if (pulledUnit.unitId == evolvedUnit.unitId)
        {
            isSurprise = Random.Range(0f, 100f) <= surpriseDoorBreakChance;
        }

        bool isNew = !PlayerData.unitDex.Contains(evolvedUnit.unitId);

        MainUI.inventoryRenderer.AddUnit(evolvedUnit.unitId);
        return (pulledUnit, evolvedUnit, isSurprise, isNew);
    }

    public Unit PullUnit()
    {
        float randomValue = Random.Range(0f, 100f);
        string pulledUnitKey;

        if(randomValue <= featuredPullChance)
        {
            int poolIndex = Random.Range(0, summonBanner.featuredSummonPools.Count);
            var pool = summonBanner.featuredSummonPools[poolIndex];
            pulledUnitKey = pool.poolUnitKeys[Random.Range(0, pool.poolUnitKeys.Count)];
        }
        else
        {
            int poolIndex = Random.Range(0, summonBanner.baseSummonPools.Count);
            var pool = summonBanner.baseSummonPools[poolIndex];
            pulledUnitKey = pool.poolUnitKeys[Random.Range(0, pool.poolUnitKeys.Count)];
        }

        return UnitRegistry.GetUnitById(pulledUnitKey);
    }

    public Unit EvolveUnitAtSummon(Unit unit)
    {
        Unit evolvedUnit = unit;

        Unit evo = UnitRegistry.GetUnitById(unit.evoInto);
        if(evo == null)
        {
            return unit;
        }

        int i = 1;
        while (evolvedUnit.evoInto != "" && 
            evolvedUnit.rarity != UnitRarity.FIVE &&
            evolvedUnit.rarity != UnitRarity.SIX &&
            evolvedUnit.rarity != UnitRarity.SEVEN &&
            evolvedUnit.rarity != UnitRarity.OMNI)
        {
            Unit nextEvo = UnitRegistry.GetUnitById(evolvedUnit.evoInto);
            if(Random.Range(0f, 100f) <= 4f/i && nextEvo != null)
            {
                evolvedUnit = nextEvo;
            }
            else
            {
                break;
            }
            i++;
        }

        return evolvedUnit;
    }

    // Builds the summon rate breakdown into `content` (grid/vertical layout).
    // Layout: "Featured Units (n%):" separator, then each featured unit with
    // its individual pull chance, then "Base Units (n%):" separator and the
    // base units. Call this whenever the rates panel is opened.
    public void PopulateSummonRatesUI()
    {
        if (content == null || summonBanner == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        content.position = Vector3.zero;

        AddSeparator($"Featured Units ({FormatPercent(featuredPullChance)}%):");
        AddPoolEntries(summonBanner.featuredSummonPools, featuredPullChance);

        float basePullChance = 100f - featuredPullChance;
        AddSeparator($"Base Units ({FormatPercent(basePullChance)}%):");
        AddPoolEntries(summonBanner.baseSummonPools, basePullChance);
    }

    private void AddPoolEntries(List<SummonPool> pools, float sectionChance)
    {
        if (pools == null || pools.Count == 0) return;

        float chancePerPool = sectionChance / pools.Count;

        foreach (var pool in pools)
        {
            if (pool == null || pool.poolUnitKeys == null || pool.poolUnitKeys.Count == 0) continue;

            float chancePerUnit = chancePerPool / pool.poolUnitKeys.Count;

            foreach (var unitKey in pool.poolUnitKeys)
            {
                Unit unit = UnitRegistry.GetUnitById(unitKey);
                string unitName = unit != null ? unit.unitName : unitKey;
                AddUnitEntry(unitName, chancePerUnit);
            }
        }
    }

    private void AddSeparator(string label)
    {
        GameObject go = new GameObject("Separator");
        go.transform.SetParent(content, false);
        go.AddComponent<RectTransform>();

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.richText = true;
        text.text = $"<b>{label}</b>";
        text.fontSize = separatorFontSize;
        text.alignment = TextAlignmentOptions.Left;
        if (ratesFont != null) text.font = ratesFont;
    }

    private void AddUnitEntry(string unitName, float chance)
    {
        GameObject go = new GameObject("UnitEntry");
        go.transform.SetParent(content, false);
        go.AddComponent<RectTransform>();

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.richText = true;
        text.text = $"{unitName}    {FormatPercent(chance)}%";
        text.fontSize = unitFontSize;
        text.alignment = TextAlignmentOptions.Left;
        if (ratesFont != null) text.font = ratesFont;
    }

    private string FormatPercent(float value)
    {
        return value % 1f == 0f ? value.ToString("0") : value.ToString("0.##");
    }
}