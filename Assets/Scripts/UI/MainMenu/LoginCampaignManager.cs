using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class SpecialDayReward
{
    [Tooltip("1 = January ... 12 = December")]
    public int month;
    [Tooltip("Day of month, 1-31")]
    public int day;
    public PresentBoxData reward;

    [Header("Extra presents for this day, collected independently of the main reward")]
    public List<PresentBoxData> secondaryPresents = new();
}

public class LoginCampaignManager : MonoBehaviour
{
    public static LoginCampaignManager Instance { get; private set; }

    [Header("Reward given on the 1st of every month")]
    public PresentBoxData firstDayOfMonthReward;

    [Header("Reward given on every other day (unless overridden)")]
    public PresentBoxData defaultDailyReward;

    [Header("Reward given on the last day of every month")]
    public PresentBoxData lastDayOfMonthReward;

    [Header("Special one-off rewards for fixed calendar days (e.g. April 1st, Dec 25th)")]
    public List<SpecialDayReward> specialDayRewards = new();

    [Header("Activated when today's login reward hasn't been claimed yet")]
    public GameObject unclaimedRewardIndicator;

    [Header("Calendar strip UI")]
    public GameObject contentParent;
    public GameObject dayRewardSlotPrefab;
    public Sprite claimedIcon;

    [Header("Reward thumbnails (mirrors PresentBoxSlot)")]
    public Sprite zelThumb;
    public Sprite karmaThumb;
    public Sprite gemThumb;

    [Header("Visual for missed (unclaimed + past) days")]
    [Range(0f, 1f)]
    public float missedDayAlpha = 0.4f;

