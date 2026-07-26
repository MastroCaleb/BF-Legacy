using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
