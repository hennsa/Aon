using System.Collections.Concurrent;
using Aon.Content;
using Aon.Core;

namespace Aon.Rules;

public sealed class RulesEngine : IRulesEngine
{
    private readonly RandomNumberTable _randomNumberTable;
    private readonly CombatResolver _combatResolver;
    private readonly IRandomNumberGenerator _randomNumberGenerator;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly Func<string?, CombatTable> _combatTableLoader;
    private readonly Func<string?, IRuleCatalog> _catalogLoader;
    private readonly ConcurrentDictionary<string, CombatTable> _combatTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IRuleCatalog> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    public RulesEngine()
        : this(
            new RandomNumberTable(),
            new CombatResolver(),
            new DefaultRandomNumberGenerator(),
            new ConditionEvaluator(),
            CombatTable.Load,
            RuleCatalog.Load)
    {
    }

    public RulesEngine(
        RandomNumberTable randomNumberTable,
        CombatResolver combatResolver,
        IRandomNumberGenerator randomNumberGenerator,
        IConditionEvaluator conditionEvaluator,
        Func<string?, CombatTable> combatTableLoader,
        Func<string?, IRuleCatalog> catalogLoader)
    {
        _randomNumberTable = randomNumberTable;
        _combatResolver = combatResolver;
        _randomNumberGenerator = randomNumberGenerator;
        _conditionEvaluator = conditionEvaluator;
        _combatTableLoader = combatTableLoader ?? throw new ArgumentNullException(nameof(combatTableLoader));
        _catalogLoader = catalogLoader ?? throw new ArgumentNullException(nameof(catalogLoader));
    }

    public int RollRandomNumber()
    {
        return _randomNumberTable.Roll(_randomNumberGenerator);
    }

    public CombatResult ResolveCombatRound(
        Character player,
        int enemyCombatSkill,
        int enemyEndurance,
        int randomNumber,
        string? seriesId)
    {
        var combatTable = GetCombatTable(seriesId);
        return _combatResolver.ResolveRound(player, enemyCombatSkill, enemyEndurance, randomNumber, combatTable);
    }

    public ChoiceEvaluationResult EvaluateChoice(Choice choice, RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(context);

        var rules = ResolveChoiceRules(choice, context);

        var isAvailable = rules.Requirements.All(requirement => _conditionEvaluator.CanApply(requirement, context));
        var rollMetadata = ChoiceRollMetadata.FromChoice(choice);

        return new ChoiceEvaluationResult(isAvailable, rollMetadata);
    }

    public ChoiceRuleSet ResolveChoiceRules(Choice choice, RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(context);

        var requirements = new List<Requirement>();
        var effects = new List<Effect>();

        foreach (var requirement in choice.Requirements)
        {
            requirements.Add(RequirementParser.Parse(requirement));
        }

        foreach (var effect in choice.Effects)
        {
            effects.Add(EffectParser.Parse(effect));
        }

        var catalog = GetCatalog(context.GameState.SeriesId);
        var resolvedRules = catalog.Resolve(choice.RuleIds);
        foreach (var rule in resolvedRules)
        {
            foreach (var requirement in rule.Requirements)
            {
                requirements.Add(RequirementParser.Parse(requirement));
            }

            foreach (var effect in rule.Effects)
            {
                effects.Add(EffectParser.Parse(effect));
            }
        }

        return new ChoiceRuleSet(requirements, effects);
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
                case AdjustCombatModifierEffect combat:
                    ApplyCombatModifierDelta(context.Character, combat.Delta);
                    break;
                case EnduranceDamageEffect damage:
                    context.Character.Endurance -= damage.Amount;
                    break;
                case EnduranceHealEffect heal:
                    context.Character.Endurance += heal.Amount;
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
                case UpdateSlotEffect slot:
                    UpdateSlot(context.Inventory, slot);
                    break;
                case UnsupportedEffect:
                    break;
            }
        }
    }

    private IRuleCatalog GetCatalog(string? seriesId)
    {
        var key = string.IsNullOrWhiteSpace(seriesId) ? string.Empty : seriesId.Trim();
        return _catalogs.GetOrAdd(key, catalogKey => _catalogLoader(catalogKey));
    }

    private CombatTable GetCombatTable(string? seriesId)
    {
        var key = string.IsNullOrWhiteSpace(seriesId) ? string.Empty : seriesId.Trim();
        return _combatTables.GetOrAdd(key, tableKey => _combatTableLoader(tableKey));
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

    private static void ApplyCombatModifierDelta(Character character, int delta)
    {
        character.Attributes.TryGetValue(Character.CombatSkillBonusAttribute, out var value);
        character.Attributes[Character.CombatSkillBonusAttribute] = value + delta;
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

    private static void UpdateSlot(Inventory inventory, UpdateSlotEffect effect)
    {
        var slotKey = GetSlotKey(effect.SlotName);
        if (effect.IsAbsolute)
        {
            inventory.Counters[slotKey] = effect.Value;
            return;
        }

        inventory.Counters.TryGetValue(slotKey, out var current);
        inventory.Counters[slotKey] = current + effect.Value;
    }

    private static string GetSlotKey(string slotName)
    {
        return $"slot:{slotName}";
    }
}
