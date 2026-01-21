namespace Aon.Content;

public sealed class RandomOutcome
{
    public int Min { get; init; }
    public int Max { get; init; }
    public string TargetId { get; init; } = string.Empty;
    public List<string> Effects { get; init; } = new();
}
