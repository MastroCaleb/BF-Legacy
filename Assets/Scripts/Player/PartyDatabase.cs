using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class PartyDatabase
{
    public const int MaxPartySize = 5;
    private static string SavePath => Application.persistentDataPath + "/parties.json";

    private class SaveData
    {
        public int nextKey = 0;
        public Dictionary<int, PartyData> parties = new Dictionary<int, PartyData>();
    }

    private static int _nextKey = 0;
    public static Dictionary<int, PartyData> parties = new Dictionary<int, PartyData>();

    // ─── Persistence ──────────────────────────────────────────────────────────────

    public static void SaveToJson()
    {
        var saveData = new SaveData { nextKey = _nextKey, parties = parties };
        string json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static void LoadFromJson()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        var saveData = JsonConvert.DeserializeObject<SaveData>(json);

        parties = saveData.parties;
        _nextKey = saveData.nextKey;

        RefreshAllIsInParty();
    }

    // ─── Add / Remove Parties ─────────────────────────────────────────────────────

    public static int CreateParty()
    {
        int key = _nextKey++;
        parties.Add(key, new PartyData());
        SaveToJson();
        return key;
    }

    public static void RemoveParty(int partyKey)
    {
        if (!parties.ContainsKey(partyKey)) return;

        // Clear isInParty for all units in this party before removing
        foreach (int unitKey in parties[partyKey].unitKeys)
            if (unitKey != -1) SetIsInParty(unitKey, false);

        parties.Remove(partyKey);
        SaveToJson();
    }

    // ─── Slot-Based Unit Management ───────────────────────────────────────────────

    // Places a unit into a specific slot. If the unit is already in another slot
    // in the same party, the two slots are swapped. If the slot is occupied by a
    // different unit coming from outside the party, the displaced unit is freed.
    public static bool SetUnitAtSlot(int partyKey, int slotIndex, int inventoryUnitKey)
    {
        if (!parties.ContainsKey(partyKey)) return false;

        PartyData party = parties[partyKey];

        if (slotIndex < 0 || slotIndex >= MaxPartySize) return false;

        int displaced = party.GetUnitAt(slotIndex);

        // If the unit is already in a slot in this party, swap the two slots
        int existingSlot = party.unitKeys.IndexOf(inventoryUnitKey);
        if (existingSlot != -1)
        {
            party.SetUnitAt(existingSlot, displaced);
            party.SetUnitAt(slotIndex, inventoryUnitKey);
            SaveToJson();
            return true;
        }

        // Slot is occupied by a different unit coming from outside — free the displaced unit
        if (displaced != -1 && !IsUnitInAnyPartyExceptSlot(partyKey, slotIndex, displaced))
            SetIsInParty(displaced, false);

        party.SetUnitAt(slotIndex, inventoryUnitKey);
        SetIsInParty(inventoryUnitKey, true);

        SaveToJson();
        return true;
    }

    // Clears a specific slot, freeing the unit that was in it.
    public static bool ClearSlot(int partyKey, int slotIndex)
    {
        if (!parties.ContainsKey(partyKey)) return false;

        PartyData party = parties[partyKey];
        int unitKey = party.GetUnitAt(slotIndex);
        if (unitKey == -1) return false;

        party.ClearSlot(slotIndex);

        if (!IsUnitInAnyParty(unitKey))
            SetIsInParty(unitKey, false);

        SaveToJson();
        return true;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────────

    public static PartyData GetParty(int partyKey)
    {
        return parties.TryGetValue(partyKey, out var party) ? party : null;
    }

    public static List<UnitInventoryData> GetPartyUnits(int partyKey)
    {
        if (!parties.ContainsKey(partyKey)) return null;

        return parties[partyKey].unitKeys
            .Where(key => key != -1)
            .Select(key => PlayerUnitInventoryDatabase.GetUnitByKey(key))
            .Where(unit => unit != null)
            .ToList();
    }

    public static bool IsUnitInSlot(int partyKey, int slotIndex)
    {
        return parties.TryGetValue(partyKey, out var party) && party.GetUnitAt(slotIndex) != -1;
    }

    public static bool IsUnitInParty(int partyKey, int inventoryUnitKey)
    {
        return parties.TryGetValue(partyKey, out var party) && party.unitKeys.Contains(inventoryUnitKey);
    }

    public static bool IsUnitInAnyParty(int inventoryUnitKey)
    {
        if (inventoryUnitKey == -1) return false;
        return parties.Values.Any(p => p.unitKeys.Contains(inventoryUnitKey));
    }

    public static bool IsPartyFull(int partyKey)
    {
        return parties.TryGetValue(partyKey, out var party) && party.unitKeys.All(k => k != -1);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static void SetIsInParty(int inventoryUnitKey, bool value)
    {
        UnitInventoryData unit = PlayerUnitInventoryDatabase.GetUnitByKey(inventoryUnitKey);
        if (unit != null) unit.isInParty = value;
    }

    // Checks if a unit appears in any party, ignoring a specific slot (used when
    // determining whether a displaced unit should have isInParty cleared).
    private static bool IsUnitInAnyPartyExceptSlot(int partyKey, int slotIndex, int inventoryUnitKey)
    {
        foreach (var kvp in parties)
        {
            for (int i = 0; i < kvp.Value.unitKeys.Count; i++)
            {
                if (kvp.Key == partyKey && i == slotIndex) continue;
                if (kvp.Value.unitKeys[i] == inventoryUnitKey) return true;
            }
        }
        return false;
    }

    // Called after loading to restore all isInParty flags from saved party data.
    private static void RefreshAllIsInParty()
    {
        foreach (var kvp in PlayerUnitInventoryDatabase.playerUnits)
            kvp.Value.isInParty = false;

        foreach (var party in parties.Values)
            foreach (int unitKey in party.unitKeys)
                if (unitKey != -1) SetIsInParty(unitKey, true);
    }
}

// ─── Data Class ───────────────────────────────────────────────────────────────

public class PartyData
{
    public int leaderUnitIndex = 0;
    public Dictionary<int, int> slots = new Dictionary<int, int>(); // slotIndex -> unitKey

    [JsonIgnore]
    public List<int> unitKeys
    {
        get
        {
            var list = new List<int>(new int[PartyDatabase.MaxPartySize]);
            for (int i = 0; i < list.Count; i++) list[i] = -1;
            foreach (var kvp in slots)
                if (kvp.Key >= 0 && kvp.Key < PartyDatabase.MaxPartySize)
                    list[kvp.Key] = kvp.Value;
            return list;
        }
    }

    public bool IsSlotEmpty(int slot)  => !slots.ContainsKey(slot);
    public int  GetUnitAt(int slot)    => slots.TryGetValue(slot, out int key) ? key : -1;
    public void SetUnitAt(int slot, int key) => slots[slot] = key;
    public void ClearSlot(int slot)    => slots.Remove(slot);
}