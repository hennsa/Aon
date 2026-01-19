namespace Aon.Desktop.Wpf.ViewModels;

public sealed class StatEntryViewModel
{
    public StatEntryViewModel(string label, int value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; set; }  // now writable
    public int Value { get; set; }     // consider INotifyPropertyChanged if UI must update on change
}
