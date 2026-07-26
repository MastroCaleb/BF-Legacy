using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class MenuSectionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public GameObject sectionToActivate;
    public List<GameObject> sectionsToDeactivate;
    public AudioClip music;

    public GameObject mainMenuObject;
    public List<GameObject> objectsInMenu;

    public Sprite activeSprite;
    public Sprite inactiveSprite;

    public bool rotateIfActive = false;
    public bool resetNavigationInMenu = true;
    public GameObject navigationResetTarget;

    private bool isActive = false;
    private Quaternion originalRotation;
    private Image buttonImage;
    private RectTransform rectTransform;

    [System.Obsolete]
    void Start()
    {
        buttonImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        originalRotation = rectTransform.rotation;

        if (buttonImage != null)
            buttonImage.sprite = inactiveSprite;

        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnButtonPressed);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonImage != null)
            buttonImage.sprite = activeSprite;

        if (rotateIfActive)
            rectTransform.rotation = Quaternion.Euler(0, 0, 90);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (buttonImage != null)
            buttonImage.sprite = inactiveSprite;

        isActive = false;

        if (!isActive)
            rectTransform.rotation = originalRotation;
    }

    [System.Obsolete]
    public void OnButtonPressed()
    {
        if (resetNavigationInMenu) ResetNavigation();

        isActive = true;

        SoundManager.Instance.PlayMusicLoop(music);

        if (sectionToActivate != null)
            sectionToActivate.SetActive(true);

        if (mainMenuObject != null)
            mainMenuObject.SetActive(true);

        foreach (var section in sectionsToDeactivate)
        {
            if (section != null)
                section.SetActive(false);
        }

        if(objectsInMenu != null)
        {
            foreach (var g in objectsInMenu)
            {
                if (g != null)
                    g.SetActive(false);
            }
        }

        foreach (var button in FindObjectsOfType<MenuSectionButton>())
        {
            if (button != this)
            {
                button.isActive = false;

                if (button.buttonImage != null)
                    button.buttonImage.sprite = button.inactiveSprite;

                button.rectTransform.rotation = button.originalRotation;
            }
        }
    }

    public void ResetNavigation()
    {
        foreach (Transform child in transform)
            child.gameObject.SetActive(false);

        if (navigationResetTarget != null)
            navigationResetTarget.SetActive(true);
    }
}