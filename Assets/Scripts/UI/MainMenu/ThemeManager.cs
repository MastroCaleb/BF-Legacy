using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThemeManager : MonoBehaviour
{
    public List<Theme> themes;
    public Theme defaultTheme;

    public static ThemeManager instance;

    public Vector3 startPosition = new Vector3(0, 230.8f, 0);

    public Image characterFrameImage;
    public Image wallpaperImage;
    public MenuSectionButton homeButton;
    public MenuSectionButton unitButton;
    public MenuSectionButton townButton;
    public MenuSectionButton summonButton;
    public MenuSectionButton shopButton;
    public MenuSectionButton socialButton;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        Theme currentTheme = GetCurrentTheme();
        ApplyTheme(currentTheme);
    }

    public Theme GetCurrentTheme()
    {
        DateTime now = DateTime.Now;
        int nowKey = now.Month * 100 + now.Day;

        foreach (var theme in themes)
        {
            if (IsWithinRange(nowKey, theme.startDate, theme.endDate))
            {
                return theme;
            }
        }
        return defaultTheme;
    }

    private bool IsWithinRange(int nowKey, string startStr, string endStr)
    {
        int startKey = ParseDayMonth(startStr);
        int endKey = ParseDayMonth(endStr);

        if (startKey <= endKey)
        {
            return nowKey >= startKey && nowKey <= endKey;
        }
        else
        {
            return nowKey >= startKey || nowKey <= endKey;
        }
    }

    private int ParseDayMonth(string ddMM)
    {
        string[] parts = ddMM.Split('/');
        int day = int.Parse(parts[0]);
        int month = int.Parse(parts[1]);
        return month * 100 + day;
    }

    public void ApplyTheme(Theme theme)
    {
        characterFrameImage.sprite = theme.characterFrame;
        characterFrameImage.SetNativeSize();
        Vector3 target = startPosition + theme.offset;
        characterFrameImage.GetComponent<RectTransform>().anchoredPosition = target;
        wallpaperImage.sprite = theme.wallpaper;
        homeButton.activeSprite = theme.homeButtonOn;
        homeButton.inactiveSprite = theme.homeButtonOff;
        unitButton.activeSprite = theme.unitButtonOn;
        unitButton.inactiveSprite = theme.unitButtonOff;
        townButton.activeSprite = theme.townButtonOn;
        townButton.inactiveSprite = theme.townButtonOff;
        summonButton.activeSprite = theme.summonButtonOn;
        summonButton.inactiveSprite = theme.summonButtonOff;
        shopButton.activeSprite = theme.shopButtonOn;
        shopButton.inactiveSprite = theme.shopButtonOff;
        socialButton.activeSprite = theme.socialButtonOn;
        socialButton.inactiveSprite = theme.socialButtonOff;
    }
}
[Serializable]
public class Theme
{
    public string startDate;
    public string endDate;
    public Vector3 offset;
    public Sprite characterFrame;
    public Sprite wallpaper;
    public Sprite homeButtonOff;
    public Sprite homeButtonOn;
    public Sprite unitButtonOff;
    public Sprite unitButtonOn;
    public Sprite townButtonOff;
    public Sprite townButtonOn;
    public Sprite summonButtonOff;
    public Sprite summonButtonOn;
    public Sprite shopButtonOff;
    public Sprite shopButtonOn;
    public Sprite socialButtonOff;
    public Sprite socialButtonOn;
}