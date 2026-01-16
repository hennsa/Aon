using Aon.Core;

namespace Aon.Rules;

public sealed class CombatResolver
{
    public CombatResult ResolveRound(Character player, int enemyCombatSkill, int enemyEndurance, int randomNumber)
    {
        var combatRatio = player.CombatSkill - enemyCombatSkill;
        var ratioModifier = Math.Clamp(combatRatio / 5, -4, 4);
        var playerLoss = Math.Max(0, 2 - ratioModifier + (randomNumber % 2));
        var enemyLoss = Math.Max(0, 2 + ratioModifier + (randomNumber % 3));

        var isPlayerDefeated = player.Endurance - playerLoss <= 0;
        var isEnemyDefeated = enemyEndurance - enemyLoss <= 0;

        return new CombatResult(playerLoss, enemyLoss, isPlayerDefeated, isEnemyDefeated);
    }
}
