using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BeginQuestMenu : MonoBehaviour
{
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI leaderSkillNameText;
    public ScrollingTMPText leaderSkillDescriptionText;
    public TextMeshProUGUI friendSkillNameText;
    public ScrollingTMPText friendSkillDescriptionText;
    public PartyIconView partyIconView;
    public GameObject noFriendSkillText;

    void OnEnable()
    {
        // Update the UI elements with the relevant data when the menu is enabled
        UpdateSkillInfo();
        UpdateQuestInfo();
        UpdatePartyIcons();
    }

    private void UpdateSkillInfo()
    {
        // Assuming you have a method to get the leader and friend skills
        var leaderSkill = PartyDatabase.GetLeaderInCurrentParty().unit.leaderAbility;

        if (leaderSkill != null)
        {
            leaderSkillNameText.text = leaderSkill.abilityName;
            leaderSkillDescriptionText.SetText(leaderSkill.abilityDesc);
        }

        bool isFriend = false;

        if (isFriend)
        {
            noFriendSkillText.SetActive(false);
            friendSkillNameText.color = Color.white;
            friendSkillDescriptionText.color = Color.white;
        }
        else
        {
            noFriendSkillText.SetActive(true);
            friendSkillNameText.color = Color.gray;
            friendSkillDescriptionText.color = Color.gray;
        }

        var friendSkill = UnitRegistry.GetUnitById(BattleManager.selectedAllyBot.units[0].unitId).leaderAbility;

        if (friendSkill != null)
        {
            friendSkillNameText.text = friendSkill.abilityName;
            friendSkillDescriptionText.SetText(friendSkill.abilityDesc);
        }
    }

    private void UpdateQuestInfo()
    {
        // Assuming you have a QuestManager or similar to get the current quest info
        var currentQuest = BattleManager.missionData;
        if (currentQuest != null)
        {
            questNameText.text = currentQuest.missionName;
        }
    }

    private void UpdatePartyIcons()
    {
        if (partyIconView != null)
        {
            // Update the party icons based on the current quest data
            for (int i = 0; i < 5; i++)
            {
                if (i < PartyDatabase.GetParty(PartyDatabase.currentPartyKey).slots.Count)
                {
                    if (PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[i] != null)
                    {
                        partyIconView.partyIcons[i].gameObject.SetActive(true);
                        partyIconView.partyIcons[i].sprite = PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[i].unit.unitSlotIcon;
                        int currentLevel = PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[i].currentLevel;
                        partyIconView.partyLevels[i].text = currentLevel == PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[i].unit.maxLevel ? "Lv. MAX" : $"Lv. {currentLevel}";
                        partyIconView.partyLevels[i].color = currentLevel == PartyDatabase.GetPartyUnits(PartyDatabase.currentPartyKey)[i].unit.maxLevel ? Color.yellow : Color.white;
                    }
                    else
                    {
                        partyIconView.partyIcons[i].gameObject.SetActive(false);
                        partyIconView.partyIcons[i].sprite = null;
                        partyIconView.partyLevels[i].text = "";
                    }
                }
            }

            if(BattleManager.selectedAllyBot != null)
            {
                partyIconView.partyIcons[5].gameObject.SetActive(true);
                partyIconView.partyIcons[5].sprite = UnitRegistry.GetUnitById(BattleManager.selectedAllyBot.units[0].unitId).unitSlotIcon;
                int friendCurrentLevel = BattleManager.selectedAllyBot.units[0].unitLevel;
                partyIconView.partyLevels[5].text = friendCurrentLevel == UnitRegistry.GetUnitById(BattleManager.selectedAllyBot.units[0].unitId).maxLevel ? "Lv. MAX" : $"Lv. {friendCurrentLevel}";
                partyIconView.partyLevels[5].color = friendCurrentLevel == UnitRegistry.GetUnitById(BattleManager.selectedAllyBot.units[0].unitId).maxLevel ? Color.yellow : Color.white;
            }
            else
            {
                partyIconView.partyIcons[5].gameObject.SetActive(false);
                partyIconView.partyIcons[5].sprite = null;
                partyIconView.partyLevels[5].text = "";
            }
        }
    }
}
