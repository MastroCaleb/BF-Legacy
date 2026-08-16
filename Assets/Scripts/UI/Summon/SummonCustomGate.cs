using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummonCustomGate : MonoBehaviour
{
    public SummonBanner summonBanner;

    public SummonGate summonGate;
    public TextMeshProUGUI gateTitleText;
    public TextMeshProUGUI gateDescText;
    public TextMeshProUGUI bannerCostDescText;
    public Image summonButtonImage;
    public Image gateImage;

    public TextMeshProUGUI headerText;

    void OnEnable()
    {
        if (summonBanner != null)
        {
            summonGate.summonBanner = summonBanner;

            gateTitleText.text = summonBanner.gateName;
            gateDescText.text = summonBanner.gateDesc;
            BannerCostDescTextUpdate();
            summonButtonImage.sprite = summonBanner.buttonSprite;
            Button btn = summonButtonImage.GetComponent<Button>();
            SpriteState state = btn.spriteState;
            state.pressedSprite = summonBanner.pressedButtonSprite;
            btn.spriteState = state;
            gateImage.sprite = summonBanner.gateSprite;

            headerText.text = summonBanner.headerText;
        }
    }

    void BannerCostDescTextUpdate()
    {
        if (summonBanner != null)
        {
            switch(summonBanner.costType)
            {
                case CostType.Gems:
                    bannerCostDescText.text =  $"\nSummon once for <color=green>{summonBanner.cost} {summonBanner.costType}</color>" + $"\nAvailable: <color=green>{PlayerData.gems} Gem(s)</color>" + $"\nYou can summon {PlayerData.gems / summonBanner.cost} time(s)";
                    break;
                case CostType.Zel:
                    bannerCostDescText.text =  $"\nSummon once for <color=yellow>{summonBanner.cost} {summonBanner.costType}</color>" + $"\nAvailable: <color=yellow>{PlayerData.zel} Zel</color>" + $"\nYou can summon {PlayerData.zel / summonBanner.cost} time(s)";
                    break;
                case CostType.Karma:
                    bannerCostDescText.text =  $"\nSummon once for <color=#03a1fc>{summonBanner.cost} {summonBanner.costType}</color>" + $"\nAvailable: <color=#03a1fc>{PlayerData.karma} Karma</color>" + $"\nYou can summon {PlayerData.karma / summonBanner.cost} time(s)";
                    break;
            }
        }
    }
}
