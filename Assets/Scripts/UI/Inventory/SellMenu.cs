using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SellMenu : MonoBehaviour
{
    public static List<int> sellUnits = new List<int>();
    public static int totalZelGain;
    public static SellBasePlate sellBasePlate;
    public SellBasePlate sellBasePlateHelper;

    void Awake()
    {
        sellBasePlate = sellBasePlateHelper;
    }

    public void SellButton()
    {
        PlayerUnitInventoryDatabase.SellUnits(sellUnits);
        totalZelGain = 0;
        sellUnits.Clear();
        sellBasePlate.UpdateView();
    }

    public static void ClearSelectedSlots()
    {
        List<int> cloned = new List<int>(sellUnits);

        // Clear the sell units list and renderers
        sellUnits.Clear();

        // Update all selected slot indicators to hide them
        foreach (int unitKey in cloned)
        {
            if (MainUI.inventoryRenderer.renderedSlots.TryGetValue(unitKey, out UnitSlot slot))
            {
                slot.SetupSelectionIndicator();
            }
        }
    }

    public void ClearSelectionButton()
    {
        ClearSelectedSlots();
    }
}
