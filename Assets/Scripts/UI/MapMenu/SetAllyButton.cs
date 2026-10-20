using UnityEngine;
using UnityEngine.UI;

public class SetAllyButton : MonoBehaviour
{
    public BotData botData = null;
    Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    public void OnButtonClicked()
    {
        BattleManager.selectedAllyBot = botData;

        if(botData == null)
        {
            Debug.Log("Selected ally bot: None");
            return;
        }
        
        Debug.Log($"Selected ally bot: {botData.botName} (ID: {botData.botId})");
    }
}