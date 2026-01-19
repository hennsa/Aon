namespace Aon.Core;

public interface ISeriesProfile
{
    string Name { get; }
    IReadOnlyDictionary<string, int> CoreSkills { get; }
    IReadOnlyList<string> SkillNames { get; }
    IReadOnlyDictionary<string, int> DefaultCounters { get; }
    IReadOnlyList<Item> DefaultItems { get; }
}

public static class SeriesProfiles
{
    public static ISeriesProfile LoneWolf { get; } = new LoneWolfProfile();
    public static ISeriesProfile GreyStar { get; } = new GreyStarProfile();
    public static ISeriesProfile FreewayWarrior { get; } = new FreewayWarriorProfile();
}

internal sealed class LoneWolfProfile : ISeriesProfile
{
    public string Name => "Lone Wolf";
    public IReadOnlyDictionary<string, int> CoreSkills { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SkillNames { get; } = new[]
    {
        "Camouflage",
        "Hunting",
        "Sixth Sense",
        "Tracking",
        "Healing",
        "Weaponskill",
        "Mindshield",
        "Mindblast",
        "Animal Kinship",
        "Mind Over Matter"
    };

    public IReadOnlyDictionary<string, int> DefaultCounters { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Gold Crowns"] = 0,
        ["Meals"] = 0
    };

    public IReadOnlyList<Item> DefaultItems { get; } = Array.Empty<Item>();
}

internal sealed class GreyStarProfile : ISeriesProfile
{
    public string Name => "Grey Star";
    public IReadOnlyDictionary<string, int> CoreSkills { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SkillNames { get; } = new[]
    {
        "Sorcery",
        "Enchantment",
        "Elementalism",
        "Alchemy",
        "Prophecy",
        "Psychomancy",
        "Evocation"
    };

    public IReadOnlyDictionary<string, int> DefaultCounters { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Gold Crowns"] = 0
    };

    public IReadOnlyList<Item> DefaultItems { get; } = Array.Empty<Item>();
}

internal sealed class FreewayWarriorProfile : ISeriesProfile
{
    public string Name => "Freeway Warrior";
    public IReadOnlyDictionary<string, int> CoreSkills { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Driving"] = 3,
        ["Shooting"] = 3,
        ["Field Craft"] = 3,
        ["Stealth"] = 3,
        ["Perception"] = 3
    };

    public IReadOnlyList<string> SkillNames { get; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, int> DefaultCounters { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Bullets"] = 0,
        ["Fuel"] = 0
    };

    public IReadOnlyList<Item> DefaultItems { get; } = Array.Empty<Item>();
}
