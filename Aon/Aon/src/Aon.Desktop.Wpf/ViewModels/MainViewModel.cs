using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using Aon.Application;
using Aon.Content;
using Aon.Core;
using Aon.Persistence;
using Aon.Rules;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly IBookRepository _bookRepository;
    private readonly GameState _state = new();
    private readonly Queue<FrontMatterSection> _frontMatterQueue = new();
    private Book? _book;
    private BookSection? _firstSectionForFrontMatter;
    private string _bookTitle = "Aon Companion";
    private string _sectionTitle = "Select a book";
    private BookListItemViewModel? _selectedBook;

    public MainViewModel()
    {
        var booksDirectory = FindBooksDirectory();
        _bookRepository = new JsonBookRepository(booksDirectory);
        _gameService = new GameService(
            _bookRepository,
            new JsonGameStateRepository(GetSaveDirectory()),
            new RulesEngine());

        Blocks = new ObservableCollection<ContentBlockViewModel>
        {
            new("p", $"Looking for book exports in: {booksDirectory}")
        };

        Choices = new ObservableCollection<ChoiceViewModel>();
        Books = new ObservableCollection<BookListItemViewModel>();
        LoadBooks(booksDirectory);
    }

    public string BookTitle
    {
        get => _bookTitle;
        set
        {
            if (_bookTitle == value)
            {
                return;
            }

            _bookTitle = value;
            OnPropertyChanged();
        }
    }

    public string SectionTitle
    {
        get => _sectionTitle;
        set
        {
            if (_sectionTitle == value)
            {
                return;
            }

            _sectionTitle = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ContentBlockViewModel> Blocks { get; }
    public ObservableCollection<ChoiceViewModel> Choices { get; }
    public ObservableCollection<BookListItemViewModel> Books { get; }

    public BookListItemViewModel? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (_selectedBook == value)
            {
                return;
            }

            _selectedBook = value;
            OnPropertyChanged();

            if (_selectedBook is not null)
            {
                _ = LoadBookAsync(_selectedBook.Id);
            }
        }
    }

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

        foreach (var file in bookFiles)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var displayName = id.Replace("__", " ");
            Books.Add(new BookListItemViewModel(id, displayName));
        }

        SelectedBook = Books.FirstOrDefault();
    }

    private async Task LoadBookAsync(string bookId)
    {
        var book = await _bookRepository.GetBookAsync(bookId);
        if (!string.Equals(SelectedBook?.Id, bookId, StringComparison.Ordinal))
        {
            return;
        }

        _book = book;
        _state.BookId = _book.Id;
        var firstSection = _book.Sections.FirstOrDefault();
        _state.SectionId = firstSection?.Id ?? string.Empty;
        BookTitle = _book.Title;

        if (firstSection is null)
        {
            SectionTitle = "No sections found";
            Blocks.Clear();
            Blocks.Add(new ContentBlockViewModel("p", "This book has no sections."));
            Choices.Clear();
            return;
        }

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

        UpdateSection(firstSection);
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
        SectionTitle = section.Title;
        Blocks.Clear();
        foreach (var block in section.Blocks)
        {
            Blocks.Add(new ContentBlockViewModel(block.Kind, block.Text));
        }

        Choices.Clear();
        foreach (var choice in section.Choices)
        {
            var command = new RelayCommand(() => _ = ApplyChoiceAsync(choice));
            Choices.Add(new ChoiceViewModel(choice.Text, command));
        }
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
        SectionTitle = GetFrontMatterTitle(frontMatter);
        Blocks.Clear();
        foreach (var paragraph in ExtractFrontMatterParagraphs(frontMatter.Html))
        {
            Blocks.Add(new ContentBlockViewModel("p", paragraph));
        }

        Choices.Clear();
        var command = new RelayCommand(continueAction);
        Choices.Add(new ChoiceViewModel("Continue", command));
    }

    private static IReadOnlyList<FrontMatterSection> BuildFrontMatterSequence(Book book)
    {
        var sequence = new List<FrontMatterSection>();
        var introduction = book.FrontMatter
            .FirstOrDefault(item => string.Equals(item.Id, "frontmatter-2", StringComparison.OrdinalIgnoreCase))
            ?? book.FrontMatter.FirstOrDefault(item => !IsTableOfContents(item) && !IsStorySoFar(item));

        if (introduction is not null)
        {
            sequence.Add(introduction);
        }

        var storySoFar = book.FrontMatter.FirstOrDefault(IsStorySoFar);
        if (storySoFar is not null && !ReferenceEquals(storySoFar, introduction))
        {
            sequence.Add(storySoFar);
        }

        return sequence;
    }

    private static bool IsStorySoFar(FrontMatterSection section)
    {
        if (string.Equals(section.Id, "tssf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return section.Title.Contains("story so far", StringComparison.OrdinalIgnoreCase);
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

    private static IEnumerable<string> ExtractFrontMatterParagraphs(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var normalized = Regex.Replace(html, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"</\s*p\s*>", "\n\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*p[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"</\s*li\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<[^>]+>", string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);

        return normalized
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => paragraph.Replace("\r", string.Empty).Replace("\n", " ").Trim())
            .Where(paragraph => paragraph.Length > 0)
            .ToList();
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

    private static string GetSaveDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "Aon", "Saves");
    }
}
