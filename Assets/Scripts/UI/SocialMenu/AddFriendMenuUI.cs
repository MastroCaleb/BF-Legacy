using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class AddFriendMenuUI : MonoBehaviour
{
    public TMP_InputField codeTextField;
    public Button addFriendButton;
    public TextMeshProUGUI feedbackText;

    void Start()
    {
        if(addFriendButton != null)
            addFriendButton.onClick.AddListener(OnAddFriendButtonClicked);
    }

    public void OnAddFriendButtonClicked()
    {
        string friendCode = codeTextField.text;
        if (!string.IsNullOrEmpty(friendCode))
        {
            // Call your method to add a friend here
            Debug.Log($"Adding friend: {friendCode}");
            bool success = PlayerData.AddFriendFromCode(friendCode);
            if (success)
            {
                PlayerData.honorPoints += 10;
                feedbackText.text = "Friend added or updated successfully!";
                feedbackText.color = Color.white;
            }
            else
            {
                feedbackText.text = "Failed to add friend. Please check the friend code.";
                feedbackText.color = Color.red;
            }
        }
        else
        {
            Debug.LogWarning("Friend code is empty.");
            feedbackText.text = "Please enter a valid friend code.";
            feedbackText.color = Color.red;
        }
    }

}