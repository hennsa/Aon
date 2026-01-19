namespace Aon.Core;

public sealed class Inventory
{
    public List<Item> Items { get; set; } = new();
    public Dictionary<string, int> Counters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
