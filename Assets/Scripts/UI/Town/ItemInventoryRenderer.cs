using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ItemInventoryRenderer : MonoBehaviour
{
    public static ItemInventorySelectionMode selectionMode = ItemInventorySelectionMode.None;
    public GameObject itemSlotPrefab;
    public GameObject sliderContent;

    // Keyed by stack key now, not item id — an item id can span several
    // stacks once it hits max_stack, and each stack gets its own slot.
    public Dictionary<int, ItemSlot> renderedSlots = new Dictionary<int, ItemSlot>();

    void Start()
    {
        Debug.Log("Rendering full inventory at start");
        RenderFullInventory();
    }

    /// <summary>Adds one (or more) of an item and creates/refreshes exactly the slots that changed — the stacks PlayerItemInventoryDatabase.AddItem actually touched, not a full re-render.</summary>
    public void AddItem(string itemId, int amount = 1)
    {
        List<int> touchedKeys = PlayerItemInventoryDatabase.AddItem(itemId, amount, saveAfterAdd: true);

        foreach (int key in touchedKeys)
        {
            if (renderedSlots.ContainsKey(key))
                UpdateSlotView(key);
            else
                CreateRenderedSlot(key);
        }

        SortInventory();
    }

    public void UpdateVisibility()
    {
        foreach (ItemSlot slot in renderedSlots.Values)
        {
            RectTransform rt = slot.GetComponent<RectTransform>();
            bool visible = Vector2.Distance(rt.position, Vector2.zero) <= 25f;
            slot.SetVisible(visible);
        }
    }

    public void DestroySlot(int stackKey)
    {
        if (!renderedSlots.TryGetValue(stackKey, out ItemSlot slotToRemove))
        {
            Debug.LogWarning("Attempted to remove a stack that isn't rendered: " + stackKey);
            return;
        }

        if (slotToRemove != null)
        {
            Debug.Log("Destroying slot for stackKey: " + stackKey);
            slotToRemove.transform.SetParent(null);
            Destroy(slotToRemove.gameObject);
            renderedSlots.Remove(stackKey);
        }
    }

    public void RenderFullInventory()
    {
        foreach (ItemSlot itemSlot in renderedSlots.Values)
        {
            Destroy(itemSlot.gameObject);
        }
        renderedSlots.Clear();

        foreach (int stackKey in PlayerItemInventoryDatabase.stacks.Keys.OrderBy(k => k))
        {
            CreateRenderedSlot(stackKey);
        }

        SortInventory();
    }

    private void CreateRenderedSlot(int stackKey)
    {
        if (renderedSlots.ContainsKey(stackKey))
        {
            UpdateSlotView(stackKey);
            return;
        }

        GameObject slot = CreateItemSlot(stackKey);
        slot.transform.SetParent(sliderContent.transform, false);
        renderedSlots.Add(stackKey, slot.GetComponent<ItemSlot>());
    }

    public void RefreshAllSlots()
    {
        foreach (ItemSlot slot in renderedSlots.Values)
        {
            slot.UpdateView();
        }
    }

    private GameObject CreateItemSlot(int stackKey)
    {
        GameObject slot = Instantiate(itemSlotPrefab);
        slot.transform.SetParent(sliderContent.transform, false);
        ItemSlot itemSlot = slot.GetComponent<ItemSlot>();
        itemSlot.stackKey = stackKey;
        itemSlot.button = slot.GetComponent<Button>();
        itemSlot.button.onClick.AddListener(itemSlot.OnClick);
        itemSlot.UpdateView();
        return slot;
    }

    public void UpdateSlotView(int stackKey)
    {
        if (renderedSlots.TryGetValue(stackKey, out ItemSlot itemSlot))
        {
            itemSlot.UpdateView();
        }
    }

    /// <summary>Orders slots by item name, then by stack key so multiple stacks of the same item stay adjacent and in creation order.</summary>
    public void SortInventory()
    {
        List<int> orderedKeys = renderedSlots.Keys
            .OrderBy(key => ItemDatabase.GetItem(PlayerItemInventoryDatabase.GetStack(key)?.itemId)?.itemName ?? "")
            .ThenBy(key => key)
            .ToList();

        for (int i = 0; i < orderedKeys.Count; i++)
        {
            if (renderedSlots.TryGetValue(orderedKeys[i], out ItemSlot slot))
            {
                slot.transform.SetSiblingIndex(i);
            }
        }
    }
}
public enum ItemInventorySelectionMode
{
    None,
    ItemSell
}