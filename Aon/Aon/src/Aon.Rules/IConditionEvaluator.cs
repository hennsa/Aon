namespace Aon.Rules;

public interface IConditionEvaluator
{
    bool CanApply(Requirement requirement, RuleContext context);
}
