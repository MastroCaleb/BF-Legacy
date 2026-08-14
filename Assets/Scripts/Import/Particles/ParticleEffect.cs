using UnityEngine;

[CreateAssetMenu(fileName = "ParticleEffect", menuName = "BraveFrontier/Particle Effect")]
public class ParticleEffect : ScriptableObject
{
    public string effectId;
    public string effectType;
    public string battleEffectGroupId;

    public ParticleType particleType;
    
    //PLIST
    public TextAsset plistJson;

    //CGG
    public TextAsset cggJson;
    public TextAsset cgsJson;
    public Sprite spriteSheet;

    //SAM
    public TextAsset samJson;
}
public enum ParticleType
{
    PLIST,
    CGG,
    SAM
}

