using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText;

    private float timer;
    private int frameCount;

    void Update()
    {
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= 1f)
        {
            int fps = Mathf.RoundToInt(frameCount / timer);
            fpsText.text = "FPS: " + fps;

            frameCount = 0;
            timer = 0f;
        }
    }
}
