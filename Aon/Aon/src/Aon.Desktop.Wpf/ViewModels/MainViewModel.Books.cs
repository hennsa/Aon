using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aon.Content;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private void LoadBooks(string booksDirectory)
    {
        if (!Directory.Exists(booksDirectory))
        {
            Blocks.Clear();
            Blocks.Add(new ContentBlockViewModel("p", "No book exports were found."));
            return;
        }

        var bookFiles = Directory.EnumerateFiles(booksDirectory, "*.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bookFiles.Count == 0)
        {
            Blocks.Clear();
            Blocks.Add(new ContentBlockViewModel("p", "No book exports were found."));
            return;
        }

        var bookEntries = bookFiles.Select(file =>
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var title = TryReadBookTitle(file) ?? id.Replace("__", " ");
            var order = TryGetBookOrder(id);
            var seriesId = ResolveSeriesId(id);
            return new
            {
                Id = id,
                Title = title,
                Order = order,
                SeriesId = seriesId,
                SeriesName = ResolveSeriesName(seriesId),
                SeriesSortOrder = ResolveSeriesSortOrder(seriesId),
                SectionIds = TryReadBookSectionIds(file)
            };
        });

        foreach (var entry in bookEntries
            .OrderBy(item => item.SeriesSortOrder)
            .ThenBy(item => item.Order ?? int.MaxValue)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            Books.Add(new BookListItemViewModel(
                entry.Id,
                entry.Title,
                entry.Order,
                entry.SeriesId,
                entry.SeriesName,
                entry.SeriesSortOrder,
                entry.SectionIds));
        }

        UpdateBookProgressIndicators();
        SelectedBook = null;
    }

    private static string? TryReadBookTitle(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "title", StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.GetString();
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static IReadOnlyList<string> TryReadBookSectionIds(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "sections", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Array)
                {
                    var sections = new List<string>();
                    foreach (var section in property.Value.EnumerateArray())
                    {
                        if (section.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (section.TryGetProperty("id", out var idProperty)
                            && idProperty.ValueKind == JsonValueKind.String)
                        {
                            var id = idProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                sections.Add(id);
                            }
                        }
                    }

                    return sections;
                }
            }
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }

        return Array.Empty<string>();
    }

    private static int? TryGetBookOrder(string bookId)
    {
        var match = Regex.Match(bookId, "\\d+");
        if (!match.Success || !int.TryParse(match.Value, out var order))
        {
            return null;
        }

        return order;
    }

    private async Task LoadBookAsync(string bookId)
    {
        var book = await _bookRepository.GetBookAsync(bookId);
        if (!string.Equals(SelectedBook?.Id, bookId, StringComparison.Ordinal))
        {
            return;
        }

        var seriesId = ResolveSeriesId(book.Id);
        if (!EnsureSeriesProfile(seriesId))
        {
            ClearBookDisplay();
            return;
        }

        if (_lastWizardCreatedNewCharacter && string.Equals(_lastWizardSeriesId, seriesId, StringComparison.OrdinalIgnoreCase))
        {
            var firstBookId = GetFirstBookIdForSeries(seriesId);
            if (!string.IsNullOrWhiteSpace(firstBookId)
                && !string.Equals(firstBookId, book.Id, StringComparison.OrdinalIgnoreCase))
            {
                var firstBook = Books.FirstOrDefault(item => string.Equals(item.Id, firstBookId, StringComparison.OrdinalIgnoreCase));
                if (firstBook is not null)
                {
                    SelectedBook = firstBook;
                    return;
                }
            }
        }
        _lastWizardCreatedNewCharacter = false;
        _lastWizardSeriesId = null;

        _book = book;
        _state.BookId = _book.Id;
        _state.SeriesId = seriesId;
        BookTitle = _book.Title;
        var firstSection = _book.Sections.FirstOrDefault();
        var savedSectionId = GetSavedSectionId(_book.Id);
        _state.SectionId = string.IsNullOrWhiteSpace(savedSectionId)
            ? firstSection?.Id ?? string.Empty
            : savedSectionId;

        if (firstSection is null)
        {
            SectionTitle = "No sections found";
            Blocks.Clear();
            Blocks.Add(new ContentBlockViewModel("p", "This book has no sections."));
            Choices.Clear();
            ResetRandomNumberState();
            return;
        }

        if (string.IsNullOrWhiteSpace(savedSectionId) || string.Equals(savedSectionId, firstSection.Id, StringComparison.OrdinalIgnoreCase))
        {
            var frontMatterSequence = BuildFrontMatterSequence(_book);
            _frontMatterQueue.Clear();
            foreach (var frontMatter in frontMatterSequence)
            {
                _frontMatterQueue.Enqueue(frontMatter);
            }

            _firstSectionForFrontMatter = firstSection;

            if (_frontMatterQueue.Count > 0)
            {
                ShowNextFrontMatterOrSection();
                return;
            }
        }

        var sectionToDisplay = _book.Sections.FirstOrDefault(item => item.Id == _state.SectionId) ?? firstSection;
        UpdateSection(sectionToDisplay);
    }

    private void ClearBookDisplay()
    {
        _book = null;
        _firstSectionForFrontMatter = null;
        _frontMatterQueue.Clear();
        _state.BookId = string.Empty;
        _state.SeriesId = string.Empty;
        _state.SectionId = string.Empty;
        BookTitle = "Aon Companion";
        SectionTitle = "Select a book";
        Blocks.Clear();
        Choices.Clear();
        ResetRandomNumberState();
        SuggestedActions.Clear();
        AreChoicesVisible = false;
        SelectedBook = null;
        UpdateBookProgressIndicators();
    }

    private async Task ApplyChoiceAsync(Choice choice)
    {
        if (_book is null)
        {
            return;
        }

        var section = await _gameService.ApplyChoiceAsync(_state, choice);
        if (section is null)
        {
            return;
        }

        UpdateSection(section);
    }

    private void UpdateSection(BookSection section)
    {
        _state.SectionId = section.Id;
        SectionTitle = ReplaceCharacterTokens(section.Title);
        Blocks.Clear();
        foreach (var block in section.Blocks)
        {
            Blocks.Add(new ContentBlockViewModel(block.Kind, ReplaceCharacterTokens(block.Text)));
        }

        UpdateSuggestedActions(section);
        ResetRandomNumberState();
        RecordBookProgress();
        if (RequiresRandomNumber(section))
        {
            PrepareRandomNumberSection(section);
            return;
        }

        ShowChoices(section.Choices);
    }

    private void ShowNextFrontMatterOrSection()
    {
        if (_firstSectionForFrontMatter is null)
        {
            return;
        }

        if (_frontMatterQueue.Count == 0)
        {
            UpdateSection(_firstSectionForFrontMatter);
            return;
        }

        var frontMatter = _frontMatterQueue.Dequeue();

        // Make the delegate type explicit -- avoid mixing a method group with a lambda in the conditional operator.
        Action continueAction = _frontMatterQueue.Count > 0
            ? () => ShowNextFrontMatterOrSection()
            : () => UpdateSection(_firstSectionForFrontMatter);

        ShowFrontMatter(frontMatter, continueAction);
    }

    private void ShowFrontMatter(FrontMatterSection frontMatter, Action continueAction)
    {
        SectionTitle = ReplaceCharacterTokens(GetFrontMatterTitle(frontMatter));
        Blocks.Clear();
        foreach (var block in ExtractFrontMatterBlocks(frontMatter.Html))
        {
            Blocks.Add(new ContentBlockViewModel(block.Kind, ReplaceCharacterTokens(block.Text)));
        }

        Choices.Clear();
        var command = new RelayCommand(continueAction);
        Choices.Add(new ChoiceViewModel("Continue", command, true));
        SuggestedActions.Clear();
        ResetRandomNumberState();
        AreChoicesVisible = true;
    }

    private static readonly string[] RecapIdPriority =
    {
        "tssf",
        "calstory"
    };

    private static readonly string[] RecapTitleKeywords =
    {
        "story so far",
        "cal's story"
    };

    private static IReadOnlyList<FrontMatterSection> BuildFrontMatterSequence(Book book)
    {
        var sequence = new List<FrontMatterSection>();
        var introduction = book.FrontMatter
            .FirstOrDefault(item => string.Equals(item.Id, "frontmatter-2", StringComparison.OrdinalIgnoreCase))
            ?? book.FrontMatter.FirstOrDefault(item => !IsTableOfContents(item) && !IsRecapSection(item));

        if (introduction is not null)
        {
            sequence.Add(introduction);
        }

        foreach (var recap in GetRecapSections(book))
        {
            if (ReferenceEquals(recap, introduction))
            {
                continue;
            }

            sequence.Add(recap);
        }

        return sequence;
    }

    private static IEnumerable<FrontMatterSection> GetRecapSections(Book book)
    {
        var recaps = book.FrontMatter
            .Where(IsRecapSection)
            .Distinct()
            .ToList();

        if (recaps.Count <= 1)
        {
            return recaps;
        }

        return recaps
            .OrderBy(GetRecapSortKey)
            .ThenBy(section => section.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetRecapSortKey(FrontMatterSection section)
    {
        for (var index = 0; index < RecapIdPriority.Length; index++)
        {
            if (string.Equals(section.Id, RecapIdPriority[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        for (var index = 0; index < RecapTitleKeywords.Length; index++)
        {
            if (section.Title.Contains(RecapTitleKeywords[index], StringComparison.OrdinalIgnoreCase))
            {
                return RecapIdPriority.Length + index;
            }
        }

        return int.MaxValue;
    }

    private static bool IsRecapSection(FrontMatterSection section)
    {
        foreach (var recapId in RecapIdPriority)
        {
            if (string.Equals(section.Id, recapId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var keyword in RecapTitleKeywords)
        {
            if (section.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTableOfContents(FrontMatterSection section)
    {
        return section.Title.Contains("table of contents", StringComparison.OrdinalIgnoreCase)
            || string.Equals(section.Id, "toc", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFrontMatterTitle(FrontMatterSection section)
    {
        if (string.Equals(section.Id, "frontmatter-2", StringComparison.OrdinalIgnoreCase)
            && section.Title.StartsWith("frontmatter", StringComparison.OrdinalIgnoreCase))
        {
            return "Introduction";
        }

        return section.Title;
    }

    private static IEnumerable<ContentBlockViewModel> ExtractFrontMatterBlocks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<ContentBlockViewModel>();
        }

        var normalized = Regex.Replace(html, @"<\s*h(?<level>[1-3])[^>]*>", "\n\n[[h${level}]]", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"</\s*h[1-3]\s*>", "[[/h]]\n\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"</\s*p\s*>", "\n\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*p[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"</\s*li\s*>", "\n\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<[^>]+>", string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);

        return normalized
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => paragraph.Replace("\r", string.Empty).Replace("\n", " ").Trim())
            .Where(paragraph => paragraph.Length > 0)
            .Select(paragraph =>
            {
                var headingMatch = Regex.Match(paragraph, @"^\[\[h(?<level>[1-3])\]\](?<text>.*)\[\[/h\]\]$");
                if (headingMatch.Success)
                {
                    var level = headingMatch.Groups["level"].Value;
                    var text = headingMatch.Groups["text"].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new ContentBlockViewModel($"h{level}", text);
                    }
                }

                return new ContentBlockViewModel("p", paragraph);
            })
            .ToList();
    }

    private void RecordBookProgress()
    {
        if (_book is null || _currentCharacterState is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.BookId) || string.IsNullOrWhiteSpace(_state.SectionId))
        {
            return;
        }

        var progress = _currentCharacterState.BookProgress.TryGetValue(_state.BookId, out var existingProgress)
            ? existingProgress
            : new BookProgressState();
        UpdateProgressState(progress, _state.BookId, _state.SectionId);
        _currentCharacterState.BookProgress[_state.BookId] = progress;
        _currentCharacterState.LastBookId = _state.BookId;
        _currentCharacterState.LastSectionId = _state.SectionId;
        UpdateBookProgressIndicators();
    }

    private string? GetSavedSectionId(string bookId)
    {
        if (_currentCharacterState?.BookProgress.TryGetValue(bookId, out var progress) == true)
        {
            return progress.SectionId;
        }

        return null;
    }

    private void UpdateBookProgressIndicators()
    {
        foreach (var book in Books)
        {
            if (_currentCharacterState is null)
            {
                book.SetProgress(null);
                continue;
            }

            if (_currentCharacterState.BookProgress.TryGetValue(book.Id, out var progress))
            {
                book.SetProgress(progress);
            }
            else
            {
                book.SetProgress(null);
            }
        }

        UpdateBookAvailability();
    }

    private void UpdateProgressState(BookProgressState progress, string bookId, string sectionId)
    {
        progress.SectionId = sectionId;
        var book = Books.FirstOrDefault(item => string.Equals(item.Id, bookId, StringComparison.OrdinalIgnoreCase));
        if (book is not null && book.TryGetSectionIndex(sectionId, out var index))
        {
            progress.MaxSectionIndex = Math.Max(progress.MaxSectionIndex, index);
        }
    }

    private void UpdateBookAvailability()
    {
        var selectedBookId = SelectedBook?.Id;
        foreach (var seriesGroup in Books.GroupBy(book => book.SeriesId, StringComparer.OrdinalIgnoreCase))
        {
            var orderedBooks = seriesGroup
                .Where(book => book.Order.HasValue)
                .OrderBy(book => book.Order!.Value)
                .ToList();

            if (orderedBooks.Count == 0)
            {
                foreach (var book in seriesGroup)
                {
                    book.SetAvailability(true);
                }

                continue;
            }

            var firstOrder = orderedBooks.First().Order!.Value;
            var maxCompletedOrder = orderedBooks
                .Where(book => book.IsCompleted)
                .Select(book => book.Order!.Value)
                .DefaultIfEmpty(firstOrder - 1)
                .Max();
            var nextOrder = maxCompletedOrder + 1;

            foreach (var book in seriesGroup)
            {
                var isEnabled = !book.Order.HasValue
                    || book.Order.Value <= maxCompletedOrder
                    || book.Order.Value == nextOrder;

                if (selectedBookId is not null
                    && string.Equals(book.Id, selectedBookId, StringComparison.OrdinalIgnoreCase))
                {
                    isEnabled = true;
                }

                book.SetAvailability(isEnabled);
            }
        }
    }

    private static string ResolveSeriesId(string bookId)
    {
        if (bookId.StartsWith("lw", StringComparison.OrdinalIgnoreCase))
        {
            return "lw";
        }

        if (bookId.StartsWith("gs", StringComparison.OrdinalIgnoreCase))
        {
            return "gs";
        }

        if (bookId.StartsWith("fw", StringComparison.OrdinalIgnoreCase))
        {
            return "fw";
        }

        return "unknown";
    }

    private static string ResolveSeriesName(string seriesId)
    {
        return seriesId switch
        {
            "lw" => "Lone Wolf",
            "gs" => "Grey Star",
            "fw" => "Freeway Warrior",
            _ => "Other"
        };
    }

    private static int ResolveSeriesSortOrder(string seriesId)
    {
        return seriesId switch
        {
            "fw" => 0,
            "lw" => 1,
            "gs" => 2,
            _ => 99
        };
    }

    private static string FindBooksDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var repoCandidate = Path.Combine(current.FullName, "Aon.Content", "Books");
            if (Directory.Exists(repoCandidate))
            {
                return repoCandidate;
            }

            var localCandidate = Path.Combine(current.FullName, "Books");
            if (Directory.Exists(localCandidate))
            {
                return localCandidate;
            }

            current = current.Parent;
        }

        return Path.Combine(baseDirectory, "Books");
    }
}