    [Header("Header text, shown as '[Month], Day [number]'")]
    public TextMeshProUGUI monthDayText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GenerateCurrentMonthCampaign();
    }

    public void GenerateCurrentMonthCampaign()
    {
        DateTime today = DateTime.Now;
        int year = today.Year;
        int month = today.Month;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int todayDay = Mathf.Min(today.Day, daysInMonth);

        string todaysPresentId = BuildPresentId(year, month, todayDay);

        // Login-campaign entries are transient (rebuilt each run), so clear only
        // our own entries — leave any other event presents in the list alone.
        PresentBoxMenu.Instance.presentBoxList.RemoveAll(p => p.presentId.Contains("loginDay"));

        // Housekeeping only: once a login reward has actually been received, its
        // collected-dex entry no longer needs to exist. This does NOT touch
        // unclaimed entries from earlier days — those must persist untouched so
        // a missed day's reward stays claimable indefinitely.
        PlayerData.presentCollectedDex.RemoveAll(id => id.Contains("loginDay") && PlayerData.presentReceivedDex.Contains(id));

        // Only today's login triggers a NEW entry. If the player skips a day,
        // no entry is ever created for that skipped day — it simply never happened.
        if (!PlayerData.presentReceivedDex.Contains(todaysPresentId))
        {
            PresentBoxData reward = BuildRewardForDay(month, todayDay, daysInMonth, todaysPresentId);
            if (reward != null)
            {
                if (!PlayerData.presentCollectedDex.Contains(todaysPresentId))
                {
                    PlayerData.presentCollectedDex.Add(todaysPresentId);
                }
            }
        }

        SpecialDayReward special = GetSpecialDayReward(month, todayDay);
        if (special != null && special.secondaryPresents != null)
        {
            for (int i = 0; i < special.secondaryPresents.Count; i++)
            {
                PresentBoxData secondaryTemplate = special.secondaryPresents[i];
                if (secondaryTemplate == null) continue;

                string secondaryPresentId = BuildSecondaryPresentId(todaysPresentId, i);
                if (PlayerData.presentReceivedDex.Contains(secondaryPresentId)) continue;

                if (!PlayerData.presentCollectedDex.Contains(secondaryPresentId))
                {
                    PlayerData.presentCollectedDex.Add(secondaryPresentId);
                }
            }
        }

        // presentBoxList is rebuilt every run from EVERY collected-but-unreceived
        // id we've accumulated across all past logins, not just today's — this is
        // what makes a day-6 reward still show up on day 8 if never claimed.
        RebuildPresentBoxListFromCollectedDex(year, month, daysInMonth);

        PlayerData.SaveDataToJson();
        PresentBoxMenu.Instance.PopulatePresentBoxMenu();
        MainUI.header.GetComponent<HeaderPlayerData>().UpdateHeader();

        UpdateUnclaimedRewardIndicator(year, month, todayDay);
        PopulateMonthRewardIcons(year, month, daysInMonth, todayDay);
        UpdateMonthDayText(month, todayDay);
    }

    /// <summary>
    /// Public wrapper so PresentBoxMenu.ReceiveAll can refresh the indicator/calendar
    /// without needing full campaign regeneration.
    /// </summary>
    public void RefreshUnclaimedIndicatorAndCalendar()
    {
        DateTime today = DateTime.Now;
        int year = today.Year;
        int month = today.Month;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int todayDay = Mathf.Min(today.Day, daysInMonth);

        UpdateUnclaimedRewardIndicator(year, month, todayDay);
        PopulateMonthRewardIcons(year, month, daysInMonth, todayDay);
    }

    private void UpdateMonthDayText(int month, int day)
    {
        if (monthDayText == null) return;

        string monthName = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
        monthDayText.text = $"{monthName}, Day {day}";
    }

    private void UpdateUnclaimedRewardIndicator(int year, int month, int day)
    {
        if (unclaimedRewardIndicator == null) return;

        string todaysPresentId = BuildPresentId(year, month, day);
        bool alreadyReceived = PlayerData.presentReceivedDex.Contains(todaysPresentId);

        unclaimedRewardIndicator.SetActive(!alreadyReceived && !HeaderPlayerData.openRewardsScreen);
    }

    private void PopulateMonthRewardIcons(int year, int month, int daysInMonth, int todayDay)
    {
        if (contentParent == null || dayRewardSlotPrefab == null) return;

        foreach (Transform child in contentParent.transform)
        {
            Destroy(child.gameObject);
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            string presentId = BuildPresentId(year, month, day);
            PresentBoxData reward = BuildRewardForDay(month, day, daysInMonth, presentId);
            if (reward == null) continue;

            GameObject slotObj = Instantiate(dayRewardSlotPrefab, contentParent.transform);

            Image thumbnailImage = slotObj.GetComponent<Image>();
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = GetThumbnailForReward(reward);
            }

            Transform dayNumTransform = slotObj.transform.Find("DayNum");
            if (dayNumTransform != null)
            {
                TextMeshProUGUI dayNumText = dayNumTransform.GetComponent<TextMeshProUGUI>();
                if (dayNumText != null)
                {
                    dayNumText.text = day.ToString();
                }
            }

            Transform qtNumTransform = slotObj.transform.Find("QtNum");
            if (qtNumTransform != null)
            {
                TextMeshProUGUI qtNumText = qtNumTransform.GetComponent<TextMeshProUGUI>();
                if (qtNumText != null)
                {
                    qtNumText.text = $"x{reward.rewardAmount}";
                }
            }

            bool claimed = PlayerData.presentReceivedDex.Contains(presentId);

            Transform claimedImageTransform = slotObj.transform.Find("ClaimedImage");
            if (claimedImageTransform != null)
            {
                claimedImageTransform.gameObject.SetActive(claimed);
            }

            // Missed: day already passed and the reward was never claimed.
            bool missed = !claimed && day < todayDay;

            CanvasGroup canvasGroup = slotObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = slotObj.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = missed ? missedDayAlpha : 1f;
        }
    }

    private Sprite GetThumbnailForReward(PresentBoxData reward)
    {
        switch (reward.rewardType)
        {
            case RewardType.Zels:
                return zelThumb;
            case RewardType.Karma:
                return karmaThumb;
            case RewardType.Gems:
                return gemThumb;
            case RewardType.Unit:
                Unit unit = UnitRegistry.GetUnitById(reward.rewardData);
                return unit != null ? unit.unitSlotIcon : null;
            default:
                return null; // Item has no registry yet, same limitation as PresentBoxSlot
        }
    }

    private string BuildPresentId(int year, int month, int day)
    {
        // Matches PresentBoxSlot.GetDescription() parsing: split('_')[3] -> "loginDayN"
        return $"{year}_{month:00}_{day:00}_loginDay{day}";
    }

    private string BuildSecondaryPresentId(string mainPresentId, int index)
    {
        return $"{mainPresentId}_secondary{index}";
    }

    private SpecialDayReward GetSpecialDayReward(int month, int day)
    {
        return specialDayRewards.Find(s => s.month == month && s.day == day);
    }

    private PresentBoxData BuildRewardForDay(int month, int day, int daysInMonth, string presentId)
    {
        SpecialDayReward special = GetSpecialDayReward(month, day);
        PresentBoxData template;

        if (special != null)
        {
            template = special.reward;
        }
        else if (day == 1)
        {
            template = firstDayOfMonthReward;
        }
        else if (day == daysInMonth)
        {
            template = lastDayOfMonthReward;
        }
        else
        {
            template = defaultDailyReward;
        }

        if (template == null) return null;

        return new PresentBoxData
        {
            presentId = presentId,
            rewardType = template.rewardType,
            rewardAmount = template.rewardAmount,
            rewardData = template.rewardData,
            customDescription = template.customDescription
        };
    }

    /// <summary>
    /// presentBoxList only ever needs to contain rewards the player is still owed
    /// (i.e. present in presentCollectedDex but not yet in presentReceivedDex).
    /// We rebuild it from the dex rather than tracking it separately, so a login
    /// on day 6 followed by silence still shows the day-6 reward on day 8, 9, etc.
    /// </summary>
    private void RebuildPresentBoxListFromCollectedDex(int year, int month, int daysInMonth)
    {
        foreach (string presentId in PlayerData.presentCollectedDex)
        {
            if (!presentId.Contains("loginDay")) continue;
            if (PlayerData.presentReceivedDex.Contains(presentId)) continue;

            PresentBoxData reward = RebuildRewardFromPresentId(presentId, year, month, daysInMonth);
            if (reward != null)
            {
                PresentBoxMenu.Instance.presentBoxList.Add(reward);
            }
        }
    }

    /// <summary>
    /// Reconstructs a PresentBoxData purely from its stored id, so rewards from
    /// past days can be re-shown without needing extra saved state per entry.
    /// Handles both main-day ids ("{y}_{m}_{d}_loginDayN") and secondary ids
    /// ("{y}_{m}_{d}_loginDayN_secondaryI").
    /// </summary>
    private PresentBoxData RebuildRewardFromPresentId(string presentId, int year, int month, int daysInMonth)
    {
        string[] parts = presentId.Split('_');
        if (parts.Length < 4) return null;
        if (!int.TryParse(parts[0], out int idYear)) return null;
        if (!int.TryParse(parts[1], out int idMonth)) return null;
        if (!int.TryParse(parts[2], out int idDay)) return null;

        // Only reconstruct for the currently-active month; a previous month's
        // stale entries are left alone here (they'll just never be rebuilt/shown,
        // effectively aging out of the visible list without deleting the dex data).
        if (idYear != year || idMonth != month) return null;

        bool isSecondary = parts.Length >= 5 && parts[3].StartsWith("loginDay") && parts[4].StartsWith("secondary");

        if (isSecondary)
        {
            if (!int.TryParse(parts[4].Replace("secondary", ""), out int secondaryIndex)) return null;

            SpecialDayReward special = GetSpecialDayReward(idMonth, idDay);
            if (special == null || special.secondaryPresents == null) return null;
            if (secondaryIndex < 0 || secondaryIndex >= special.secondaryPresents.Count) return null;

            PresentBoxData secondaryTemplate = special.secondaryPresents[secondaryIndex];
            if (secondaryTemplate == null) return null;

            return new PresentBoxData
            {
                presentId = presentId,
                rewardType = secondaryTemplate.rewardType,
                rewardAmount = secondaryTemplate.rewardAmount,
                rewardData = secondaryTemplate.rewardData,
                customDescription = secondaryTemplate.customDescription
            };
        }

        return BuildRewardForDay(idMonth, idDay, daysInMonth, presentId);
    }
}