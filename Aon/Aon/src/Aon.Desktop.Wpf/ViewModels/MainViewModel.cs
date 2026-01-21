using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Text.Json;
using Aon.Application;
using Aon.Content;
using Aon.Core;
using Aon.Desktop.Wpf;
using Aon.Persistence;
using Aon.Rules;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly IBookRepository _bookRepository;
    private readonly GameState _state = new();
    private readonly Queue<FrontMatterSection> _frontMatterQueue = new();
    private readonly RelayCommand _rollRandomNumberCommand;
    private readonly RelayCommand _confirmRandomNumberCommand;
    private readonly RelayCommand _showRandomNumberTableCommand;
    private readonly RelayCommand _saveGameCommand;
    private readonly RelayCommand _loadGameCommand;
    private readonly RelayCommand _newSaveSlotCommand;
    private readonly RelayCommand _newProfileCommand;
    private readonly RelayCommand _newCharacterCommand;
    private readonly RelayCommand _addSkillCommand;
    private readonly RelayCommand _removeSkillCommand;
    private readonly RelayCommand _addItemCommand;
    private readonly RelayCommand _removeItemCommand;
    private readonly RelayCommand _addCounterCommand;
    private readonly List<Choice> _pendingRandomChoices = new();
    private readonly List<RandomNumberChoice> _randomNumberChoices = new();
    private readonly Queue<int> _recentRolls = new();
    private readonly string _saveDirectory;
    private const string CharacterNameToken = "{{characterName}}";
    private ISeriesProfile _currentProfile = SeriesProfiles.LoneWolf;
    private CharacterProfileState? _currentCharacterState;
    private Book? _book;
    private BookSection? _firstSectionForFrontMatter;
    private string _bookTitle = "Aon Companion";
    private string _sectionTitle = "Select a book";
    private string _randomNumberStatus = string.Empty;
    private string _rollHistoryText = string.Empty;
    private string _rollModifierText = "0";
    private string _saveSlot = "default";
    private string _characterSetupHint = string.Empty;
    private string _selectedAvailableSkill = string.Empty;
    private string? _selectedSkill;
    private string _newItemName = string.Empty;
    private string _newItemCategory = "general";
    private string _counterNameInput = string.Empty;
    private BookListItemViewModel? _selectedBook;
    private ProfileOptionViewModel? _selectedProfile;
    private CharacterOptionViewModel? _selectedCharacter;
    private ItemEntryViewModel? _selectedInventoryItem;
    private bool _isRandomNumberVisible;
    private bool _areChoicesVisible = true;
    private int? _randomNumberResult;
    private Choice? _resolvedRandomChoice;
    private bool _isManualRandomMode;
    private bool _isProfileReady;
    private bool _isUpdatingCharacters;
    private bool _isUpdatingProfiles;
    private bool _isSwitchingCharacter;
    private bool _lastWizardCreatedNewCharacter;
    private string? _lastWizardSeriesId;

    public MainViewModel()
    {
        var booksDirectory = FindBooksDirectory();
        _saveDirectory = GetSaveDirectory();
        _bookRepository = new JsonBookRepository(booksDirectory);
        _gameService = new GameService(
            _bookRepository,
            new JsonGameStateRepository(_saveDirectory),
            new RulesEngine());

        Blocks = new ObservableCollection<ContentBlockViewModel>
        {
            new("p", $"Looking for book exports in: {booksDirectory}")
        };

        Choices = new ObservableCollection<ChoiceViewModel>();
        Books = new ObservableCollection<BookListItemViewModel>();
        _rollRandomNumberCommand = new RelayCommand(RollRandomNumber);
        _confirmRandomNumberCommand = new RelayCommand(ConfirmRandomNumber, () => _resolvedRandomChoice is not null);
        _showRandomNumberTableCommand = new RelayCommand(ShowRandomNumberTable);
        _saveGameCommand = new RelayCommand(() => _ = SaveGameAsync());
        _loadGameCommand = new RelayCommand(() => _ = LoadGameAsync());
        _newSaveSlotCommand = new RelayCommand(CreateNewSaveSlot);
        _newProfileCommand = new RelayCommand(StartNewProfile);
        _newCharacterCommand = new RelayCommand(() => _ = CreateNewCharacterAsync(), () => CanCreateCharacter);
        _addSkillCommand = new RelayCommand(AddSkill, () => !string.IsNullOrWhiteSpace(SelectedAvailableSkill));
        _removeSkillCommand = new RelayCommand(RemoveSkill, () => SelectedSkill is not null);
        _addItemCommand = new RelayCommand(AddItem, () => !string.IsNullOrWhiteSpace(NewItemName));
        _removeItemCommand = new RelayCommand(RemoveItem, () => SelectedInventoryItem is not null);
        _addCounterCommand = new RelayCommand(AddCounter, () => !string.IsNullOrWhiteSpace(CounterNameInput));
        LoadBooks(booksDirectory);
        LoadSaveSlots();
        LoadProfiles();
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
    public ObservableCollection<ProfileOptionViewModel> Profiles { get; } = new();
    public ObservableCollection<CharacterOptionViewModel> Characters { get; } = new();
    public ObservableCollection<StatEntryViewModel> CoreStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> CoreSkills { get; } = new();
    public ObservableCollection<StatEntryViewModel> AttributeStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> InventoryCounters { get; } = new();
    public ObservableCollection<string> SaveSlots { get; } = new();
    public ObservableCollection<string> AvailableSkills { get; } = new();
    public ObservableCollection<string> CharacterSkills { get; } = new();
    public ObservableCollection<ItemEntryViewModel> InventoryItems { get; } = new();
    public ObservableCollection<QuickActionViewModel> SuggestedActions { get; } = new();
    public RelayCommand RollRandomNumberCommand => _rollRandomNumberCommand;
    public RelayCommand ConfirmRandomNumberCommand => _confirmRandomNumberCommand;
    public RelayCommand ShowRandomNumberTableCommand => _showRandomNumberTableCommand;
    public RelayCommand SaveGameCommand => _saveGameCommand;
    public RelayCommand LoadGameCommand => _loadGameCommand;
    public RelayCommand NewSaveSlotCommand => _newSaveSlotCommand;
    public RelayCommand NewProfileCommand => _newProfileCommand;
    public RelayCommand NewCharacterCommand => _newCharacterCommand;
    public RelayCommand AddSkillCommand => _addSkillCommand;
    public RelayCommand RemoveSkillCommand => _removeSkillCommand;
    public RelayCommand AddItemCommand => _addItemCommand;
    public RelayCommand RemoveItemCommand => _removeItemCommand;
    public RelayCommand AddCounterCommand => _addCounterCommand;
    public bool HasCoreSkills => CoreSkills.Count > 0;
    public bool HasAvailableSkills => AvailableSkills.Count > 0;
    public bool CanCreateCharacter => SelectedProfile is not null;
    public bool IsProfileSelected => SelectedProfile is not null;
    public bool IsProfileReady
    {
        get => _isProfileReady;
        private set
        {
            if (_isProfileReady == value)
            {
                return;
            }

            _isProfileReady = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CharacterPanelTitle));
            _newCharacterCommand.RaiseCanExecuteChanged();
        }
    }

    public ProfileOptionViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile == value)
            {
                return;
            }

            _selectedProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProfileSelected));
            _newCharacterCommand.RaiseCanExecuteChanged();

            if (_isUpdatingProfiles)
            {
                return;
            }

            if (_selectedProfile is null)
            {
                SetProfileSetupRequired("Select a profile to continue.");
                return;
            }

            ApplySelectedProfile(_selectedProfile.Profile);
        }
    }

    public string CharacterPanelTitle
    {
        get
        {
            if (!IsProfileReady)
            {
                return "Character";
            }

            if (!string.IsNullOrWhiteSpace(_state.Character.Name))
            {
                return _state.Character.Name;
            }

            return _currentProfile.DefaultCharacterName;
        }
    }

    public string SaveSlot
    {
        get => _saveSlot;
        set
        {
            if (_saveSlot == value)
            {
                return;
            }

            _saveSlot = value;
            OnPropertyChanged();
        }
    }

    public string CharacterSetupHint
    {
        get => _characterSetupHint;
        private set
        {
            if (_characterSetupHint == value)
            {
                return;
            }

            _characterSetupHint = value;
            OnPropertyChanged();
        }
    }

    public string SelectedAvailableSkill
    {
        get => _selectedAvailableSkill;
        set
        {
            if (_selectedAvailableSkill == value)
            {
                return;
            }

            _selectedAvailableSkill = value;
            OnPropertyChanged();
            _addSkillCommand.RaiseCanExecuteChanged();
        }
    }

    public string? SelectedSkill
    {
        get => _selectedSkill;
        set
        {
            if (_selectedSkill == value)
            {
                return;
            }

            _selectedSkill = value;
            OnPropertyChanged();
            _removeSkillCommand.RaiseCanExecuteChanged();
        }
    }


    public string NewItemName
    {
        get => _newItemName;
        set
        {
            if (_newItemName == value)
            {
                return;
            }

            _newItemName = value;
            OnPropertyChanged();
            _addItemCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewItemCategory
    {
        get => _newItemCategory;
        set
        {
            if (_newItemCategory == value)
            {
                return;
            }

            _newItemCategory = value;
            OnPropertyChanged();
        }
    }

    public string CounterNameInput
    {
        get => _counterNameInput;
        set
        {
            if (_counterNameInput == value)
            {
                return;
            }

            _counterNameInput = value;
            OnPropertyChanged();
            _addCounterCommand.RaiseCanExecuteChanged();
        }
    }

    public ItemEntryViewModel? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            if (_selectedInventoryItem == value)
            {
                return;
            }

            _selectedInventoryItem = value;
            OnPropertyChanged();
            _removeItemCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsRandomNumberVisible
    {
        get => _isRandomNumberVisible;
        private set
        {
            if (_isRandomNumberVisible == value)
            {
                return;
            }

            _isRandomNumberVisible = value;
            OnPropertyChanged();
        }
    }

    public bool AreChoicesVisible
    {
        get => _areChoicesVisible;
        private set
        {
            if (_areChoicesVisible == value)
            {
                return;
            }

            _areChoicesVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsRandomNumberConfirmVisible => _resolvedRandomChoice is not null;

    public string RandomNumberStatus
    {
        get => _randomNumberStatus;
        private set
        {
            if (_randomNumberStatus == value)
            {
                return;
            }

            _randomNumberStatus = value;
            OnPropertyChanged();
        }
    }

    public string RollHistoryText
    {
        get => _rollHistoryText;
        private set
        {
            if (_rollHistoryText == value)
            {
                return;
            }

            _rollHistoryText = value;
            OnPropertyChanged();
        }
    }

    public string RollModifierText
    {
        get => _rollModifierText;
        set
        {
            if (_rollModifierText == value)
            {
                return;
            }

            _rollModifierText = value;
            OnPropertyChanged();
        }
    }

    public bool IsManualRandomMode
    {
        get => _isManualRandomMode;
        set
        {
            if (_isManualRandomMode == value)
            {
                return;
            }

            _isManualRandomMode = value;
            OnPropertyChanged();
        }
    }

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

            if (_selectedBook is not null && IsProfileReady)
            {
                _ = LoadBookAsync(_selectedBook.Id);
            }
        }
    }

    public CharacterOptionViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (_selectedCharacter == value)
            {
                return;
            }

        _selectedCharacter = value;
        OnPropertyChanged();

        if (_isUpdatingCharacters || _isSwitchingCharacter || _selectedCharacter is null)
        {
            return;
        }

        if (!IsProfileReady)
        {
            _ = LoadCharacterSelectionAsync(_selectedCharacter);
            return;
        }

        _ = SwitchCharacterAsync(_selectedCharacter);
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

    private void ResetRandomNumberState()
    {
        _pendingRandomChoices.Clear();
        _randomNumberChoices.Clear();
        _randomNumberResult = null;
        _resolvedRandomChoice = null;
        RandomNumberStatus = string.Empty;
        IsRandomNumberVisible = false;
        AreChoicesVisible = true;
        OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
        _confirmRandomNumberCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCharacterPanels()
    {
        CoreStats.Clear();
        CoreStats.Add(new StatEntryViewModel("Combat Skill", _state.Character.CombatSkill));
        CoreStats.Add(new StatEntryViewModel("Effective Combat Skill", _state.Character.GetEffectiveCombatSkill()));
        CoreStats.Add(new StatEntryViewModel("Endurance", _state.Character.Endurance));

        CoreSkills.Clear();
        foreach (var entry in _state.Character.CoreSkills.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            CoreSkills.Add(new StatEntryViewModel(
                entry.Key,
                entry.Value,
                () => AdjustCoreSkill(entry.Key, 1),
                () => AdjustCoreSkill(entry.Key, -1)));
        }
        OnPropertyChanged(nameof(HasCoreSkills));

        AttributeStats.Clear();
        foreach (var entry in _state.Character.Attributes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            AttributeStats.Add(new StatEntryViewModel(entry.Key, entry.Value));
        }

        InventoryCounters.Clear();
        foreach (var entry in _state.Character.Inventory.Counters.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            InventoryCounters.Add(new StatEntryViewModel(
                entry.Key,
                entry.Value,
                () => AdjustCounter(entry.Key, 1),
                () => AdjustCounter(entry.Key, -1)));
        }

        CharacterSkills.Clear();
        foreach (var skill in _state.Character.Disciplines.OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase))
        {
            CharacterSkills.Add(skill);
        }

        InventoryItems.Clear();
        foreach (var item in _state.Character.Inventory.Items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            InventoryItems.Add(new ItemEntryViewModel(item.Name, item.Category));
        }

        SelectedSkill = null;
        SelectedInventoryItem = null;
        OnPropertyChanged(nameof(CharacterPanelTitle));
    }

    private void UpdateCharacterOptions(SeriesProfileState seriesState)
    {
        _isUpdatingCharacters = true;
        Characters.Clear();

        foreach (var entry in seriesState.Characters.Values
                     .Where(state => !string.IsNullOrWhiteSpace(state.Character.Name))
                     .OrderBy(state => state.Character.Name, StringComparer.OrdinalIgnoreCase))
        {
            Characters.Add(new CharacterOptionViewModel(entry.Character.Name, entry, _state.SeriesId, ResolveSeriesName(_state.SeriesId)));
        }

        SelectedCharacter = Characters
            .FirstOrDefault(option => string.Equals(option.Name, seriesState.ActiveCharacterName, StringComparison.OrdinalIgnoreCase))
            ?? Characters.FirstOrDefault();

        _isUpdatingCharacters = false;
    }

    private void UpdateCharacterOptionsForProfile(PlayerProfile profile, string? activeSeriesId = null, string? activeCharacterName = null)
    {
        _isUpdatingCharacters = true;
        Characters.Clear();

        if (profile.SeriesStates is null)
        {
            SelectedCharacter = null;
            _isUpdatingCharacters = false;
            return;
        }

        foreach (var seriesEntry in profile.SeriesStates.OrderBy(entry => ResolveSeriesSortOrder(entry.Key)))
        {
            var seriesId = seriesEntry.Key;
            var seriesName = ResolveSeriesName(seriesId);
            foreach (var entry in seriesEntry.Value.Characters.Values
                         .Where(state => !string.IsNullOrWhiteSpace(state.Character.Name))
                         .OrderBy(state => state.Character.Name, StringComparer.OrdinalIgnoreCase))
            {
                Characters.Add(new CharacterOptionViewModel(entry.Character.Name, entry, seriesId, seriesName));
            }
        }

        if (!string.IsNullOrWhiteSpace(activeSeriesId) && !string.IsNullOrWhiteSpace(activeCharacterName))
        {
            SelectedCharacter = Characters.FirstOrDefault(option =>
                string.Equals(option.SeriesId, activeSeriesId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(option.Name, activeCharacterName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            SelectedCharacter = null;
        }
        _isUpdatingCharacters = false;
    }

    private bool EnsureSeriesProfile(string seriesId)
    {
        _currentProfile = ResolveSeriesProfile(seriesId);
        EnsureProfileContainer();

        var seriesState = EnsureSeriesState(_state.Profile, seriesId);
        _lastWizardCreatedNewCharacter = false;
        _lastWizardSeriesId = null;
        if (!seriesState.IsInitialized || seriesState.Characters.Count == 0)
        {
            if (!TryRunProfileWizard(seriesId, _state.Profile, false, out var wizardResult))
            {
                SetProfileSetupRequired($"Profile setup required for {_currentProfile.Name}.");
                return false;
            }

            ApplyProfileWizardResult(wizardResult);
            seriesState = wizardResult.SeriesState;
            LoadProfiles();
            _lastWizardCreatedNewCharacter = wizardResult.IsNewCharacter;
            _lastWizardSeriesId = wizardResult.SeriesId;
        }
        else if (_currentCharacterState is null)
        {
            var shouldUseExisting = MessageBox.Show(
                $"Use existing {_currentProfile.Name} character? Select 'No' to choose a different character.",
                "Profile Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (shouldUseExisting == MessageBoxResult.No)
            {
                if (TryRunProfileWizard(seriesId, _state.Profile, false, out var wizardResult))
                {
                    ApplyProfileWizardResult(wizardResult);
                    seriesState = wizardResult.SeriesState;
                    LoadProfiles();
                    _lastWizardCreatedNewCharacter = wizardResult.IsNewCharacter;
                    _lastWizardSeriesId = wizardResult.SeriesId;
                }
            }
        }

        if (!TryResolveActiveCharacter(seriesState, out var activeCharacter))
        {
            SetProfileSetupRequired($"Profile setup required for {_currentProfile.Name}.");
            return false;
        }

        _currentCharacterState = activeCharacter;
        _state.Character = activeCharacter.Character;
        EnsureSeriesDefaults(activeCharacter.Character);
        UpdateAvailableSkills();
        SuggestedActions.Clear();
        CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
        IsProfileReady = true;
        ApplyProfileNameToSaveSlot();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();
        UpdateCharacterOptions(seriesState);
        _isUpdatingProfiles = true;
        try
        {
            SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingProfiles = false;
        }
        return true;
    }

    private void SetProfileSetupRequired(string message)
    {
        _state.Character = new Character();
        _currentCharacterState = null;
        IsProfileReady = false;
        CharacterSetupHint = message;
        Characters.Clear();
        SelectedCharacter = null;
        AreChoicesVisible = false;
        ClearBookDisplay();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();
    }

    private void ApplySelectedProfile(PlayerProfile profile)
    {
        _state.Profile = profile;
        EnsureProfileContainer();
        _state.SeriesId = string.Empty;
        _currentCharacterState = null;
        IsProfileReady = false;
        CharacterSetupHint = "Select an existing character or create a new one to begin.";
        UpdateCharacterOptionsForProfile(profile);
        ClearBookDisplay();
        RefreshCharacterPanels();
    }

    private void EnsureSeriesDefaults(Character character)
    {
        if (!character.Attributes.ContainsKey(Character.CombatSkillBonusAttribute))
        {
            character.Attributes[Character.CombatSkillBonusAttribute] = 0;
        }

        foreach (var entry in _currentProfile.CoreSkills)
        {
            if (!character.CoreSkills.ContainsKey(entry.Key))
            {
                character.CoreSkills[entry.Key] = entry.Value;
            }
        }

        if (_currentProfile.CoreSkills.Count > 0 && !character.Attributes.ContainsKey("CoreSkillPoolTotal"))
        {
            character.Attributes["CoreSkillPoolTotal"] = character.CoreSkills.Values.Sum();
        }

        foreach (var counter in _currentProfile.DefaultCounters)
        {
            if (!character.Inventory.Counters.ContainsKey(counter.Key))
            {
                character.Inventory.Counters[counter.Key] = counter.Value;
            }
        }

        foreach (var item in _currentProfile.DefaultItems)
        {
            if (character.Inventory.Items.Any(existing =>
                    string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Category, item.Category, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            character.Inventory.Items.Add(item);
        }
    }

    private void UpdateAvailableSkills()
    {
        AvailableSkills.Clear();
        foreach (var skill in _currentProfile.SkillNames)
        {
            AvailableSkills.Add(skill);
        }
        OnPropertyChanged(nameof(HasAvailableSkills));
    }

    private void EnsureProfileContainer()
    {
        _state.Profile ??= new PlayerProfile();
        if (_state.Profile.SeriesStates is null)
        {
            _state.Profile.SeriesStates = new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private SeriesProfileState EnsureSeriesState(PlayerProfile profile, string seriesId)
    {
        profile.SeriesStates ??= new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);
        if (!profile.SeriesStates.TryGetValue(seriesId, out var seriesState))
        {
            seriesState = new SeriesProfileState();
            profile.SeriesStates[seriesId] = seriesState;
        }

        seriesState.Characters ??= new Dictionary<string, CharacterProfileState>(StringComparer.OrdinalIgnoreCase);

        if (seriesState.Characters.Count == 0 && HasLegacyCharacterData(seriesState.Character))
        {
            var legacyName = string.IsNullOrWhiteSpace(seriesState.Character.Name)
                ? _currentProfile.DefaultCharacterName
                : seriesState.Character.Name;
            if (string.IsNullOrWhiteSpace(seriesState.Character.Name))
            {
                seriesState.Character.Name = legacyName;
            }
            seriesState.Characters[legacyName] = new CharacterProfileState
            {
                Character = seriesState.Character
            };
            if (string.IsNullOrWhiteSpace(seriesState.ActiveCharacterName))
            {
                seriesState.ActiveCharacterName = legacyName;
            }
        }

        if (!string.IsNullOrWhiteSpace(seriesState.ActiveCharacterName)
            && !seriesState.Characters.ContainsKey(seriesState.ActiveCharacterName)
            && seriesState.Characters.Count > 0)
        {
            seriesState.ActiveCharacterName = seriesState.Characters.Keys.First();
        }

        if (seriesState.Characters.Count > 0)
        {
            seriesState.IsInitialized = true;
        }

        return seriesState;
    }

    private static bool HasLegacyCharacterData(Character character)
    {
        return !string.IsNullOrWhiteSpace(character.Name)
            || character.CombatSkill > 0
            || character.Endurance > 0
            || character.Attributes.Count > 0
            || character.CoreSkills.Count > 0
            || character.Disciplines.Count > 0
            || character.Inventory.Items.Count > 0
            || character.Inventory.Counters.Count > 0;
    }

    private bool TryResolveActiveCharacter(SeriesProfileState seriesState, out CharacterProfileState activeCharacter)
    {
        if (!string.IsNullOrWhiteSpace(seriesState.ActiveCharacterName)
            && seriesState.Characters.TryGetValue(seriesState.ActiveCharacterName, out activeCharacter))
        {
            return true;
        }

        if (seriesState.Characters.Count > 0)
        {
            activeCharacter = seriesState.Characters.Values.First();
            if (string.IsNullOrWhiteSpace(activeCharacter.Character.Name))
            {
                activeCharacter.Character.Name = _currentProfile.DefaultCharacterName;
            }

            seriesState.ActiveCharacterName = activeCharacter.Character.Name;
            return true;
        }

        activeCharacter = null!;
        return false;
    }

    private void ApplyProfileWizardResult(ProfileWizardResult wizardResult)
    {
        _state.Profile = wizardResult.Profile;
        EnsureProfileContainer();
        _state.Profile.SeriesStates[wizardResult.SeriesId] = wizardResult.SeriesState;
        _state.SeriesId = wizardResult.SeriesId;
        _currentProfile = ResolveSeriesProfile(wizardResult.SeriesId);
        _currentCharacterState = wizardResult.CharacterState;
        if (wizardResult.CharacterState is not null)
        {
            wizardResult.SeriesState.Character = wizardResult.CharacterState.Character;
        }
    }

    private void PersistActiveCharacterState()
    {
        if (string.IsNullOrWhiteSpace(_state.SeriesId) || _currentCharacterState is null)
        {
            return;
        }

        var seriesState = EnsureSeriesState(_state.Profile, _state.SeriesId);
        var characterName = string.IsNullOrWhiteSpace(seriesState.ActiveCharacterName)
            ? _currentCharacterState.Character.Name
            : seriesState.ActiveCharacterName;

        if (string.IsNullOrWhiteSpace(characterName))
        {
            characterName = _currentProfile.DefaultCharacterName;
        }

        seriesState.ActiveCharacterName = characterName;
        seriesState.Characters[characterName] = _currentCharacterState;
        seriesState.Character = _currentCharacterState.Character;
        seriesState.IsInitialized = true;
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

    private bool TryRunProfileWizard(
        string? initialSeriesId,
        PlayerProfile? selectedProfile,
        bool isSeriesSelectionEnabled,
        out ProfileWizardResult result)
    {
        var existingProfiles = LoadExistingProfiles();
        var viewModel = new ProfileWizardViewModel(
            _gameService.RollRandomNumber,
            existingProfiles,
            initialSeriesId,
            isSeriesSelectionEnabled);

        var profileToSelect = selectedProfile;
        if (profileToSelect is null && !string.IsNullOrWhiteSpace(_state.Profile.Name))
        {
            profileToSelect = existingProfiles.FirstOrDefault(option =>
                string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (profileToSelect is not null)
        {
            var existingProfileOption = viewModel.ExistingProfiles
                .FirstOrDefault(option => string.Equals(option.Name, profileToSelect.Name, StringComparison.OrdinalIgnoreCase));
            if (existingProfileOption is not null)
            {
                viewModel.SelectedExistingProfile = existingProfileOption;
            }
            else
            {
                viewModel.ProfileName = profileToSelect.Name;
            }

            if (profileToSelect.SeriesStates.TryGetValue(viewModel.SeriesId, out var seriesState)
                && !string.IsNullOrWhiteSpace(seriesState.ActiveCharacterName))
            {
                var existingCharacterOption = viewModel.ExistingCharacters
                    .FirstOrDefault(option => !option.IsNew
                        && string.Equals(option.Name, seriesState.ActiveCharacterName, StringComparison.OrdinalIgnoreCase));
                if (existingCharacterOption is not null)
                {
                    viewModel.SelectedExistingCharacter = existingCharacterOption;
                }
            }
        }

        var window = new ProfileWizardWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var dialogResult = window.ShowDialog();
        if (dialogResult != true || !viewModel.IsValid)
        {
            result = new ProfileWizardResult(new PlayerProfile(), string.Empty, new SeriesProfileState(), string.Empty, null, false);
            return false;
        }

        var selectedProfile = viewModel.SelectedExistingProfile?.Profile ?? new PlayerProfile();
        selectedProfile.Name = viewModel.ProfileName.Trim();
        selectedProfile.SeriesStates ??= new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);

        var seriesId = viewModel.SeriesId;
        var seriesProfile = ResolveSeriesProfile(seriesId);
        var selectedSeriesState = EnsureSeriesState(selectedProfile, seriesId);

        if (!viewModel.IsCharacterCreationEnabled)
        {
            var selectedCharacterState = viewModel.SelectedExistingCharacter?.CharacterState;
            if (selectedCharacterState is null)
            {
                result = new ProfileWizardResult(selectedProfile, seriesId, selectedSeriesState, string.Empty, null, false);
                return false;
            }

            var selectedName = selectedCharacterState.Character.Name;
            selectedSeriesState.ActiveCharacterName = selectedName;
            selectedSeriesState.IsInitialized = true;
            result = new ProfileWizardResult(selectedProfile, seriesId, selectedSeriesState, selectedName, selectedCharacterState, false);
            return true;
        }

        var character = viewModel.BuildCharacter();
        var characterName = string.IsNullOrWhiteSpace(character.Name)
            ? seriesProfile.DefaultCharacterName
            : character.Name.Trim();
        if (string.IsNullOrWhiteSpace(character.Name))
        {
            character.Name = characterName;
        }

        if (selectedSeriesState.Characters.ContainsKey(characterName))
        {
            var confirm = MessageBox.Show(
                $"A character named '{characterName}' already exists for this profile. Continuing will reset their progress. Continue?",
                "Overwrite Character",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                result = new ProfileWizardResult(selectedProfile, seriesId, selectedSeriesState, string.Empty, null, false);
                return false;
            }
        }

        var characterState = new CharacterProfileState
        {
            Character = character
        };

        selectedSeriesState.Characters[characterName] = characterState;
        selectedSeriesState.ActiveCharacterName = characterName;
        selectedSeriesState.IsInitialized = true;

        result = new ProfileWizardResult(selectedProfile, seriesId, selectedSeriesState, characterName, characterState, true);
        return true;
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

    private string? GetFirstBookIdForSeries(string seriesId)
    {
        return Books
            .Where(book => string.Equals(book.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(book => book.Order ?? int.MaxValue)
            .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .Select(book => book.Id)
            .FirstOrDefault();
    }

    private static ISeriesProfile ResolveSeriesProfile(string seriesId)
    {
        return seriesId switch
        {
            "lw" => SeriesProfiles.LoneWolf,
            "gs" => SeriesProfiles.GreyStar,
            "fw" => SeriesProfiles.FreewayWarrior,
            _ => SeriesProfiles.LoneWolf
        };
    }

    private async Task SaveGameAsync()
    {
        if (_book is null)
        {
            return;
        }

        var slot = NormalizeSaveSlot();
        PersistActiveCharacterState();
        await _gameService.SaveGameAsync(slot, _state);
        LoadSaveSlots();
        SaveSlot = slot;
        MessageBox.Show($"Saved to slot '{slot}'.", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task LoadGameAsync()
    {
        var slot = NormalizeSaveSlot();
        var loaded = await _gameService.LoadGameAsync(slot);
        if (loaded is null)
        {
            MessageBox.Show($"No save found for slot '{slot}'.", "Load Game", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_book is not null
            && !string.IsNullOrWhiteSpace(loaded.SeriesId)
            && !string.Equals(loaded.SeriesId, _state.SeriesId, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("This save belongs to a different series and cannot be loaded for this book.", "Load Game", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ApplyLoadedState(loaded);
        if (_book is null || !string.Equals(_book.Id, _state.BookId, StringComparison.OrdinalIgnoreCase))
        {
            _book = await _bookRepository.GetBookAsync(_state.BookId);
            BookTitle = _book.Title;
        }

        var section = _book.Sections.FirstOrDefault(item => item.Id == _state.SectionId);
        if (section is not null)
        {
            UpdateSection(section);
        }
    }

    private void ApplyLoadedState(GameState loaded)
    {
        _state.BookId = loaded.BookId;
        _state.SectionId = loaded.SectionId;
        _state.SeriesId = loaded.SeriesId;
        _state.Profile = loaded.Profile ?? new PlayerProfile();
        EnsureProfileContainer();
        LoadProfiles(false);

        if (!string.IsNullOrWhiteSpace(_state.SeriesId))
        {
            _currentProfile = ResolveSeriesProfile(_state.SeriesId);
            var seriesState = EnsureSeriesState(_state.Profile, _state.SeriesId);
            if (seriesState.IsInitialized && TryResolveActiveCharacter(seriesState, out var activeCharacter))
            {
                _currentCharacterState = activeCharacter;
                _state.Character = activeCharacter.Character;
                EnsureSeriesDefaults(_state.Character);
                UpdateAvailableSkills();
                CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
                IsProfileReady = true;
                ApplyProfileNameToSaveSlot();
                RefreshCharacterPanels();
                UpdateBookProgressIndicators();
                UpdateCharacterOptionsForProfile(_state.Profile, _state.SeriesId, activeCharacter.Character.Name);
                _isUpdatingProfiles = true;
                try
                {
                    SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
                }
                finally
                {
                    _isUpdatingProfiles = false;
                }
                return;
            }
        }
        else
        {
            IsProfileReady = false;
        }

        _state.Character = loaded.Character;
        _currentProfile = ResolveSeriesProfile(_state.SeriesId);
        if (!string.IsNullOrWhiteSpace(_state.SeriesId))
        {
            var seriesState = EnsureSeriesState(_state.Profile, _state.SeriesId);
            var characterName = string.IsNullOrWhiteSpace(_state.Character.Name)
                ? _currentProfile.DefaultCharacterName
                : _state.Character.Name;
            var characterState = new CharacterProfileState
            {
                Character = _state.Character
            };
            characterState.LastBookId = _state.BookId;
            characterState.LastSectionId = _state.SectionId;
            seriesState.Characters[characterName] = characterState;
            seriesState.ActiveCharacterName = characterName;
            seriesState.IsInitialized = true;
            _currentCharacterState = characterState;
            if (!string.IsNullOrWhiteSpace(_state.BookId) && !string.IsNullOrWhiteSpace(_state.SectionId))
            {
                var progress = new BookProgressState();
                UpdateProgressState(progress, _state.BookId, _state.SectionId);
                characterState.BookProgress[_state.BookId] = progress;
            }
        }
        EnsureSeriesDefaults(_state.Character);
        UpdateAvailableSkills();
        CharacterSetupHint = $"Use the book's character creation instructions in the main text. Series: {_currentProfile.Name}.";
        IsProfileReady = !string.IsNullOrWhiteSpace(_state.SeriesId);
        ApplyProfileNameToSaveSlot();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();
        if (!string.IsNullOrWhiteSpace(_state.SeriesId))
        {
            var seriesState = EnsureSeriesState(_state.Profile, _state.SeriesId);
            UpdateCharacterOptionsForProfile(_state.Profile, _state.SeriesId, seriesState.ActiveCharacterName);
            _isUpdatingProfiles = true;
            try
            {
                SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _isUpdatingProfiles = false;
            }
        }
    }

    private void ApplyProfileNameToSaveSlot()
    {
        if (string.IsNullOrWhiteSpace(_state.Profile.Name))
        {
            return;
        }

        SaveSlot = _state.Profile.Name.Trim();
    }

    private string ReplaceCharacterTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var name = string.IsNullOrWhiteSpace(_state.Character.Name)
            ? _currentProfile.DefaultCharacterName
            : _state.Character.Name;

        return Regex.Replace(text, Regex.Escape(CharacterNameToken), name, RegexOptions.IgnoreCase);
    }

    private void AddSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedAvailableSkill))
        {
            return;
        }

        if (!_state.Character.Disciplines.Contains(SelectedAvailableSkill, StringComparer.OrdinalIgnoreCase))
        {
            _state.Character.Disciplines.Add(SelectedAvailableSkill);
        }

        RefreshCharacterPanels();
    }

    private void RemoveSkill()
    {
        if (SelectedSkill is null)
        {
            return;
        }

        _state.Character.Disciplines.RemoveAll(skill => string.Equals(skill, SelectedSkill, StringComparison.OrdinalIgnoreCase));
        SelectedSkill = null;
        RefreshCharacterPanels();
        _removeSkillCommand.RaiseCanExecuteChanged();
    }

    private void AddItem()
    {
        var name = NewItemName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var category = string.IsNullOrWhiteSpace(NewItemCategory) ? "general" : NewItemCategory.Trim();
        _state.Character.Inventory.Items.Add(new Item(name, category));
        NewItemName = string.Empty;
        RefreshCharacterPanels();
    }

    private void RemoveItem()
    {
        if (SelectedInventoryItem is null)
        {
            return;
        }

        var item = _state.Character.Inventory.Items.FirstOrDefault(entry =>
            string.Equals(entry.Name, SelectedInventoryItem.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Category, SelectedInventoryItem.Category, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            _state.Character.Inventory.Items.Remove(item);
        }

        SelectedInventoryItem = null;
        RefreshCharacterPanels();
    }

    private void AddCounter()
    {
        var name = CounterNameInput.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_state.Character.Inventory.Counters.ContainsKey(name))
        {
            return;
        }

        _state.Character.Inventory.Counters[name] = 0;
        CounterNameInput = string.Empty;
        RefreshCharacterPanels();
    }

    private void StartNewProfile()
    {
        var result = MessageBox.Show(
            "Starting a new profile will unload your current character and clear the story text. Continue?",
            "New Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        ResetToStartState();
        if (!TryRunProfileWizard(null, null, true, out var wizardResult))
        {
            SetProfileSetupRequired("Select a profile to continue.");
            return;
        }

        ApplyProfileWizardResult(wizardResult);
        if (wizardResult.CharacterState is not null)
        {
            _currentCharacterState = wizardResult.CharacterState;
            _state.Character = wizardResult.CharacterState.Character;
            EnsureSeriesDefaults(_state.Character);
            UpdateAvailableSkills();
            SuggestedActions.Clear();
            CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
            IsProfileReady = true;
            ApplyProfileNameToSaveSlot();
            RefreshCharacterPanels();
            UpdateBookProgressIndicators();
        }

        LoadProfiles();
        UpdateCharacterOptionsForProfile(_state.Profile, _state.SeriesId, wizardResult.CharacterName);

        if (_currentCharacterState is not null)
        {
            var targetBookId = wizardResult.IsNewCharacter
                ? GetFirstBookIdForSeries(wizardResult.SeriesId)
                : _currentCharacterState.LastBookId;

            if (string.IsNullOrWhiteSpace(targetBookId))
            {
                targetBookId = GetFirstBookIdForSeries(wizardResult.SeriesId);
            }

            if (!string.IsNullOrWhiteSpace(targetBookId))
            {
                var targetBook = Books.FirstOrDefault(book => string.Equals(book.Id, targetBookId, StringComparison.OrdinalIgnoreCase));
                if (targetBook is not null)
                {
                    SelectedBook = targetBook;
                }
                else
                {
                    _ = LoadBookAsync(targetBookId);
                }
            }
        }
    }

    private void ResetToStartState()
    {
        _book = null;
        _firstSectionForFrontMatter = null;
        _frontMatterQueue.Clear();
        _state.BookId = string.Empty;
        _state.SeriesId = string.Empty;
        _state.SectionId = string.Empty;
        _state.Character = new Character();
        _state.Profile = new PlayerProfile();
        _currentProfile = SeriesProfiles.LoneWolf;
        _currentCharacterState = null;

        BookTitle = "Aon Companion";
        SectionTitle = "Select a book";
        CharacterSetupHint = string.Empty;
        Blocks.Clear();
        Choices.Clear();
        ResetRandomNumberState();
        SuggestedActions.Clear();
        AreChoicesVisible = false;
        AvailableSkills.Clear();
        CharacterSkills.Clear();
        CoreStats.Clear();
        CoreSkills.Clear();
        AttributeStats.Clear();
        InventoryCounters.Clear();
        InventoryItems.Clear();
        Characters.Clear();
        SelectedCharacter = null;
        SelectedBook = null;
        SelectedProfile = null;
        IsProfileReady = false;
        OnPropertyChanged(nameof(HasCoreSkills));
        OnPropertyChanged(nameof(HasAvailableSkills));
        UpdateBookProgressIndicators();
        LoadProfiles();
    }

    private async Task SwitchCharacterAsync(CharacterOptionViewModel option)
    {
        if (_state.Profile is null || option.CharacterState is null)
        {
            return;
        }

        if (_currentCharacterState == option.CharacterState)
        {
            return;
        }

        PersistActiveCharacterState();
        _isSwitchingCharacter = true;
        try
        {
            var seriesId = option.SeriesId ?? _state.SeriesId;
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return;
            }

            _state.SeriesId = seriesId;
            _currentProfile = ResolveSeriesProfile(seriesId);
            var seriesState = EnsureSeriesState(_state.Profile, seriesId);
            seriesState.ActiveCharacterName = option.Name;

            _currentCharacterState = option.CharacterState;
            _state.Character = option.CharacterState.Character;
            EnsureSeriesDefaults(_state.Character);
            UpdateAvailableSkills();
            SuggestedActions.Clear();
            CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
            IsProfileReady = true;
            RefreshCharacterPanels();
            UpdateBookProgressIndicators();

            var targetBookId = option.CharacterState.LastBookId;
            if (string.IsNullOrWhiteSpace(targetBookId))
            {
                targetBookId = GetFirstBookIdForSeries(seriesId) ?? _book?.Id ?? _state.BookId;
            }

            if (!string.IsNullOrWhiteSpace(targetBookId))
            {
                var targetBook = Books.FirstOrDefault(book => string.Equals(book.Id, targetBookId, StringComparison.OrdinalIgnoreCase));
                if (targetBook is not null)
                {
                    if (SelectedBook?.Id == targetBook.Id)
                    {
                        await LoadBookAsync(targetBook.Id);
                    }
                    else
                    {
                        SelectedBook = targetBook;
                    }
                }
                else
                {
                    await LoadBookAsync(targetBookId);
                }
            }
        }
        finally
        {
            _isSwitchingCharacter = false;
        }
    }

    private async Task LoadCharacterSelectionAsync(CharacterOptionViewModel option)
    {
        if (option.CharacterState is null || SelectedProfile is null)
        {
            return;
        }

        var seriesId = option.SeriesId;
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            return;
        }

        _state.Profile = SelectedProfile.Profile;
        EnsureProfileContainer();
        _state.SeriesId = seriesId;
        _currentProfile = ResolveSeriesProfile(seriesId);
        _currentCharacterState = option.CharacterState;
        _state.Character = option.CharacterState.Character;
        EnsureSeriesDefaults(_state.Character);
        UpdateAvailableSkills();
        SuggestedActions.Clear();
        CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
        IsProfileReady = true;
        ApplyProfileNameToSaveSlot();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();

        var targetBookId = option.CharacterState.LastBookId;
        if (string.IsNullOrWhiteSpace(targetBookId))
        {
            targetBookId = GetFirstBookIdForSeries(seriesId);
        }

        if (!string.IsNullOrWhiteSpace(targetBookId))
        {
            var targetBook = Books.FirstOrDefault(book => string.Equals(book.Id, targetBookId, StringComparison.OrdinalIgnoreCase));
            if (targetBook is not null)
            {
                SelectedBook = targetBook;
            }
            else
            {
                await LoadBookAsync(targetBookId);
            }
        }
    }

    private async Task CreateNewCharacterAsync()
    {
        var initialSeriesId = string.IsNullOrWhiteSpace(_state.SeriesId) ? null : _state.SeriesId;
        var existingProfile = SelectedProfile?.Profile ?? _state.Profile;
        if (!TryRunProfileWizard(initialSeriesId, existingProfile, true, out var wizardResult))
        {
            return;
        }

        ApplyProfileWizardResult(wizardResult);
        var seriesState = wizardResult.SeriesState;
        if (wizardResult.CharacterState is not null)
        {
            _currentCharacterState = wizardResult.CharacterState;
            _state.Character = wizardResult.CharacterState.Character;
            EnsureSeriesDefaults(_state.Character);
            UpdateAvailableSkills();
            SuggestedActions.Clear();
            CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
            IsProfileReady = true;
            RefreshCharacterPanels();
            UpdateBookProgressIndicators();
        }

        LoadProfiles();
        UpdateCharacterOptions(seriesState);
        _isUpdatingProfiles = true;
        try
        {
            SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingProfiles = false;
        }

        if (_currentCharacterState is null)
        {
            return;
        }

        var targetBookId = wizardResult.IsNewCharacter
            ? GetFirstBookIdForSeries(wizardResult.SeriesId)
            : _currentCharacterState.LastBookId;

        if (string.IsNullOrWhiteSpace(targetBookId))
        {
            targetBookId = GetFirstBookIdForSeries(wizardResult.SeriesId);
        }

        if (!string.IsNullOrWhiteSpace(targetBookId))
        {
            var targetBook = Books.FirstOrDefault(book => string.Equals(book.Id, targetBookId, StringComparison.OrdinalIgnoreCase));
            if (targetBook is not null)
            {
                if (SelectedBook?.Id == targetBook.Id)
                {
                    await LoadBookAsync(targetBook.Id);
                }
                else
                {
                    SelectedBook = targetBook;
                }
            }
            else
            {
                await LoadBookAsync(targetBookId);
            }
        }
    }

    private void AdjustCoreSkill(string skillName, int delta)
    {
        if (!_state.Character.CoreSkills.TryGetValue(skillName, out var value))
        {
            value = 0;
        }

        if (delta > 0 && TryGetCoreSkillPool(out var poolTotal))
        {
            var currentTotal = _state.Character.CoreSkills.Values.Sum();
            if (currentTotal + delta > poolTotal)
            {
                return;
            }
        }

        _state.Character.CoreSkills[skillName] = Math.Max(0, value + delta);
        RefreshCharacterPanels();
    }

    private bool TryGetCoreSkillPool(out int poolTotal)
    {
        poolTotal = 0;
        if (!_state.Character.Attributes.TryGetValue("CoreSkillPoolTotal", out var value))
        {
            return false;
        }

        poolTotal = value;
        return poolTotal > 0;
    }

    private void AdjustCounter(string name, int delta)
    {
        if (!_state.Character.Inventory.Counters.TryGetValue(name, out var current))
        {
            return;
        }

        var updated = Math.Max(0, current + delta);
        _state.Character.Inventory.Counters[name] = updated;
        RefreshCharacterPanels();
    }

    private void UpdateSuggestedActions(BookSection section)
    {
        SuggestedActions.Clear();
        if (_currentProfile is null)
        {
            return;
        }

        var text = string.Join(" ", section.Blocks.Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var counter in _currentProfile.DefaultCounters.Keys)
        {
            var amount = GetCounterAmountFromText(text, counter);
            if (amount <= 0)
            {
                continue;
            }

            var label = $"Add {amount} {counter}";
            SuggestedActions.Add(new QuickActionViewModel(label, () =>
            {
                var current = _state.Character.Inventory.Counters.GetValueOrDefault(counter, 0);
                _state.Character.Inventory.Counters[counter] = current + amount;
                RefreshCharacterPanels();
            }));
        }

        foreach (var skill in _currentProfile.SkillNames)
        {
            if (_state.Character.Disciplines.Contains(skill, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsSkillSuggested(text, skill))
            {
                continue;
            }

            SuggestedActions.Add(new QuickActionViewModel($"Add skill: {skill}", () =>
            {
                _state.Character.Disciplines.Add(skill);
                RefreshCharacterPanels();
            }));
        }
    }

    private static int GetCounterAmountFromText(string text, string counterName)
    {
        var escaped = Regex.Escape(counterName);
        var match = Regex.Match(text, $"(?<value>\\d+)\\s+{escaped}\\b", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return 0;
        }

        return int.TryParse(match.Groups["value"].Value, out var value) ? value : 0;
    }

    private static bool IsSkillSuggested(string text, string skillName)
    {
        var escaped = Regex.Escape(skillName);
        return Regex.IsMatch(text, $"\\b(?:gain|learn|choose|acquire|pick)\\b[^.]*\\b{escaped}\\b", RegexOptions.IgnoreCase);
    }

    private void PrepareRandomNumberSection(BookSection section)
    {
        _pendingRandomChoices.Clear();
        _pendingRandomChoices.AddRange(section.Choices);
        _randomNumberChoices.Clear();
        _randomNumberChoices.AddRange(BuildRandomNumberChoices(section.Choices));
        _randomNumberResult = null;
        _resolvedRandomChoice = null;
        ResetRollHistory();
        RandomNumberStatus = "Roll a number from the Random Number Table (0–9).";
        IsRandomNumberVisible = true;
        AreChoicesVisible = false;
        Choices.Clear();
        OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
        _confirmRandomNumberCommand.RaiseCanExecuteChanged();
    }

    private void ShowChoices(IEnumerable<Choice> choices)
    {
        Choices.Clear();
        foreach (var choice in choices)
        {
            var command = new RelayCommand(() => _ = ApplyChoiceAsync(choice));
            var isEnabled = IsChoiceAvailable(choice);
            Choices.Add(new ChoiceViewModel(ReplaceCharacterTokens(choice.Text), command, isEnabled));
        }

        AreChoicesVisible = Choices.Count > 0;
    }

    private bool IsChoiceAvailable(Choice choice)
    {
        if (string.IsNullOrWhiteSpace(choice.Text))
        {
            return true;
        }

        if (!TryGetRequiredSkill(choice.Text, out var requiredSkill))
        {
            return true;
        }

        return _state.Character.Disciplines.Contains(requiredSkill, StringComparer.OrdinalIgnoreCase);
    }

    private bool TryGetRequiredSkill(string text, out string requiredSkill)
    {
        requiredSkill = string.Empty;
        if (_currentProfile.SkillNames.Count == 0)
        {
            return false;
        }

        var requiresKaiDiscipline = _state.SeriesId.Equals("lw", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Kai Discipline", StringComparison.OrdinalIgnoreCase);
        var requiresMagicalPower = _state.SeriesId.Equals("gs", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("Magical Power", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Power of", StringComparison.OrdinalIgnoreCase));

        if (!requiresKaiDiscipline && !requiresMagicalPower)
        {
            return false;
        }

        foreach (var skill in _currentProfile.SkillNames)
        {
            if (text.Contains(skill, StringComparison.OrdinalIgnoreCase))
            {
                requiredSkill = skill;
                return true;
            }
        }

        return false;
    }

    private void RollRandomNumber()
    {
        if (_book is null)
        {
            return;
        }

        var roll = _gameService.RollRandomNumber();
        _randomNumberResult = roll;
        TrackRoll(roll);
        var modifier = GetRollModifier();
        var effectiveRoll = Math.Clamp(roll + modifier, 0, 9);

        if (IsManualRandomMode)
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, effectiveRoll, "Select the correct outcome below.");
            ShowChoices(_pendingRandomChoices);
            OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
            _confirmRandomNumberCommand.RaiseCanExecuteChanged();
            return;
        }

        var matches = _randomNumberChoices
            .Where(choice => choice.IsMatch(effectiveRoll))
            .Select(choice => choice.Choice)
            .Distinct()
            .ToList();

        if (matches.Count == 1)
        {
            _resolvedRandomChoice = matches[0];
            RandomNumberStatus = BuildRollStatus(roll, modifier, effectiveRoll, $"This directs you to section {_resolvedRandomChoice.TargetId}.");
            AreChoicesVisible = false;
        }
        else if (matches.Count > 1)
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, effectiveRoll, "Multiple outcomes match—choose the correct option below.");
            ShowChoices(matches);
        }
        else
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, effectiveRoll, "Choose the correct option below.");
            ShowChoices(_pendingRandomChoices);
        }

        OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
        _confirmRandomNumberCommand.RaiseCanExecuteChanged();
    }

    private void ConfirmRandomNumber()
    {
        if (_resolvedRandomChoice is null)
        {
            return;
        }

        _ = ApplyChoiceAsync(_resolvedRandomChoice);
    }

    private void ShowRandomNumberTable()
    {
        const string tableText = "0  1  2  3  4  5  6  7  8  9";
        MessageBox.Show($"Random Number Table\n\n{tableText}", "Random Number Table", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool RequiresRandomNumber(BookSection section)
    {
        return section.Blocks.Any(block => block.Text.Contains("random number table", StringComparison.OrdinalIgnoreCase)
            || block.Text.Contains("pick a number", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<RandomNumberChoice> BuildRandomNumberChoices(IEnumerable<Choice> choices)
    {
        foreach (var choice in choices)
        {
            var ranges = ParseRandomNumberRanges(choice.Text);
            if (ranges.Count == 0)
            {
                continue;
            }

            yield return new RandomNumberChoice(choice, ranges);
        }
    }

    private static List<RandomNumberRange> ParseRandomNumberRanges(string text)
    {
        var sanitized = StripTargetReferences(text);
        if (Regex.Matches(sanitized, "\\bif\\b", RegexOptions.IgnoreCase).Count > 1)
        {
            return new List<RandomNumberRange>();
        }

        var exceptMatch = Regex.Match(sanitized, "\\bexcept(?:\\s+a)?\\s+(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
        if (exceptMatch.Success)
        {
            if (!TryParseDigit(exceptMatch.Groups["value"].Value, out var exceptValue))
            {
                return new List<RandomNumberRange>();
            }

            return BuildExceptRanges(exceptValue);
        }

        var rangeMatches = RangeRegex.Matches(sanitized);
        if (rangeMatches.Count > 1)
        {
            return new List<RandomNumberRange>();
        }

        if (rangeMatches.Count == 1)
        {
            var minText = rangeMatches[0].Groups["min"].Value;
            var maxText = rangeMatches[0].Groups["max"].Value;
            if (!TryParseDigit(minText, out var min) || !TryParseDigit(maxText, out var max))
            {
                return new List<RandomNumberRange>();
            }

            return new List<RandomNumberRange> { RandomNumberRange.From(min, max) };
        }

        if (TryMatchComparison(sanitized, BelowRegex, out var belowValue))
        {
            return new List<RandomNumberRange> { RandomNumberRange.From(0, belowValue - 1) };
        }

        if (TryMatchComparison(sanitized, AtMostRegex, out var atMostValue))
        {
            return new List<RandomNumberRange> { RandomNumberRange.From(0, atMostValue) };
        }

        if (TryMatchComparison(sanitized, AtLeastRegex, out var atLeastValue))
        {
            return new List<RandomNumberRange> { RandomNumberRange.From(atLeastValue, 9) };
        }

        if (TryMatchComparison(sanitized, AboveRegex, out var aboveValue))
        {
            return new List<RandomNumberRange> { RandomNumberRange.From(aboveValue + 1, 9) };
        }

        var exactMatch = Regex.Match(sanitized, "\\b(?:number|pick|picked|roll|rolled|score)\\s+(?:is\\s+|a\\s+)?(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
        if (exactMatch.Success && TryParseDigit(exactMatch.Groups["value"].Value, out var exactValue))
        {
            return new List<RandomNumberRange> { RandomNumberRange.From(exactValue, exactValue) };
        }

        return new List<RandomNumberRange>();
    }

    private static List<RandomNumberRange> BuildExceptRanges(int value)
    {
        var ranges = new List<RandomNumberRange>();
        if (value > 0)
        {
            ranges.Add(RandomNumberRange.From(0, value - 1));
        }

        if (value < 9)
        {
            ranges.Add(RandomNumberRange.From(value + 1, 9));
        }

        return ranges;
    }

    private static bool TryMatchComparison(string text, Regex regex, out int value)
    {
        value = 0;
        var match = regex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        return TryParseDigit(match.Groups["value"].Value, out value);
    }

    private static bool TryParseDigit(string value, out int parsed)
    {
        parsed = 0;
        if (!int.TryParse(value, out parsed))
        {
            return false;
        }

        return parsed is >= 0 and <= 9;
    }

    private static string StripTargetReferences(string text)
    {
        return Regex.Replace(text, "\\b(turn to|turning to|go to)\\s+\\d+\\b", string.Empty, RegexOptions.IgnoreCase);
    }

    private void TrackRoll(int roll)
    {
        _recentRolls.Enqueue(roll);
        while (_recentRolls.Count > 3)
        {
            _recentRolls.Dequeue();
        }

        var rolls = _recentRolls.Select(value => value.ToString());
        RollHistoryText = $"Recent rolls: {string.Join(", ", rolls)}";
    }

    private void ResetRollHistory()
    {
        _recentRolls.Clear();
        RollHistoryText = "Recent rolls: —";
    }

    private int GetRollModifier()
    {
        if (int.TryParse(RollModifierText, out var modifier))
        {
            return modifier;
        }

        RollModifierText = "0";
        return 0;
    }

    private static string BuildRollStatus(int roll, int modifier, int effectiveRoll, string suffix)
    {
        return modifier == 0
            ? $"You rolled {roll}. {suffix}"
            : $"You rolled {roll} + {modifier} = {effectiveRoll}. {suffix}";
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

    private void LoadSaveSlots()
    {
        SaveSlots.Clear();
        if (!Directory.Exists(_saveDirectory))
        {
            return;
        }

        var slots = Directory.EnumerateFiles(_saveDirectory, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            SaveSlots.Add(slot);
        }
    }

    private void LoadProfiles(bool shouldSetProfileRequired = true)
    {
        Profiles.Clear();
        var profiles = LoadExistingProfiles()
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .DistinctBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            Profiles.Add(new ProfileOptionViewModel(profile));
        }

        _isUpdatingProfiles = true;
        try
        {
            SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile?.Name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingProfiles = false;
        }
        if (shouldSetProfileRequired && SelectedProfile is null)
        {
            SetProfileSetupRequired("Select a profile to continue.");
        }
    }

    private IReadOnlyList<PlayerProfile> LoadExistingProfiles()
    {
        var profiles = new List<PlayerProfile>();
        if (!string.IsNullOrWhiteSpace(_state.Profile?.Name))
        {
            profiles.Add(_state.Profile);
        }

        if (!Directory.Exists(_saveDirectory))
        {
            return profiles;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        foreach (var file in Directory.EnumerateFiles(_saveDirectory, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var loaded = JsonSerializer.Deserialize<GameState>(stream, options);
                if (!string.IsNullOrWhiteSpace(loaded?.Profile?.Name))
                {
                    profiles.Add(loaded.Profile);
                }
            }
            catch (Exception)
            {
                continue;
            }
        }

        return profiles;
    }

    private string NormalizeSaveSlot()
    {
        var slot = string.IsNullOrWhiteSpace(SaveSlot) ? "default" : SaveSlot.Trim();
        return string.IsNullOrWhiteSpace(slot) ? "default" : slot;
    }

    private void CreateNewSaveSlot()
    {
        var nextSlot = GetNextSaveSlotName();
        SaveSlot = nextSlot;
        if (!SaveSlots.Any(slot => string.Equals(slot, nextSlot, StringComparison.OrdinalIgnoreCase)))
        {
            SaveSlots.Add(nextSlot);
        }
    }

    private string GetNextSaveSlotName()
    {
        const string prefix = "save-";
        var existing = new HashSet<string>(SaveSlots, StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (existing.Contains($"{prefix}{index}"))
        {
            index++;
        }

        return $"{prefix}{index}";
    }

    private sealed record ProfileWizardResult(
        PlayerProfile Profile,
        string SeriesId,
        SeriesProfileState SeriesState,
        string CharacterName,
        CharacterProfileState? CharacterState,
        bool IsNewCharacter);

    private sealed class RandomNumberChoice
    {
        public RandomNumberChoice(Choice choice, IReadOnlyList<RandomNumberRange> ranges)
        {
            Choice = choice;
            Ranges = ranges;
        }

        public Choice Choice { get; }
        public IReadOnlyList<RandomNumberRange> Ranges { get; }

        public bool IsMatch(int value) => Ranges.Any(range => range.Contains(value));
    }

    private readonly struct RandomNumberRange
    {
        private RandomNumberRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }

        public bool Contains(int value) => value >= Min && value <= Max;

        public static RandomNumberRange From(int min, int max)
        {
            if (min > max)
            {
                (min, max) = (max, min);
            }

            min = Math.Clamp(min, 0, 9);
            max = Math.Clamp(max, 0, 9);
            return new RandomNumberRange(min, max);
        }
    }

    private static readonly Regex RangeRegex = new("\\b(?<min>[0-9])\\s*(?:-|–|—|to)\\s*(?<max>[0-9])\\b", RegexOptions.IgnoreCase);
    private static readonly Regex BelowRegex = new("\\b(?:below|under|less than)\\s+(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
    private static readonly Regex AtMostRegex = new("\\b(?:at most|or less|or lower)\\s+(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
    private static readonly Regex AtLeastRegex = new("\\b(?:at least|or above|or higher)\\s+(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
    private static readonly Regex AboveRegex = new("\\b(?:above|over|greater than)\\s+(?<value>[0-9])\\b", RegexOptions.IgnoreCase);
}
