using Aon.Core;

namespace Aon.Rules;

public sealed class ConditionEvaluator : IConditionEvaluator
{
    public bool CanApply(Requirement requirement, RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(context);

        return requirement switch
        {
            SkillPresenceRequirement skill => HasSkill(context.Character, skill.SkillName),
            StatThresholdRequirement stat => MeetsStatThreshold(context.Character, stat),
            ItemPresenceRequirement item => HasItem(context.Inventory, item.ItemName),
            FlagRequirement flag => MeetsFlagRequirement(context.Flags, flag),
            CounterThresholdRequirement counter => MeetsCounterThreshold(context.Inventory, counter),
            CombatModifierRequirement combat => MeetsCombatRequirement(context.Character, combat),
            SlotThresholdRequirement slot => MeetsSlotRequirement(context.Inventory, slot),
            _ => false
        };
    }

    private static bool HasSkill(Character character, string skillName)
    {
        return character.Disciplines.Any(discipline =>
                   string.Equals(discipline, skillName, StringComparison.OrdinalIgnoreCase))
               || character.CoreSkills.ContainsKey(skillName);
    }

    private static bool MeetsStatThreshold(Character character, StatThresholdRequirement requirement)
    {
        return TryGetStatValue(character, requirement.StatName, out var value)
            && value >= requirement.Minimum;
    }

    private static bool TryGetStatValue(Character character, string statName, out int value)
    {
        if (string.Equals(statName, nameof(Character.CombatSkill), StringComparison.OrdinalIgnoreCase))
        {
            value = character.CombatSkill;
            return true;
        }

        if (string.Equals(statName, nameof(Character.Endurance), StringComparison.OrdinalIgnoreCase))
        {
            value = character.Endurance;
            return true;
        }

        if (character.CoreSkills.TryGetValue(statName, out value))
        {
            return true;
        }

        return character.Attributes.TryGetValue(statName, out value);
    }

    private static bool HasItem(Inventory inventory, string itemName)
    {
        return inventory.Items.Any(item =>
            string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MeetsFlagRequirement(IReadOnlyDictionary<string, string> flags, FlagRequirement requirement)
    {
        if (!flags.TryGetValue(requirement.FlagName, out var value))
        {
            return false;
        }

        if (requirement.ExpectedValue is null)
        {
            return true;
        }

        return string.Equals(value, requirement.ExpectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MeetsCounterThreshold(Inventory inventory, CounterThresholdRequirement requirement)
    {
        return inventory.Counters.TryGetValue(requirement.CounterName, out var value)
            && value >= requirement.Minimum;
    }

    private static bool MeetsCombatRequirement(Character character, CombatModifierRequirement requirement)
    {
        character.Attributes.TryGetValue(Character.CombatSkillBonusAttribute, out var value);
        return value >= requirement.Minimum;
    }

    private static bool MeetsSlotRequirement(Inventory inventory, SlotThresholdRequirement requirement)
    {
        var slotKey = GetSlotKey(requirement.SlotName);
        return inventory.Counters.TryGetValue(slotKey, out var value)
            && value >= requirement.Minimum;
    }

    private static string GetSlotKey(string slotName)
    {
        return $"slot:{slotName}";
    }
}
