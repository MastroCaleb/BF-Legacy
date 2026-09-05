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
    public GameObject unitContentView;
    public GameObject itemContentView;

    //Part1
    public GameObject part1;
    public TextMeshProUGUI levelUpRewardText;
    //Part 2
    public GameObject part2;
    public TextMeshProUGUI totalSparks;
    public TextMeshProUGUI criticalHits;
    public TextMeshProUGUI bcDrops;
    public TextMeshProUGUI hcDrops;

    //Page 2 - item rewards
    public GameObject page2;
    public Sprite itemBackgroundSprite;
    public Sprite itemBgUnknownSprite;
    public Sprite unknownSlotSprite;
    public Sprite materialSlotSprite;
    public Sprite consumableSlotSprite;
    public Sprite sphereSlotSprite;
    public Sprite lsSphereSlotSprite;
    public Sprite evoMatSlotSprite;
    public Sprite raidMatSlotSprite;
    public Sprite boosterSlotSprite;

    //Page 3 - unit rewards
    public GameObject page3;
    public Sprite commonUnitIcon;
    public Sprite rareUnitIcon;
    public Sprite superRareUnitIcon;
    public Sprite ultraRareUnitIcon;

    //Page 4 - extra rewards
    public GameObject page4;
    public Button button;
    int clicks = 0;

    private List<GameObject> unitIcons;
    private List<Image> unitIconOverlays;
    private List<GameObject> itemIcons;
    private List<Image> itemIconOverlays;

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

    // Page 4 is the "extra rewards" summary. It should only show for missions
    // that are being completed for the first time right now.
    private bool ShouldShowPage4()
    {
        return BattleManager.obtainedGemsForMission;
    }

    private void ShowNextPage()
    {
        if (clicks == 2)
        {
            if (BattleManager.itemDrops.Count > 0)
            {
                page1.SetActive(false);
                page2.SetActive(true);
                GenerateItemIcons();
                GetOrCreateCoroutineHost().StartCoroutine(RevealItemIcons());
                clicks = 3;
            }
            else
            {
                ShowNextPageWithoutItems();
            }
        }
        else if (clicks == 3)
        {
            if (BattleManager.unitDrops.Count > 0)
            {
                page2.SetActive(false);
                page3.SetActive(true);
                GenerateUnitIcons();
                GetOrCreateCoroutineHost().StartCoroutine(RevealUnitIcons());
                clicks = 4;
            }
            else
            {
                ShowPage4OrFinish();
            }
        }
        else if (clicks == 4)
        {
            ShowPage4OrFinish();
        }
        else
        {
            FinishRewards();
        }
    }

    private void ShowNextPageWithoutItems()
    {
        if (BattleManager.unitDrops.Count > 0)
        {
            page1.SetActive(false);
            page3.SetActive(true);
            GenerateUnitIcons();
            GetOrCreateCoroutineHost().StartCoroutine(RevealUnitIcons());
            clicks = 4;
        }
        else
        {
            ShowPage4OrFinish();
        }
    }

    private void ShowPage4OrFinish()
    {
        if (ShouldShowPage4())
        {
            page1.SetActive(false);
            page2.SetActive(false);
            page3.SetActive(false);
            page4.SetActive(true);
            clicks = 5;
        }
        else
        {
            FinishRewards();
        }
    }

    private void FinishRewards()
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
            MainUI.missionSelectionText.text = BattleManager.dungeonLevelData.levelName;
        }

        if (coroutineHostGO != null)
        {
            Destroy(coroutineHostGO);
            coroutineHostGO = null;
        }
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
                clicks = 1;
            }
            else
            {
                part2.SetActive(true);
                totalSparks.text  = BattleManager.totalSparkCount.ToString();
                criticalHits.text = BattleManager.totalCriticalHits.ToString();
                bcDrops.text      = BattleManager.totalBcDropCount.ToString();
                hcDrops.text      = BattleManager.totalHcDropCount.ToString();
                clicks = 2;
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

            clicks = 2;
        }
        else if(clicks >= 2)
        {
            ShowNextPage();
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
            clicks = 1;
        }
        else
        {
            part2.SetActive(true);
            totalSparks.text  = BattleManager.totalSparkCount.ToString();
            criticalHits.text = BattleManager.totalCriticalHits.ToString();
            bcDrops.text      = BattleManager.totalBcDropCount.ToString();
            hcDrops.text      = BattleManager.totalHcDropCount.ToString();
            clicks = 2;
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
            unitIcon.transform.SetParent(unitContentView.transform, false);
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

     // Composited icon: background -> itemData.thumbnail -> slot frame -> reveal overlay.
    private void GenerateItemIcons()
    {
        itemIcons = new List<GameObject>();
        itemIconOverlays = new List<Image>();

        foreach (var item in BattleManager.itemDrops)
        {
            ItemData itemData = ItemDatabase.GetItemByName(item.itemName);

            GameObject itemIcon = new GameObject("ItemIcon");
            itemIcon.transform.SetParent(itemContentView.transform, false);
            itemIcon.AddComponent<RectTransform>();

            Image background = itemIcon.AddComponent<Image>();
            background.sprite = itemBgUnknownSprite;
            background.preserveAspect = true;
            itemIcons.Add(itemIcon);

            Image thumbnailImage = CreateStretchedChild(itemIcon.transform, "Thumbnail");
            thumbnailImage.sprite = null;
            thumbnailImage.enabled = false;
            thumbnailImage.preserveAspect = true;

            Image slotImage = CreateStretchedChild(itemIcon.transform, "SlotFrame");
            slotImage.sprite = unknownSlotSprite;
            slotImage.preserveAspect = true;

            Image overlayImage = CreateStretchedChild(itemIcon.transform, "WhiteOverlay");
            overlayImage.color = new Color(1f, 1f, 1f, 0f);
            itemIconOverlays.Add(overlayImage);
        }
    }

    private Image CreateStretchedChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj.AddComponent<Image>();
    }

    // Mirrors ItemSlot.GetSpriteForItemType — kept in sync manually since
    // this UI composites icons itself rather than instantiating ItemSlot prefabs.
    private Sprite GetSlotSpriteForItem(ItemData itemData)
    {
        return itemData.itemType switch
        {
            ItemType.Material => materialSlotSprite,
            ItemType.Consumable when itemData.raid => raidMatSlotSprite,
            ItemType.Sphere => sphereSlotSprite,
            ItemType.LsSphere => lsSphereSlotSprite,
            ItemType.EvoMat => evoMatSlotSprite,
            ItemType.Consumable => consumableSlotSprite,
            ItemType.Unknown => boosterSlotSprite,
            _ => unknownSlotSprite
        };
    }

    private IEnumerator RevealItemIcons()
    {
        for (int i = 0; i < itemIcons.Count; i++)
        {
            yield return GetOrCreateCoroutineHost().StartCoroutine(RevealSingleItemIcon(i));
            yield return new WaitForSeconds(delayBetweenReveals);
        }
    }

    private IEnumerator RevealSingleItemIcon(int index)
    {
        Image overlay = itemIconOverlays[index];

        yield return GetOrCreateCoroutineHost().StartCoroutine(FadeImageAlpha(overlay, 0f, 1f, overlayFadeDuration));

        ItemData itemData = ItemDatabase.GetItemByName(BattleManager.itemDrops[index].itemName);
        if (itemData != null)
        {
            Transform itemIcon = itemIcons[index].transform;
            itemIcon.GetComponent<Image>().sprite = itemBackgroundSprite;

            Image thumbnailImage = itemIcon.Find("Thumbnail").GetComponent<Image>();
            thumbnailImage.sprite = itemData.thumbnailSprite;
            thumbnailImage.enabled = true;

            itemIcon.Find("SlotFrame").GetComponent<Image>().sprite = GetSlotSpriteForItem(itemData);
        }

        SoundManager.Instance.PlaySound(revealSound);
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