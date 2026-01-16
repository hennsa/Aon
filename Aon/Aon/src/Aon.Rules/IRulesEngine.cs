using Aon.Core;

namespace Aon.Rules;

public interface IRulesEngine
{
    int RollRandomNumber();
    CombatResult ResolveCombatRound(Character player, int enemyCombatSkill, int enemyEndurance, int randomNumber);
}
