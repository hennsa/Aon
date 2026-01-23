using Aon.Core;

namespace Aon.Rules;

public sealed class CombatResolver
{
    public CombatResult ResolveRound(
        Character player,
        int enemyCombatSkill,
        int enemyEndurance,
        int randomNumber,
        CombatTable combatTable)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(combatTable);

        var combatRatio = player.CombatSkill - enemyCombatSkill;
        var outcome = combatTable.Resolve(combatRatio, randomNumber);
        var playerLoss = outcome.PlayerLoss;
        var enemyLoss = outcome.EnemyLoss;

        var isPlayerDefeated = player.Endurance - playerLoss <= 0;
        var isEnemyDefeated = enemyEndurance - enemyLoss <= 0;

        return new CombatResult(playerLoss, enemyLoss, isPlayerDefeated, isEnemyDefeated);
    }
}
