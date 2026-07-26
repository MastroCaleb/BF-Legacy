using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dialogue", menuName = "NPCs/Dialogue", order = 1)]
public class Dialogue : ScriptableObject
{
    public string NPCName;
    public List<string> lines;
}
