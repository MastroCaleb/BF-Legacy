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
        playerLevelText.text = $"Level {friendData.botLevel}";
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

        int currentLevel = PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[friendData.party.mainUnitIndex].currentLevel;
        unitLevelText.text = currentLevel == PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[friendData.party.mainUnitIndex].unit.maxLevel ? "Lv. MAX" : $"Lv. {currentLevel}";
        unitLevelText.color = currentLevel == PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[friendData.party.mainUnitIndex].unit.maxLevel ? Color.yellow : Color.white;
    }

}