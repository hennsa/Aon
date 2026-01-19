namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ItemEntryViewModel
{
    public ItemEntryViewModel(string name, string category)
    {
        Name = name;
        Category = category;
    }

    public string Name { get; }
    public string Category { get; }
}
