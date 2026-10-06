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

        slotRect.Find("PlayerNameText").GetComponent<TextMeshProUGUI>().text = botData.botName;
        slotRect.Find("LvNumText").GetComponent<TextMeshProUGUI>().text = botData.botLevel + "";
        slotRect.Find("Thumbnail").GetComponent<Image>().sprite = UnitRegistry.GetUnitById(botData.units[0].unitId).unitSlotIcon;

        currentSlots.Add(slot);
    }
}
