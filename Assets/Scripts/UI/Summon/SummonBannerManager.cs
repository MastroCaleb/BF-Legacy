using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummonBannerManager : MonoBehaviour
{
    public List<SummonBanner> summonBanners;
    public Transform bannerParent;
    public GameObject summonGateUI;
    public GameObject pullList;
    public RectTransform contentPullList;

    void OnEnable()
    {
        GenerateSummonBanners();
    }

    void GenerateSummonBanners()
    {
        for (int i = bannerParent.childCount - 1; i >= 0; i--)
        {
            Destroy(bannerParent.GetChild(i).gameObject);
        }

        var scrollMenu = GetComponent<CustomScrollMenu>();
        scrollMenu.menuItems.Clear();

        foreach (var banner in summonBanners)
        {
            if(PlayerData.completedMissionDex.Contains(banner.requiredMissionId)){

                GameObject bannerUI = Instantiate(PrefabCache.Get("SummonBanner"), transform);
                bannerUI.transform.SetParent(bannerParent, false);
                SummonCustomBanner customBanner = bannerUI.GetComponent<SummonCustomBanner>();
                customBanner.summonBanner = banner;
                customBanner.summonGate = summonGateUI;
                customBanner.content = contentPullList;
                customBanner.pullChancesList = pullList;

                scrollMenu.menuItems.Add(bannerUI);
            }
        }

        scrollMenu.ResetMenu();
    }
}
