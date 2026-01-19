using System.Collections.ObjectModel;
using System.Net;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
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
    private readonly RelayCommand _addSkillCommand;
    private readonly RelayCommand _removeSkillCommand;
    private readonly RelayCommand _addItemCommand;
    private readonly RelayCommand _removeItemCommand;
    private readonly RelayCommand _upsertCounterCommand;
    private readonly RelayCommand _removeCounterCommand;
    private readonly List<Choice> _pendingRandomChoices = new();
    private readonly List<RandomNumberChoice> _randomNumberChoices = new();
    private readonly Queue<int> _recentRolls = new();
    private readonly string _saveDirectory;
    private ISeriesProfile _currentProfile = SeriesProfiles.LoneWolf;
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
    private string _counterValueInput = "0";
    private BookListItemViewModel? _selectedBook;
    private ItemEntryViewModel? _selectedInventoryItem;
    private StatEntryViewModel? _selectedCounter;
    private bool _isRandomNumberVisible;
    private bool _areChoicesVisible = true;
    private int? _randomNumberResult;
    private Choice? _resolvedRandomChoice;
    private bool _isManualRandomMode;
    private bool _isProfileReady;

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
        _addSkillCommand = new RelayCommand(AddSkill, () => !string.IsNullOrWhiteSpace(SelectedAvailableSkill));
        _removeSkillCommand = new RelayCommand(RemoveSkill, () => SelectedSkill is not null);
        _addItemCommand = new RelayCommand(AddItem, () => !string.IsNullOrWhiteSpace(NewItemName));
        _removeItemCommand = new RelayCommand(RemoveItem, () => SelectedInventoryItem is not null);
        _upsertCounterCommand = new RelayCommand(UpsertCounter, () => !string.IsNullOrWhiteSpace(CounterNameInput));
        _removeCounterCommand = new RelayCommand(RemoveCounter, () => SelectedCounter is not null);
        LoadBooks(booksDirectory);
        LoadSaveSlots();
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
    public RelayCommand AddSkillCommand => _addSkillCommand;
    public RelayCommand RemoveSkillCommand => _removeSkillCommand;
    public RelayCommand AddItemCommand => _addItemCommand;
    public RelayCommand RemoveItemCommand => _removeItemCommand;
    public RelayCommand UpsertCounterCommand => _upsertCounterCommand;
    public RelayCommand RemoveCounterCommand => _removeCounterCommand;
    public bool HasCoreSkills => CoreSkills.Count > 0;
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
            _upsertCounterCommand.RaiseCanExecuteChanged();
        }
    }

    public string CounterValueInput
    {
        get => _counterValueInput;
        set
        {
            if (_counterValueInput == value)
            {
                return;
            }

            _counterValueInput = value;
            OnPropertyChanged();
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

    public StatEntryViewModel? SelectedCounter
    {
        get => _selectedCounter;
        set
        {
            if (_selectedCounter == value)
            {
                return;
            }

            _selectedCounter = value;
            OnPropertyChanged();
            _removeCounterCommand.RaiseCanExecuteChanged();
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
        _state.SeriesId = ResolveSeriesId(_book.Id);
        var firstSection = _book.Sections.FirstOrDefault();
        _state.SectionId = firstSection?.Id ?? string.Empty;
        BookTitle = _book.Title;
        EnsureSeriesProfile(_state.SeriesId);

        if (firstSection is null)
        {
            SectionTitle = "No sections found";
            Blocks.Clear();
            Blocks.Add(new ContentBlockViewModel("p", "This book has no sections."));
            Choices.Clear();
            ResetRandomNumberState();
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

        UpdateSuggestedActions(section);
        ResetRandomNumberState();
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
        SectionTitle = GetFrontMatterTitle(frontMatter);
        Blocks.Clear();
        foreach (var block in ExtractFrontMatterBlocks(frontMatter.Html))
        {
            Blocks.Add(block);
        }

        Choices.Clear();
        var command = new RelayCommand(continueAction);
        Choices.Add(new ChoiceViewModel("Continue", command));
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
        SelectedCounter = null;
    }

    private void EnsureSeriesProfile(string seriesId)
    {
        _currentProfile = ResolveSeriesProfile(seriesId);
        EnsureProfileContainer();

        if (!_state.Profile.SeriesStates.TryGetValue(seriesId, out var seriesState) || !seriesState.IsInitialized)
        {
            seriesState = new SeriesProfileState();
            if (!TryRunProfileWizard(seriesId, _currentProfile, null, out var character))
            {
                _state.Character = new Character();
                IsProfileReady = false;
                CharacterSetupHint = $"Profile setup required for {_currentProfile.Name}.";
                RefreshCharacterPanels();
                return;
            }

            seriesState.IsInitialized = true;
            seriesState.Character = character;
            _state.Profile.SeriesStates[seriesId] = seriesState;
        }
        else
        {
            var shouldUpdate = MessageBox.Show(
                $"Use existing {_currentProfile.Name} profile? Select 'No' to update it for this book.",
                "Profile Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (shouldUpdate == MessageBoxResult.No)
            {
                if (TryRunProfileWizard(seriesId, _currentProfile, seriesState.Character, out var updatedCharacter))
                {
                    seriesState.Character = updatedCharacter;
                    _state.Profile.SeriesStates[seriesId] = seriesState;
                }
            }
        }

        _state.Character = seriesState.Character;
        EnsureSeriesDefaults(seriesState.Character);
        UpdateAvailableSkills();
        SuggestedActions.Clear();
        CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
        IsProfileReady = true;
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
    }

    private void EnsureProfileContainer()
    {
        _state.Profile ??= new PlayerProfile();
        if (_state.Profile.SeriesStates is null)
        {
            _state.Profile.SeriesStates = new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool TryRunProfileWizard(string seriesId, ISeriesProfile profile, Character? existingCharacter, out Character character)
    {
        var viewModel = new ProfileWizardViewModel(seriesId, profile, _gameService.RollRandomNumber);
        if (existingCharacter is not null)
        {
            viewModel.CharacterName = existingCharacter.Name;
            viewModel.CombatSkill = existingCharacter.CombatSkill;
            viewModel.Endurance = existingCharacter.Endurance;

            if (existingCharacter.Attributes.TryGetValue("Willpower", out var willpower))
            {
                viewModel.Willpower = willpower;
            }

            if (viewModel.HasSkillSelection)
            {
                foreach (var skill in viewModel.Skills)
                {
                    if (existingCharacter.Disciplines.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        skill.IsSelected = true;
                    }
                }
            }

            if (viewModel.HasCoreSkills)
            {
                foreach (var coreSkill in viewModel.CoreSkills)
                {
                    if (existingCharacter.CoreSkills.TryGetValue(coreSkill.Label, out var value))
                    {
                        coreSkill.Value = value;
                    }
                }

                if (existingCharacter.Attributes.TryGetValue("CoreSkillPoolTotal", out var poolTotal))
                {
                    var minimumTotal = viewModel.CoreSkills.Sum(entry => entry.Value);
                    viewModel.BonusSkillPoints = Math.Max(0, poolTotal - minimumTotal);
                }

                viewModel.RefreshCoreSkillStatus();
            }

            foreach (var counter in viewModel.Counters)
            {
                if (existingCharacter.Inventory.Counters.TryGetValue(counter.Name, out var value))
                {
                    counter.Value = value;
                }
            }
        }
        var window = new ProfileWizardWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow
        };

        var result = window.ShowDialog();
        if (result == true && viewModel.IsValid)
        {
            character = viewModel.BuildCharacter();
            return true;
        }

        character = new Character();
        return false;
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

        if (!string.IsNullOrWhiteSpace(_state.SeriesId))
        {
            _currentProfile = ResolveSeriesProfile(_state.SeriesId);
            if (_state.Profile.SeriesStates.TryGetValue(_state.SeriesId, out var seriesState) && seriesState.IsInitialized)
            {
                _state.Character = seriesState.Character;
                EnsureSeriesDefaults(_state.Character);
                UpdateAvailableSkills();
                CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
                IsProfileReady = true;
                RefreshCharacterPanels();
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
            _state.Profile.SeriesStates[_state.SeriesId] = new SeriesProfileState
            {
                IsInitialized = true,
                Character = _state.Character
            };
        }
        EnsureSeriesDefaults(_state.Character);
        UpdateAvailableSkills();
        CharacterSetupHint = $"Use the book's character creation instructions in the main text. Series: {_currentProfile.Name}.";
        IsProfileReady = !string.IsNullOrWhiteSpace(_state.SeriesId);
        RefreshCharacterPanels();
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

    private void UpsertCounter()
    {
        var name = CounterNameInput.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!int.TryParse(CounterValueInput, out var value))
        {
            value = 0;
        }

        _state.Character.Inventory.Counters[name] = value;
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

        BookTitle = "Aon Companion";
        SectionTitle = "Select a book";
        CharacterSetupHint = string.Empty;
        Blocks.Clear();
        Choices.Clear();
        ResetRandomNumberState();
        SuggestedActions.Clear();
        AvailableSkills.Clear();
        CharacterSkills.Clear();
        CoreStats.Clear();
        CoreSkills.Clear();
        AttributeStats.Clear();
        InventoryCounters.Clear();
        InventoryItems.Clear();
        SelectedBook = null;
        IsProfileReady = false;
        OnPropertyChanged(nameof(HasCoreSkills));
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

    private void RemoveCounter()
    {
        if (SelectedCounter is null)
        {
            return;
        }

        _state.Character.Inventory.Counters.Remove(SelectedCounter.Label);
        SelectedCounter = null;
        RefreshCharacterPanels();
        _removeCounterCommand.RaiseCanExecuteChanged();
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
            Choices.Add(new ChoiceViewModel(choice.Text, command));
        }

        AreChoicesVisible = Choices.Count > 0;
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
