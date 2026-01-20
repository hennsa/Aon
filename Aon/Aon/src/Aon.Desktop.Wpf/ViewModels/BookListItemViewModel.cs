using System;
using System.Collections.Generic;
using System.Linq;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class BookListItemViewModel : ViewModelBase
{
    private readonly Dictionary<string, int> _sectionIndexMap;
    private bool _isCompleted;
    private bool _isEnabled = true;
    private string _progressLabel = "New";
    private double _progressPercentage;
    private string _progressPercentageText = "0%";

    public BookListItemViewModel(
        string id,
        string title,
        int? order,
        string seriesId,
        string seriesName,
        int seriesSortOrder,
        IReadOnlyList<string> sectionIds)
    {
        Id = id;
        Title = title;
        Order = order;
        SeriesId = seriesId;
        SeriesName = seriesName;
        SeriesSortOrder = seriesSortOrder;
        SectionCount = sectionIds.Count;
        _sectionIndexMap = sectionIds
            .Select((sectionId, index) => new { sectionId, index })
            .ToDictionary(entry => entry.sectionId, entry => entry.index, StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; }
    public string Title { get; }
    public int? Order { get; }
    public string SeriesId { get; }
    public string SeriesName { get; }
    public int SeriesSortOrder { get; }
    public int SectionCount { get; }

    public string DisplayName
    {
        get
        {
            var prefix = Order.HasValue ? $"{Order.Value}. " : string.Empty;
            return $"{prefix}{Title}";
        }
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set => SetProperty(ref _progressLabel, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string ProgressPercentageText
    {
        get => _progressPercentageText;
        set => SetProperty(ref _progressPercentageText, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (_isCompleted == value)
            {
                return;
            }

            _isCompleted = value;
            OnPropertyChanged();
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public void SetProgress(BookProgressState? progress)
    {
        if (progress is null || string.IsNullOrWhiteSpace(progress.SectionId) || SectionCount == 0)
        {
            ProgressLabel = "New";
            UpdateProgressPercentage(0);
            IsCompleted = false;
            return;
        }

        var label = $"Section {progress.SectionId}";
        var maxIndex = progress.MaxSectionIndex;
        if (_sectionIndexMap.TryGetValue(progress.SectionId, out var index))
        {
            maxIndex = Math.Max(maxIndex, index);
        }

        if (maxIndex < 0)
        {
            UpdateProgressPercentage(0);
            ProgressLabel = label;
            IsCompleted = false;
            return;
        }

        var completed = maxIndex + 1;
        var percentage = Math.Clamp(completed / (double)SectionCount * 100, 0, 100);
        UpdateProgressPercentage(percentage);
        IsCompleted = completed >= SectionCount;
        if (IsCompleted)
        {
            label = "Completed";
        }

        ProgressLabel = label;
    }

    public void SetAvailability(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public bool TryGetSectionIndex(string sectionId, out int index)
    {
        return _sectionIndexMap.TryGetValue(sectionId, out index);
    }

    private void UpdateProgressPercentage(double percentage)
    {
        ProgressPercentage = percentage;
        ProgressPercentageText = $"{Math.Round(percentage):0}%";
    }
}
