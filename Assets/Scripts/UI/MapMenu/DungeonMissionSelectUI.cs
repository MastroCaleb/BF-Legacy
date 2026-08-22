using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DungeonMissionSelectUI : MonoBehaviour
{
    public DungeonLevel dungeon;
    public RectTransform container;
    public TextMeshProUGUI dungeonName;
    public Button button;

    public bool dontDeactivateButton = false;
    List<GameObject> currentSlots = new List<GameObject>();

    void Start()
    {
        bool atLeastOneMissionUnlocked = false;
        bool allCompleted = false;
        for (int i = 0; i < dungeon.missions.Count; i++)
        {
            if (PlayerData.completedMissionDex.Contains(dungeon.missions[i].requiresMissionId))
            {
                atLeastOneMissionUnlocked = true;
            }

            if (PlayerData.completedMissionDex.Contains(dungeon.missions[i].missionId))
            {
                allCompleted = true;
            }
            else
            {
                allCompleted = false;
            }
        }

        if (gameObject.activeSelf)
        {
            if (allCompleted)
            {
                Transform clearImage = gameObject.transform.Find("ClearImage");
                if (clearImage != null)
                {
                    clearImage.gameObject.SetActive(true);
                }
            }
            else
            {
                Transform newSam = gameObject.transform.Find("NewSam");
                if (newSam != null)
                {
                    newSam.gameObject.SetActive(true);
                }
            }
        }

        if (!atLeastOneMissionUnlocked && !dontDeactivateButton)
        {
            gameObject.SetActive(false);
        }

        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(UpdateMissionSelect);
    }

    

    public void UpdateMissionSelect()
    {
        ClearSlots();

        for (int i = dungeon.missions.Count - 1; i >= 0; i--)
        {
            CreateMissionSlot(dungeon.missions[i]);
        }

        if(dungeonName != null)
        {
            dungeonName.text = dungeon.levelName;    
        }
    }

    void CreateMissionSlot(Mission mission)
    {
        if(PlayerData.completedMissionDex.Contains(mission.requiresMissionId)){
            GameObject slot = Instantiate(PrefabCache.Get("MissionSlot"));
            slot.GetComponent<SetMissionButton>().mission = mission;
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(container);

            BattleManager.dungeonLevelData = dungeon;

            slotRect.localScale = Vector3.one;

            slotRect.Find("MissionName").GetComponent<TextMeshProUGUI>().text = mission.missionName;
            slotRect.Find("EnergyNumText").GetComponent<TextMeshProUGUI>().text = mission.energyCost + "";
            slotRect.Find("BattlesNumText").GetComponent<TextMeshProUGUI>().text = mission.rounds.Count + "";
            slotRect.Find("MissionDesc").GetComponent<TextMeshProUGUI>().text = mission.description + "";

            if(!PlayerData.completedMissionDex.Contains(mission.missionId))
            {
                slotRect.Find("NewSam").gameObject.SetActive(true);
            }
            else
            {
                slotRect.Find("ClearImage").gameObject.SetActive(true);
            }

            currentSlots.Add(slot);
        }
    }

    void ClearSlots()
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        currentSlots.Clear();
    }
}
