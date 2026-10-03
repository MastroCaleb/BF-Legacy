using UnityEngine;

public class CopyOwnCode : MonoBehaviour
{
    public void CopyCodeToClipboard()
    {
        string friendCode = PlayerData.GetShareableFriendCode();
        GUIUtility.systemCopyBuffer = friendCode;
        Debug.Log($"Copied friend code to clipboard: {friendCode}");
    }
}
