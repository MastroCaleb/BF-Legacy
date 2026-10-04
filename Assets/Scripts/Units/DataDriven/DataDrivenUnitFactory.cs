// DataDrivenUnitFactory.cs
// Builds runtime Unit views from UnitData: deserializes the JSON DTO, resolves
// every Unity asset (sprites/textassets/audio via AssetResolver) and ability
// (via AbilityRegistry) and populates a ScriptableObject.CreateInstance<Unit>.
// The instance is never saved to disk — same consumer surface as the baked
// Unit SOs, zero .asset baking. Units are cached for the session (v1 policy).
using System.Collections.Generic;
using UnityEngine;

public static class DataDrivenUnitFactory
{
    static readonly Dictionary<string, Unit> s_units = new Dictionary<string, Unit>();

    public static Unit GetOrBuildUnit(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;
        if (s_units.TryGetValue(unitId, out Unit cached)) return cached;

        UnitData data = UnitDataRegistry.GetUnitData(unitId);
        if (data == null)
        {
            Debug.LogError($"Unit {unitId} not found.");
            return null;
        }

        Unit unit = Build(data);
        s_units[unitId] = unit;
        return unit;
    }

    public static bool TryGetLoadedUnit(string unitId, out Unit unit)
        => s_units.TryGetValue(unitId, out unit);

    /// <summary>
    /// Sync warmup for battle start: builds each unit (caching it) and preloads
    /// its SAM frame textures, so later loads resolve instantly. Parity with the
    /// baked-path Warmup + SamTextureProvider.PreloadUnitSam sequence.
    /// </summary>
    public static void Warmup(IEnumerable<string> unitIds)
    {
        foreach (string unitId in unitIds)
        {
            if (string.IsNullOrEmpty(unitId)) continue;

            Unit unit = GetOrBuildUnit(unitId);
            if (unit != null && unit.samFile != null)
                SamTextureProvider.PreloadUnitSam(unit.samFile);
        }
    }

    static Unit Build(UnitData d)
    {
        Unit u = ScriptableObject.CreateInstance<Unit>();
        u.name = $"unit_{d.unitId}";

        // ── Details ──
        u.unitId      = d.unitId;
        u.unitName    = d.unitName;
        u.unitNumber  = d.unitNumber;
        u.description = d.description;
        u.summonDesc  = d.summonDesc;
        u.fusionDesc  = d.fusionDesc;
        u.evoDesc     = d.evoDesc;
        u.element     = d.element;
        u.rarity      = d.rarity;

        // ── Visuals (runtime-resolved) ──
        u.unitSlotIcon  = AssetResolver.LoadSprite(d.slotIconPath);
        u.unitIcon      = AssetResolver.LoadSprite(d.iconPath);
        u.unitFullArt   = AssetResolver.LoadSprite(d.fullArtPath);
        u.animationType = d.animationType;

        u.spriteSheets   = LoadTextures(d.spriteSheetPaths);
        u.cggFile        = AssetResolver.LoadText(d.cggPath);
        u.idleCgsFile    = AssetResolver.LoadText(d.idleCgsPath);
        u.moveCgsFile    = AssetResolver.LoadText(d.moveCgsPath);
        u.attackCgsFile  = AssetResolver.LoadText(d.attackCgsPath);
        u.samFile        = AssetResolver.LoadText(d.samPath);

        // ── Display values ──
        u.unitDisplayHomePosition         = d.unitDisplayHomePosition;
        u.unitDisplayDetailPosition       = d.unitDisplayDetailPosition;
        u.unitDisplayConfirmImagePosition = d.unitDisplayConfirmImagePosition;
        u.unitDisplayCutInImagePosition   = d.unitDisplayCutInImagePosition;
        u.unitDisplaySummonPosition       = d.unitDisplaySummonPosition;
        u.unitDisplayHpPosition           = d.unitDisplayHpPosition;
        u.unitDisplayCursorPosition       = d.unitDisplayCursorPosition;

        // ── Statistics ──
        u.aiLevel   = d.aiLevel;
        u.maxLevel  = d.maxLevel;
        u.maxHealth = d.maxHealth;
        u.atk       = d.atk;
        u.def       = d.def;
        u.rec       = d.rec;
        u.summonCost = d.summonCost;
        u.baseExp   = d.baseExp;
        u.statsBase = d.statsBase;
        u.statsLord = d.statsLord;

        // ── Abilities ──
        u.basicAbility  = AbilityRegistry.GetAbilityById(d.basicAbilityId);
        u.bbAbility     = AbilityRegistry.GetAbilityById(d.bbAbilityId);
        u.sbbAbility    = AbilityRegistry.GetAbilityById(d.sbbAbilityId);
        u.ubbAbility    = AbilityRegistry.GetAbilityById(d.ubbAbilityId);
        u.leaderAbility = AbilityRegistry.GetAbilityById(d.leaderAbilityId);
        u.extraAbility  = AbilityRegistry.GetAbilityById(d.extraAbilityId);
        u.bbType        = d.bbType;

        // ── Movement ──
        u.moveSpeedAttack  = d.moveSpeedAttack;
        u.moveSpeedSkill   = d.moveSpeedSkill;
        u.speedTypeAttack  = d.speedTypeAttack;
        u.speedTypeSkill   = d.speedTypeSkill;
        u.moveTypeAttack   = d.moveTypeAttack;
        u.moveTypeSkill    = d.moveTypeSkill;

        // ── Evolution ──
        u.evoFrom    = d.evoFrom;
        u.evoInto    = d.evoInto;
        u.evoMats    = d.evoMats;
        u.evoItem    = d.evoItem;
        u.evoZelCost = d.evoZelCost;

        // ── Sell ──
        u.sellPrice   = d.sellPrice;
        u.sellCaution = d.sellCaution;

        return u;
    }

    static Texture2D[] LoadTextures(List<string> addresses)
    {
        if (addresses == null || addresses.Count == 0) return null;

        var sheets = new List<Texture2D>(addresses.Count);
        foreach (string address in addresses)
        {
            Texture2D tex = AssetResolver.LoadTexture(address);
            if (tex != null) sheets.Add(tex);
        }
        return sheets.Count > 0 ? sheets.ToArray() : null;
    }
}
