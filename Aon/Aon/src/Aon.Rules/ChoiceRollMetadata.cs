using Aon.Content;

namespace Aon.Rules;

public sealed record ChoiceRollMetadata(bool RequiresRoll, IReadOnlyList<RandomOutcome> Outcomes)
{
    public static ChoiceRollMetadata FromChoice(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var outcomes = choice.RandomOutcomes;
        var requiresRoll = outcomes.Count > 0;

        return new ChoiceRollMetadata(requiresRoll, outcomes);
    }
}
