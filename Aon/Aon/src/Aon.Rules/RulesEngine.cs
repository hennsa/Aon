using Aon.Core;

namespace Aon.Rules;

public sealed class RulesEngine : IRulesEngine
{
    private readonly RandomNumberTable _randomNumberTable;
    private readonly CombatResolver _combatResolver;
    private readonly IRandomNumberGenerator _randomNumberGenerator;

    public RulesEngine()
        : this(new RandomNumberTable(), new CombatResolver(), new DefaultRandomNumberGenerator())
    {
    }

    public RulesEngine(
        RandomNumberTable randomNumberTable,
        CombatResolver combatResolver,
        IRandomNumberGenerator randomNumberGenerator)
    {
        _randomNumberTable = randomNumberTable;
        _combatResolver = combatResolver;
        _randomNumberGenerator = randomNumberGenerator;
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
}
