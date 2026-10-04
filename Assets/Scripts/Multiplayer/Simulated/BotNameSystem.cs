using System;
using System.Collections.Generic;
using System.Text;

public class BotNameSystem
{
    private static readonly Random _random = new Random();

    private static readonly string[] Vocals = { "a", "e", "i", "o", "u", "ae", "ou" };
    private static readonly string[] StartConsonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z", "ch", "sh", "br", "cr", "dr", "fr", "gr", "pr", "tr" };
    private static readonly string[] EndConsonants = { "b", "d", "f", "g", "k", "l", "m", "n", "p", "r", "s", "t", "x", "z", "th", "nt", "st", "rd" };

    private static readonly List<string> AnimePrefixes = new List<string>
    {
        "Kirito", "Sasuke", "Goku", "Naruto", "Luffy", "Zoro", "Sephiroth", "Cloud", "Rex", "Sora",
        "Ichigo", "Rukia", "Aizen", "Gojo", "Sukuna", "Yuji", "Megumi", "Levi", "Eren", "Mikasa",
        "Kakashi", "Itachi", "Madara", "Minato", "Obito", "Shanks", "Ace", "Law", "Sanji", "Nami",
        "Vegeta", "Trunks", "Broly", "Frieza", "Gohan", "Killua", "Gon", "Hisoka", "Kurapika", "Chrollo",
        "Meliodas", "Escanor", "Ban", "Rimuru", "Anos", "Asta", "Yuno", "Tanjiro", "Nezuko", "Zenitsu",
        "Inosuke", "Rengoku", "Giyu", "Yami", "Jotaro", "Dio", "Joseph", "Edward", "Alphonse", "Roy",
        "Spike", "Vash", "Alucard", "Shinra", "Arthur", "Cid", "Shadow", "Naofumi", "Raphtalia", "Subaru"
    };

    private static readonly List<string> AnimeSuffixes = new List<string>
    {
        "Kun", "San", "Chan", "Uchiha", "Uzumi", "X", "God", "VGC", "King",
        "Sama", "Senpai", "Dono", "Sensei", "Chi", "Neko", "Fox", "Wolf",
        "Phoenix", "Dragon", "Titan", "Prime", "Zero", "EX", "Pro", "Elite",
        "Nova", "Omega", "Alpha", "Sigma", "VII", "XII", "RX", "DX", "Reborn",
        "Legend", "Myth", "Supreme", "Noir", "Blaze", "Storm", "Frost", "Soul",
        "Void", "Chaos", "Eclipse", "Infinity", "V2", "Ultimate", "Origin", "One"
    };

    private static readonly List<string> RpgWords = new List<string>
    {
        "Shadow", "Dark", "Light", "Alpha", "Omega", "Zero", "Ghost", "Neo",
        "Nova", "Luna", "Slayer", "Blade", "Knight", "Rogue", "Mage", "Chaos",
        "Apex", "Zenith", "Frost", "Flame",

        "Storm", "Thunder", "Inferno", "Glacier", "Tempest", "Phantom", "Specter",
        "Void", "Celestial", "Astral", "Solar", "Lunar", "Blood", "Venom",
        "Ember", "Crystal", "Obsidian", "Emerald", "Ruby", "Sapphire", "Ivory",
        "Onyx", "Titan", "Warden", "Paladin", "Assassin", "Warlock", "Necro",
        "Berserker", "Sentinel", "Champion", "Oracle", "Vanguard", "Reaper",
        "Dread", "Valor", "Destiny", "Fury", "Wrath", "Honor", "Mythic",
        "Arcane", "Eclipse", "Mirage", "Tempest", "Echo", "Vortex", "Rift",
        "Horizon", "Cipher", "Infernal", "Heaven", "Hellfire", "Dragon",
        "Wolf", "Lion", "Raven", "Falcon", "Seraph", "Demon", "Angel"
    };

