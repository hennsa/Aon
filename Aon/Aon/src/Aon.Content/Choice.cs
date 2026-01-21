namespace Aon.Content;

public sealed class Choice
{
    public string Text { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public List<string> Requirements { get; init; } = new();
    public List<string> Effects { get; init; } = new();
    public List<RandomOutcome> RandomOutcomes { get; init; } = new();
    public List<string> RuleIds { get; init; } = new();
}
