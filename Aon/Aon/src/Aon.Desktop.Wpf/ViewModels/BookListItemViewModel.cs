namespace Aon.Desktop.Wpf.ViewModels;

public sealed class BookListItemViewModel : ViewModelBase
{
    private string _progressLabel = string.Empty;

    public BookListItemViewModel(string id, string title, int? order)
    {
        Id = id;
        Title = title;
        Order = order;
        _progressLabel = string.Empty;
    }

    public string Id { get; }
    public string Title { get; }
    public int? Order { get; }

    public string DisplayName
    {
        get
        {
            var prefix = Order.HasValue ? $"{Order.Value}. " : string.Empty;
            var progress = string.IsNullOrWhiteSpace(_progressLabel) ? string.Empty : $" ({_progressLabel})";
            return $"{prefix}{Title}{progress}";
        }
    }

    public void SetProgressLabel(string? progressLabel)
    {
        _progressLabel = progressLabel?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(DisplayName));
    }
}
