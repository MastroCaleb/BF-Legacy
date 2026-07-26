using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PresentBoxMenu : MonoBehaviour
{
    public static PresentBoxMenu Instance { get; private set; }

    public List<PresentBoxData> presentBoxList = new List<PresentBoxData>();

    public GameObject presentBoxSlotPrefab;
    public RectTransform contentParent;
    public Button receiveAllButton;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (receiveAllButton != null)
        {
            receiveAllButton.onClick.AddListener(ReceiveAll);
        }

        PopulatePresentBoxMenu();
    }

    public void PopulatePresentBoxMenu()
    {
        foreach (RectTransform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var presentBox in presentBoxList)
        {
            if(PlayerData.presentCollectedDex.Contains(presentBox.presentId))
            {
                GameObject slot = Instantiate(presentBoxSlotPrefab, contentParent);
                PresentBoxSlot slotScript = slot.GetComponent<PresentBoxSlot>();
                if (slotScript != null)
                {
                    slotScript.SetPresentBoxData(presentBox);
                }
            }
        }
    }

    public void ReceiveAll()
    {
        // Only grab what's actually displayed and not already received.
        List<PresentBoxData> toReceive = presentBoxList.FindAll(p =>
            PlayerData.presentCollectedDex.Contains(p.presentId) &&
            !PlayerData.presentReceivedDex.Contains(p.presentId));

        if (toReceive.Count == 0) return;

        foreach (var presentBox in toReceive)
        {
            ReceivePresent(presentBox);
        }

        PlayerData.SaveDataToJson();
        PopulatePresentBoxMenu();

        if (LoginCampaignManager.Instance != null)
        {
            LoginCampaignManager.Instance.RefreshUnclaimedIndicatorAndCalendar();
        }
    }

    public void ReceivePresent(PresentBoxData presentBoxData)
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

        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();
    }
}
[System.Serializable]
public class PresentBoxData
{
    public string presentId;
    public RewardType rewardType;
    public int rewardAmount;
    public string rewardData;
    [TextArea]
    public string customDescription; // if set, overrides the auto-generated description
}
public enum RewardType
{
    Zels,
    Karma,
    Gems,
    Item,
    Unit
}