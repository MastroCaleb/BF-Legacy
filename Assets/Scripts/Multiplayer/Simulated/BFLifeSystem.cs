using System;
using System.Collections.Generic;
using System.Text;

public class BFLifeSystem
{
    static readonly Random rng = new Random();

    // Temporary unit pool until real selection logic exists
    static readonly string[] UnitPool = { "10011", "20011", "30011", "40011", "50011" };

    public static BotData GenerateBot(int minLevel = 1, int maxLevel = 200, int unitCount = 5)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);

        BotData bot = new BotData
        {
            botId = id,
            botName = BotNameSystem.GetRandomBotName(),
            botLevel = rng.Next(minLevel, maxLevel + 1),
            units = new List<BotUnitData>()
        };

        for (int i = 0; i < unitCount; i++)
        {
            bot.units.Add(GenerateBotUnit(i == 0, minLevel, bot.botLevel));
        }

        return bot;
    }

    public static BotUnitData GenerateBotUnit(bool isMainUnit, int minLevel, int maxLevel)
    {
        return new BotUnitData
        {
            isMainUnit = isMainUnit,
            unitId = PickRandomUnitId(),
            unitLevel = rng.Next(minLevel, maxLevel + 1),
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
        return UnitPool[rng.Next(UnitPool.Length)];
    }
}

public class BotData
{
    public string botId;
    public string botName;
    public int botLevel;
    public List<BotUnitData> units;
}

public class BotUnitData
{
    public bool isMainUnit;
    public string unitId;
    public int unitLevel;
    public string itemId;
}