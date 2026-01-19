namespace Aon.Desktop.Wpf.ViewModels;

public sealed class StatEntryViewModel : ViewModelBase
{
    private string _label;
    private int _value;

    public StatEntryViewModel(string label, int value)
    {
        _label = label;
        _value = value;
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value); // ViewModelBase should raise PropertyChanged
    }

    public int Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
