namespace Aon.Core;

public sealed class GameState
{
    public string BookId { get; set; } = string.Empty;
    public string SeriesId { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public Character Character { get; set; } = new();
}
