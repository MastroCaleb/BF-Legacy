using UnityEngine;
using UnityEngine.UI;

public class SetAllyButton : MonoBehaviour
{
    public BotData botData;
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

        Debug.Log($"Selected ally bot: {botData.botName} (ID: {botData.botId})");
    }
}