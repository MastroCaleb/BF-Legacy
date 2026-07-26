using UnityEngine;
using UnityEngine.UI;

public class BarUI : MonoBehaviour
{
    public Image fillImage;
    public int maxValue;
    public int currentValue;

    public void UpdateUI()
    {
        if (maxValue == 0) return;
        fillImage.fillAmount = (float)currentValue / maxValue;
    }
}
