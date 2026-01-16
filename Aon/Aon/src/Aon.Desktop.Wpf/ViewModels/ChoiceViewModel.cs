using System.Windows.Input;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ChoiceViewModel
{
    public ChoiceViewModel(string text, ICommand command)
    {
        Text = text;
        Command = command;
    }

    public string Text { get; }
    public ICommand Command { get; }
}
