using Aon.Content;

namespace Aon.Rules;

public sealed record ChoiceRollMetadata(bool RequiresRoll, IReadOnlyList<RollOutcomeMetadata> Outcomes)
{
    public static ChoiceRollMetadata FromChoice(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var outcomes = choice.RandomOutcomes
            .Select(outcome => new RollOutcomeMetadata(outcome.Min, outcome.Max, outcome.TargetId, outcome.Effects))
            .ToList();
        var requiresRoll = outcomes.Count > 0;

        return new ChoiceRollMetadata(requiresRoll, outcomes);
    }
}
