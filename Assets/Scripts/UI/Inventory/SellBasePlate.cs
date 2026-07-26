using TMPro;
using UnityEngine;

public class SellBasePlate : MonoBehaviour
{
    public TextMeshProUGUI zelGainText;
    public TextMeshProUGUI unitCountText;

    public void UpdateView()
    {
        UpdateZelGain();
        UpdateUnitCount();
    }

    public void UpdateZelGain()
    {
        zelGainText.text = SellMenu.totalZelGain.ToString();
    }

    public void UpdateUnitCount()
    {
        unitCountText.text = $"{SellMenu.sellUnits.Count}";
    }
}
