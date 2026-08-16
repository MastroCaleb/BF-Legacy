using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Summon Banner", menuName = "Summon/Summon Banner")]
public class SummonBanner : ScriptableObject
{
    public string headerText;
    public int cost;
    public CostType costType;
    public List<SummonPool> baseSummonPools;
    public List<SummonPool> featuredSummonPools;

    public string requiredMissionId = "2"; //mission id 2 is the tutorial

    [Range(0f, 100f)]
    public float surpriseDoorBreakChance = 25f;

    [Range(0f, 100f)]
    public float featuredPullChance = 20f;

    [Range(0f, 100f)]
    public float evoChance = 4f;

    //Banner UI
    [TextArea]
    public string bannerDesc;
    public Sprite bgBannerSprite;
    public Sprite buttonSprite;
    public Sprite pressedButtonSprite;

    //Gate UI
    public string gateName;
    [TextArea]
    public string gateDesc;
    public Sprite gateSprite;
}
public enum CostType
{
    Gems,
    Zel,
    Karma
}
