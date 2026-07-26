using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RandomStartBackground : MonoBehaviour
{
    public List<Sprite> backgroundSprites;

    private void Start()
    {
        if (backgroundSprites != null && backgroundSprites.Count > 0)
        {
            Image image = GetComponent<Image>();
            if (image != null)
            {
                int randomIndex = Random.Range(0, backgroundSprites.Count);
                image.sprite = backgroundSprites[randomIndex];
            }
        }
    }
}
