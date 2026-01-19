namespace Aon.Core;

public interface ISeriesProfile
{
    string Name { get; }
    IReadOnlyDictionary<string, int> CoreSkills { get; }
    IReadOnlyList<string> SkillNames { get; }
    IReadOnlyList<string> CounterNames { get; }
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

    public IReadOnlyList<string> CounterNames { get; } = new[]
    {
        "Gold Crowns",
        "Meals"
    };
}

internal sealed class GreyStarProfile : ISeriesProfile
{
    public string Name => "Grey Star";
    public IReadOnlyDictionary<string, int> CoreSkills { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> SkillNames { get; } = new[]
    {
        "Alether",
        "Banishing",
        "Blending",
        "Diadem",
        "Elementalism",
        "Evocation",
        "Hypnosis",
        "Nexus",
        "Prophecy",
        "Psychomancy",
        "Psi-Screen",
        "Telekinesis",
        "Telepathy",
        "Thaumaturgy",
        "Vapours"
    };

    public IReadOnlyList<string> CounterNames { get; } = new[]
    {
        "Gold Crowns"
    };
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

    public IReadOnlyList<string> CounterNames { get; } = new[]
    {
        "Bullets",
        "Fuel"
    };
}
