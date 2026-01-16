namespace Aon.Rules;

public sealed record CombatResult(
    int PlayerEnduranceLoss,
    int EnemyEnduranceLoss,
    bool IsPlayerDefeated,
    bool IsEnemyDefeated);
