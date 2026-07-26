using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class SamJsonDatabase : MonoBehaviour
{
    [Header("SAM JSONs to cache")]
    public List<TextAsset> samJsonFiles;

    // Cached data
    private Dictionary<TextAsset, SamAnimation> animationCache = new Dictionary<TextAsset, SamAnimation>();
    private Dictionary<TextAsset, Dictionary<string, SamLabel>> labelCache = new Dictionary<TextAsset, Dictionary<string, SamLabel>>();

    // Singleton for easy access
    public static SamJsonDatabase Instance { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheAllJsons();
    }

    void CacheAllJsons()
    {
        foreach (var json in samJsonFiles)
        {
            if (json == null) continue;

            if (!animationCache.ContainsKey(json))
            {
                SamAnimation anim = JsonConvert.DeserializeObject<SamAnimation>(json.text);
                animationCache[json] = anim;

                // Cache labels
                var labels = new Dictionary<string, SamLabel>();
                if (anim.mLabels != null)
                {
                    foreach (var label in anim.mLabels)
                        labels[label.mLabelName] = label;
                }
                labelCache[json] = labels;
            }
        }
    }

    // --- Public access methods ---
    public SamAnimation GetAnimation(TextAsset json)
    {
        if (json == null) return null;
        animationCache.TryGetValue(json, out var anim);
        return anim;
    }

    public Dictionary<string, SamLabel> GetLabels(TextAsset json)
    {
        if (json == null) return null;
        labelCache.TryGetValue(json, out var labels);
        return labels;
    }
}