    private static readonly List<string> CasualNames = new List<string>
    {
        "Alex", "Chris", "Sam", "Daniel", "Ryan", "Kevin", "Jessica", "Emily",
        "Kyle", "Matt", "Leon", "Claire", "Marcus", "Ethan", "Chloe", "Luke",
        "Ash", "Red", "Blue",

        "Nathan", "Noah", "Liam", "Jack", "Oliver", "Jacob", "Mason", "Logan",
        "Tyler", "Connor", "Jason", "Adam", "Ben", "Dylan", "Cole", "Aaron",
        "Eric", "Brandon", "Sean", "Cameron", "Zach", "Justin", "Trevor", "Scott",
        "Aiden", "Finn", "Owen", "Mia", "Emma", "Sophia", "Olivia", "Grace",
        "Lily", "Sarah", "Anna", "Lucy", "Ella", "Zoe", "Hannah", "Bella",
        "Ruby", "Ava", "Natalie", "Kate", "Rose", "Nina", "Maya", "Kai"
    };

    private static readonly List<string> BfLore = new List<string>
    {
        "Vargas", "Selena", "Lance", "Eze", "Atro", "Magress", "Tilith",
        "Maxwell", "Lucius", "Karna",

        "Karl", "Paris", "Lugina", "Elza", "Lilly", "Edea", "Zelnite",
        "Zeln", "Feeva", "Avant", "Rize", "Regil", "Shera", "Fiora",
        "Zeruiah", "Kulyuk", "Grahdens", "Avant", "Ark", "Sefia",
        "Kikuri", "Mifune", "Cayena", "Lario", "Zekuu", "Arius",
        "Farlon", "Michele", "Tesla", "Orna", "Felice", "Elimo",
        "Grah", "Hadaron", "Ark", "Rhodine", "XieJing", "Vern", "Ragshelm"
    };

    private static readonly string[] Adjectives =
    {
        "Grand", "Divine", "Holy", "Sacred", "Cursed", "Ancient", "Fallen",
        "Eternal", "Iron", "Steel", "Golden", "Silver", "Shadow", "Crimson",
        "Azure", "Abyssal", "Radiant", "Silent", "Savage", "Noble",

        "Mystic", "Arcane", "Celestial", "Infernal", "Frozen", "Burning",
        "Thunderous", "Stormborn", "Fearless", "Brutal", "Merciless",
        "Forgotten", "Hidden", "Lost", "Legendary", "Mythic", "Royal",
        "Imperial", "Glorious", "Fearsome", "Luminous", "Vengeful",
        "Relentless", "Swift", "Deadly", "Wild", "Prime", "Supreme",
        "Unbroken", "Boundless", "Infinite", "Spectral", "Ghostly",
        "Titanic", "Obsidian", "Emerald", "Ivory", "Runic", "Blessed"
    };

    private static readonly string[] Nouns =
    {
        "Hero", "Summoner", "Guardian", "Beast", "Dragon", "Lord", "King",
        "Wolf", "Hunter", "Soul", "Spirit", "Blade", "Shield", "Heart",
        "Sage", "Emperor", "Fiend", "Titan", "God", "Slayer",

        "Champion", "Warden", "Paladin", "Knight", "Ranger", "Assassin",
        "Warrior", "Samurai", "Ronin", "Monarch", "Overlord", "Oracle",
        "Phoenix", "Griffin", "Hydra", "Serpent", "Lion", "Falcon",
        "Raven", "Tempest", "Reaper", "Avenger", "Conqueror", "Sentinel",
        "Invoker", "Archon", "Executioner", "Protector", "Destroyer",
        "Commander", "Seeker", "Nomad", "Pilgrim", "Prophet", "Defender",
        "Wanderer", "Legend", "Myth", "Phantom", "Specter"
    };

    public static string GetRandomBotName()
    {
        int rolledType = _random.Next(0, 100);
        string baseName = "";

        if (rolledType < 33)       
            baseName = GeneratePhoneticName(); 
        else if (rolledType < 66)  
            baseName = GenerateThematicName(); 
        else                       
            baseName = GenerateTitleNounName();  

        if (_random.Next(0, 100) < 45)
        {
            baseName = ApplyGamerStyle(baseName);
        }

        return baseName;
    }

