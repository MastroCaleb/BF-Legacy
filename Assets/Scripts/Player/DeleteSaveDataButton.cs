using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class DeleteSaveDataButton : MonoBehaviour
{
    public Button button;


    private void Start()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        DeleteAllSaveData();
    }

    public void DeleteAllSaveData()
    {
        string path = Application.persistentDataPath;

        if (!Directory.Exists(path)) return;

        string[] files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to delete {file}: {e.Message}");
            }
        }

        Debug.Log($"Deleted {files.Length} save file(s) from {path}");
    }
}