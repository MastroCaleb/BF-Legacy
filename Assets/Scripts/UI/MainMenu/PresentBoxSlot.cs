using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PresentBoxSlot : MonoBehaviour
{
    public PresentBoxData presentBoxData;
    public TextMeshProUGUI presentNameText;
    public TextMeshProUGUI descriptionText;
    public Image rewardThumbnail;
    public Button receiveButton;

    public Sprite zelThumb;
    public Sprite karmaThumb;
    public Sprite gemThumb;

    public void Start()
    {
        receiveButton.onClick.AddListener(ReceivePresent);
    }

    public void SetPresentBoxData(PresentBoxData data)
    {
        presentBoxData = data;
        presentNameText.text = GetPresentName();
        descriptionText.text = GetDescription();
        SetRewardThumbnail();
    }

    public string GetPresentName()
    {
        string amount = presentBoxData.rewardAmount > 1 ? $"{presentBoxData.rewardAmount}x " : "";
        string append = presentBoxData.rewardAmount > 1 ? "s" : "";
        // For login campaigns, the present name can be derived from the presentId
        if(presentBoxData.rewardType == RewardType.Zels)
        {
            return $"{amount}Zel";
        }
        else if(presentBoxData.rewardType == RewardType.Karma)
        {
            return $"{amount}Karma Orb{append}";
        }
        else if(presentBoxData.rewardType == RewardType.Gems)
        {
            return $"{amount}Gem{append}";
        }
        else if(presentBoxData.rewardType == RewardType.Unit)
        {
            string unitName = UnitRegistry.GetUnitById(presentBoxData.rewardData)?.unitName ?? "Unknown Unit";
            return $"{amount}{unitName}{append}";
        }
        else
        {
            return $"Unknown Present{append}";
        }
    }

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(presentBoxData.customDescription))
        {
            return presentBoxData.customDescription;
        }

        // For login campaigns, the present name can be derived from the presentId
        if (presentBoxData.presentId.Contains("loginDay"))
        {
            string[] parts = presentBoxData.presentId.Split('_');
            string dayNumber = parts[3].Replace("loginDay", "");
            string monthName = "Unknown Month";
            if (int.TryParse(parts[1], out int monthIndex) && monthIndex >= 1 && monthIndex <= 12)
            {
                monthName = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthIndex);
            }
            return $"Daily login reward of Day {dayNumber} of {monthName}";
        }
        else
        {
            string append = presentBoxData.rewardAmount > 1 ? "s" : "";
            if(presentBoxData.rewardType == RewardType.Zels)
            {
                return $"You will receive {presentBoxData.rewardAmount} Zel.";
            }
            else if(presentBoxData.rewardType == RewardType.Karma)
            {
                return $"You will receive {presentBoxData.rewardAmount} Karma Orb{append}.";
            }
            else if(presentBoxData.rewardType == RewardType.Gems)
            {
                return $"You will receive {presentBoxData.rewardAmount} Gem{append}.";
            }
            else if(presentBoxData.rewardType == RewardType.Item)
            {
                return $"You will receive an item: {presentBoxData.rewardData}.";
            }
            else if(presentBoxData.rewardType == RewardType.Unit)
            {
                return $"You will receive a unit: {UnitRegistry.GetUnitById(presentBoxData.rewardData).unitName}.";
            }
            else
            {
                return "You will receive a reward.";
            }
        }
    }

    public void SetRewardThumbnail()
    {
        if(presentBoxData.rewardType == RewardType.Zels)
        {
            rewardThumbnail.sprite = zelThumb;
        }
        else if(presentBoxData.rewardType == RewardType.Karma)
        {
            rewardThumbnail.sprite = karmaThumb;
        }
        else if(presentBoxData.rewardType == RewardType.Gems)
        {
            rewardThumbnail.sprite = gemThumb;
        }
        else if(presentBoxData.rewardType == RewardType.Item)
        {
            // No ItemRegistry to get item details yet
            rewardThumbnail.sprite = null;
        }
        else if(presentBoxData.rewardType == RewardType.Unit)
        {
            Unit unit = UnitRegistry.GetUnitById(presentBoxData.rewardData);
            if(unit != null)
            {
                rewardThumbnail.sprite = unit.unitSlotIcon;
            }
            else
            {
                rewardThumbnail.sprite = null;
            }
        }
        
    }

    public void ReceivePresent()
    {
        if (presentBoxData.rewardType == RewardType.Zels)
        {
            PlayerData.zel += presentBoxData.rewardAmount;
        }
        else if (presentBoxData.rewardType == RewardType.Karma)
        {
            PlayerData.karma += presentBoxData.rewardAmount;
        }
        else if (presentBoxData.rewardType == RewardType.Gems)
        {
            PlayerData.gems += presentBoxData.rewardAmount;
        }
        else if (presentBoxData.rewardType == RewardType.Item)
        {
            // No ItemRegistry to get item details yet
        }
        else if (presentBoxData.rewardType == RewardType.Unit)
        {
            for (int i = 0; i < presentBoxData.rewardAmount; i++)
            {
                MainUI.inventoryRenderer.AddUnit(presentBoxData.rewardData);
            }
        }

        PlayerData.presentReceivedDex.Add(presentBoxData.presentId);
        PlayerData.presentCollectedDex.Remove(presentBoxData.presentId);
        PlayerData.SaveDataToJson();

        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();

        PresentBoxMenu.Instance.PopulatePresentBoxMenu();
    }
}
