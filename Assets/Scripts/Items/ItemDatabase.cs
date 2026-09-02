// ItemDatabase.cs
//
// Runtime item lookup, loaded on demand rather than up front.
//
// items.json is ~1,700 entries / 1.6MB. Two different costs are involved:
//   1. Parsing the raw JSON text into a JObject tree — cheap, happens once.
//   2. Converting one entry's "effect" data into Effect/AttackEffect/
//      StatBuffEffect/etc. objects — the allocation-heavy part, since Effect
//      alone carries ~15 always-populated sub-effect structs.
//
// This splits the two: the JSON index loads once (lazily, on first
// GetItem call), but step 2 only runs for an item id the first time it's
// actually requested (a unit's equipped sphere, an inventory row being
// drawn, a consumable being used), and the result is cached from then on.
// Nothing parses all ~1,700 effect graphs just because one item was needed.
//
// Thumbnails load synchronously, right when the item itself is parsed by
// GetItem — no separate on-demand call, no callback. WaitForCompletion()
// blocks until the Addressables load finishes, so the first GetItem call
// for a given item id costs one blocking asset load; every call after that
// is a cache hit (both for the ItemData and its thumbnailSprite).

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ItemDatabase
{
    private const string ITEMS_RESOURCE_PATH = "items"; // Assets/Resources/items.json

    private static JObject _index;                                        // raw json, loaded once
    private static readonly Dictionary<string, ItemData> _cache = new();   // parsed-on-demand
    private static Dictionary<string, List<string>> _nameToIds;            // lowercased name -> ids sharing it, built lazily

    // Holds the Addressables handle for each item's loaded thumbnail, so
    // ReleaseThumbnail/ClearThumbnailCache have something to release.
    private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> _thumbnailHandles = new();

    /// <summary>
    /// Returns the parsed ItemData for the given id, or null if the id
    /// isn't in items.json. Parses and caches on first call for that id;
    /// subsequent calls are a dictionary lookup.
    /// </summary>
    public static ItemData GetItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        EnsureIndexLoaded();

        if (_cache.TryGetValue(itemId, out ItemData cached)) return cached;

        if (_index[itemId] is not JObject raw)
        {
            Debug.LogWarning($"[ItemDatabase] No item found for id '{itemId}'.");
            return null;
        }

        ItemData item = ParseItem(itemId, raw);
        _cache[itemId] = item;
        return item;
    }

    /// <summary>
    /// Looks an item up by name instead of id (case-insensitive, exact
    /// match). items.json has a handful of duplicate names (28, mostly
    /// event-reward/test items) — if `name` is one of those, this returns
    /// whichever id happens to come first and logs a warning; use
    /// GetItemsByName if the caller actually needs every match.
    /// </summary>
    public static ItemData GetItemByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        EnsureNameIndexBuilt();

        if (!_nameToIds.TryGetValue(name.Trim().ToLowerInvariant(), out List<string> ids) || ids.Count == 0)
        {
            Debug.LogWarning($"[ItemDatabase] No item found with name '{name}'.");
            return null;
        }

        if (ids.Count > 1)
            Debug.LogWarning($"[ItemDatabase] '{name}' matches {ids.Count} items (ids: {string.Join(", ", ids)}) — returning the first. Use GetItemsByName to get all of them.");

        return GetItem(ids[0]);
    }

    /// <summary>Every item whose name matches (case-insensitive) — for the ~28 names items.json reuses across multiple ids.</summary>
    public static List<ItemData> GetItemsByName(string name)
    {
        List<ItemData> results = new();
        if (string.IsNullOrEmpty(name)) return results;

        EnsureNameIndexBuilt();

        if (_nameToIds.TryGetValue(name.Trim().ToLowerInvariant(), out List<string> ids))
            foreach (string id in ids)
                results.Add(GetItem(id));

        return results;
    }

    /// <summary>Convenience for warming the cache for a known set of ids ahead of time (e.g. a party's equipped spheres before battle) — still parses each one individually and lazily, just gets it out of the way early.</summary>
    public static void Preload(IEnumerable<string> itemIds)
    {
        foreach (string id in itemIds) GetItem(id);
    }

    /// <summary>Drops all parsed (not raw-index) items — call if memory needs reclaiming between contexts, e.g. leaving a shop/inventory screen.</summary>
    public static void ClearParsedCache() => _cache.Clear();

    // ─────────────────────────────────────────────────────────────
    //  THUMBNAILS
    // ─────────────────────────────────────────────────────────────

    // Called from ParseItem — blocks until the Addressables load for this
    // item's thumbnail finishes, then leaves the result on item.thumbnailSprite.
    private static void LoadThumbnailSync(ItemData item)
    {
        if (string.IsNullOrEmpty(item.thumbnailAddressableKey)) return;

        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(item.thumbnailAddressableKey);
        Sprite result = handle.WaitForCompletion();

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[ItemDatabase] Failed to load thumbnail '{item.thumbnailAddressableKey}' for item '{item.itemId}'.");
            return;
        }

        _thumbnailHandles[item.itemId] = handle;
        item.thumbnailSprite = result;
    }

    /// <summary>Releases one item's thumbnail handle back to Addressables and clears it off the ItemData.</summary>
    public static void ReleaseThumbnail(ItemData item)
    {
        if (item == null) return;

        if (_thumbnailHandles.TryGetValue(item.itemId, out AsyncOperationHandle<Sprite> handle))
        {
            if (handle.IsValid()) Addressables.Release(handle);
            _thumbnailHandles.Remove(item.itemId);
        }

        item.thumbnailSprite = null;
    }

    /// <summary>Releases every loaded thumbnail handle back to Addressables and clears them off every cached ItemData. Call when leaving a screen that pulled in a lot of thumbnails (shop, full inventory grid) if memory needs reclaiming.</summary>
    public static void ClearThumbnailCache()
    {
        foreach (AsyncOperationHandle<Sprite> handle in _thumbnailHandles.Values)
            if (handle.IsValid()) Addressables.Release(handle);

        _thumbnailHandles.Clear();

        foreach (ItemData item in _cache.Values)
            item.thumbnailSprite = null;
    }

    private static void EnsureIndexLoaded()
    {
        if (_index != null) return;

        TextAsset json = Resources.Load<TextAsset>(ITEMS_RESOURCE_PATH);
        if (json == null)
        {
            Debug.LogError($"[ItemDatabase] Could not find items.json at Resources/{ITEMS_RESOURCE_PATH}.");
            _index = new JObject(); // empty, so GetItem calls fail soft instead of re-attempting the load every time
            return;
        }

        _index = JObject.Parse(json.text);
    }

    // Just scans the already-loaded raw index for each entry's "name" field —
    // no effect parsing, so this doesn't touch the expensive per-item path.
    // Still deferred to first name lookup rather than built alongside the
    // index, since id-only consumers (the common case) never need it.
    private static void EnsureNameIndexBuilt()
    {
        if (_nameToIds != null) return;

        EnsureIndexLoaded();

        _nameToIds = new Dictionary<string, List<string>>();
        foreach (KeyValuePair<string, JToken> kvp in _index)
        {
            string name = (kvp.Value as JObject)?["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            string key = name.Trim().ToLowerInvariant();
            if (!_nameToIds.TryGetValue(key, out List<string> ids))
                _nameToIds[key] = ids = new List<string>();

            ids.Add(kvp.Key);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PER-ITEM PARSE
    // ─────────────────────────────────────────────────────────────

    private static ItemData ParseItem(string itemId, JObject o)
    {
        ItemType type = ParseItemType(o["type"]?.ToString());

        ItemData item = new()
        {
            itemId      = itemId,
            itemName    = o["name"]?.ToString(),
            desc        = o["desc"]?.ToString(),
            itemType    = type,
            rarity      = EffectParser.ParseInt(o["rarity"]),
            sellPrice   = EffectParser.ParseInt(o["sell_price"]),
            maxStack    = EffectParser.ParseInt(o["max_stack"]),
            maxEquipped = EffectParser.ParseInt(o["max equipped"]),
            raid        = EffectParser.ParseBool(o["raid"]),
            thumbnail   = o["thumbnail"]?.ToString()
        };

        // Just a string op — cheap either way.
        if (!string.IsNullOrEmpty(item.thumbnail))
            item.thumbnailAddressableKey = Path.GetFileNameWithoutExtension(item.thumbnail);

        // Loaded right here, synchronously, as part of parsing the item —
        // not deferred to a separate call.
        LoadThumbnailSync(item);

        if (o["recipe"] is JObject recipe)
            item.recipe = ParseRecipe(recipe);

        switch (type)
        {
            case ItemType.Sphere:
            case ItemType.LsSphere:
                item.sphereType     = EffectParser.ParseInt(o["sphere type"]);
                item.sphereTypeText = o["sphere type text"]?.ToString();

                // Flat array, conditions flattened onto each entry — no
                // wrapper-level targeting to pass through (passives don't
                // target).
                item.effects = EffectParser.ParseItemStyleEffects(o["effect"] as JArray);
                break;

            case ItemType.Consumable:
            case ItemType.SummonerConsumable:
                // Wrapped shape: { "effect": { "effect": [...], "target_type": "...", "target_area": "..." } }
                if (o["effect"] is JObject wrapper)
                {
                    string targetType = wrapper["target_type"]?.ToString();
                    string targetArea = wrapper["target_area"]?.ToString();
                    item.effects = EffectParser.ParseItemStyleEffects(wrapper["effect"] as JArray, targetType, targetArea);
                }
                break;

            case ItemType.Material:
            case ItemType.EvoMat:
            default:
                // No "effect" field on these at all — item.effects stays empty.
                break;
        }

        return item;
    }

    private static ItemRecipe ParseRecipe(JObject o)
    {
        ItemRecipe recipe = new()
        {
            karma = EffectParser.ParseInt(o["karma"])
        };

        if (o["materials"] is JArray materials)
            foreach (JToken t in materials)
                if (t is JObject m)
                    recipe.materials.Add(new ItemRecipeMaterial
                    {
                        itemId = m["id"]?.ToString(),
                        count  = EffectParser.ParseInt(m["count"])
                    });

        return recipe;
    }

    private static ItemType ParseItemType(string s) => s switch
    {
        "material"             => ItemType.Material,
        "evomat"               => ItemType.EvoMat,
        "summoner_consumable"  => ItemType.SummonerConsumable,
        "consumable"           => ItemType.Consumable,
        "sphere"               => ItemType.Sphere,
        "ls_sphere"            => ItemType.LsSphere,
        _                      => ItemType.Unknown
    };
}