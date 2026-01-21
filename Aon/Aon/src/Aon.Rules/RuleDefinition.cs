namespace Aon.Rules;

public sealed record RuleDefinition
{
    public string Id { get; init; } = string.Empty;
    public List<string> Requirements { get; init; } = new();
    public List<string> Effects { get; init; } = new();
}
