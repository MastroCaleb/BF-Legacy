using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NumberedBadgeUI : MonoBehaviour
{
    public BadgeType type;
    public TextMeshProUGUI numbers;
    public Image badge;
    public Sprite smallBadge;
    public Sprite bigBadge;
    
    public SummonBannerManager manager;

    public void UpdateBadge()
    {
        int number = 0;
        if(type == BadgeType.Summon)
        {
            number = GetHighestAmountOfSummons();
        }
        else if(type == BadgeType.Presents)
        {
            number = PlayerData.presentCollectedDex.Count;
        }

        if(number == 0)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
        }

        string text = number > 999 ? "+999" : number.ToString();
        numbers.SetText(text);
        badge.sprite = number > 99 ? bigBadge : smallBadge;
        badge.SetNativeSize();
    }

    public int GetHighestAmountOfSummons(){
        List<int> gemAmounts = new List<int>();
        List<int> zelAmounts = new List<int>();
        List<int> karmaAmounts = new List<int>();
        foreach(var s in manager.summonBanners){
            switch (s.costType)
            {
                case CostType.Gems:
                    gemAmounts.Add(PlayerData.gems / s.cost);
                    break;
                case CostType.Zel:
                    zelAmounts.Add(PlayerData.zel / s.cost);
                    break;
                case CostType.Karma:
                    karmaAmounts.Add(PlayerData.karma / s.cost);
                    break;
            }
        }

        int maxGemAmount = gemAmounts.Count > 0 ? gemAmounts.Max() : 0;
        int maxZelAmount = zelAmounts.Count > 0 ? zelAmounts.Max() : 0;
        int maxKarmaAmount = karmaAmounts.Count > 0 ? karmaAmounts.Max() : 0;

        return maxGemAmount + maxZelAmount + maxKarmaAmount;
    }
}
public enum BadgeType
{
    Summon,
    Presents
}
