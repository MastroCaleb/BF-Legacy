using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Map", menuName = "Dungeon/Map", order = 1)]
public class Map : ScriptableObject
{
    public int missionClearedId;
    public Sprite mapBg;
    public Sprite mapNameIcon;
    public List<DungeonPos> dungeonPosList = new List<DungeonPos>();
}
[System.Serializable]
public struct DungeonPos
{
    public DungeonLevel dungeon;
    public Vector2 pos;
}