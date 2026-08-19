using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InventoryRenderer : MonoBehaviour
{
    public static InventorySelectionMode selectionMode = InventorySelectionMode.None;
    public GameObject unitSlotPrefab;
    public GameObject sliderContent;
    public Dictionary<int, UnitSlot> renderedSlots = new Dictionary<int, UnitSlot>();
    public Sort currentSort;
    public bool sortAscending = true;
    public TextMeshProUGUI sortText;

    void Start()
    {
        RenderFullInventory();
    }

    public void AddUnit(string unitId)
    {
        PlayerUnitInventoryDatabase.AddUnit(UnitRegistry.GetUnitById(unitId));
        AddToRenderedInventory(PlayerUnitInventoryDatabase._nextKey - 1);
    }

    public void UpdateVisibility()
    {
        foreach (UnitSlot slot in renderedSlots.Values)
        {
            RectTransform rt = slot.GetComponent<RectTransform>();
            bool visible = Vector2.Distance(rt.position, Vector2.zero) <= 25f;
            slot.SetVisible(visible);
        }
    }

    public void DestroySlot(int unitKey)
    {
        if (!renderedSlots.TryGetValue(unitKey, out UnitSlot slotToRemove))
        {
            Debug.LogWarning("Attempted to remove a unit that isn't rendered: " + unitKey);
            return;
        }

        if (slotToRemove != null)
        {
            Debug.Log("Destroying slot for unitKey: " + unitKey);

            slotToRemove.transform.SetParent(null);

            Destroy(slotToRemove.gameObject);
            renderedSlots.Remove(unitKey);
        }
    }

    public void RenderFullInventory()
    {
        foreach (UnitSlot unitSlot in renderedSlots.Values)
        {
            Destroy(unitSlot.gameObject);
        }
        renderedSlots.Clear();

        foreach (var kvp in PlayerUnitInventoryDatabase.playerUnits.OrderBy(kvp => kvp.Key))
        {
            GameObject slot = CreateUnitSlot(kvp.Key);
            slot.transform.SetParent(sliderContent.transform, false);
            renderedSlots.Add(kvp.Key, slot.GetComponent<UnitSlot>());
        }

        SortInventory();
    }

    public void AddToRenderedInventory(int unitKey)
    {
        GameObject slot = CreateUnitSlot(unitKey);
        slot.transform.SetParent(sliderContent.transform, false);
        renderedSlots.Add(unitKey, slot.GetComponent<UnitSlot>());
    }

    public void RefreshAllSlotsBBIndicator()
    {
        Debug.Log("[BBIndicator] RefreshAllSlotsBBIndicator called, slots: " + renderedSlots.Count);
        foreach (UnitSlot slot in renderedSlots.Values)
        {
            slot.SetupBBIndicator();
        }
    }

    public void RefreshAllSlots()
    {
        foreach (UnitSlot slot in renderedSlots.Values)
        {
            slot.UpdateView();
        }
    }

    GameObject CreateUnitSlot(int unitKey)
    {
        GameObject slot = Instantiate(unitSlotPrefab);
        UnitSlot unitSlot = slot.GetComponent<UnitSlot>();
        slot.GetComponent<Image>().sprite = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey)?.unit.unitSlotIcon;
        unitSlot.unitKey = unitKey;
        unitSlot.button = slot.GetComponent<Button>();
        unitSlot.button.onClick.AddListener(unitSlot.OnClick);
        unitSlot.UpdateView();
        
        return slot;
    }
    
    public void DarkenUnclickableSlots()
    {
        switch(selectionMode)
        {
            case InventorySelectionMode.UnitFusionSelectMaterial:
                foreach (UnitSlot slot in renderedSlots.Values)
                {
                    slot.GetComponent<Image>().color = slot.CanBeFusionMaterial() ? Color.white : Color.gray;
                }
                break;
            case InventorySelectionMode.UnitSell:
                foreach (UnitSlot slot in renderedSlots.Values)
                {
                    slot.GetComponent<Image>().color = slot.CanBeSold() ? Color.white : Color.gray;
                }
                break;
            case InventorySelectionMode.UnitEvolutionSelectBase:
                foreach (UnitSlot slot in renderedSlots.Values)
                {
                    slot.GetComponent<Image>().color = slot.CanBeEvolutionBase() ? Color.white : Color.gray;
                }
                break;
            default:
                foreach (UnitSlot slot in renderedSlots.Values)
                {
                    slot.GetComponent<Image>().color = Color.white;
                }
                break;
        }
    }

    public void UpdateSlotView(int unitKey)
    {
        if (renderedSlots.TryGetValue(unitKey, out UnitSlot unitSlot))
        {
            unitSlot.UpdateView();
        }
    }

    public void SetSortDirection(bool value)
    {
        sortAscending = value;
    }

    private static readonly Dictionary<ElementalType, int> ElementPriority = new Dictionary<ElementalType, int>
    {
        { ElementalType.Fire,    0 },
        { ElementalType.Water,   1 },
        { ElementalType.Earth,   2 },
        { ElementalType.Thunder, 3 },
        { ElementalType.Light,   4 },
        { ElementalType.Dark,    5 },
    };

    public void SetSort(Sort sortType)
    {
        currentSort = sortType;
    }

    public void SortInventory()
    {
        sortText.text = "Sort: <color=#EBA016>" + (currentSort != Sort.RaisedStats ? currentSort.ToString() : "Raised Stats");

        List<int> orderedKeys = GetSortedKeys();

        for (int i = 0; i < orderedKeys.Count; i++)
        {
            if (renderedSlots.TryGetValue(orderedKeys[i], out UnitSlot slot))
            {
                slot.transform.SetSiblingIndex(i);
            }
        }
    }

    private List<int> GetSortedKeys()
    {
        var units = PlayerUnitInventoryDatabase.playerUnits;
        List<int> orderedKeys;

        switch (currentSort)
        {
            case Sort.Element:
                orderedKeys = units.OrderBy(kvp => ElementPriority.TryGetValue(kvp.Value.unit.element, out int o) ? o : int.MaxValue)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Level:
                orderedKeys = units.OrderBy(kvp => kvp.Value.currentLevel)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Rarity:
                orderedKeys = units.OrderBy(kvp => (int)kvp.Value.unit.rarity)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Cost:
                orderedKeys = units.OrderBy(kvp => kvp.Value.unit.summonCost)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.HP:
                orderedKeys = units.OrderBy(kvp => EffectiveHP(kvp.Value))
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Attack:
                orderedKeys = units.OrderBy(kvp => EffectiveAtk(kvp.Value))
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Defense:
                orderedKeys = units.OrderBy(kvp => EffectiveDef(kvp.Value))
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Recovery:
                orderedKeys = units.OrderBy(kvp => EffectiveRec(kvp.Value))
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Acquired:
                orderedKeys = units.OrderBy(kvp => kvp.Key)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.BBLv:
                orderedKeys = units.OrderBy(kvp => kvp.Value.currentBBLevel)
                                   .ThenByDescending(kvp => kvp.Value.currentSBBLevel)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.RaisedStats:
                orderedKeys = units.OrderBy(kvp => TotalImpBonus(kvp.Value))
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Favorited:
                orderedKeys = units.OrderBy(kvp => kvp.Value.isFavorite)
                                   .Select(kvp => kvp.Key).ToList();
                break;

            case Sort.Sphere:
            case Sort.SP:
                Debug.LogWarning($"[InventoryRenderer] Sort.{currentSort} not implemented yet — feature not in game.");
                orderedKeys = units.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key).ToList();
                break;

            default:
                Debug.LogWarning($"[InventoryRenderer] Unhandled sort type: {currentSort}");
                orderedKeys = units.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key).ToList();
                break;
        }

        if (!sortAscending)
        {
            orderedKeys.Reverse();
        }

        return orderedKeys;
    }

    private static int EffectiveHP(UnitInventoryData data)  => data.unit.maxHealth + data.hpLevelUpBonus  + data.hpImpBonus;
    private static int EffectiveAtk(UnitInventoryData data) => data.unit.atk + data.atkLevelUpBonus + data.atkImpBonus;
    private static int EffectiveDef(UnitInventoryData data) => data.unit.def + data.defLevelUpBonus + data.defImpBonus;
    private static int EffectiveRec(UnitInventoryData data) => data.unit.rec + data.recLevelUpBonus + data.recImpBonus;

    private static int TotalImpBonus(UnitInventoryData data) =>
        data.hpImpBonus + data.atkImpBonus + data.defImpBonus + data.recImpBonus;
}
public enum InventorySelectionMode
{
    None,
    UnitFusionSelectBase,
    UnitFusionSelectMaterial,
    UnitSell,
    UnitEvolutionSelectBase,
    UnitPartySelect
}
public enum Sort
{
    Element,
    Level,
    Rarity,
    Cost,
    HP,
    Attack,
    Defense,
    Recovery,
    Acquired,
    BBLv,
    Sphere,
    RaisedStats,
    Favorited,
    SP
}
