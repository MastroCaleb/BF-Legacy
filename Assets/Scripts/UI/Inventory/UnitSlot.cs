using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitSlot : MonoBehaviour
{
    public int unitKey;
    public Button button;
    public Image newIndicator;
    public Image partyIndicator;
    public Image bbIndicator;
    public Image favIndicator;
    public TextMeshProUGUI levelText;

    public Sprite bbProbabilityIcon;
    public Sprite bbCertaintyIcon;

    //For selection
    public Image selectionIndicator;
    public TextMeshProUGUI selectNumberText;

    private Image _image;
    private Button _button;

    void Awake()
    {
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();
    }

    public void SetVisible(bool visible)
    {
        _image.enabled = visible;
        _button.enabled = visible;
    }
    
    public void OnClick()
    {   
        Debug.Log("Clicked on unit with ID: " + unitKey);
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        unitData.isNew = false;
        newIndicator.gameObject.SetActive(unitData.isNew);
        if(InventoryRenderer.selectionMode == InventorySelectionMode.None)
        {
            NoneSelection();
        }
        else if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitFusionSelectBase || InventoryRenderer.selectionMode == InventorySelectionMode.UnitFusionSelectMaterial)
        {
            FusionSelection();
        }
        else if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitSell)
        {
            SellSelection();
        }
        else if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitEvolutionSelectBase)
        {
            EvolutionSelection();
        }
        else if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitPartySelect)
        {
            PartySelection();
        }
    }

    public void NoneSelection()
    {
        MainUI.unitMenu.SetActive(false);
        MainUI.footer.SetActive(false);
        MainUI.unitSummary.gameObject.SetActive(true);
        MainUI.unitSummary.SetUnit(PlayerUnitInventoryDatabase.GetUnitByKey(unitKey), new List<GameObject>() { MainUI.unitMenu, MainUI.footer }, null);
    }

    public void FusionSelection()
    {
        if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitFusionSelectBase)
        {
            MainUI.unitList.SetActive(false);
            MainUI.unitFusion.SetActive(true);
            FusionMenu.baseUnit = unitKey;
            FusionMenu.baseUnitRenderer.SetUnit(PlayerUnitInventoryDatabase.GetUnitByKey(unitKey), true);
            FusionMenu.baseUnitDetails.UpdateDetails();
            FusionMenu.fusionBasePlateFM.Clear();
        }
        else if(InventoryRenderer.selectionMode == InventorySelectionMode.UnitFusionSelectMaterial)
        {
            if(FusionMenu.baseUnit == unitKey) return;
            if(PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).isInParty) return;

            if(!FusionMenu.materialUnits.Contains(unitKey))
            {
                // If we already have 5 units, remove the first one and shift all
                if(FusionMenu.materialUnits.Count >= 5)
                {
                    MainUI.inventoryRenderer.renderedSlots.TryGetValue(FusionMenu.materialUnits[0], out var oldSlot);
                    FusionMenu.totalZelCost -= PlayerUnitInventoryDatabase.ZelFusionCost(FusionMenu.baseUnit, FusionMenu.materialUnits[0]);
                    FusionMenu.totalXpGain -= PlayerUnitInventoryDatabase.CalculateTotalEXPFromUnit(FusionMenu.baseUnit, FusionMenu.materialUnits[0]);
                    FusionMenu.basePlateInventory.GetComponent<FusionBasePlate>().UpdateView();
                    FusionMenu.materialUnits.RemoveAt(0);
                    oldSlot?.SetupSelectionIndicator();
                    
                    // Update selection indicators for all remaining units
                    for(int i = 0; i < 4; i++)
                    {
                        MainUI.inventoryRenderer.renderedSlots.TryGetValue(FusionMenu.materialUnits[i], out var slot);

                        slot?.SetupSelectionIndicator();
                    }
                }
                
                // Add the new unit to the list
                FusionMenu.materialUnits.Add(unitKey);
                FusionMenu.totalZelCost += PlayerUnitInventoryDatabase.ZelFusionCost(FusionMenu.baseUnit, unitKey);
                FusionMenu.totalXpGain += PlayerUnitInventoryDatabase.CalculateTotalEXPFromUnit(FusionMenu.baseUnit, unitKey);
                FusionMenu.basePlateInventory.GetComponent<FusionBasePlate>().UpdateView();

                SetupSelectionIndicator();
            }
            else
            {
                // If the unit is already selected, remove it
                FusionMenu.materialUnits.Remove(unitKey);
                FusionMenu.totalZelCost -= PlayerUnitInventoryDatabase.ZelFusionCost(FusionMenu.baseUnit, unitKey);
                FusionMenu.totalXpGain -= PlayerUnitInventoryDatabase.CalculateTotalEXPFromUnit(FusionMenu.baseUnit, unitKey);
                FusionMenu.basePlateInventory.GetComponent<FusionBasePlate>().UpdateView();
                SetupSelectionIndicator();
                for(int i = 0; i < FusionMenu.materialUnits.Count; i++)
                {
                    MainUI.inventoryRenderer.renderedSlots.TryGetValue(FusionMenu.materialUnits[i], out var slot);
                    slot?.SetupSelectionIndicator();
                }
            }
        }
    }

    public void SellSelection()
    {
        if(PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).isInParty || PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).isFavorite) return;

        if(!SellMenu.sellUnits.Contains(unitKey))
        {
            // If we already have 5 units, remove the first one and shift all
            if(SellMenu.sellUnits.Count >= 25)
            {
                MainUI.inventoryRenderer.renderedSlots.TryGetValue(SellMenu.sellUnits[0], out var oldSlot);
                SellMenu.totalZelGain -= PlayerUnitInventoryDatabase.GetUnitByKey(SellMenu.sellUnits[0]).unit.sellPrice;
                SellMenu.sellBasePlate.UpdateView();
                SellMenu.sellUnits.RemoveAt(0);
                oldSlot?.SetupSelectionIndicator();
                    
                // Update selection indicators for all remaining units
                for(int i = 0; i < 24; i++)
                {
                    MainUI.inventoryRenderer.renderedSlots.TryGetValue(SellMenu.sellUnits[i], out var slot);

                    slot?.SetupSelectionIndicator();
                }
            }
                
            // Add the new unit to the list
            SellMenu.sellUnits.Add(unitKey);
            SellMenu.totalZelGain += PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).unit.sellPrice;
            SellMenu.sellBasePlate.UpdateView();

            SetupSelectionIndicator();
        }
        else
        {
            // If the unit is already selected, remove it
            SellMenu.sellUnits.Remove(unitKey);
            SellMenu.totalZelGain -= PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).unit.sellPrice;
            SellMenu.sellBasePlate.UpdateView();

            SetupSelectionIndicator();
            for(int i = 0; i < SellMenu.sellUnits.Count; i++)
            {
                MainUI.inventoryRenderer.renderedSlots.TryGetValue(SellMenu.sellUnits[i], out var slot);
                slot?.SetupSelectionIndicator();
            }
        }
    }

    public void EvolutionSelection()
    {
        string evoId = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).unit.evoInto;
        if(string.IsNullOrEmpty(evoId)) return;

        MainUI.unitList.SetActive(false);
        MainUI.unitEvo.SetActive(true);
        EvoMenu.baseUnitKey = unitKey;
        EvoMenu.UpdateView();
    }

    public void PartySelection()
    {
        MainUI.unitList.SetActive(false);
        MainUI.unitParty.SetActive(true);

        int targetSlot = PartyEditMenu.currentUnitIndex;
        int partyKey = PartyEditMenu.currentPartyKey;
        PartyData currentParty = PartyDatabase.GetParty(partyKey);
        int currentUnitKey = currentParty.GetUnitAt(targetSlot);
        // If clicking a unit already in this slot, clear it instead
        if (currentUnitKey == unitKey)
        {
            PartyDatabase.ClearSlot(partyKey, targetSlot);
            PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).isInParty = false;
        }
        else
        {
            // SetUnitAtSlot handles the swap if unitKey is already elsewhere in the party
            PartyDatabase.SetUnitAtSlot(partyKey, targetSlot, unitKey);
            PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).isInParty = true;
        }

        PartyDatabase.SaveToJson();
        PartyEditMenu.UpdateView();
        PartyViewUI.instance.UpdatePartyView(false);
        MainUI.inventoryRenderer.UpdateSlotView(currentUnitKey);
        MainUI.inventoryRenderer.UpdateSlotView(unitKey);
    }

    public void UpdateView()
    {
        GetComponent<Image>().sprite = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey).unit.unitSlotIcon;
        SetupLevelText();
        SetupNewIndicator();
        SetupPartyIndicator();
        SetupBBIndicator();
        SetupFavouriteIndicator();
    }

    public void SetupLevelText()
    {
        levelText = levelText ?? transform.Find("LvText").GetComponent<TextMeshProUGUI>();
        levelText.gameObject.SetActive(true);
        levelText.color = Color.white;
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        int level = unitData.currentLevel;

        levelText.text = "Lv. " + (level == unitData.unit.maxLevel ? "MAX" : level.ToString());
        levelText.color = level == unitData.unit.maxLevel ? Color.yellow : Color.white;
    }

    public void SetupNewIndicator()
    {
        newIndicator = newIndicator ?? transform.Find("NewIndicator").GetComponent<Image>();
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        if(!unitData.isNew || unitData.isInParty)
        {
            newIndicator.gameObject.SetActive(false);
            return;
        }
        newIndicator.gameObject.SetActive(unitData.isNew);
    }

    public void SetupPartyIndicator()
    {
        partyIndicator = partyIndicator ?? transform.Find("PartyIndicator").GetComponent<Image>();
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        if(unitData.isInParty)
        {
            newIndicator.gameObject.SetActive(false);
        }
        partyIndicator.gameObject.SetActive(unitData.isInParty);
    }

    public void SetupFavouriteIndicator()
    {
        favIndicator = favIndicator ?? transform.Find("FavouriteIndicator").GetComponent<Image>();
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        favIndicator.gameObject.SetActive(unitData.isFavorite);
    }

    public void SetupBBIndicator()
    {
        bbIndicator = bbIndicator ?? transform.Find("BBIndicator").GetComponent<Image>();
        UnitInventoryData baseUnitData = PlayerUnitInventoryDatabase.GetUnitByKey(FusionMenu.baseUnit);
        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);

        if (InventoryRenderer.selectionMode != InventorySelectionMode.UnitFusionSelectMaterial || unitData.isInParty || baseUnitData == null || unitData == null)
        {
            SetupLevelText();
            bbIndicator.gameObject.SetActive(false);
            return;
        }

        BBLevelUpProbability probability = PlayerUnitInventoryDatabase.ShouldBBLevelUp(baseUnitData, unitData);

        if (probability == BBLevelUpProbability.None)
        {
            bbIndicator.gameObject.SetActive(false);
        }
        else if (probability == BBLevelUpProbability.Chance)
        {
            bbIndicator.sprite = bbProbabilityIcon;
            bbIndicator.gameObject.SetActive(true);
        }
        else if (probability == BBLevelUpProbability.Certain)
        {
            bbIndicator.sprite = bbCertaintyIcon;
            bbIndicator.gameObject.SetActive(true);
        }
    }

    public void SetupSelectionIndicator()
    {
        selectionIndicator = selectionIndicator ?? transform.Find("SelectionIndicator").GetComponent<Image>();
        selectNumberText = selectNumberText ?? transform.Find("SelectNumber").GetComponent<TextMeshProUGUI>();
        
        if(InventoryRenderer.selectionMode == InventorySelectionMode.None)
        {
            selectionIndicator.gameObject.SetActive(false);
            selectNumberText.gameObject.SetActive(false);
            return;
        }

        if(isSelectedAsMaterial())
        {
            selectionIndicator.gameObject.SetActive(true);
            selectNumberText.gameObject.SetActive(true);
            int selectIndex = FusionMenu.materialUnits.IndexOf(unitKey) + 1;
            selectNumberText.text = selectIndex.ToString();
        }
        else if(isSelectedAsSell())
        {
            selectionIndicator.gameObject.SetActive(true);
            selectNumberText.gameObject.SetActive(true);
            int selectIndex = SellMenu.sellUnits.IndexOf(unitKey) + 1;
            selectNumberText.text = selectIndex.ToString();
        }
        else
        {
            selectionIndicator.gameObject.SetActive(false);
            selectNumberText.gameObject.SetActive(false);
        }
    }

    public bool isSelectedAsMaterial()
    {
        return InventoryRenderer.selectionMode == InventorySelectionMode.UnitFusionSelectMaterial && FusionMenu.materialUnits.Contains(unitKey);
    }

    public bool isSelectedAsSell()
    {
        return InventoryRenderer.selectionMode == InventorySelectionMode.UnitSell && SellMenu.sellUnits.Contains(unitKey);
    }

    public bool CanBeFusionMaterial()
    {
        if(InventoryRenderer.selectionMode != InventorySelectionMode.UnitFusionSelectMaterial)
        {
            return false;
        }

        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);

        return unitKey != FusionMenu.baseUnit && !unitData.isInParty;
    }

    public bool CanBeSold()
    {
        if(InventoryRenderer.selectionMode != InventorySelectionMode.UnitSell)
        {
            return false;
        }

        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);

        return !unitData.isInParty;
    }

    public bool CanBeEvolutionBase()
    {
        if(InventoryRenderer.selectionMode != InventorySelectionMode.UnitEvolutionSelectBase)
        {
            return false;
        }

        UnitInventoryData unitData = PlayerUnitInventoryDatabase.GetUnitByKey(unitKey);
        string evoId = unitData.unit.evoInto;

        return !string.IsNullOrEmpty(evoId);
    }
}
