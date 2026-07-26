using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FandomExpDownloader : MonoBehaviour
{
    private const string API_URL = "https://bravefrontierglobal.fandom.com/api.php";

    private readonly Dictionary<string, string> pages = new Dictionary<string, string>()
    {
        { "exp_base_10.json", "Unit_Leveling:10" },
        { "exp_base_15.json", "Unit_Leveling:15" },
        { "exp_base_21.json", "Unit_Leveling:21" }
    };

    void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        foreach (var entry in pages)
            yield return StartCoroutine(FetchAndSave(entry.Value, entry.Key));

        Debug.Log("All files saved!");
    }

    IEnumerator FetchAndSave(string page, string filename)
    {
        string url = $"{API_URL}?action=parse&page={Uri.EscapeDataString(page)}&prop=wikitext&format=json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", "UnityFandomClient/1.0");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to fetch {page}: {request.error}");
                yield break;
            }

            try
            {
                JObject json = JObject.Parse(request.downloadHandler.text);
                string wikitext = json["parse"]?["wikitext"]?["*"]?.ToString();

                if (string.IsNullOrEmpty(wikitext))
                {
                    Debug.LogError($"No wikitext found for {page}");
                    yield break;
                }

                List<ExpEntry> entries = ParseExpTable(wikitext);

                if (entries.Count == 0)
                {
                    Debug.LogWarning($"No entries parsed for {page}");
                    yield break;
                }

                string outputJson = JsonConvert.SerializeObject(entries, Formatting.Indented);
                string path = Path.Combine(Application.dataPath, filename);
                File.WriteAllText(path, outputJson);

                Debug.Log($"Saved {entries.Count} entries → {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing {page}: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    List<ExpEntry> ParseExpTable(string wikitext)
    {
        var entries = new List<ExpEntry>();

        // Split into rows on |-
        // Each data row looks like:
        //   !style="..."|  23        <- level (in a ! header cell)
        //   | 4,104||...||...        <- first | value is "To Next"
        string[] lines = wikitext.Split('\n');

        int pendingLevel = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Row separator — reset pending level
            if (line.StartsWith("|-"))
            {
                pendingLevel = -1;
                continue;
            }

            // Level cell: starts with ! and contains a number (not a column header word)
            if (line.StartsWith("!"))
            {
                // Strip style attributes: !style="..."| 23  →  23
                string cellValue = Regex.Replace(line, @"^!([^|]*\|)?", "").Trim();

                // Skip header rows like "Lv", "EXP", etc.
                if (int.TryParse(cellValue, out int lvl))
                    pendingLevel = lvl;

                continue;
            }

            // Data cell row — only care about it if we have a pending level
            if (line.StartsWith("|") && pendingLevel > 0)
            {
                // Format: | 4,104||...||...
                // Split on || to get individual columns; first one is "To Next"
                string cellLine = line.TrimStart('|').Trim();
                string[] cells = Regex.Split(cellLine, @"\|\|");

                if (cells.Length == 0) continue;

                // "To Next" is the first cell; strip commas and spaces
                string rawExp = cells[0].Replace(",", "").Trim();

                if (long.TryParse(rawExp, out long toNext))
                {
                    entries.Add(new ExpEntry { level = pendingLevel, toNext = toNext });
                    pendingLevel = -1; // consumed
                }
            }
        }

        return entries;
    }

    [Serializable]
    private class ExpEntry
    {
        public int level;
        public long toNext;
    }
}