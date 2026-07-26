using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FileLoadUI : MonoBehaviour
{
    public TextMeshProUGUI loadingStateText;
    public BarUI loadingBar;
    public TextMeshProUGUI loadingPercentageText;

    public void Awake()
    {
        loadingBar.currentValue = 0;
        loadingBar.maxValue = 100;
        string playerDataPath = Application.persistentDataPath + "/playerdata.json";
        string inventoryDataPath = Application.persistentDataPath + "/unitinventory.json";
        string partyDataPath = Application.persistentDataPath + "/parties.json";

        if(System.IO.File.Exists(playerDataPath) && System.IO.File.Exists(inventoryDataPath) && System.IO.File.Exists(partyDataPath))
        {
            loadingStateText.text = "Loading Player Data...";
            loadingPercentageText.text = "0%";
            StartCoroutine(StartLoading());
        }
        else
        {
            loadingStateText.text = "Player Data Not Found";
            loadingBar.currentValue = 0;
            loadingPercentageText.text = "0%";
            StartCoroutine(SimulateDataLoad(0, 100));
            SceneManager.LoadScene("ProfileCreationScene");
        }
    }

    IEnumerator StartLoading()
    {
        yield return new WaitForSeconds(1f);
        
        // Load Player Data (0-33%)
        loadingStateText.text = "Loading Player Data...";
        PlayerData.LoadDataFromJson();
        yield return StartCoroutine(SimulateDataLoad(0, 33));

        yield return new WaitForSeconds(1f);
        
        // Load Inventory Data (33-66%)
        loadingStateText.text = "Loading Inventory Data...";
        PlayerUnitInventoryDatabase.LoadFromJson();
        yield return StartCoroutine(SimulateDataLoad(33, 66));
        
        yield return new WaitForSeconds(1f);
        
        // Load Party Data (66-100%)
        loadingStateText.text = "Loading Party Data...";
        PartyDatabase.LoadFromJson();
        yield return StartCoroutine(SimulateDataLoad(66, 100));
        
        loadingBar.currentValue = 100;
        loadingPercentageText.text = "100%";
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("MainMenuScene");
    }

    IEnumerator SimulateDataLoad(int startProgress, int endProgress)
    {
        for (int i = startProgress; i <= endProgress; i++)
        {
            loadingBar.currentValue = i;
            loadingPercentageText.text = i + "%";
            loadingBar.UpdateUI();
            yield return null;
        }
    }
}
