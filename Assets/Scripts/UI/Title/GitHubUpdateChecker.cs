using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json.Linq; 

public class GitHubUpdateChecker : MonoBehaviour
{
    [Header("GitHub Settings")]
    public string username = "MastroCaleb";
    public string repository = "BF-Legacy";

    [Header("UI Elements")]
    public GameObject updateMenuPanel;
    public Button updateButton;

    private string downloadUrl;

    void Start()
    {
        if (updateMenuPanel != null)
        {
            updateMenuPanel.SetActive(false);
        }

        StartCoroutine(CheckForUpdates());
    }

    IEnumerator CheckForUpdates()
    {
        string url = $"https://api.github.com/repos/{username}/{repository}/releases";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("User-Agent", "Unity-Update-Checker");
            webRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                ProcessResponse(webRequest.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"Failed to check for updates: {webRequest.error}");
            }
        }
    }

    void ProcessResponse(string jsonResponse)
    {
        try
        {
            JArray releases = JArray.Parse(jsonResponse);
            if (releases.Count > 0)
            {
                JObject latestRelease = (JObject)releases[0];
                string tagName = latestRelease["tag_name"]?.ToString();
                downloadUrl = latestRelease["html_url"]?.ToString();
                
                string currentVersion = Application.version;

                if (tagName != currentVersion)
                {
                    Debug.Log($"New version found! Update to {tagName} from {currentVersion}");
                    TriggerUpdateUI();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing update data with Newtonsoft: {e.Message}");
        }
    }

    void TriggerUpdateUI()
    {
        if (updateMenuPanel != null)
        {
            updateMenuPanel.SetActive(true);
        }

        if (updateButton != null)
        {
            updateButton.onClick.RemoveAllListeners();
            updateButton.onClick.AddListener(OpenUpdateLink);
        }
    }

    void OpenUpdateLink()
    {
        if (!string.IsNullOrEmpty(downloadUrl))
        {
            Application.OpenURL(downloadUrl);
        }
    }
}
