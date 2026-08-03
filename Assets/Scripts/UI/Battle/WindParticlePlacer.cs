using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WindParticlePlacer : MonoBehaviour
{
    public int numberOfParticles = 10;
    public Sprite windParticleSprite;

    // Placement extents (half-width / half-height) in local units
    public float xWidth = 10f;
    public float yHeight = 5f;

    [Header("UI Mode")]
    public bool placeAsUI = true;
    public RectTransform parentRect; // optional - will find Canvas if null

    [Header("Scale / Stretch")]
    public float baseScaleMin = 0.5f;
    public float baseScaleMax = 1.5f;
    public float xStretchMin = 1.2f; // ensure X is larger than Y
    public float xStretchMax = 2.0f;

    [Header("Misc")]
    public int sortingOrder = 10; // used for SpriteRenderer fallback

    private List<GameObject> placedParticles = new List<GameObject>();

    public void CreateWind()
    {
        ClearWind();

        if (windParticleSprite == null)
        {
            Debug.LogWarning("WindParticlePlacer: No sprite assigned.");
            return;
        }

        for (int i = 0; i < numberOfParticles; i++)
        {
            float xPos = Random.Range(-xWidth, xWidth);
            float yPos = Random.Range(-yHeight, yHeight);

            float baseScale = Random.Range(baseScaleMin, baseScaleMax);
            float xStretch = Random.Range(xStretchMin, xStretchMax);

            GameObject go = new GameObject("WindParticle_UI_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            Image img = go.GetComponent<Image>();
            img.sprite = windParticleSprite;
            img.raycastTarget = false;

            // Position in local UI coordinates
            rt.anchoredPosition = new Vector2(xPos, yPos);

            // Stretch X > Y
            rt.localScale = new Vector3(baseScale * xStretch, baseScale, 1f);
            placedParticles.Add(go);
        }
    }

    void ClearWind()
    {
        foreach (var go in placedParticles)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        placedParticles.Clear();
    }
}
