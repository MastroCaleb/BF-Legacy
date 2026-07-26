using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Summon Banner", menuName = "Summon/Summon Banner")]
public class SummonBanner : ScriptableObject
{
    public string bannerName;
    public int cost;
    [TextArea]
    public string bannerDesc;
    public CostType costType;
    public List<SummonPool> baseSummonPools;
    public List<SummonPool> featuredSummonPools;
}
public enum CostType
{
    Gems
}
