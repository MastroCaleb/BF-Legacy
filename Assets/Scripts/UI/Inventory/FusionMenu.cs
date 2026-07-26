using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FusionMenu : MonoBehaviour
{
    public static int baseUnit;
    public static SuccessType lastSuccessType;
    public static List<int> materialUnits = new List<int>();
    public UnitTableRenderer baseUnitRendererHelper;
    public static UnitTableRenderer baseUnitRenderer;
    public List<UnitTableRenderer> materialUnitRenderersHelpers;
    public static List<UnitTableRenderer> materialUnitRenderers;
    public List<Image> bbUpImageHelpers;
    public static List<Image> bbUpImages;
    public BaseUnitDetails baseUnitDetailsHelper;
    public static BaseUnitDetails baseUnitDetails;
    public GameObject basePlateInventoryHelper;
    public static GameObject basePlateInventory;
    public FusionBasePlate fusionBasePlateFMHelper;
    public static FusionBasePlate fusionBasePlateFM;

    public Sprite bbProbabilityIcon;
    public Sprite bbCertaintyIcon;

    public static int totalZelCost;
    public static int totalXpGain;

    public void Awake()
    {
        baseUnitRenderer = baseUnitRendererHelper;
        materialUnitRenderers = materialUnitRenderersHelpers;
        baseUnitDetails = baseUnitDetailsHelper;
        basePlateInventory = basePlateInventoryHelper;
        fusionBasePlateFM = fusionBasePlateFMHelper;
        bbUpImages = bbUpImageHelpers;
    }

    public void SelectMaterialUnit()
    {
        MainUI.unitFusion.SetActive(false);
        MainUI.unitList.SetActive(true);
        InventoryRenderer.selectionMode = InventorySelectionMode.UnitFusionSelectMaterial;
        basePlateInventory.SetActive(true);
        MainUI.inventoryRenderer.RefreshAllSlotsBBIndicator();
        MainUI.inventoryRenderer.DarkenUnclickableSlots();
    }

    public void SelectBaseUnit()
    {
        MainUI.unitFusion.SetActive(false);
        MainUI.unitList.SetActive(true);
        InventoryRenderer.selectionMode = InventorySelectionMode.UnitFusionSelectBase;
    }

    public void ConfirmMaterialUnits()
    {
        MainUI.unitFusion.SetActive(true);
        MainUI.unitList.SetActive(false);
        SetAllUnitRenderers();
        InventoryRenderer.selectionMode = InventorySelectionMode.None;
        fusionBasePlateFM.UpdateView();
        basePlateInventory.SetActive(false);
    }

    public void SetAllUnitRenderers()
    {
        for(int i = 0; i < 5; i++)
        {
            if(i < materialUnits.Count)
            {
                UnitInventoryData baseUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnit);
                UnitInventoryData materialUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(materialUnits[i]);
                materialUnitRenderers[i].SetUnit(materialUnitData, false);
                bbUpImages[i].gameObject.SetActive(PlayerUnitInventoryDatabase.ShouldBBLevelUp(baseUnitData, materialUnitData) != BBLevelUpProbability.None);
                bbUpImages[i].sprite = PlayerUnitInventoryDatabase.ShouldBBLevelUp(baseUnitData, materialUnitData) == BBLevelUpProbability.Chance ? bbProbabilityIcon : bbCertaintyIcon;
            }
            else
            {
                materialUnitRenderers[i].ClearUnit();
                bbUpImages[i].gameObject.SetActive(false);
            }
        }
    }

    public static void ClearSelectedSlots()
    {
        List<int> cloned = new List<int>(materialUnits);

        materialUnits.Clear();

        // Update all selected slot indicators to hide them
        foreach (int unitKey in cloned)
        {
            if (MainUI.inventoryRenderer.renderedSlots.TryGetValue(unitKey, out UnitSlot slot))
            {
                slot.SetupSelectionIndicator();
            }
        }

        materialUnitRenderers.ForEach(renderer => renderer.ClearUnit());
        bbUpImages.ForEach(image => image.gameObject.SetActive(false));
    }

    public void ClearSelectedButton()
    {
        ClearSelectedSlots();
    }

    public void FuseButton()
    {
        UnitInventoryData current = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnit);
        Unit unit = current.unit;
        LevelUpDetailsUI.oldLevel = current.currentLevel;
        LevelUpDetailsUI.oldHp    = unit.maxHealth + current.hpImpBonus + current.hpLevelUpBonus;
        LevelUpDetailsUI.oldAtk   = unit.atk       + current.atkImpBonus + current.atkLevelUpBonus;
        LevelUpDetailsUI.oldDef   = unit.def       + current.defImpBonus + current.defLevelUpBonus;
        LevelUpDetailsUI.oldRec   = unit.rec       + current.recImpBonus + current.recLevelUpBonus;
        LevelUpDetailsUI.oldBBLv  = current.currentBBLevel;

        if(PlayerData.zel < totalZelCost) return;
        
        if(materialUnits == null || materialUnits.Count == 0) return;
        
        float successChance = Random.Range(0, 100);
        
        FuseAnimation();

        if (successChance <= 87)
        {
            PlayerUnitInventoryDatabase.FuseUnits(baseUnit, materialUnits, SuccessType.Success);
            lastSuccessType = SuccessType.Success;
        }
        else if (successChance > 87 && successChance <= 94)
        {
            PlayerUnitInventoryDatabase.FuseUnits(baseUnit, materialUnits, SuccessType.GreatSuccess);
            lastSuccessType = SuccessType.GreatSuccess;
        }
        else
        {
            PlayerUnitInventoryDatabase.FuseUnits(baseUnit, materialUnits, SuccessType.SuperSuccess);
            lastSuccessType = SuccessType.SuperSuccess;
        }

        materialUnitRenderers.ForEach(renderer => renderer.ClearUnit());
        bbUpImages.ForEach(image => image.gameObject.SetActive(false));
        MainUI.inventoryRenderer.UpdateSlotView(baseUnit);
        totalZelCost = 0;
        totalXpGain = 0;
        fusionBasePlateFM.UpdateView();
        basePlateInventory.GetComponent<FusionBasePlate>().UpdateView();

        baseUnitDetails.UpdateDetails();

        MainUI.unitFusion.SetActive(false);
        MainUI.footer.SetActive(false);
        MainUI.header.SetActive(false);
        MainUI.extensionLow.SetActive(false);
        MainUI.extensionUp.SetActive(false);

        MainUI.fuseAndEvoText.text = PlayerUnitInventoryDatabase.GetUnitByKey(baseUnit).unit.fusionDesc;

        InventoryRenderer.selectionMode = InventorySelectionMode.None;
        materialUnits.Clear();
    }

    public void FuseAnimation()
    {
        MainUI.fuseAndEvoAnimations.SetActive(true);
        MainUI.fusionCircleSam.SetActive(true);

        List<Unit> materialUnitsData = new List<Unit>();

        foreach (int key in materialUnits)
        {
            materialUnitsData.Add(PlayerUnitInventoryDatabase.GetUnitByKey(key).unit);
        }

        StartCoroutine(MainUI.fusionCircleSam.GetComponent<CircleAnimation>().Play(baseUnit, materialUnitsData, false));
    }
}
