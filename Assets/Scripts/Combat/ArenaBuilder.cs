using UnityEngine;
using UnityEngine.UI;

public class ArenaBuilder : MonoBehaviour
{
    public Image backGround;
    public RectTransform backSamParent;
    public RectTransform frontSamParent;

    public Material blendMaterial;

    public void Awake()
    {
        BuildArena();
    }

    public void BuildArena()
    {
        backGround.sprite = BattleManager.dungeonLevelData.bg;
        if(BattleManager.dungeonLevelData.foreGroundSams != null)
        {
            foreach(TextAsset t in BattleManager.dungeonLevelData.backGroundSams)
            {
                AddSam(t, backSamParent);
            }
        }
        if(BattleManager.dungeonLevelData.foreGroundSams != null)
        {
            foreach(TextAsset t in BattleManager.dungeonLevelData.foreGroundSams)
            {
                AddSam(t, frontSamParent);
            }
        }
    }

    public void AddSam(TextAsset json, RectTransform parent)
    {
        GameObject sam = new GameObject("ArenaSam");
        RectTransform r = sam.AddComponent<RectTransform>();
        OldSamAnimator animator = sam.AddComponent<OldSamAnimator>();
        animator.defaultAnimation = "loop";
        animator.isEffect = true;
        animator.jsonFile = json;
        animator.blendMaterial = json.name.Contains("Add") ? blendMaterial : null;
        animator.enabled = true;
        sam.GetComponent<RectTransform>().SetParent(parent);
        animator.InitializeAnimator();
        r.localScale = Vector3.one;
        r.localPosition = Vector3.zero;
    }
}
