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
}
