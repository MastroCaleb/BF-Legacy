using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendSlotUI : MonoBehaviour
{
    public Image unitSlotIcon;
    public TextMeshProUGUI unitLevelText;

    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerLevelText;
    public TextMeshProUGUI playerDescriptionText;
    public BotData friendData;

    void Start()
    {
        if (friendData != null)
        {
            UpdateUI(friendData);
        }
    }

    public void UpdateUI(BotData data)
    {
        friendData = data;
        if (friendData == null) return;

        playerNameText.text = friendData.botName;
        playerLevelText.text = $"{friendData.botLevel}";
        playerDescriptionText.text = "Hello! I'm a bot!";

        Unit unit = UnitRegistry.GetUnitById(friendData.party.units[friendData.party.mainUnitIndex].unitId);

        Sprite unitIcon = unit?.unitSlotIcon;
        if (unitIcon != null)
        {
            unitSlotIcon.sprite = unitIcon;
            unitSlotIcon.enabled = true;
        }
        else
        {
            unitSlotIcon.enabled = false; // Hide the icon if not found
        }

        int currentLevel = data.party.units[data.party.mainUnitIndex].unitLevel;

        unitLevelText.text = currentLevel == unit.maxLevel ? "Lv. MAX" : $"Lv. {currentLevel}";
        unitLevelText.color = currentLevel == unit.maxLevel ? Color.yellow : Color.white;
    }

}