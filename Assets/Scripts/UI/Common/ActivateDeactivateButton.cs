using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActivateDeactivateButton : MonoBehaviour
{
    public GameObject objectToActivate;
    public List<GameObject> objectsToActivate;
    public GameObject objectToDeactivate;
    public List<GameObject> objectsToDeactivate;

    void Start()
    {
        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonPressed);
        }
    }

    public void OnButtonPressed()
    {
        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
        }

        if (objectsToActivate != null && objectsToActivate.Count > 0)
        {
            foreach (var obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }

        if (objectToDeactivate != null)
        {
            objectToDeactivate.SetActive(false);
        }

        if (objectsToDeactivate != null && objectsToDeactivate.Count > 0)
        {
            foreach (var obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
}
