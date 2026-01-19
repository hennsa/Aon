namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ItemEntryViewModel : ViewModelBase
{
    private string _name;
    private string _category;

    public ItemEntryViewModel(string name, string category)
    {
        _name = name;
        _category = category;
    }

    // Make Name writable so TwoWay bindings work
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    // Make Category writable as well (keeps behaviour consistent)
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }
}
