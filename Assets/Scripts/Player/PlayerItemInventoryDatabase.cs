// PlayerItemInventoryDatabase.cs
//
// Player-owned item counts.
//
// Unlike units, an item copy carries no per-instance state — no level, no
// exp, no individual bonuses. "5 Green Grass" is just a count, not five
// separate records. So where PlayerUnitInventoryDatabase hands out an
// autoincrementing key per unit instance, this only needs one integer count
// per item id.
//
// Item definitions (name, effects, max_stack, sell price, thumbnail, etc.)
// live in ItemDatabase/ItemData — this class only tracks how many of each
// the player currently owns.

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
        public Dictionary<string, int> itemCounts = new Dictionary<string, int>();
    }

    public static Dictionary<string, int> itemCounts = new Dictionary<string, int>();

    // ─── Persistence ────────────────────────────────────────────────────────────

    public static void SaveToJson()
    {
        var saveData = new SaveData { itemCounts = itemCounts };
        string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static void LoadFromJson()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        var saveData = JsonConvert.DeserializeObject<SaveData>(json);

        itemCounts = saveData.itemCounts ?? new Dictionary<string, int>();
    }

    // ─── Add / Remove ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds `amount` of itemId, clamped at that item's max_stack (from
    /// items.json — 999 for most materials, 1 for most spheres, etc.).
    /// Returns the amount actually added, which can be less than requested
    /// if the cap was hit; check the return value if the caller needs to
    /// react to overflow (e.g. show "inventory full") instead of it being
    /// silently discarded.
    /// </summary>
    public static int AddItem(string itemId, int amount = 1, bool saveAfterAdd = true)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return 0;

        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerItemInventoryDatabase] Tried to add unknown item id '{itemId}'.");
            return 0;
        }

        int current = itemCounts.TryGetValue(itemId, out int c) ? c : 0;
        int newTotal = Mathf.Min(current + amount, data.maxStack);
        int actuallyAdded = newTotal - current;

        if (actuallyAdded <= 0) return 0;

        itemCounts[itemId] = newTotal;

        if (actuallyAdded < amount)
            Debug.LogWarning($"[PlayerItemInventoryDatabase] '{itemId}' hit max stack ({data.maxStack}) — added {actuallyAdded}/{amount}.");

        if (saveAfterAdd) SaveToJson();
        return actuallyAdded;
    }

    /// <summary>Removes `amount` of itemId. Returns false (and removes nothing) if the player doesn't own at least that many.</summary>
    public static bool RemoveItem(string itemId, int amount = 1, bool saveAfterRemove = true)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        if (!HasItem(itemId, amount)) return false;

        int remaining = itemCounts[itemId] - amount;
        if (remaining <= 0)
            itemCounts.Remove(itemId); // keep the dictionary free of zero-count entries
        else
            itemCounts[itemId] = remaining;

        if (saveAfterRemove) SaveToJson();
        return true;
    }

    // ─── Queries ─────────────────────────────────────────────────────────────────

    public static int GetItemCount(string itemId) =>
        itemCounts.TryGetValue(itemId, out int c) ? c : 0;

    public static bool HasItem(string itemId, int amount = 1) =>
        GetItemCount(itemId) >= amount;

    /// <summary>True only if the player has enough of every id in `required` — for recipe/synthesis/evolution-material checks before committing to RemoveItems.</summary>
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

    public static List<string> GetOwnedItemIds() => itemCounts.Keys.ToList();

    /// <summary>Every owned item of a given type (material, sphere, consumable, ...) with its current count. Resolves ItemData lazily through ItemDatabase per id, same as everywhere else that touches item definitions.</summary>
    public static List<(ItemData data, int count)> GetItemsByType(ItemType type)
    {
        List<(ItemData, int)> results = new();
        foreach (var kvp in itemCounts)
        {
            ItemData data = ItemDatabase.GetItem(kvp.Key);
            if (data != null && data.itemType == type)
                results.Add((data, kvp.Value));
        }
        return results;
    }

    // ─── Gameplay Logic ───────────────────────────────────────────────────────────

    /// <summary>Sells the given amount of each item id for zel (mirrors PlayerUnitInventoryDatabase.SellUnits). Skips ids the player doesn't own enough of rather than failing the whole batch.</summary>
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