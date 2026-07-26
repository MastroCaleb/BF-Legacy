using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerData
{
    public static string playerName = "Player";
    public static int level = 1;
    public static int experience = 0;
    public static int zel = 0;
    public static int gems = 0;
    public static int karma = 0;

    // Stats derived from current level data
    public static int energy = 0;
    public static int friendCapacity = 0;
    public static int cost = 0;
    public static int additionalCost = 0;

    private static Dictionary<int, LevelData> _levelTable;
    public static List<string> unitDex = new();
    public static List<string> completedMissionDex = new();
    public static List<string> presentReceivedDex = new();  // ids of present boxes received (obtained, may be unopened)
    public static List<string> presentCollectedDex = new(); // ids of present boxes opened/collected by the player

    public static void LoadLevelTable()
    {
        TextAsset json = Resources.Load<TextAsset>("Player/player_level");
        if (json == null)
        {
            Debug.LogError("Could not load level table from Resources/Player/player_level.json");
            return;
        }

        _levelTable = JsonConvert.DeserializeObject<Dictionary<int, LevelData>>(json.text);
        Debug.Log($"Loaded level table with {_levelTable.Count} entries.");
    }

    public static LevelData GetLevelData(int lvl)
    {
        if (_levelTable == null) LoadLevelTable();
        return _levelTable.TryGetValue(lvl, out LevelData data) ? data : null;
    }

    private static void ApplyLevelData(int lvl)
    {
        LevelData data = GetLevelData(lvl);
        if (data == null) return;

        energy = data.energy;
        friendCapacity = data.friendCapacity;
        cost = data.cost;
        additionalCost = data.additionalCost;
    }

    public static void LoadDataFromJson()
    {
        if (_levelTable == null) LoadLevelTable();

        string path = Application.persistentDataPath + "/playerdata.json";
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            PlayerDataSerializable data = JsonConvert.DeserializeObject<PlayerDataSerializable>(json);

            playerName = data.playerName;
            level = data.level;
            experience = data.experience;
            zel = data.zel < 0 ? data.zel * -1 : data.zel;
            gems = data.gems;
            karma = data.karma;

            // Older save files won't have these fields
            unitDex = data.unitDex ?? new List<string>();
            completedMissionDex = data.completedMissionDex ?? new List<string>();
            presentReceivedDex = data.presentReceivedDex ?? new List<string>();
            presentCollectedDex = data.presentCollectedDex ?? new List<string>();

            ApplyLevelData(level);
            PlayerUnitInventoryDatabase.LoadFromJson();
            PartyDatabase.LoadFromJson();
        }
        else
        {
            Debug.LogWarning("Player data file not found, using defaults.");
            ApplyLevelData(level);
        }
    }

    public static void SaveDataToJson()
    {
        PlayerDataSerializable data = new PlayerDataSerializable
        {
            playerName = playerName,
            level = level,
            experience = experience,
            zel = zel,
            gems = gems,
            karma = karma,

            unitDex = unitDex,
            completedMissionDex = completedMissionDex,
            presentReceivedDex = presentReceivedDex,
            presentCollectedDex = presentCollectedDex
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/playerdata.json", json);

        PlayerUnitInventoryDatabase.SaveToJson();
        PartyDatabase.SaveToJson();
    }

    public static void AddExperience(int amount)
    {
        experience += amount;
        CheckLevelUp();
    }

    private static void CheckLevelUp()
    {
        if (_levelTable == null) LoadLevelTable();

        bool didLevelUp = false;

        while (true)
        {
            // Level 1000 is max — exp to next level is 0
            if (level >= 1000) break;

            LevelData current = GetLevelData(level);
            if (current == null) break;

            int expNeeded = current.expToNextLevel;
            if (experience < expNeeded) break;

            experience -= expNeeded;
            level++;
            gems+=2;
            didLevelUp = true;

            Debug.Log($"Level up! Now level {level}.");
        }

        if (didLevelUp)
        {
            ApplyLevelData(level);
            SaveDataToJson();
        }
    }
}

[System.Serializable]
public class LevelData
{
    [JsonProperty("cost")]
    public int cost;

    [JsonProperty("additional cost")]
    public int additionalCost;

    [JsonProperty("energy")]
    public int energy;

    [JsonProperty("exp to next level")]
    public int expToNextLevel;

    [JsonProperty("friend capacity")]
    public int friendCapacity;
}

internal class PlayerDataSerializable
{
    public string playerName { get; set; }
    public int level { get; set; }
    public int experience { get; set; }
    public int zel { get; set; }
    public int gems { get; set; }
    public int karma { get; set; }

    public List<string> unitDex { get; set; }
    public List<string> completedMissionDex { get; set; }
    public List<string> presentReceivedDex { get; set; }
    public List<string> presentCollectedDex { get; set; }
}