using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MapLoader : MonoBehaviour
{
    public RectTransform dungeonContent;
    public GameObject missionSelectionMenu;
    public Image mapBg;
    public Image mapName;
    public TextMeshProUGUI missionSelectText;
    public GameObject dungeonParent;

    public static Map lastMap;

    public List<Map> maps = new List<Map>();
    public Button rightButton;
    public Button leftButton;

    void Start()
    {
        rightButton.onClick.AddListener(Right);
        leftButton.onClick.AddListener(Left);

        var unlocked = GetUnlockedMaps();
        if (unlocked.Count <= 1)
        {
            rightButton.gameObject.SetActive(false);
            leftButton.gameObject.SetActive(false);
        }
        else
        {
            rightButton.gameObject.SetActive(true);
            leftButton.gameObject.SetActive(true);
        }

        if(lastMap != null && IsMapUnlocked(lastMap))
        {
            LoadMap(lastMap);
        }
        else if (unlocked.Count > 0)
        {
            LoadMap(unlocked[0]);
        }
        else if (maps.Count > 0)
        {
            LoadMap(maps[0]);
        }
    }

    public bool IsMapUnlocked(Map map)
    {
        foreach (DungeonPos dungeonPos in map.dungeonPosList)
        {
            if (dungeonPos.dungeon != null)
            {
                foreach (Mission mission in dungeonPos.dungeon.missions)
                {
                    if (PlayerData.completedMissionDex.Contains(mission.requiresMissionId))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public int MapsUnlockedCount()
    {
        int count = 0;
        foreach (Map map in maps)
        {
            if (IsMapUnlocked(map))
            {
                count++;
            }
        }
        return count;
    }

    public List<Map> GetUnlockedMaps()
    {
        List<Map> unlocked = new List<Map>();
        foreach (Map map in maps)
        {
            if (IsMapUnlocked(map))
                unlocked.Add(map);
        }
        return unlocked;
    }

    public int GetCurrentUnlockedIndex()
    {
        var unlocked = GetUnlockedMaps();
        if (unlocked.Count == 0)
            return -1;

        Sprite current = mapBg.sprite;
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (unlocked[i].mapBg == current)
                return i;
        }
        return 0;
    }

    public void Right()
    {
        var unlocked = GetUnlockedMaps();
        if (unlocked.Count == 0)
            return;

        int currentIndex = GetCurrentUnlockedIndex();
        if (currentIndex == -1)
            currentIndex = 0;

        int nextIndex = (currentIndex + 1) % unlocked.Count;
        LoadMap(unlocked[nextIndex]);
    }

    public void Left()
    {
        var unlocked = GetUnlockedMaps();
        if (unlocked.Count == 0)
            return;

        int currentIndex = GetCurrentUnlockedIndex();
        if (currentIndex == -1)
            currentIndex = 0;

        int nextIndex = (currentIndex - 1 + unlocked.Count) % unlocked.Count;
        LoadMap(unlocked[nextIndex]);
    }

    public void LoadMap(Map map)
    {
        lastMap = map;
        ClearMap();
        mapBg.sprite = map.mapBg;
        mapName.sprite = map.mapNameIcon;

        foreach (DungeonPos dungeonPos in map.dungeonPosList)
        {
            if (dungeonPos.dungeon != null)
            {
                GameObject dungeonButton = Instantiate(Resources.Load("DungeonButton") as GameObject);
                dungeonButton.transform.SetParent(dungeonParent.transform, false);
                dungeonButton.transform.localPosition = new Vector3(dungeonPos.pos.x, dungeonPos.pos.y, 0);

                dungeonButton.GetComponentInChildren<TextMeshProUGUI>().text = dungeonPos.dungeon.levelName;

                dungeonButton.GetComponent<ActivateDeactivateButton>().objectToActivate = missionSelectionMenu;
                dungeonButton.GetComponent<ActivateDeactivateButton>().objectsToActivate = new List<GameObject> { MainUI.header };
                dungeonButton.GetComponent<ActivateDeactivateButton>().objectToDeactivate = dungeonParent;
                dungeonButton.GetComponent<ActivateDeactivateButton>().objectsToDeactivate = new List<GameObject> { mapName.gameObject };

                dungeonButton.GetComponent<DungeonMissionSelectUI>().dungeon = dungeonPos.dungeon;
                dungeonButton.GetComponent<DungeonMissionSelectUI>().container = dungeonContent;
                dungeonButton.GetComponent<DungeonMissionSelectUI>().dungeonName = missionSelectText;
            }
        }
    }

    public void ClearMap()
    {
        foreach (Transform child in dungeonParent.transform)
        {
            Destroy(child.gameObject);
        }
    }
}

