using System;
using System.Collections.Generic;
using System.Text;

public class BFLifeSystem
{
    static readonly Random rng = new Random();

    public static BotData GenerateBot(int minLevel = 1, int maxLevel = 200, int unitCount = 5)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);

        BotData bot = new BotData
        {
            botId = id,
            botName = BotNameSystem.GetRandomBotName(),
            botLevel = rng.Next(minLevel, maxLevel + 1),
            party = new BotPartyData
            {
                mainUnitIndex = 0,
                units = new List<BotUnitData>()
            }
        };

        for (int i = 0; i < unitCount; i++)
        {
            bot.party.units.Add(GenerateBotUnit());
        }

        return bot;
    }

    public static BotUnitData GenerateBotUnit()
    {
        string unitId = PickRandomUnitId();
        return new BotUnitData
        {
            unitId = unitId,
            unitLevel = rng.Next(1, UnitRegistry.GetUnitById(unitId).maxLevel),
            itemId = ""
        };
    }

    public static List<BotData> GenerateBots(int count, int minLevel = 1, int maxLevel = 100, int unitCount = 5)
    {
        List<BotData> bots = new List<BotData>(count);
        for (int i = 0; i < count; i++)
            bots.Add(GenerateBot(minLevel, maxLevel, unitCount));
        return bots;
    }

    static string PickRandomUnitId()
    {
        return UnitDataDb.GetRandomUnitRow()?.UnitId ?? "10011" ; // fallback to a default unit ID if none found
    }
}
[Serializable]
public class BotData
{
    public string botId;
    public string botName;
    public int botLevel;
    public BotPartyData party;

    public bool IsNull => string.IsNullOrEmpty(botId);
}
[Serializable]
public class BotUnitData
{
    public string unitId;
    public int unitLevel;
    public int bbLevel;
    public int sbbLevel;
    public string itemId;
    public string itemId2;
}
[Serializable]
public class BotPartyData
{
    public int mainUnitIndex;
    public List<BotUnitData> units;
}