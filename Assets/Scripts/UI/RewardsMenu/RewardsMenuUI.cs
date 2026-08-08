using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardsMenuUI : MonoBehaviour
{
    //Page 1
    public GameObject page1;
    public AudioClip worldMusic;
    public AudioClip vortexMusic;
    public AudioClip counterUpSound;
    public AudioClip progressSound;
    public TextMeshProUGUI areaName;
    public TextMeshProUGUI missionName;
    public TextMeshProUGUI zelObtained;
    public TextMeshProUGUI karmaObtained;
    public TextMeshProUGUI gemObtained;
    public TextMeshProUGUI xpObtainedText;
    public TextMeshProUGUI xpToNextObtained;
    public BarUI xpBar;
    //Part1
    public GameObject part1;
    public TextMeshProUGUI levelUpRewardText;
    //Part 2
    public GameObject part2;
    public TextMeshProUGUI totalSparks;
    public TextMeshProUGUI criticalHits;
    public TextMeshProUGUI bcDrops;
    public TextMeshProUGUI hcDrops;

    //Page 2
    public GameObject page2;
    public GameObject contentView;
    public Sprite commonUnitIcon;
    public Sprite rareUnitIcon;
    public Sprite superRareUnitIcon;
    public Sprite ultraRareUnitIcon;

    //Page 3
    public GameObject page3;
    public Button button;
    int clicks = 0;

    private List<GameObject> unitIcons;
    private List<Image> unitIconOverlays;

    private GameObject coroutineHostGO;

    [Header("Reveal Animation")]
    public AudioClip revealSound;
    public float overlayFadeDuration = 0.15f;
    public float delayBetweenReveals = 0.07f;

    private const float CountSpeed = 800f;
    private const float XpBarDuration = 2.5f;

    void Start()
    {
        xpBar.currentValue = 0;
        xpBar.maxValue = 1;
        StartCoroutine(UpdateUI());
        HeaderPlayerData.openRewardsScreen = false;

        if (button != null)
            button.onClick.AddListener(OnButtonClick);
    }

    MonoBehaviour GetOrCreateCoroutineHost()
    {
        if (coroutineHostGO == null)
        {
            coroutineHostGO = new GameObject($"_CoroutineHost_{gameObject.name}");
            DontDestroyOnLoad(coroutineHostGO);
        }
        var host = coroutineHostGO.GetComponent<CoroutineHost>();
        if (host == null) host = coroutineHostGO.AddComponent<CoroutineHost>();
        return host;
    }

    // Page 3 is the "extra rewards" summary. It should only show for missions
    // that are being completed for the first time right now, and never during
    // a vortex run.
    private bool ShouldShowPage3()
    {
        return BattleManager.obtainedGemsForMission;
    }

    void OnButtonClick()
    {
        if(clicks == 0)
        {
            StopAllCoroutines();
            SoundManager.Instance.StopLoopingSound();

            areaName.text    = BattleManager.dungeonLevelData.levelName;
            missionName.text = "\"" + BattleManager.missionData.missionName + "\"";

            zelObtained.text   = BattleManager.totalZelReward.ToString();
            karmaObtained.text = BattleManager.totalKarmaReward.ToString();
            gemObtained.text   = BattleManager.totalGemReward.ToString();

            xpObtainedText.text = BattleManager.xpObtained.ToString();

            int xpToNext = GetExpToNext(PlayerData.level);
            xpBar.currentValue = PlayerData.experience;
            xpBar.maxValue = xpToNext > 0 ? xpToNext : 1;
            xpBar.UpdateUI();
            xpToNextObtained.text = xpToNext > 0 ? xpToNext.ToString() : "MAX";

            bool hasLeveledUp = PlayerData.level > BattleManager.oldLevel;
            if (hasLeveledUp)
            {
                int levelsGained = PlayerData.level - BattleManager.oldLevel;
                int gemsAwarded = levelsGained * 2; 

                part1.SetActive(true);
                levelUpRewardText.text = $"You received <color=green>{gemsAwarded} Gem(s)</color> for leveling up!";
                clicks++;
            }
            else
            {
                part2.SetActive(true);
                totalSparks.text  = BattleManager.totalSparkCount.ToString();
                criticalHits.text = BattleManager.totalCriticalHits.ToString();
                bcDrops.text      = BattleManager.totalBcDropCount.ToString();
                hcDrops.text      = BattleManager.totalHcDropCount.ToString();
                if(BattleManager.unitDrops.Count == 0)
                {
                    clicks += ShouldShowPage3() ? 3 : 4;
                }
                else
                {
                    clicks+=2;
                }
            }
        }
        else if(clicks == 1)
        {
            part1.SetActive(false);
            part2.SetActive(true);
            totalSparks.text  = BattleManager.totalSparkCount.ToString();
            criticalHits.text = BattleManager.totalCriticalHits.ToString();
            bcDrops.text      = BattleManager.totalBcDropCount.ToString();
            hcDrops.text      = BattleManager.totalHcDropCount.ToString();

            if(BattleManager.unitDrops.Count == 0)
            {
                clicks += ShouldShowPage3() ? 3 : 2;
            }
            else
            {
                clicks++;
            }
        }
        else if(clicks == 2)
        {
            page1.SetActive(false);
            page2.SetActive(true);
            GenerateUnitIcons();
            GetOrCreateCoroutineHost().StartCoroutine(RevealUnitIcons());

            clicks += ShouldShowPage3() ? 1 : 2;
        }
        else if(clicks == 3)
        {
            page1.SetActive(false);
            page2.SetActive(false);
            page3.SetActive(true);

            clicks++;
        }
        else
        {
            if (BattleManager.isVortex)
            {
                SoundManager.Instance.PlayMusicLoop(vortexMusic);
                MainUI.rewardsScreen.SetActive(false);
                MainUI.extensionLow.SetActive(false);
                MainUI.header.SetActive(false);
                MainUI.vortexMenu.SetActive(true);
                BattleManager.isVortex = false;
            }
            else
            {
                SoundManager.Instance.PlayMusicLoop(worldMusic);
                MainUI.rewardsScreen.SetActive(false);
                MainUI.extensionLow.SetActive(false);
                MainUI.mapName.SetActive(false);
                MainUI.mapDungeons.SetActive(false);
                MainUI.mapMenu.SetActive(true);
                MainUI.missionSelection.SetActive(true);
            }

            if (coroutineHostGO != null)
            {
                Destroy(coroutineHostGO);
                coroutineHostGO = null;
            }
        }
    }

    IEnumerator UpdateUI()
    {
        areaName.text    = BattleManager.dungeonLevelData.levelName;
        missionName.text = "\"" + BattleManager.missionData.missionName + "\"";

        zelObtained.text   = "0";
        karmaObtained.text = "0";

        xpBar.currentValue = 0;
        xpBar.maxValue = 1;

        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(AnimateCounter(zelObtained, 0, BattleManager.totalZelReward));
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(AnimateCounter(karmaObtained, 0, BattleManager.totalKarmaReward));
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(AnimateCounter(gemObtained, 0, BattleManager.totalGemReward));
        yield return new WaitForSeconds(0.5f);

        xpObtainedText.text = BattleManager.xpObtained.ToString();
        yield return StartCoroutine(AnimateXpBar());
        yield return new WaitForSeconds(0.5f);

        bool hasLeveledUp = PlayerData.level > BattleManager.oldLevel;
        if (hasLeveledUp)
        {
            int levelsGained = PlayerData.level - BattleManager.oldLevel;
            int gemsAwarded = levelsGained * 2; 

            part1.SetActive(true);
            levelUpRewardText.text = $"You received <color=green>{gemsAwarded} Gem(s)</color> for leveling up!";
            clicks++;
        }
        else
        {
            part2.SetActive(true);
            totalSparks.text  = BattleManager.totalSparkCount.ToString();
            criticalHits.text = BattleManager.totalCriticalHits.ToString();
            bcDrops.text      = BattleManager.totalBcDropCount.ToString();
            hcDrops.text      = BattleManager.totalHcDropCount.ToString();
            if(BattleManager.unitDrops.Count == 0)
            {
                clicks += ShouldShowPage3() ? 3 : 4;
            }
            else
            {
                clicks+=2;
            }
        }
    }

    IEnumerator AnimateCounter(TextMeshProUGUI label, int startVal, int endVal)
    {
        if (startVal == endVal) { label.text = endVal.ToString(); yield break; }

        SoundManager.Instance.PlayLoopingSound(counterUpSound);
        float current = startVal;
        while (Mathf.RoundToInt(current) != endVal)
        {
            current = Mathf.MoveTowards(current, endVal, CountSpeed * Time.deltaTime);
            label.text = Mathf.RoundToInt(current).ToString();
            yield return null;
        }
        label.text = endVal.ToString();
        SoundManager.Instance.StopLoopingSound();
    }

    IEnumerator AnimateXpBar()
    {
        int currentLevel = BattleManager.oldLevel;
        int currentXp    = BattleManager.oldExperience;
        int targetLevel  = PlayerData.level;
        int targetXp     = PlayerData.experience;

        var segments = new System.Collections.Generic.List<(int lvl, int xpFrom, int xpTo, int xpMax)>();

        int simLevel = currentLevel;
        int simXp    = currentXp;

        SoundManager.Instance.PlaySound(progressSound);

        while (simLevel < targetLevel)
        {
            LevelData ld = PlayerData.GetLevelData(simLevel);
            int xpNeeded = ld != null ? ld.expToNextLevel : 1;
            segments.Add((simLevel, simXp, xpNeeded, xpNeeded));
            simXp = 0;
            simLevel++;
        }

        segments.Add((targetLevel, simLevel == currentLevel ? currentXp : 0, targetXp,
                      GetExpToNext(targetLevel)));

        float totalProgress = 0f;
        foreach (var seg in segments)
            totalProgress += seg.xpMax > 0 ? (float)(seg.xpTo - seg.xpFrom) / seg.xpMax : 0f;

        foreach (var (lvl, xpFrom, xpTo, xpMax) in segments)
        {
            if (xpMax <= 0) continue;

            float segProgress = (float)(xpTo - xpFrom) / xpMax;
            float segDuration = totalProgress > 0f
                ? XpBarDuration * (segProgress / totalProgress)
                : XpBarDuration;

            int xpToNext = GetExpToNext(lvl);
            xpToNextObtained.text = xpToNext > 0 ? xpToNext.ToString() : "MAX";

            xpBar.maxValue = xpMax;
            xpBar.currentValue = xpFrom;
            xpBar.UpdateUI();

            float elapsed = 0f;
            while (elapsed < segDuration)
            {
                elapsed += Time.deltaTime * 2;
                float t = Mathf.Clamp01(elapsed / segDuration);
                xpBar.currentValue = Mathf.RoundToInt(Mathf.Lerp(xpFrom, xpTo, t));
                xpBar.UpdateUI();
                yield return null;
            }
            xpBar.currentValue = xpTo;
            xpBar.UpdateUI();

            if (lvl < targetLevel)
            {
                xpBar.currentValue = xpMax;
                xpBar.UpdateUI();
                yield return new WaitForSeconds(0.3f);
                xpBar.currentValue = 0;
                xpBar.maxValue = GetExpToNext(lvl + 1);
                xpBar.UpdateUI();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private int GetExpToNext(int lvl)
    {
        LevelData ld = PlayerData.GetLevelData(lvl);
        return ld != null ? ld.expToNextLevel : 0;
    }

    private void GenerateUnitIcons()
    {
        unitIcons = new List<GameObject>();
        unitIconOverlays = new List<Image>();

        foreach (var unit in BattleManager.unitDrops)
        {
            GameObject unitIcon = new GameObject("UnitIcon");
            unitIcon.transform.SetParent(contentView.transform, false);
            unitIcon.AddComponent<RectTransform>();
            Image image = unitIcon.AddComponent<Image>();
            switch (UnitRegistry.GetUnitById(unit.unitId).rarity)
            {
                case UnitRarity.ONE or UnitRarity.TWO:
                image.sprite = commonUnitIcon;
                break;
                case UnitRarity.THREE:
                image.sprite = rareUnitIcon;
                break;
                case UnitRarity.FOUR:
                image.sprite = superRareUnitIcon;
                break;
                case UnitRarity.FIVE or UnitRarity.SIX or UnitRarity.SEVEN or UnitRarity.OMNI:
                image.sprite = ultraRareUnitIcon;
                break;
            }
            image.preserveAspect = true;
            unitIcons.Add(unitIcon);

            GameObject overlayObj = new GameObject("WhiteOverlay");
            overlayObj.transform.SetParent(unitIcon.transform, false);
            RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.color = new Color(1f, 1f, 1f, 0f);
            unitIconOverlays.Add(overlayImage);
        }
    }

    private IEnumerator RevealUnitIcons()
    {
        for (int i = 0; i < unitIcons.Count; i++)
        {
            yield return GetOrCreateCoroutineHost().StartCoroutine(RevealSingleUnitIcon(i));
            yield return new WaitForSeconds(delayBetweenReveals);
        }
    }

    private IEnumerator RevealSingleUnitIcon(int index)
    {
        Image overlay = unitIconOverlays[index];

        yield return GetOrCreateCoroutineHost().StartCoroutine(FadeImageAlpha(overlay, 0f, 1f, overlayFadeDuration));

        unitIcons[index].GetComponent<Image>().sprite =
            UnitRegistry.GetUnitById(BattleManager.unitDrops[index].unitId).unitSlotIcon;
        SoundManager.Instance.PlaySound(revealSound);

        if (!PlayerData.unitDex.Contains(BattleManager.unitDrops[index].unitId) || BattleManager.newUnits.Contains(PlayerUnitInventoryDatabase._nextKey - (BattleManager.unitDrops.Count - index)))
        {
            MainUI.rewardsScreen.SetActive(false);
            MainUI.header.SetActive(false);
            MainUI.extensionLow.SetActive(false);
            MainUI.newSummonUnitUI.gameObject.SetActive(true);
            MainUI.newSummonUnitUI.Play(PlayerUnitInventoryDatabase.GetUnitByKey(PlayerUnitInventoryDatabase._nextKey-(BattleManager.unitDrops.Count - index)), new List<GameObject>() { MainUI.rewardsScreen}, new List<GameObject>() { MainUI.header, MainUI.extensionLow, MainUI.extensionUp}, null);

            yield return new WaitUntil(() => MainUI.rewardsScreen.activeInHierarchy);
        }

        yield return GetOrCreateCoroutineHost().StartCoroutine(FadeImageAlpha(overlay, 1f, 0f, overlayFadeDuration));
    }

    private IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = image.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(from, to, t);
            image.color = c;
            yield return null;
        }

        c.a = to;
        image.color = c;
    }
}