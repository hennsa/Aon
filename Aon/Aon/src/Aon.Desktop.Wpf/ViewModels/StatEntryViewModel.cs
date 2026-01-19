namespace Aon.Desktop.Wpf.ViewModels;

public sealed class StatEntryViewModel
{
    public StatEntryViewModel(string label, int value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public int Value { get; }
}
