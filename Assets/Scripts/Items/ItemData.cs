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

    [System.NonSerialized] public Sprite thumbnailSprite; // null until ItemDatabase.LoadThumbnailAsync is called for this item; cached here from then on

    // Sphere / ls_sphere only
    public int    sphereType;
    public string sphereTypeText;

    // Consumable / summoner_consumable only — the wrapper-level targeting
    // that applies to every entry in effects.
    public TargetType targetType;
    public TargetArea targetArea;

    // Same Effect type Abilities use — parsed by EffectParser.
    // Empty for materials/evomats, which have no "effect" field at all.
    public List<Effect> effects = new();
}