using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class EffectDescriptionPatternCollector : MonoBehaviour
{
    [Header("Input")]
    public string unitJsonFolder = "BFUnits";

    [Header("Output")]
    public string outputFileName = "effect_description_patterns.json";

    [Header("Behavior")]
    public bool debugLogging = true;

    void Start()
    {
        CompileEffectPatterns();
    }

    void CompileEffectPatterns()
    {
        string folderPath = Path.Combine(Application.dataPath, unitJsonFolder);
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"[PatternCollector] Folder not found: {folderPath}");
            return;
        }

        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);

        if (debugLogging)
            Debug.Log($"[PatternCollector] Scanning {files.Length} unit JSON files");

        foreach (var file in files)
        {
            try
            {
                var json = JObject.Parse(File.ReadAllText(file));

                foreach (var prop in json.Properties())
                {
                    if (!prop.Name.EndsWith("_effects"))
                        continue;

                    if (prop.Value is not JArray effects)
                        continue;

                    foreach (var effect in effects)
                    {
                        string name = effect["name"]?.ToString()?.Trim();
                        string value = effect["value"]?.ToString()?.Trim();

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                            continue;

                        string normalized = NormalizeDescription(value);

                        if (!result.TryGetValue(name, out var set))
                        {
                            set = new HashSet<string>();
                            result[name] = set;
                        }

                        set.Add(normalized);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PatternCollector] Failed parsing {file}: {ex.Message}");
            }
        }

        // Convert HashSet -> sorted list for readability
        var output = new JObject();
        foreach (var kvp in result)
        {
            var list = new List<string>(kvp.Value);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            output[kvp.Key] = JArray.FromObject(list);
        }

        string outputPath = Path.Combine(Application.dataPath, outputFileName);
        File.WriteAllText(outputPath, output.ToString(Newtonsoft.Json.Formatting.Indented));

        Debug.Log($"✔ Effect pattern corpus saved to:\n{outputPath}");
    }

    /// <summary>
    /// Replaces all numeric forms with [number] while preserving structure.
    /// Examples:
    ///  - 2060~2360 → [number]~[number]
    ///  - 27% → [number]%
    ///  - 5~8% → [number]~[number]%
    /// </summary>
    string NormalizeDescription(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        string s = input;

        // 1️⃣ Remove equipment requirements
        s = Regex.Replace(
            s,
            @"\s*,?\s*Requires\s+.+?\s+equipped",
            "",
            RegexOptions.IgnoreCase
        );

        // 2️⃣ Replace ranges first (e.g. 2060~2360)
        s = Regex.Replace(s, @"\d+\s*~\s*\d+", "[number]~[number]");

        // 3️⃣ Replace percentages
        s = Regex.Replace(s, @"\d+%", "[number]%");

        // 4️⃣ Replace remaining standalone numbers
        s = Regex.Replace(s, @"\d+", "[number]");

        // 5️⃣ Normalize whitespace & punctuation
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();
        s = s.TrimEnd(',', '.', ';');

        return s;
    }

}

