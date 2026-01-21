namespace Aon.Core;

public sealed class PlayerProfile
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, SeriesProfileState> SeriesStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SeriesProfileState
{
    public bool IsInitialized { get; set; }
    public string ActiveCharacterName { get; set; } = string.Empty;
    public Dictionary<string, CharacterProfileState> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Character Character { get; set; } = new();
}

public sealed class CharacterProfileState
{
    public Character Character { get; set; } = new();
    public Dictionary<string, BookProgressState> BookProgress { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string LastBookId { get; set; } = string.Empty;
    public string LastSectionId { get; set; } = string.Empty;
}

public sealed class BookProgressState
{
    public string SectionId { get; set; } = string.Empty;
    public int MaxSectionIndex { get; set; } = -1;
    public int MaxSectionNumber { get; set; } = -1;
}
