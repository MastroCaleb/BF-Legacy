using UnityEngine;

[CreateAssetMenu(fileName = "ParticleEffect", menuName = "BraveFrontier/Particle Effect")]
public class ParticleEffect : ScriptableObject
{
    public string effectId;
    public string effectType;
    public string battleEffectGroupId;
    
    public TextAsset plistJson;   // JSON extracted from the plist
    public Sprite sprite;         // PNG extracted from the plist
}

