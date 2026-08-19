using UnityEngine;
using UnityEngine.UI;

public class SortFilterMenuButton : MonoBehaviour
{
    public Sprite selectedSprite;
    public Sprite otherDeselectedSprite;

    public Image oppositeButtonImage;

    Button button;

    void Start()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(SwapImage);
    }


    public void SwapImage()
    {
        GetComponent<Image>().sprite = selectedSprite;
        oppositeButtonImage.sprite = otherDeselectedSprite;
    }
}
