using System;
using System.Collections.Generic;
using System.Text;

public class BFLifeSystem
{
    //Need this to simulate bot progress.

    //we need this for:
    //Having bots as friends to use their main unit as a friend unit in battle (the 6th slot)
    //Having bots as enemies in the Arena in pvp battles, using a system to calculate their combat power and balance the pvp aspect of the game
    //Create bot teams that make sense, so attacker, tank, healer, support attack etc and also equipped items (lower levels doesnt need items)
    //Having bots as helpers in the raid battle system where they help the player beat a powerful boss
    
    
}
struct BotData
{
    public string botId;
    public string botName;
    public int botLevel;
    public List<BotUnitData> units;
}
struct BotUnitData
{
    public bool isMainUnit;
    public string unitId;
    public int unitLevel;
    public string itemId;
}