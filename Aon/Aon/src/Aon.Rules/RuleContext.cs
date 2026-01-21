using Aon.Core;

namespace Aon.Rules;

public sealed class RuleContext
{
    public RuleContext(GameState gameState)
    {
        GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    }

    public GameState GameState { get; }
    public Character Character => GameState.Character;
    public Inventory Inventory => GameState.Character.Inventory;
    public Dictionary<string, int> Counters => GameState.Character.Inventory.Counters;
    public Dictionary<string, string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
}
