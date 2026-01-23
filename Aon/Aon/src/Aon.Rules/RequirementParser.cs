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

        if (key.Equals("flag", StringComparison.OrdinalIgnoreCase))
        {
            return ParseFlagRequirement(raw, value);
        }

        if (key.Equals("counter", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCounterRequirement(raw, value);
        }

        if (key.Equals("combat", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCombatRequirement(raw, value);
        }

        if (key.Equals("slot", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSlotRequirement(raw, value);
        }

        return new UnsupportedRequirement(raw);
    }

    private static Requirement ParseStatRequirement(string raw, string value)
    {
        return TryParseNamedThreshold(value, out var name, out var minValue)
            ? new StatThresholdRequirement(name, minValue)
            : new UnsupportedRequirement(raw);
    }

    private static Requirement ParseFlagRequirement(string raw, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new UnsupportedRequirement(raw);
        }

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new UnsupportedRequirement(raw);
        }

        var expectedValue = parts.Length == 2 ? parts[1] : null;
        return new FlagRequirement(parts[0], expectedValue);
    }

    private static Requirement ParseCounterRequirement(string raw, string value)
    {
        return TryParseNamedThreshold(value, out var name, out var minValue)
            ? new CounterThresholdRequirement(name, minValue)
            : new UnsupportedRequirement(raw);
    }

    private static Requirement ParseCombatRequirement(string raw, string value)
    {
        return TryParseThreshold(value, out var minValue)
            ? new CombatModifierRequirement(minValue)
            : new UnsupportedRequirement(raw);
    }

    private static Requirement ParseSlotRequirement(string raw, string value)
    {
        return TryParseNamedThreshold(value, out var name, out var minValue)
            ? new SlotThresholdRequirement(name, minValue)
            : new UnsupportedRequirement(raw);
    }

    private static bool TryParseNamedThreshold(string value, out string name, out int minValue)
    {
        name = string.Empty;
        minValue = 0;
        var comparison = value.Split(">=", StringSplitOptions.TrimEntries);
        if (comparison.Length == 2
            && !string.IsNullOrWhiteSpace(comparison[0])
            && int.TryParse(comparison[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minValue))
        {
            name = comparison[0];
            return true;
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && !string.IsNullOrWhiteSpace(parts[0])
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minValue))
        {
            name = parts[0];
            return true;
        }

        return false;
    }

    private static bool TryParseThreshold(string value, out int minValue)
    {
        minValue = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith(">=", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = trimmed[2..].Trim();
            return int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out minValue);
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out minValue);
    }
}