    private static string ApplyGamerStyle(string name)
    {
        int style = _random.Next(0, 6);

        switch (style)
        {
            case 0:
                return $"xX_{name}_Xx";

            case 1: 
                string[] suffixes = { "VGC", "YT", "TTV", "Pro", "Gamer", "PvP" };
                string separator = _random.Next(0, 2) == 0 ? "_" : "";
                return name + separator + suffixes[_random.Next(suffixes.Length)];

            case 2: 
                int[] sillyNumbers = { 69, 420, 123, 99, 777, 00, 11 };
                return name + sillyNumbers[_random.Next(sillyNumbers.Length)];

            case 3: 
                StringBuilder leetName = new StringBuilder();
                foreach (char c in name)
                {
                    char upperC = char.ToUpper(c);
                    if (upperC == 'A' && _random.Next(0, 2) == 0) leetName.Append('4');
                    else if (upperC == 'E' && _random.Next(0, 2) == 0) leetName.Append('3');
                    else if (upperC == 'I' && _random.Next(0, 2) == 0) leetName.Append('1');
                    else if (upperC == 'O' && _random.Next(0, 2) == 0) leetName.Append('0');
                    else if (upperC == 'S' && _random.Next(0, 2) == 0) leetName.Append('5');
                    else leetName.Append(c);
                }
                return leetName.ToString();

            case 4:
                return $"__{name}__";

            case 5:
                return name + _random.Next(100, 9999).ToString();

            default:
                return name;
        }
    }

    public static string GeneratePhoneticName()
    {
        StringBuilder name = new StringBuilder();
        int syllablesCount = _random.Next(2, 4); 

        for (int i = 0; i < syllablesCount; i++)
        {
            bool isCvc = _random.Next(0, 100) < 30;

            name.Append(StartConsonants[_random.Next(StartConsonants.Length)]);
            name.Append(Vocals[_random.Next(Vocals.Length)]);
            
            if (isCvc || (i == syllablesCount - 1 && _random.Next(0, 100) < 50))
            {
                name.Append(EndConsonants[_random.Next(EndConsonants.Length)]);
            }
        }

        string result = name.ToString();
        if (string.IsNullOrEmpty(result)) return "Bot";
        
        // Corretto il modo in cui viene resa maiuscola la prima lettera
        return char.ToUpper(result[0]) + result.Substring(1);
    }

    public static string GenerateThematicName()
    {
        int style = _random.Next(0, 5);

        switch (style)
        {
            case 0: 
                return AnimePrefixes[_random.Next(AnimePrefixes.Count)] + AnimeSuffixes[_random.Next(AnimeSuffixes.Count)];
            case 1: 
                return RpgWords[_random.Next(RpgWords.Count)] + RpgWords[_random.Next(RpgWords.Count)];
            case 2: 
                string sep = _random.Next(0, 2) == 0 ? "_" : "";
                string num = _random.Next(0, 2) == 0 ? _random.Next(10, 99).ToString() : _random.Next(1995, 2008).ToString();
                return CasualNames[_random.Next(CasualNames.Count)] + sep + num;
            case 3: 
                return _random.Next(0, 2) == 0 
                    ? BfLore[_random.Next(BfLore.Count)] + RpgWords[_random.Next(RpgWords.Count)]
                    : RpgWords[_random.Next(RpgWords.Count)] + BfLore[_random.Next(BfLore.Count)];
            default: 
                return RpgWords[_random.Next(RpgWords.Count)];
        }
    }

    public static string GenerateTitleNounName()
    {
        string adjective = Adjectives[_random.Next(Adjectives.Length)];
        string noun = Nouns[_random.Next(Nouns.Length)];

        int formatStyle = _random.Next(0, 2);
        if (formatStyle == 0)
            return adjective + noun; 
        else
            return adjective + "_" + noun; 
    }
}
