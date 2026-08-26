using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SortButton : MonoBehaviour
{
    Button button;

    public Sprite selectedSprite;
    public Sprite deselectedSprite;
    public bool isSort = true;
    public Sort sort;
    public bool isAscending;
    public bool isSelect;

    static List<SortButton> allButtons = new List<SortButton>();

    void Start()
    {
        button = GetComponent<Button>();
        if(isSelect)
            button.onClick.AddListener(SortByType);
        else
            button.onClick.AddListener(SetType);

        if (!isSelect && !allButtons.Contains(this))
        {
            allButtons.Add(this);
        }

        RefreshSelected();
    }

    public void SortByType()
    {
        MainUI.inventoryRenderer.SortInventory();
    }

    public void SetType()
    {
        if (isSort)
        {
            MainUI.inventoryRenderer.SetSort(sort);
            MainUI.inventoryRenderer.RefreshAllSlots();
        }

        MainUI.inventoryRenderer.SetSortDirection(isAscending);

        RefreshSelected();
    }

    void ApplySelectedVisual(bool selected)
    {
        if (button != null)
        {
            button.image.sprite = selected ? selectedSprite : deselectedSprite;
        }
    }

    // isSort buttons form one exclusive group (by sort type);
    // non-isSort (direction) buttons form a separate exclusive group (by isAscending).
    public static void RefreshSelected()
    {
        foreach (var s in allButtons)
        {
            bool isActive = s.isSort
                ? MainUI.inventoryRenderer.currentSort == s.sort
                : MainUI.inventoryRenderer.sortAscending == s.isAscending;

            s.ApplySelectedVisual(isActive);
        }
    }
}
