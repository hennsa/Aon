namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ContentBlockViewModel
{
    public ContentBlockViewModel(string kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public string Kind { get; }
    public string Text { get; }
}
