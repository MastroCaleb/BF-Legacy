using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShakeDmgText : MonoBehaviour
{
    RectTransform rect;
    ImageToFont img;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        img = GetComponent<ImageToFont>();
    }

    public void PlayEffect(Vector3 pos, Vector3 scale, Color color)
    {
        ShakeDmgTextManager.Instance.Play(rect, img, pos, scale, color);
    }
}

