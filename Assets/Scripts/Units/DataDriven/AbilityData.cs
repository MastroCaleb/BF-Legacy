// AbilityData.cs
// Data-driven ability DTO. Hit frames / levels reuse the plain serializable
// classes from Ability.cs (HitFrame, AbilityLevel, Effect + sub-structs) — they
// hold no Unity asset references, so they round-trip through Newtonsoft.
// Effect frames are EffectFrameData: string ids/paths instead of resolved
// ParticleEffect/AudioClip references.
using System.Collections.Generic;
using Newtonsoft.Json;

public class AbilityData
{
    [JsonProperty("abilityId")]   public string abilityId;
    [JsonProperty("abilityName")] public string abilityName;
    [JsonProperty("abilityDesc")] public string abilityDesc;

    [JsonProperty("targetType")] public TargetType targetType;
    [JsonProperty("targetArea")] public TargetArea targetArea;

    [JsonProperty("dropChecksPerHit")] public int dropChecksPerHit;
    [JsonProperty("hitFrames")]    public List<HitFrame> hitFrames;
    [JsonProperty("effectFrames")] public List<EffectFrameData> effectFrames;

    [JsonProperty("levels")] public List<AbilityLevel> levels;
}

/// <summary>
/// Data twin of EffectFrame (Ability.cs) with the Unity references replaced by
/// ids/paths: particleEffect → particleEffectId (addressable ParticleEffect SO),
/// audioClip → audioClipPath (addressable AudioClip). Absolute frame arithmetic
/// (skillFrame + subFrame) is already baked into `frame` by the importer.
/// </summary>
public class EffectFrameData
{
    [JsonProperty("frame")] public int frame;

    [JsonProperty("battleEffectGroupId")] public string battleEffectGroupId;

    [JsonProperty("particleEffectId")] public string particleEffectId;

    [JsonProperty("placementType")] public int placementType;
    [JsonProperty("xAnchor")]  public int xAnchor;
    [JsonProperty("yAnchor")]  public int yAnchor;
    [JsonProperty("offsetX")]  public float offsetX;
    [JsonProperty("offsetY")]  public float offsetY;

    [JsonProperty("audioClipPath")] public string audioClipPath;
}
