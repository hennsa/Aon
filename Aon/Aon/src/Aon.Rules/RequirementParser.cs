using System.Globalization;

namespace Aon.Rules;

public static class RequirementParser
{
    public static Requirement Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new UnsupportedRequirement(raw);
        }

        var trimmed = raw.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
        {
            return new UnsupportedRequirement(raw);
        }

        var key = trimmed[..colonIndex].Trim();
        var value = trimmed[(colonIndex + 1)..].Trim();

        if (key.Equals("skill", StringComparison.OrdinalIgnoreCase))
        {
            return new SkillPresenceRequirement(value);
        }

        if (key.Equals("item", StringComparison.OrdinalIgnoreCase))
        {
            return new ItemPresenceRequirement(value);
        }

        if (key.Equals("stat", StringComparison.OrdinalIgnoreCase))
        {
            return ParseStatRequirement(raw, value);
        }

        return new UnsupportedRequirement(raw);
    }

    private static Requirement ParseStatRequirement(string raw, string value)
    {
        var comparison = value.Split(">=", StringSplitOptions.TrimEntries);
        if (comparison.Length == 2 && int.TryParse(comparison[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minValue))
        {
            return new StatThresholdRequirement(comparison[0], minValue);
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minValue))
        {
            return new StatThresholdRequirement(parts[0], minValue);
        }

        return new UnsupportedRequirement(raw);
    }
}
