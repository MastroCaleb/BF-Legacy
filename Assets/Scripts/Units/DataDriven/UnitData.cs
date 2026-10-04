// UnitData.cs
// Data-driven unit DTO. Pure primitives + string asset addresses — no Unity
// asset references, so it survives Newtonsoft serialization. Unity objects
// (sprites/textassets/audio) are resolved at runtime from these addresses via
// AssetResolver; abilities are referenced by id and loaded via AbilityRegistry.
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class UnitData
{
    [JsonProperty("unitId")]    public string unitId;
    [JsonProperty("unitName")]  public string unitName;
    [JsonProperty("unitNumber")] public string unitNumber;
    [JsonProperty("description")] public string description;
    [JsonProperty("summonDesc")] public string summonDesc;
    [JsonProperty("fusionDesc")] public string fusionDesc;
    [JsonProperty("evoDesc")]   public string evoDesc;
    [JsonProperty("element")]   public ElementalType element;
    [JsonProperty("rarity")]    public UnitRarity rarity;

    // ── Asset addresses (resolved via AssetResolver) ──
    [JsonProperty("slotIconPath")] public string slotIconPath;
    [JsonProperty("iconPath")]     public string iconPath;
    [JsonProperty("fullArtPath")]  public string fullArtPath;
    [JsonProperty("animationType")] public AnimationType animationType;

    [JsonProperty("spriteSheetPaths")] public List<string> spriteSheetPaths;
    [JsonProperty("cggPath")]       public string cggPath;
    [JsonProperty("idleCgsPath")]   public string idleCgsPath;
    [JsonProperty("moveCgsPath")]   public string moveCgsPath;
    [JsonProperty("attackCgsPath")] public string attackCgsPath;
    [JsonProperty("samPath")]       public string samPath;

    // ── Display values ──
    [JsonProperty("unitDisplayHomePosition")]         public UnitDisplay unitDisplayHomePosition;
    [JsonProperty("unitDisplayDetailPosition")]      public UnitDisplay unitDisplayDetailPosition;
    [JsonProperty("unitDisplayConfirmImagePosition")] public UnitDisplay unitDisplayConfirmImagePosition;
    [JsonProperty("unitDisplayCutInImagePosition")]  public UnitDisplay unitDisplayCutInImagePosition;
    [JsonProperty("unitDisplaySummonPosition")]      public UnitDisplay unitDisplaySummonPosition;
    [JsonProperty("unitDisplayHpPosition")]          public Vector2 unitDisplayHpPosition;
    [JsonProperty("unitDisplayCursorPosition")]      public Vector2 unitDisplayCursorPosition;

    // ── Statistics ──
    [JsonProperty("aiLevel")]   public int aiLevel;
    [JsonProperty("maxLevel")]  public int maxLevel;
    [JsonProperty("maxHealth")] public int maxHealth;
    [JsonProperty("atk")]       public int atk;
    [JsonProperty("def")]       public int def;
    [JsonProperty("rec")]       public int rec;
    [JsonProperty("summonCost")] public int summonCost;
    [JsonProperty("baseExp")]   public int baseExp;

    [JsonProperty("statsBase")] public UnitStats statsBase;
    [JsonProperty("statsLord")] public UnitStats statsLord;

    // ── Abilities (ids into the shared ability JSON files) ──
    [JsonProperty("basicAbilityId")]  public string basicAbilityId;
    [JsonProperty("bbAbilityId")]     public string bbAbilityId;
    [JsonProperty("sbbAbilityId")]    public string sbbAbilityId;
    [JsonProperty("ubbAbilityId")]    public string ubbAbilityId;
    [JsonProperty("leaderAbilityId")] public string leaderAbilityId;
    [JsonProperty("extraAbilityId")]  public string extraAbilityId;
    [JsonProperty("bbType")]          public string bbType;

    // ── Movement ──
    [JsonProperty("moveSpeedAttack")]  public float moveSpeedAttack;
    [JsonProperty("moveSpeedSkill")]   public float moveSpeedSkill;
    [JsonProperty("speedTypeAttack")]  public int speedTypeAttack;
    [JsonProperty("speedTypeSkill")]   public int speedTypeSkill;
    [JsonProperty("moveTypeAttack")]   public int moveTypeAttack;
    [JsonProperty("moveTypeSkill")]    public int moveTypeSkill;

    // ── Evolution ──
    [JsonProperty("evoFrom")]    public string evoFrom;
    [JsonProperty("evoInto")]    public string evoInto;
    [JsonProperty("evoMats")]    public List<string> evoMats;
    [JsonProperty("evoItem")]    public string evoItem;
    [JsonProperty("evoZelCost")] public int evoZelCost;

    // ── Sell ──
    [JsonProperty("sellPrice")]   public int sellPrice;
    [JsonProperty("sellCaution")] public bool sellCaution;
}
