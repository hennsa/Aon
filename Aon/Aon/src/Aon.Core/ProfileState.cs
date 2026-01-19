namespace Aon.Core;

public sealed class PlayerProfile
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, SeriesProfileState> SeriesStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SeriesProfileState
{
    public bool IsInitialized { get; set; }
    public Character Character { get; set; } = new();
}
