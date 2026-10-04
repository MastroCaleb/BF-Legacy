// DataDrivenAddresses.cs
// Central Addressables address scheme for the data-driven pipeline.
// Asset addresses mirror the source paths under Assets/ (extension-less):
//   BF_Assets/content/unit/img/unit_ills_thum_10011
//   BF_Assets/content/unit/cgg/unit_cgg_10011
//   BF_Assets/content/unit/cgs/unit_idle_cgs_10011
//   BF_Assets/content/sound/bf307_se_od_invocation_1
//   Sams/Unit_SAMS/unit_sam/10011/unit_anime_10011   (same as BfAddressablesSetup)
//   {effectId}                                        (ParticleEffect SOs)
// Data files (emitted by the regen tool) are flat:
//   unit_{unitId}          → UnitData JSON
//   ability_{skillId}      → AbilityData JSON
// Editor table data ships under their EditorData paths.
public static class DataDrivenAddresses
{
    public const string SpriteRoot = "BF_Assets/content/unit/img";
    public const string CggRoot    = "BF_Assets/content/unit/cgg";
    public const string CgsRoot    = "BF_Assets/content/unit/cgs";
    public const string SoundRoot  = "BF_Assets/content/sound";
    public const string SamRoot    = "Sams/Unit_SAMS/unit_sam";

    public const string SkillMstLabel       = "dd_skillmst";
    public const string EffectGroupMstLabel = "dd_effectgroupmst";

    public static string UnitJson(string unitId)       => $"unit_{unitId}";
    public static string AbilityJson(string abilityId) => $"ability_{abilityId}";

    public static string Sprite(string fileNameNoExt)       => $"{SpriteRoot}/{fileNameNoExt}";
    public static string Cgg(string animationId)            => $"{CggRoot}/unit_cgg_{animationId}";
    public static string Cgs(string kind, string animationId) => $"{CgsRoot}/unit_{kind}_cgs_{animationId}"; // idle/move/atk
    public static string SpriteSheet(string unitId)         => $"{SpriteRoot}/unit_anime_{unitId}";
    public static string Sam(string unitId)                 => $"{SamRoot}/unit_{unitId}/unit_anime_{unitId}";
    public static string Sound(string clipNameNoExt)        => $"{SoundRoot}/{clipNameNoExt}";
}
