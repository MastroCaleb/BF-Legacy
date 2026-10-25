using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendListMenuUI : MonoBehaviour
{
    public RectTransform content;

    void OnEnable()
    {
        PopulateFriendList();
    }

    private void PopulateFriendList()
    {
        // Clear existing friend slots
        foreach (RectTransform child in content)
        {
            Destroy(child.gameObject);
        }

        // Populate with current friends
        foreach (var friend in PlayerData.friends)
        {
            GameObject slot = Instantiate(PrefabCache.Get("FriendSlot"));

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(content, false);
            slotRect.localScale = Vector3.one;
            FriendSlotUI slotUI = slot.GetComponent<FriendSlotUI>();
            if (slotUI != null)
            {
                slotUI.UpdateUI(friend);
            }
        }
    }
}