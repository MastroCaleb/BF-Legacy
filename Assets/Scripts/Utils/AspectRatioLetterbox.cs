using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    [Tooltip("Target aspect ratio width component (e.g. 9 for 9:16)")]
    public float targetWidth = 9f;

    [Tooltip("Target aspect ratio height component (e.g. 16 for 9:16)")]
    public float targetHeight = 16f;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        #if UNITY_ANDROID || UNITY_IOS
            enabled = false; // let mobile use native screen, no forced letterboxing
        #endif
    }

    void OnDisable()
    {
        if (cam != null)
            cam.rect = new Rect(0, 0, 1, 1);
    }

    void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyLetterbox();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    void ApplyLetterbox()
    {
        float targetAspect = targetWidth / targetHeight;
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect = cam.rect;

        if (scaleHeight < 1.0f)
        {
            // Window is narrower/taller than target -> letterbox top/bottom
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        }
        else
        {
            // Window is wider than target -> pillarbox left/right
            float scaleWidth = 1.0f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }

        cam.rect = rect;
    }
}