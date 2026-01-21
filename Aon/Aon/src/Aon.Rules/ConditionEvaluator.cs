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
}
