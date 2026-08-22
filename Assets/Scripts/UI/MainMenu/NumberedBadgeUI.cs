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
        List<int> amounts = new List<int>();
        foreach(var s in manager.summonBanners){
            int amount = 0;
            switch (s.costType)
            {
                case CostType.Gems:
                    amount = PlayerData.gems / s.cost;
                    break;
                case CostType.Zel:
                    amount = PlayerData.zel / s.cost;
                    break;
                case CostType.Karma:
                    amount = PlayerData.karma / s.cost;
                    break;
            }

            amounts.Add(amount);
        }

        return amounts.Max();
    }
}
public enum BadgeType
{
    Summon,
    Presents
}
