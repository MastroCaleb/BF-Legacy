using UnityEngine;

public class FPSUnlocker : MonoBehaviour
{
    void Awake()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void Update()
    {
        // Force Android to treat this as continuously animating
        GL.InvalidateState();
    }
}
