using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PulseImage : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Time in seconds for a full fade-out and fade-in cycle.")]
    public float cycleDuration = 1f;

    private Image img;
    private float timer = 0f;

    void Awake()
    {
        img = GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("PulseImage requires an Image component!");
            enabled = false;
        }
    }

    void Update()
    {
        if (cycleDuration <= 0f) return;

        timer += Time.deltaTime;

        // Ping-pong timer between 0 and 1
        float t = Mathf.PingPong(timer / (cycleDuration * 0.5f), 1f);

        // Lerp alpha between 1 (white) and 0 (transparent)
        Color c = img.color;
        c.a = t;
        img.color = c;
    }

    // Optional: reset the animation
    public void ResetPulse()
    {
        timer = 0f;
        Color c = img.color;
        c.a = 1f;
        img.color = c;
    }
}
