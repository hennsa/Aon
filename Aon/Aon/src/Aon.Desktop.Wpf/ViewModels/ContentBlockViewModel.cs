namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ContentBlockViewModel
{
    public ContentBlockViewModel(string kind, string html)
    {
        Kind = kind;
        Html = html;
    }

    public string Kind { get; }
    public string Html { get; }
}
