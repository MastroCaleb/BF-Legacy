using UnityEngine;
using UnityEngine.UI;

public class AutoBattleButtonUI : MonoBehaviour
{
    public Button button;
    public bool canBeClicked = true;

    public Sprite autoBattleOnSprite;
    public Sprite autoBattleOffSprite;

    void Start()
    {
        button = GetComponent<Button>();
        
        if (button != null){
            UpdateButtonVisual();
            button.onClick.AddListener(OnSlotClicked);
        }
    }

    private void OnSlotClicked()
    {
        if(CutInAnimation.isPlaying) return;
        if (canBeClicked)
        {
            BattleManager.isCombatAutomatic = !BattleManager.isCombatAutomatic;
        }

        UpdateButtonVisual();
    }

    public void UpdateButtonVisual()
    {
        if (BattleManager.isCombatAutomatic)
        {
            button.image.sprite = autoBattleOnSprite;
        }
        else
        {
            button.image.sprite = autoBattleOffSprite;
        }
    }
}
