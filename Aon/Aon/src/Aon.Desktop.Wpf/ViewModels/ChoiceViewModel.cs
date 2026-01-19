using System.Windows.Input;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ChoiceViewModel
{
    public ChoiceViewModel(string text, ICommand command, bool isEnabled)
    {
        Text = text;
        Command = command;
        IsEnabled = isEnabled;
    }

    public string Text { get; }
    public ICommand Command { get; }
    public bool IsEnabled { get; }
}
