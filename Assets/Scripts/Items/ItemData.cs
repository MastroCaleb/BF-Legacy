// ItemData.cs
//
// Plain runtime representation of one items.json entry. Not a
// ScriptableObject — items.json has ~1,700 entries and most of them
// (materials, evo mats) carry no gameplay effects at all, so baking every
// one into an asset isn't worth it. ItemDatabase parses these on demand;
// see ItemDatabase.cs.

using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Material,
    EvoMat,
    SummonerConsumable,
    Consumable,
    Sphere,
    LsSphere,
    Unknown
}

[System.Serializable]
public class ItemData
{
    public string   itemId;
    public string   itemName;
    public string   desc;
    public ItemType itemType;

    public int  rarity;
    public int  sellPrice;
    public int  maxStack;
    public int  maxEquipped;   // consumables/summoner_consumables only; 0 if not present
    public bool raid;
    public string thumbnail;              // raw filename from items.json, e.g. "item_thum_102.png"
    public string thumbnailAddressableKey; // thumbnail without extension — the actual Addressables key. Cheap, always populated at parse time.

    [System.NonSerialized] public Sprite thumbnailSprite; // loaded synchronously by ItemDatabase.GetItem, alongside the rest of the item

    // Sphere / ls_sphere only
    public int    sphereType;
    public string sphereTypeText;

    // Consumable / summoner_consumable only — the wrapper-level targeting
    // that applies to every entry in effects.
    public TargetType targetType;
    public TargetArea targetArea;

    // Sphere / ls_sphere / some consumable — synthesis/crafting cost. Null
    // if this item has no "recipe" field (materials, evomats, and most
    // consumables can't be crafted).
    public ItemRecipe recipe;

    // Same Effect type Abilities use — parsed by EffectParser.
    // Empty for materials/evomats, which have no "effect" field at all.
    public List<Effect> effects = new();
}

[System.Serializable]
public class ItemRecipe
{
    public int karma;
    public List<ItemRecipeMaterial> materials = new();
}

[System.Serializable]
public class ItemRecipeMaterial
{
    public string itemId; // items.json stores this as a bare int, but every other id in ItemData is a string for consistency with ItemDatabase's string-keyed lookups
    public int    count;
}