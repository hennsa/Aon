namespace Aon.Core;

public sealed class Item
{
    public Item(string name, string category = "general")
    {
        Name = name;
        Category = category;
    }

    public string Name { get; }
    public string Category { get; }
}
