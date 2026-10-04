// PlayerItemInventoryDatabase.cs
//
// Player-owned item stacks.
//
// An item copy still carries no per-instance state — no level, no exp —
// but items.json's max_stack is a real cap (999 for most materials, as low
// as 1 for most spheres), and a maxed-out item shouldn't just stop being
// obtainable. So this is closer to PlayerUnitInventoryDatabase's shape than
// the old single-count-per-id version: an autoincrementing key per *stack*
// (not per item id), each stack holding an itemId + count. Most items will
// only ever have one stack in play; anything with a low max_stack (mostly
// spheres) will commonly have several.
//
// Item definitions (name, effects, max_stack, sell price, thumbnail, etc.)
// live in ItemDatabase/ItemData — this class only tracks what the player
// owns.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class PlayerItemInventoryDatabase
{
    private static string SavePath => Application.persistentDataPath + "/iteminventory.json";

    private class SaveData
    {
        public int nextStackKey = 0;
        public Dictionary<int, ItemStack> stacks = new Dictionary<int, ItemStack>();
    }

    public static int _nextStackKey = 0;
    public static Dictionary<int, ItemStack> stacks = new Dictionary<int, ItemStack>();

    // ─── Persistence ────────────────────────────────────────────────────────────

    public static void SaveToJson()
    {
        var saveData = new SaveData { nextStackKey = _nextStackKey, stacks = stacks };
        string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static void LoadFromJson()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        var saveData = JsonConvert.DeserializeObject<SaveData>(json);

        stacks = saveData.stacks ?? new Dictionary<int, ItemStack>();
        _nextStackKey = saveData.nextStackKey;
    }

    // ─── Add / Remove ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds `amount` of itemId. Tops up existing non-full stacks of it
    /// first (oldest key first), then opens new stacks for whatever's
    /// left — items are never refused for being "maxed out", they just
    /// spill into another stack. Returns every stack key touched (existing
    /// stacks topped up, plus any newly created ones), so callers like the
    /// renderer know exactly which slots to create or refresh instead of
    /// having to re-scan everything.
    /// </summary>
    public static List<int> AddItem(string itemId, int amount = 1, bool saveAfterAdd = true)
    {
        List<int> touched = new();
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return touched;

        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerItemInventoryDatabase] Tried to add unknown item id '{itemId}'.");
            return touched;
        }

        int remaining = amount;

        // Top up existing non-full stacks of this item before opening new ones.
        List<int> openStacks = stacks
            .Where(kvp => kvp.Value.itemId == itemId && kvp.Value.count < data.maxStack)
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (int key in openStacks)
        {
            if (remaining <= 0) break;

            ItemStack stack = stacks[key];
            int space = data.maxStack - stack.count;
            int toAdd = Mathf.Min(space, remaining);
            stack.count += toAdd;
            remaining -= toAdd;
            touched.Add(key);
        }

        // Whatever's left opens as many new stacks as it takes.
        while (remaining > 0)
        {
            int toAdd = Mathf.Min(data.maxStack, remaining);
            int key = _nextStackKey++;
            stacks[key] = new ItemStack { itemId = itemId, count = toAdd };
            remaining -= toAdd;
            touched.Add(key);
        }

        if (saveAfterAdd) SaveToJson();
        return touched;
    }

    /// <summary>
    /// Removes `amount` of itemId, draining its stacks oldest-key-first and
    /// dropping any stack that hits zero. Returns false (and removes
    /// nothing) if the player doesn't own at least that many in total.
    /// </summary>
    public static bool RemoveItem(string itemId, int amount = 1, bool saveAfterRemove = true)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        if (!HasItem(itemId, amount)) return false;

        int remaining = amount;
        foreach (int key in GetStackKeysForItem(itemId))
        {
            if (remaining <= 0) break;

            ItemStack stack = stacks[key];
            int taken = Mathf.Min(stack.count, remaining);
            stack.count -= taken;
            remaining -= taken;

            if (stack.count <= 0)
                stacks.Remove(key); // keep the dictionary free of empty stacks
        }

        if (saveAfterRemove) SaveToJson();
        return true;
    }

    /// <summary>Removes one specific stack entirely by its key, regardless of amount — for UI flows that act on "this stack" (e.g. selling one sphere copy) rather than "N of this item" in the abstract.</summary>
    public static bool RemoveStack(int stackKey, bool saveAfterRemove = true)
    {
        if (!stacks.Remove(stackKey)) return false;
        if (saveAfterRemove) SaveToJson();
        return true;
    }

    // ─── Queries ─────────────────────────────────────────────────────────────────

    public static ItemStack GetStack(int stackKey) =>
        stacks.TryGetValue(stackKey, out ItemStack s) ? s : null;

    /// <summary>Every stack key holding this item id, oldest (lowest key) first.</summary>
    public static List<int> GetStackKeysForItem(string itemId) =>
        stacks.Where(kvp => kvp.Value.itemId == itemId)
              .OrderBy(kvp => kvp.Key)
              .Select(kvp => kvp.Key)
              .ToList();

    public static int GetItemCount(string itemId) =>
        stacks.Values.Where(s => s.itemId == itemId).Sum(s => s.count);

    public static bool HasItem(string itemId, int amount = 1) =>
        GetItemCount(itemId) >= amount;

    /// <summary>True only if the player has enough total of every id in `required` (summed across all of that item's stacks) — for recipe/synthesis/evolution-material checks before committing to RemoveItems.</summary>
    public static bool HasItems(Dictionary<string, int> required)
    {
        foreach (var kvp in required)
            if (!HasItem(kvp.Key, kvp.Value)) return false;
        return true;
    }

    /// <summary>Removes every entry in `required` as one atomic operation — fails (removing nothing) unless HasItems(required) would already be true.</summary>
    public static bool RemoveItems(Dictionary<string, int> required, bool saveAfterRemove = true)
    {
        if (!HasItems(required)) return false;

        foreach (var kvp in required)
            RemoveItem(kvp.Key, kvp.Value, saveAfterRemove: false);

        if (saveAfterRemove) SaveToJson();
        return true;
    }

    public static List<string> GetOwnedItemIds() =>
        stacks.Values.Select(s => s.itemId).Distinct().ToList();

    /// <summary>Every distinct owned item id of a given type (material, sphere, consumable, ...) with its total count summed across stacks. Resolves ItemData lazily through ItemDatabase per id, same as everywhere else that touches item definitions.</summary>
    public static List<(ItemData data, int count)> GetItemsByType(ItemType type)
    {
        List<(ItemData, int)> results = new();
        foreach (string itemId in GetOwnedItemIds())
        {
            ItemData data = ItemDatabase.GetItem(itemId);
            if (data != null && data.itemType == type)
                results.Add((data, GetItemCount(itemId)));
        }
        return results;
    }

    // ─── Gameplay Logic ───────────────────────────────────────────────────────────

    /// <summary>Sells the given amount of each item id for zel (mirrors PlayerUnitInventoryDatabase.SellUnits), draining across that item's stacks. Skips ids the player doesn't own enough of rather than failing the whole batch.</summary>
    public static void SellItems(Dictionary<string, int> sellItems)
    {
        int totalZel = 0;

        foreach (var kvp in sellItems)
        {
            ItemData data = ItemDatabase.GetItem(kvp.Key);
            if (data == null) continue;

            int amount = Mathf.Min(kvp.Value, GetItemCount(kvp.Key));
            if (amount <= 0) continue;

            totalZel += Mathf.Abs(data.sellPrice) * amount;
            RemoveItem(kvp.Key, amount, saveAfterRemove: false);
        }

        if (totalZel <= 0) return;

        PlayerData.zel += totalZel;
        SaveToJson();
        PlayerData.SaveDataToJson();
        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
    }
}

public class ItemStack
{
    public string itemId;
    public int count;
}