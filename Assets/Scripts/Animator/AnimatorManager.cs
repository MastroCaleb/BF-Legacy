using UnityEngine;

public class AnimatorManager : MonoBehaviour
{
    //Yes this is just for this dont judge me
    public Material blendMaterialHelper;
    public static Material blendMaterial;

    void Awake()
    {
        blendMaterial = blendMaterialHelper;
    }
}
