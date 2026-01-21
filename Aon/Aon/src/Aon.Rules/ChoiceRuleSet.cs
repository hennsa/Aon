namespace Aon.Rules;

public sealed record ChoiceRuleSet(IReadOnlyList<Requirement> Requirements, IReadOnlyList<Effect> Effects)
{
    public static readonly ChoiceRuleSet Empty = new(Array.Empty<Requirement>(), Array.Empty<Effect>());
}
