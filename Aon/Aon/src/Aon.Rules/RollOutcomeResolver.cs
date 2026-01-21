using Aon.Content;

namespace Aon.Rules;

public static class RollOutcomeResolver
{
    public static IReadOnlyList<RollOutcomeMetadata> ResolveOutcomes(ChoiceRollMetadata metadata, int roll)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.Outcomes
            .Where(outcome => outcome.Contains(roll))
            .ToList();
    }

    public static IReadOnlyList<Choice> ResolveChoices(IEnumerable<Choice> choices, int roll)
    {
        ArgumentNullException.ThrowIfNull(choices);

        return choices
            .Select(choice => new { Choice = choice, Metadata = ChoiceRollMetadata.FromChoice(choice) })
            .Where(entry => entry.Metadata.RequiresRoll && ResolveOutcomes(entry.Metadata, roll).Count > 0)
            .Select(entry => entry.Choice)
            .Distinct()
            .ToList();
    }
}
