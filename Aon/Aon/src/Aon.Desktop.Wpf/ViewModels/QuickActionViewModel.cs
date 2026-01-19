namespace Aon.Desktop.Wpf.ViewModels;

public sealed class QuickActionViewModel
{
    public QuickActionViewModel(string label, Action action)
    {
        Label = label;
        Command = new RelayCommand(action);
    }

    public string Label { get; }
    public RelayCommand Command { get; }
}
