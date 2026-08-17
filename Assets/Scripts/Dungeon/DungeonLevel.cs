using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dungeon Level", menuName = "Dungeon/Dungeon Level")]
public class DungeonLevel : ScriptableObject
{
    public string levelName;
    public List<Mission> missions;
    public Sprite bg;
    public List<TextAsset> backGroundSams;
    public List<TextAsset> foreGroundSams;
    public AudioClip bgm;
    public Enemy mimicEnemyData;
    public float mimicChance;
}

