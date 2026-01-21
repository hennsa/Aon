using Aon.Content;
using Aon.Core;

namespace Aon.Rules;

public sealed class RulesEngine : IRulesEngine
{
    private readonly RandomNumberTable _randomNumberTable;
    private readonly CombatResolver _combatResolver;
    private readonly IRandomNumberGenerator _randomNumberGenerator;
    private readonly IConditionEvaluator _conditionEvaluator;

    public RulesEngine()
        : this(
            new RandomNumberTable(),
            new CombatResolver(),
            new DefaultRandomNumberGenerator(),
            new ConditionEvaluator())
    {
    }

    public RulesEngine(
        RandomNumberTable randomNumberTable,
        CombatResolver combatResolver,
        IRandomNumberGenerator randomNumberGenerator,
        IConditionEvaluator conditionEvaluator)
    {
        _randomNumberTable = randomNumberTable;
        _combatResolver = combatResolver;
        _randomNumberGenerator = randomNumberGenerator;
        _conditionEvaluator = conditionEvaluator;
    }

    public int RollRandomNumber()
    {
        return _randomNumberTable.Roll(_randomNumberGenerator);
    }

    public CombatResult ResolveCombatRound(
        Character player,
        int enemyCombatSkill,
        int enemyEndurance,
        int randomNumber)
    {
        return _combatResolver.ResolveRound(player, enemyCombatSkill, enemyEndurance, randomNumber);
    }

    public ChoiceEvaluationResult EvaluateChoice(Choice choice, RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(context);

        var requirements = choice.Requirements
            .Select(RequirementParser.Parse)
            .ToList();

        var isAvailable = requirements.All(requirement => _conditionEvaluator.CanApply(requirement, context));
        var rollMetadata = ChoiceRollMetadata.FromChoice(choice);

        return new ChoiceEvaluationResult(isAvailable, rollMetadata);
    }

    public void ApplyEffects(IEnumerable<Effect> effects, RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var effect in effects)
        {
            switch (effect)
            {
                case AdjustStatEffect stat:
                    ApplyStatDelta(context.Character, stat.StatName, stat.Delta);
                    break;
                case AddItemEffect addItem:
                    context.Inventory.Items.Add(new Item(addItem.ItemName, addItem.Category));
                    break;
                case RemoveItemEffect removeItem:
                    RemoveItem(context.Inventory, removeItem.ItemName);
                    break;
                case SetFlagEffect flag:
                    context.Flags[flag.FlagName] = flag.Value;
                    break;
                case GrantDisciplineEffect discipline:
                    GrantDiscipline(context.Character, discipline.DisciplineName);
                    break;
                case UpdateCounterEffect counter:
                    UpdateCounter(context.Inventory, counter);
                    break;
                case UnsupportedEffect:
                    break;
            }
        }
    }

    private static void ApplyStatDelta(Character character, string statName, int delta)
    {
        if (string.Equals(statName, nameof(Character.CombatSkill), StringComparison.OrdinalIgnoreCase))
        {
            character.CombatSkill += delta;
            return;
        }

        if (string.Equals(statName, nameof(Character.Endurance), StringComparison.OrdinalIgnoreCase))
        {
            character.Endurance += delta;
            return;
        }

        if (character.CoreSkills.TryGetValue(statName, out var coreSkill))
        {
            character.CoreSkills[statName] = coreSkill + delta;
            return;
        }

        character.Attributes.TryGetValue(statName, out var value);
        character.Attributes[statName] = value + delta;
    }

    private static void RemoveItem(Inventory inventory, string itemName)
    {
        var match = inventory.Items
            .FirstOrDefault(item => string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            inventory.Items.Remove(match);
        }
    }

    private static void GrantDiscipline(Character character, string disciplineName)
    {
        if (!character.Disciplines.Contains(disciplineName, StringComparer.OrdinalIgnoreCase))
        {
            character.Disciplines.Add(disciplineName);
        }
    }

    private static void UpdateCounter(Inventory inventory, UpdateCounterEffect effect)
    {
        if (effect.IsAbsolute)
        {
            inventory.Counters[effect.CounterName] = effect.Value;
            return;
        }

        inventory.Counters.TryGetValue(effect.CounterName, out var current);
        inventory.Counters[effect.CounterName] = current + effect.Value;
    }
}
