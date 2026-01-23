using Aon.Content;
using Aon.Core;

namespace Aon.Rules;

public interface IRulesEngine
{
    int RollRandomNumber();
    CombatResult ResolveCombatRound(
        Character player,
        int enemyCombatSkill,
        int enemyEndurance,
        int randomNumber,
        string? seriesId);
    ChoiceEvaluationResult EvaluateChoice(Choice choice, RuleContext context);
    ChoiceRuleSet ResolveChoiceRules(Choice choice, RuleContext context);
    void ApplyEffects(IEnumerable<Effect> effects, RuleContext context);
}
