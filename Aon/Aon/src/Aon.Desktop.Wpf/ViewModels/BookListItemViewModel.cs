namespace Aon.Desktop.Wpf.ViewModels;

public sealed class BookListItemViewModel
{
    public BookListItemViewModel(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
}
