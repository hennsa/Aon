using Aon.Content;
using Aon.Core;

namespace Aon.Rules;

public interface IRulesEngine
{
    int RollRandomNumber();
    CombatResult ResolveCombatRound(Character player, int enemyCombatSkill, int enemyEndurance, int randomNumber);
    ChoiceEvaluationResult EvaluateChoice(Choice choice, RuleContext context);
    ChoiceRuleSet ResolveChoiceRules(Choice choice);
    void ApplyEffects(IEnumerable<Effect> effects, RuleContext context);
}
