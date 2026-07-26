using UnityEngine;

public class DebugManager : MonoBehaviour
{
    // Singleton instance
    public static DebugManager instance;

    // Variables
    public bool isDebugMode = true;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Log(string message)
    {
        Debug.Log(message);
    }
}
