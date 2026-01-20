using System;
using System.Collections.Generic;
using System.Linq;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class BookListItemViewModel : ViewModelBase
{
    private readonly Dictionary<string, int> _sectionIndexMap;
    private string _progressLabel = "New";
    private double _progressPercentage;
    private string _progressPercentageText = "0%";

    public BookListItemViewModel(string id, string title, int? order, IReadOnlyList<string> sectionIds)
    {
        Id = id;
        Title = title;
        Order = order;
        SectionCount = sectionIds.Count;
        _sectionIndexMap = sectionIds
            .Select((sectionId, index) => new { sectionId, index })
            .ToDictionary(entry => entry.sectionId, entry => entry.index, StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; }
    public string Title { get; }
    public int? Order { get; }
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

    public void SetProgress(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId) || SectionCount == 0)
        {
            ProgressLabel = "New";
            UpdateProgressPercentage(0);
            return;
        }

        var label = $"Section {sectionId}";
        if (_sectionIndexMap.TryGetValue(sectionId, out var index))
        {
            var completed = index + 1;
            var percentage = Math.Clamp(completed / (double)SectionCount * 100, 0, 100);
            UpdateProgressPercentage(percentage);

            if (completed >= SectionCount)
            {
                label = "Completed";
            }
        }
        else
        {
            UpdateProgressPercentage(0);
        }

        ProgressLabel = label;
    }

    private void UpdateProgressPercentage(double percentage)
    {
        ProgressPercentage = percentage;
        ProgressPercentageText = $"{Math.Round(percentage):0}%";
    }
}
