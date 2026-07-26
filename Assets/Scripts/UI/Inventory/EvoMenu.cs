using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class EvoMenu : MonoBehaviour
{
    public static int baseUnitKey;

    public UnitDetailsEvoView unitDetailsEvoViewHelper;
    public static UnitDetailsEvoView unitDetailsEvoView;

    public UnitTableRenderer baseUnitRendererHelper;
    public static UnitTableRenderer baseUnitRenderer;
    public UnitTableRenderer evoUnitRendererHelper;
    public static UnitTableRenderer evoUnitRenderer;

    public TextMeshProUGUI evoCostTextHelper;
    public static TextMeshProUGUI evoCostText;
    public GameObject evoPossibleImgHelper;
    public static GameObject evoPossibleImg;
    public TextMeshProUGUI evoPossibleTextHelper;
    public static TextMeshProUGUI evoPossibleText;

    public GameObject materialUnitIconPropHelper;
    public static GameObject materialUnitIconProp;

    public GameObject materialIconsViewHelper;
    public static GameObject materialIconsView;

    public static List<GameObject> materialUnitIcons = new List<GameObject>();

    void Awake()
    {
        unitDetailsEvoView = unitDetailsEvoViewHelper;

        baseUnitRenderer = baseUnitRendererHelper;
        evoUnitRenderer = evoUnitRendererHelper;

        evoCostText = evoCostTextHelper;
        evoPossibleImg = evoPossibleImgHelper;
        evoPossibleText = evoPossibleTextHelper;

        materialUnitIconProp = materialUnitIconPropHelper;

        materialIconsView = materialIconsViewHelper;
    }

    public static void UpdateView()
    {
        UnitRenderersUpdate();
        EvoPossibleUpdate();
        RefreshMaterialUnitIcons();
        unitDetailsEvoView.UpdateDetails();
    }

    public void EvolveButton()
    {
        if(!PlayerUnitInventoryDatabase.CanEvolve(baseUnitKey)) return;

        bool isNew = false;
        if (!PlayerData.unitDex.Contains(PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoInto))
        {
            isNew = true;
        }

        EvoAnimation(isNew);

        PlayerUnitInventoryDatabase.EvolveUnit(baseUnitKey);
        UpdateView();
        PartyViewUI.instance.UpdatePartyView(false);
        MainUI.inventoryRenderer.UpdateSlotView(baseUnitKey);
        
        MainUI.unitEvo.SetActive(false);
        MainUI.footer.SetActive(false);
        MainUI.header.SetActive(false);
        MainUI.extensionLow.SetActive(false);
        MainUI.extensionUp.SetActive(false);

        MainUI.fuseAndEvoText.text = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoDesc;
    }

    public void EvoAnimation(bool isNew)
    {
        MainUI.fuseAndEvoAnimations.SetActive(true);
        MainUI.evoCircleSam.SetActive(true);

        Unit baseUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit;
        List<Unit> materialUnitsData = new List<Unit>();

        foreach(string unitId in baseUnitData.evoMats)
        {
           int key = PlayerUnitInventoryDatabase.GetUnitKeyWithId(unitId);
           materialUnitsData.Add(PlayerUnitInventoryDatabase.GetUnitByKey(key).unit);
        }

        StartCoroutine(MainUI.evoCircleSam.GetComponent<CircleAnimation>().Play(baseUnitKey, materialUnitsData, true, isNew));
    }

    public static void UnitRenderersUpdate()
    {
        Unit baseUnit = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit;
        Unit evoUnit = UnitRegistry.GetUnitById(PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoInto);

        baseUnitRenderer.SetUnit(baseUnit, false);
        evoUnitRenderer.SetUnit(evoUnit, false);

        ObscureUnknownEvolution(evoUnit);
    }

    public static void ObscureUnknownEvolution(Unit unit)
    {
        if(PlayerData.unitDex.Contains(unit.unitId)) return;

        if (unit.animationType == AnimationType.CGG)
            evoUnitRenderer.GetComponent<Image>().color = Color.black;
        else
            foreach (var sr in evoUnitRenderer.GetComponentsInChildren<SamUIQuadGraphic>())
                sr.color = Color.black;
    }

    public static void EvoPossibleUpdate()
    {
        evoCostText.text = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoZelCost.ToString();
        evoPossibleImg.SetActive(PlayerUnitInventoryDatabase.CanEvolve(baseUnitKey));
        evoPossibleText.text = PlayerUnitInventoryDatabase.CanEvolve(baseUnitKey) ? "Can be Evolved" : PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoZelCost > PlayerData.zel ? "Not enough Zel" : "Can not be Evolved";
        evoPossibleText.color =  PlayerUnitInventoryDatabase.CanEvolve(baseUnitKey) ? Color.white : Color.red;
    }

    public static void RefreshMaterialUnitIcons()
    {
        if (materialUnitIcons != null || materialUnitIcons.Count > 0)
        {
            List<GameObject> cloned = new List<GameObject>(materialUnitIcons);
            materialUnitIcons.Clear();
            foreach (var icon in cloned)
                Destroy(icon);
        }

        // Track how many of each unit ID we've already "used" as we iterate
        Dictionary<string, int> usedCounts = new Dictionary<string, int>();

        foreach (string unitId in PlayerUnitInventoryDatabase.GetUnitByKey(baseUnitKey).unit.evoMats)
        {
            Unit unitData = UnitRegistry.GetUnitById(unitId);
            
            if (!usedCounts.ContainsKey(unitId))
                usedCounts[unitId] = 0;

            int available = PlayerUnitInventoryDatabase.GetUnitCountWithId(unitId);
            bool isCovered = usedCounts[unitId] < available;

            usedCounts[unitId]++;

            CreateMaterialUnitIcons(unitData, available, isCovered);
        }
    }

    public static void CreateMaterialUnitIcons(Unit unitData, int availableCount, bool isCovered)
    {
        GameObject icon = Instantiate(materialUnitIconProp);
        icon.GetComponentInChildren<Image>().sprite = unitData.unitSlotIcon;
        icon.GetComponentInChildren<TextMeshProUGUI>().text = "Have " + availableCount;

        icon.GetComponentInChildren<Image>().color = isCovered ? Color.white : Color.gray;

        icon.transform.SetParent(materialIconsView.transform);
        icon.transform.localScale = Vector3.one;

        materialUnitIcons.Add(icon);
    }
}
