using UnityEngine;

public class DropAnimation : MonoBehaviour
{
    [Header("Vertical Movement")]
    public float initialDownVelocity = -600f;
    public float bounceOffsetY = 50f;

    [Header("Horizontal Movement")]
    public float minTargetX = -150f;
    public float maxTargetX = 75f;

    [Header("Rotation")]
    public float startRotationZ = 90f;

     void Start()
    {
        RectTransform rect = GetComponent<RectTransform>();
        Vector3 pos = rect.localPosition;

        float bounceY = pos.y - Random.Range(0f, bounceOffsetY);
        float targetX = pos.x + Random.Range(minTargetX, maxTargetX);

        rect.localEulerAngles = new Vector3(0f, 0f, startRotationZ);

        DropData drop = new DropData
        {
            rect = rect,
            velocityY = initialDownVelocity,
            bounceY = bounceY,
            targetX = targetX,
            rotationZ = startRotationZ,
            isBouncing = true
        };

        DropManager.Instance.Register(drop);

        // Optional: disable this component permanently
        enabled = false;
    }
}
public struct DropData
{
    public RectTransform rect;

    // vertical
    public float velocityY;
    public float bounceY;
    public bool isBouncing;

    // horizontal
    public float targetX;

    // rotation
    public float rotationZ;
}

