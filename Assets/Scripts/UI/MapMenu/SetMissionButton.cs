using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SetMissionButton : MonoBehaviour
{
    public DungeonLevel dungeon;
    public Mission mission;
    Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    public void OnButtonClicked()
    {   
        if(dungeon != null)
        {
            BattleManager.dungeonLevelData = dungeon;
            BattleManager.isVortex = mission.landName == "Vortex" ? true : false;
        }
        BattleManager.missionData = mission;
    }
}
