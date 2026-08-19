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
        string url = $"https://github.com{username}/{repository}/releases/latest";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("User-Agent", "Unity-Update-Checker");
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
            JObject releaseData = JObject.Parse(jsonResponse);
            
            string tagName = releaseData["tag_name"]?.ToString();
            downloadUrl = releaseData["html_url"]?.ToString();

            string currentVersion = Application.version;

            if (tagName != currentVersion)
            {
                TriggerUpdateUI();
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
