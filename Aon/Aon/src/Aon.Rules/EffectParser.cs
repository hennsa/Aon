using System.Globalization;

namespace Aon.Rules;

public static class EffectParser
{
    public static Effect Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new UnsupportedEffect(raw);
        }

        var trimmed = raw.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
        {
            return new UnsupportedEffect(raw);
        }

        var key = trimmed[..colonIndex].Trim();
        var value = trimmed[(colonIndex + 1)..].Trim();

        if (key.Equals("stat", StringComparison.OrdinalIgnoreCase))
        {
            return ParseStatEffect(raw, value);
        }

        if (key.Equals("combat", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCombatEffect(raw, value);
        }

        if (key.Equals("endurance", StringComparison.OrdinalIgnoreCase))
        {
            return ParseEnduranceEffect(raw, value);
        }

        if (key.Equals("item", StringComparison.OrdinalIgnoreCase))
        {
            return ParseItemEffect(raw, value);
        }

        if (key.Equals("flag", StringComparison.OrdinalIgnoreCase))
        {
            return ParseFlagEffect(raw, value);
        }

        if (key.Equals("discipline", StringComparison.OrdinalIgnoreCase))
        {
            return ParseDisciplineEffect(raw, value);
        }

        if (key.Equals("counter", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCounterEffect(raw, value);
        }

        if (key.Equals("slot", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSlotEffect(raw, value);
        }

        return new UnsupportedEffect(raw);
    }

    private static Effect ParseStatEffect(string raw, string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return new UnsupportedEffect(raw);
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return new UnsupportedEffect(raw);
        }

        return new AdjustStatEffect(parts[0], delta, raw);
    }

    private static Effect ParseItemEffect(string raw, string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return new UnsupportedEffect(raw);
        }

        if (parts[0].Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return new UnsupportedEffect(raw);
            }

            var category = parts.Length >= 3 ? parts[2] : "general";
            return new AddItemEffect(parts[1], category, raw);
        }

        if (parts[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return new UnsupportedEffect(raw);
            }

            return new RemoveItemEffect(parts[1], raw);
        }

        var defaultCategory = parts.Length >= 2 ? parts[1] : "general";
        return new AddItemEffect(parts[0], defaultCategory, raw);
    }

    private static Effect ParseFlagEffect(string raw, string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new UnsupportedEffect(raw);
        }

        var flagValue = parts.Length == 2 ? parts[1] : "true";
        return new SetFlagEffect(parts[0], flagValue, raw);
    }

    private static Effect ParseDisciplineEffect(string raw, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new UnsupportedEffect(raw);
        }

        return new GrantDisciplineEffect(value, raw);
    }

    private static Effect ParseCounterEffect(string raw, string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new UnsupportedEffect(raw);
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return new UnsupportedEffect(raw);
        }

        var isAbsolute = parts[1][0] != '+' && parts[1][0] != '-';
        return new UpdateCounterEffect(parts[0], parsed, isAbsolute, raw);
    }

    private static Effect ParseCombatEffect(string raw, string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return new UnsupportedEffect(raw);
        }

        return new AdjustCombatModifierEffect(delta, raw);
    }

    private static Effect ParseEnduranceEffect(string raw, string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return new UnsupportedEffect(raw);
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return new UnsupportedEffect(raw);
        }

        var amount = Math.Abs(parsed);
        if (parts[0].Equals("damage", StringComparison.OrdinalIgnoreCase))
        {
            return new EnduranceDamageEffect(amount, raw);
        }

        if (parts[0].Equals("heal", StringComparison.OrdinalIgnoreCase))
        {
            return new EnduranceHealEffect(amount, raw);
        }

        return new UnsupportedEffect(raw);
    }

    private static Effect ParseSlotEffect(string raw, string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new UnsupportedEffect(raw);
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return new UnsupportedEffect(raw);
        }

        var isAbsolute = parts[1][0] != '+' && parts[1][0] != '-';
        return new UpdateSlotEffect(parts[0], parsed, isAbsolute, raw);
    }
}
