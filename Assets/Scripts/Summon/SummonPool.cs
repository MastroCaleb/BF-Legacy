using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Summon Pool", menuName = "Summon/Summon Pool")]
public class SummonPool : ScriptableObject
{
    public string poolName;
    public List<string> poolUnitKeys;
}
