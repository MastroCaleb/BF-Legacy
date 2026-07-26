using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleEffectGroup", menuName = "BraveFrontier/Battle Effect Group")]
public class BattleEffectGroup : ScriptableObject
{
    public int battleEffectGroupId;
    public List<EffectFrame> effectFrames;
}
