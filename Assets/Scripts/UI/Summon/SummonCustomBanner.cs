using TMPro;
using UnityEngine;

public class SummonCustomBanner : MonoBehaviour
{
    public SummonBanner summonBanner;
    public TextMeshProUGUI bannerTitleText;
    public TextMeshProUGUI bannerDescText;
    public TextMeshProUGUI bannerCostDescText;

    void OnEnable()
    {
        if (summonBanner != null)
        {
            bannerTitleText.text = summonBanner.bannerName;
            bannerDescText.text = summonBanner.bannerDesc;
            bannerCostDescText.text =  $"\nSummon once for <color=green>{summonBanner.cost} {summonBanner.costType}</color>" + $"\nAvailable: <color=green>{PlayerData.gems} Gem(s)</color>" + $"\nYou can summon {PlayerData.gems / summonBanner.cost} time(s)";
        }
    }
}
