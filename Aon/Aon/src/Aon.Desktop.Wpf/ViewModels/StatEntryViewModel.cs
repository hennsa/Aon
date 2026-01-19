namespace Aon.Desktop.Wpf.ViewModels;

public sealed class StatEntryViewModel : ViewModelBase
{
    private string _label;
    private int _value;

    public StatEntryViewModel(string label, int value, Action? increase = null, Action? decrease = null)
    {
        _label = label;
        _value = value;
        IncreaseCommand = increase is null ? null : new RelayCommand(increase);
        DecreaseCommand = decrease is null ? null : new RelayCommand(decrease, () => Value > 0);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value); // ViewModelBase should raise PropertyChanged
    }

    public int Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                DecreaseCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand? IncreaseCommand { get; }
    public RelayCommand? DecreaseCommand { get; }
}
