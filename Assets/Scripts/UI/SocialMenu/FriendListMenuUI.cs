using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class FriendListMenuUI : MonoBehaviour
{
    public GameObject friendSlotPrefab;
    public RectTransform friendListContent;

    void OnEnable()
    {
        PopulateFriendList();
    }

    private void PopulateFriendList()
    {
        // Clear existing friend slots
        foreach (RectTransform child in friendListContent)
        {
            Destroy(child.gameObject);
        }

        // Populate with current friends
        foreach (var friend in PlayerData.friends)
        {
            GameObject slot = Instantiate(friendSlotPrefab, friendListContent);
            FriendSlotUI slotUI = slot.GetComponent<FriendSlotUI>();
            if (slotUI != null)
            {
                slotUI.UpdateUI(friend);
            }
        }
    }
}