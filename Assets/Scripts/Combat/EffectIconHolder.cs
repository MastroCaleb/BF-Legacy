using System.Collections.Generic;
using UnityEngine;

public class EffectIconHolder : MonoBehaviour
{
    public static EffectIconHolder Instance { get; private set; }
    public List<EffectIcon> effectIcons = new List<EffectIcon>();
}
[System.Serializable]
public class EffectIcon
{
    public string effectId;
    public Sprite icon;
}
