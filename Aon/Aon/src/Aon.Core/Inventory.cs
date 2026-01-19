namespace Aon.Core;

public sealed class Inventory
{
    public List<Item> Items { get; } = new();
    public Dictionary<string, int> Counters { get; } = new(StringComparer.OrdinalIgnoreCase);
}
