using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class UnitSlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public UnitBehaviour unit;

    public Button button;
    public bool canBeClicked;

    public Sprite fullBarColor;
    public Sprite halfBarColor;
    public Sprite lowBarColor;

    // Long press settings
    public AudioClip holdSound;
    public AudioClip bbSound;
    public AudioClip skillFullSound;
    public float longPressThreshold = 0.4f;

    private bool isPointerDown = false;
    private float pointerDownTime = 0f;
    private bool longPressTriggered = false;

    // Direction detection
    private Vector2 startPos;
    private bool isDraggingAfterLongPress = false;

    // Prevents double-activation (normal + special)
    private bool actionCompleted = false;

    //UI Overlays
    public Image overlay;
    public Sprite normalOverlay;
    public Sprite clickedOverlay;
    public Sprite deadOverlay;
    public GameObject deadText;
    public GameObject emptyText;
    public GameObject nameBackground;

    //Unit Data Related
    public TextMeshProUGUI unitNameText;
    public Image unitIcon;
    Vector2 originalIconPosition;
    public Image elementIcon;
    public ImageToFont healthText;
    public BarUI healthBar;
    public BarUI bcBar;
    public GameObject bbSlotAnimation;
    public GameObject bbBarAnimation;

    public BattleManager battleManager;

    private bool suppressNextClick = false;

    void Start()
    {
        originalIconPosition = unitIcon.rectTransform.anchoredPosition;
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnSlotClicked);
    }

    private void OnSlotClicked()
    {
        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if(unit == null) return;

        Debug.Log($"Clicked on unit: {unit.unitData.unitName}");

        if (unit.currentState == UnitState.Idle && canBeClicked && !actionCompleted)
        {
            actionCompleted = true;
            canBeClicked = false;
            battleManager.PlayerIssuedCommand(unit);
        }
    }

    void Update()
    {
        SlotState();
        HandleLongPressCheck();
    }

    public void UpdateUI()
    {
        if (unit == null) return;
        unitNameText.text = unit.unitData.unitName;
        unitIcon.sprite = unit.unitData.unitIcon;
        healthBar.maxValue = unit.unitData.maxHealth + unit.inventoryData.hpLevelUpBonus + unit.inventoryData.hpImpBonus;
        healthBar.currentValue = unit.currentHealth;
        BarColor();
        bcBar.maxValue = unit.unitData.bbAbility != null ? unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost : 1;
        bcBar.currentValue = unit.bcCount;
        healthText.SetText($"{unit.currentHealth}/{unit.unitData.maxHealth + unit.inventoryData.hpLevelUpBonus + unit.inventoryData.hpImpBonus}");

        if(healthBar.currentValue != healthBar.maxValue)
            healthBar.UpdateUI();

        if(bcBar.currentValue != bcBar.maxValue)
        {
            bcBar.UpdateUI();
        }
        else
        {
            SoundManager.Instance.PlaySound(skillFullSound);
        }
            
        if (bcBar.currentValue >= bcBar.maxValue)
        {
            bbBarAnimation.SetActive(true);
            bbSlotAnimation.SetActive(true);
        }
        else
        {
            bbBarAnimation.SetActive(false);
            bbSlotAnimation.SetActive(false);
        }
    }

    void BarColor()
    {
        if (healthBar.currentValue > healthBar.maxValue * 0.5f)
        {
            healthBar.fillImage.sprite = fullBarColor;
        }
        else if (healthBar.currentValue <= healthBar.maxValue * 0.5f && healthBar.currentValue > healthBar.maxValue * 0.2f)
        {
            healthBar.fillImage.sprite = halfBarColor;
        }
        else if (healthBar.currentValue <= healthBar.maxValue * 0.2f)
        {
            healthBar.fillImage.sprite = lowBarColor;
        }
    }

    void SlotState()
    {
        if (unit == null)
        {
            canBeClicked = false;
            overlay.sprite = deadOverlay;
            elementIcon.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(false);
            bcBar.gameObject.SetActive(false);
            healthText.gameObject.SetActive(false);
            unitNameText.gameObject.SetActive(false);
            unitIcon.gameObject.SetActive(false);
            emptyText.SetActive(true);
            nameBackground.SetActive(false);
            return;
        }

        if (unit.currentState == UnitState.Dead)
        {
            deadText.SetActive(true);
            overlay.sprite = deadOverlay;
            elementIcon.sprite = BattleUI.elementDisableIcons[(int)unit.unitData.element];
        }
        else
        {
            deadText.SetActive(false);
            if (canBeClicked)
            {
                overlay.sprite = normalOverlay;
                elementIcon.sprite = BattleUI.elementIcons[(int)unit.unitData.element];
            }
            else
            {
                overlay.sprite = clickedOverlay;
                elementIcon.sprite = BattleUI.elementDisableIcons[(int)unit.unitData.element];
            }
        }
    }

    void HandleLongPressCheck()
    {
        // Only run long press detection while pointer is down, not already completed, and unit is idle
        if (isPointerDown && !longPressTriggered && !actionCompleted && unit.currentState == UnitState.Idle)
        {
            if (Time.unscaledTime - pointerDownTime >= longPressThreshold)
            {
                // Only trigger long-press UI if BC is sufficient for special
                longPressTriggered = true;
                OnLongPress();
            }
        }
    }

    private void OnLongPress()
    {
        SoundManager.Instance.PlayLoopingSound(holdSound);
        Debug.Log("Long Press Triggered on " + unit.unitData.unitName);
        suppressNextClick = true; 
        RectTransform parentRect = BattleUI.slider.parent.GetComponent<RectTransform>();
        Canvas canvas = parentRect.GetComponentInParent<Canvas>();

        // pick the camera correctly:
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        // convert screen point to local point in parent's space
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, cam, out localPoint);

        // set anchoredPosition (local UI coordinates)
        BattleUI.slider.anchoredPosition = localPoint;
        if(unit.bcCount >= unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost)
        {
            BattleUI.bbSlider.SetActive(true);
            BattleUI.guardSlider.SetActive(false);
        }
        else
        {
            BattleUI.bbSlider.SetActive(false);
            BattleUI.guardSlider.SetActive(true);
        }
        BattleUI.slider.gameObject.SetActive(true);

        isDraggingAfterLongPress = true;

        // Do NOT change actionCompleted yet; only set when special is actually executed
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingAfterLongPress || actionCompleted)
            return;

        Vector2 dragVector = eventData.position - startPos;

        // Min distance before it's considered a direction
        if (dragVector.magnitude < 30f)
            return;

        // Normalize direction
        Vector2 dir = dragVector.normalized;

        // Determine major direction
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x > 0)
                OnSlideDirection(Vector2.right);
            else
                OnSlideDirection(Vector2.left);
        }
        else
        {
            if (dir.y > 0)
                OnSlideDirection(Vector2.up);
            else
                OnSlideDirection(Vector2.down);
        }
    }

    private void OnSlideDirection(Vector2 direction)
    {
        Debug.Log("Slide direction: " + direction);

        // EXAMPLE: Up triggers a special ability
        if (direction == Vector2.up)
        {
            // Double-check BC and state before executing special
            if (unit.bcCount >= unit.unitData.bbAbility.levels[unit.inventoryData.currentBBLevel-1].bcCost && unit.currentState == UnitState.Idle && canBeClicked)
            {
                SoundManager.Instance.PlaySound(bbSound);
                actionCompleted = true; // prevent normal click from happening after this
                canBeClicked = false;
                battleManager.PlayerSlideCommand(unit, direction, unit.unitData.bbAbility);
            }
        }
        else if (direction == Vector2.right)
        {
            // Double-check BC and state before executing special
            if (unit.bcCount >= unit.unitData.sbbAbility.levels[unit.inventoryData.currentSBBLevel-1].bcCost && unit.currentState == UnitState.Idle && canBeClicked)
            {
                SoundManager.Instance.PlaySound(bbSound);
                actionCompleted = true; // prevent normal click from happening after this
                canBeClicked = false;
                battleManager.PlayerSlideCommand(unit, direction, unit.unitData.sbbAbility);
            }
        }
        else if (direction == Vector2.left)
        {
            if(!unit.isInOverdrive && BattleManager.ubbCount == BattleManager.ubbCurrentMaxGauge && unit.currentState == UnitState.Idle && canBeClicked)
            {
                SoundManager.Instance.PlaySound(bbSound);
                actionCompleted = true; // prevent normal click from happening after this
                canBeClicked = false;
                unit.Overdrive();
            }
            else if (unit.bcCount >= unit.unitData.ubbAbility.levels[0].bcCost && unit.isInOverdrive && unit.currentState == UnitState.Idle && canBeClicked)
            {
                SoundManager.Instance.PlaySound(bbSound);
                actionCompleted = true; // prevent normal click from happening after this
                canBeClicked = false;
                battleManager.PlayerSlideCommand(unit, direction, unit.unitData.ubbAbility);
            }
        }
        else if(direction == Vector2.down)
        {
            actionCompleted = true; // prevent normal click from happening after this
            canBeClicked = false;
            battleManager.PlayerGuardCommand(unit, direction);
        }

        // After a direction is chosen, close slider and reset pointer flags for this input
        longPressTriggered = false;
        isPointerDown = false;
        isDraggingAfterLongPress = false;
        BattleUI.slider.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (canBeClicked == false || unit.currentState == UnitState.Dead)
            return;

        isPointerDown = true;
        longPressTriggered = false;
        isDraggingAfterLongPress = false;
        suppressNextClick = false; // nuova interazione, reset del flag

        startPos = eventData.position;
        pointerDownTime = Time.unscaledTime;

        actionCompleted = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SoundManager.Instance.StopLoopingSound();
        bool wasDragging = isDraggingAfterLongPress; // capture before reset

        isPointerDown = false;
        longPressTriggered = false;
        isDraggingAfterLongPress = false;
        BattleUI.slider.gameObject.SetActive(false);

        // Only fire normal click if no drag/long-press gesture was involved at all
        if (!wasDragging && !actionCompleted && canBeClicked && unit.currentState != UnitState.Dead)
        {
            //OnSlotClicked();
        }

        actionCompleted = false;
    }

    public IEnumerator ShakeIcon(float duration, float strength)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = originalIconPosition.x + Random.Range(-strength, strength);
            float y = originalIconPosition.y + Random.Range(-strength, strength);
            unitIcon.rectTransform.anchoredPosition = new Vector2(x, y);
            yield return null;
        }

        unitIcon.rectTransform.anchoredPosition = originalIconPosition;
    }

}
