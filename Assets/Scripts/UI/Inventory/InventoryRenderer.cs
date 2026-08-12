using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InventoryRenderer : MonoBehaviour
{
    public static InventorySelectionMode selectionMode = InventorySelectionMode.None;
    public GameObject unitSlotPrefab;
    public GameObject sliderContent;
    public Dictionary<int, UnitSlot> renderedSlots = new Dictionary<int, UnitSlot>();

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
