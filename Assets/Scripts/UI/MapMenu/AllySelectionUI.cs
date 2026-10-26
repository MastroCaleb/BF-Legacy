using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllySelectionMenu : MonoBehaviour
{
    public List<GameObject> currentSlots = new List<GameObject>();
    public RectTransform container;

    public void OnEnable()
    {
        foreach (GameObject slot in currentSlots)
        {
            Destroy(slot);
        }
        currentSlots.Clear();

        foreach (var friend in PlayerData.friends)
        {
            CreateAllySlot(friend);
        }

        for(int i = 0; i < 15; i++)
        {
            CreateAllySlot(BFLifeSystem.GenerateBot(1, 200, 1));
        }
    }

    void CreateAllySlot(BotData botData)
    {
        GameObject slot = Instantiate(PrefabCache.Get("AllySlot"));

        slot.GetComponent<SetAllyButton>().botData = botData;
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        slotRect.SetParent(container, false);
        slotRect.localScale = Vector3.one;

        Unit unit = UnitRegistry.GetUnitById(botData.party.units[botData.party.mainUnitIndex].unitId);

        slotRect.Find("PlayerNameText").GetComponent<TextMeshProUGUI>().text = botData.botName;
        slotRect.Find("LvNumText").GetComponent<TextMeshProUGUI>().text = botData.botLevel + "";
        slotRect.Find("Thumbnail").GetComponent<Image>().sprite = unit.unitSlotIcon;

        int currentLevel = botData.party.units[botData.party.mainUnitIndex].unitLevel;

        slotRect.Find("LvUnitText").GetComponent<TextMeshProUGUI>().text = currentLevel == unit.maxLevel ? "Lv. MAX" : $"Lv. {currentLevel}";
        slotRect.Find("LvUnitText").GetComponent<TextMeshProUGUI>().color = currentLevel == unit.maxLevel ? Color.yellow : Color.white;

        if (PlayerData.IsFriend(botData.botId))
        {
            slotRect.Find("FriendIcon").gameObject.SetActive(true);
        }

        slot.GetComponent<ActivateDeactivateButton>().objectToActivate = MainUI.beginQuestMenu;
        slot.GetComponent<ActivateDeactivateButton>().objectToDeactivate = MainUI.allySelection;

        currentSlots.Add(slot);
    }
}
