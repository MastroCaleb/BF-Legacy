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

    public void UpdateBadge()
    {
        int number = 0;
        if(type == BadgeType.Summon)
        {
            number = PlayerData.gems / 1;
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
}
public enum BadgeType
{
    Summon,
    Presents
}
