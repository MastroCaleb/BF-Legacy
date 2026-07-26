using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public static class ExperienceTable
{
    // Loaded tables keyed by exp base (10, 15, 21)
    private static Dictionary<int, long[]> tables = new Dictionary<int, long[]>();

    private static readonly Dictionary<int, string> tableFiles = new Dictionary<int, string>
    {
        { 10, "ExpTables/exp_base_10" },
        { 15, "ExpTables/exp_base_15" },
        { 21, "ExpTables/exp_base_21" }
    };

    // Call this once at startup (e.g. from a GameManager Awake)
    public static void LoadAll()
    {
        foreach (var entry in tableFiles)
            LoadTable(entry.Key, entry.Value);
    }

    private static void LoadTable(int expBase, string resourcePath)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogError($"ExperienceTable: Could not load {resourcePath}");
            return;
        }

        List<ExpEntry> entries = JsonConvert.DeserializeObject<List<ExpEntry>>(asset.text);
        if (entries == null || entries.Count == 0)
        {
            Debug.LogError($"ExperienceTable: Empty or invalid data in {resourcePath}");
            return;
        }

        // Find max level to size the array
        int maxLevel = 0;
        foreach (var e in entries)
            if (e.level > maxLevel) maxLevel = e.level;

        // Index by level directly (1-based, so array size = maxLevel + 1)
        long[] expArray = new long[maxLevel + 1];
        foreach (var e in entries)
            expArray[e.level] = e.toNext;

        tables[expBase] = expArray;
        Debug.Log($"ExperienceTable: Loaded base {expBase} ({entries.Count} levels)");
    }

    /// <summary>
    /// Returns the EXP needed to go FROM level to level+1.
    /// Uses the unit's expBase (10, 15, or 21).
    /// </summary>
    public static long GetExpToNextLevel(int currentLevel, int expBase)
    {
        if (!tables.TryGetValue(expBase, out long[] table))
        {
            Debug.LogError($"ExperienceTable: Table for base {expBase} not loaded.");
            return long.MaxValue;
        }

        if (currentLevel < 0 || currentLevel >= table.Length)
            return 0; // Max level reached

        return table[currentLevel];
    }

    public static long GetCumulativeExp(int level, int expBase)
    {
        long total = 0;
        for (int lvl = 1; lvl < level; lvl++)
            total += GetExpToNextLevel(lvl, expBase);
        return total;
    }

    private class ExpEntry
    {
        public int level;
        public long toNext;
    }
}