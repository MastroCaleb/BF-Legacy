using UnityEngine;
using UnityEngine.UI;

public class ActivateDeactivateInventoryButton : MonoBehaviour
{
    public GameObject objectToActivate;
    public GameObject objectToDeactivate;
    public InventorySelectionMode selectionMode;

    void Start()
    {
        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonPressed);
        }
    }

    public void OnButtonPressed()
    {
        
        InventoryRenderer.selectionMode = selectionMode;
        
        if(InventoryRenderer.selectionMode == InventorySelectionMode.None)
        {
            FusionMenu.basePlateInventory.SetActive(false);
            FusionMenu.ClearSelectedSlots();
            FusionMenu.totalZelCost = 0;
            FusionMenu.totalXpGain = 0;

            SellMenu.sellBasePlate.gameObject.SetActive(false);
            SellMenu.totalZelGain = 0;
            SellMenu.ClearSelectedSlots();
            SellMenu.sellBasePlate.UpdateView();

            MainUI.inventoryRenderer.RefreshAllSlotsBBIndicator();
        }

        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
            objectToActivate?.GetComponentInChildren<CustomScrollView>()?.ResetScroll();


            if(selectionMode == InventorySelectionMode.UnitSell)
            {
                SellMenu.sellBasePlate.gameObject.SetActive(true);
            }

            if(selectionMode == InventorySelectionMode.UnitPartySelect)
            {
                PartyEditMenu.UpdateView();
            }

            MainUI.inventoryRenderer.DarkenUnclickableSlots();
        }

        if (objectToDeactivate != null)
        {
            objectToDeactivate.SetActive(false);
        }
    }
}
