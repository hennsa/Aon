using Aon.Content;

namespace Aon.Rules;

public static class RuleMetadataValidator
{
    private static readonly HashSet<string> KnownRequirementKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "skill",
        "item",
        "stat",
        "flag",
        "counter",
        "combat",
        "slot"
    };

    private static readonly HashSet<string> KnownEffectKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "stat",
        "combat",
        "endurance",
        "item",
        "flag",
        "discipline",
        "counter",
        "slot"
    };

    public static List<string> ValidateBook(Book book, RuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(catalog);

        var warnings = new List<string>();

        foreach (var section in book.Sections)
        {
            foreach (var choice in section.Choices)
            {
                warnings.AddRange(ValidateChoice(section.Id, choice, catalog));
            }
        }

        return warnings;
    }

    public static List<string> ValidateChoice(string sectionId, Choice choice, RuleCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(catalog);

        var warnings = new List<string>();
        var choiceLabel = $"{sectionId} -> {choice.TargetId}";

        foreach (var ruleId in choice.RuleIds)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                continue;
            }

            var trimmed = ruleId.Trim();
            if (!catalog.Contains(trimmed))
            {
                warnings.Add($"Choice {choiceLabel} references missing rule id '{trimmed}'.");
            }
        }

        foreach (var requirement in choice.Requirements)
        {
            if (!TryGetKnownRequirementKey(requirement, out var key))
            {
                continue;
            }

            if (RequirementParser.Parse(requirement) is UnsupportedRequirement)
            {
                warnings.Add($"Choice {choiceLabel} has invalid {key} requirement token '{requirement}'.");
            }
        }

        foreach (var effect in choice.Effects)
        {
            warnings.AddRange(ValidateEffect(effect, $"Choice {choiceLabel}"));
        }

        foreach (var outcome in choice.RandomOutcomes)
        {
            var outcomeLabel = string.IsNullOrWhiteSpace(outcome.TargetId)
                ? $"Choice {choiceLabel} random outcome {outcome.Min}-{outcome.Max}"
                : $"Choice {choiceLabel} random outcome {outcome.Min}-{outcome.Max} -> {outcome.TargetId}";

            foreach (var effect in outcome.Effects)
            {
                warnings.AddRange(ValidateEffect(effect, outcomeLabel));
            }
        }

        return warnings;
    }

    private static IEnumerable<string> ValidateEffect(string effect, string contextLabel)
    {
        if (!TryGetKnownEffectKey(effect, out var key))
        {
            return Array.Empty<string>();
        }

        if (EffectParser.Parse(effect) is UnsupportedEffect)
        {
            return new[] { $"{contextLabel} has invalid {key} effect token '{effect}'." };
        }

        return Array.Empty<string>();
    }

    private static bool TryGetKnownRequirementKey(string requirement, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return false;
        }

        var trimmed = requirement.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        key = trimmed[..colonIndex].Trim();
        return !string.IsNullOrWhiteSpace(key) && KnownRequirementKeys.Contains(key);
    }

    private static bool TryGetKnownEffectKey(string effect, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(effect))
        {
            return false;
        }

        var trimmed = effect.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        key = trimmed[..colonIndex].Trim();
        return !string.IsNullOrWhiteSpace(key) && KnownEffectKeys.Contains(key);
    }
}
