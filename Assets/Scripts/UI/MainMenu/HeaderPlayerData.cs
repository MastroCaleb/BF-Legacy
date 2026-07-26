using TMPro;
using UnityEngine;

public class HeaderPlayerData : MonoBehaviour
{
    public AudioClip homeMusic;
    public AudioClip rewardsMusic;
    public TextMeshProUGUI playerNameText;
    public ImageToFont playerLevel;
    public BarUI expBar;
    public ImageToFont gems;
    public ImageToFont zel;
    public ImageToFont karma;

    public NumberedBadgeUI summonBadge;
    public NumberedBadgeUI presentsBadge;
    public static bool openRewardsScreen = false;

    void Awake()
    {
        PlayerData.LoadDataFromJson();
    }

    public void Start()
    {
        SoundManager.Instance.PlayMusicLoop(homeMusic);
        if (openRewardsScreen)
        {
            SoundManager.Instance.PlayMusicLoop(rewardsMusic);
            MainUI.rewardsScreen.GetComponent<RewardsMenuUI>().button.GetComponent<DungeonMissionSelectUI>().dungeon = BattleManager.dungeonLevelData;
            MainUI.rewardsScreen.GetComponent<RewardsMenuUI>().button.GetComponent<DungeonMissionSelectUI>().dontDeactivateButton = true;
            MainUI.rewardsScreen.SetActive(true);
            MainUI.footer.SetActive(false);
            MainUI.homeMenu.SetActive(false);
        }

        UpdateHeader();
    }

    // Update is called once per frame
    public void UpdateHeader()
    {
        playerNameText.text = PlayerData.playerName;
        playerLevel.SetText(PlayerData.level+"");
        gems.SetText(PlayerData.gems+"");
        zel.SetText(PlayerData.zel+"");
        karma.SetText(PlayerData.karma+"");
        LevelData ld = PlayerData.GetLevelData(PlayerData.level);
        expBar.maxValue = ld != null ? ld.expToNextLevel : 1;
        expBar.currentValue = PlayerData.experience;
        expBar.UpdateUI();
        summonBadge.UpdateBadge();
        presentsBadge.UpdateBadge();
    }
    
}
