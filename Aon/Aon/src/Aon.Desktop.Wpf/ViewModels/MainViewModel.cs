using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
using System.Diagnostics.CodeAnalysis;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
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
    private readonly RelayCommand _deleteProfilesCommand;
    private readonly RelayCommand _addSkillCommand;
    private readonly RelayCommand _removeSkillCommand;
    private readonly RelayCommand _addItemCommand;
    private readonly RelayCommand _removeItemCommand;
    private readonly RelayCommand _addCounterCommand;
    private readonly RelayCommand _rollCombatNumberCommand;
    private readonly List<Choice> _pendingRandomChoices = new();
    private readonly Queue<int> _recentRolls = new();
    private readonly string _saveDirectory;
    private readonly bool _isDev;
    private RuleCatalog? _ruleCatalog;
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
    private string _roundsToFireText = "1";
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
    private SeriesFilterOptionViewModel? _selectedCharacterSeries;
    private int _activeTabIndex = 1;
    private bool _isRandomNumberVisible;
    private bool _areChoicesVisible = true;
    private int? _randomNumberResult;
    private int? _randomNumberTotalScore;
    private Choice? _resolvedRandomChoice;
    private bool _isManualRandomMode;
    private bool _isRoundsToFireVisible;
    private bool _isProfileReady;
    private bool _isUpdatingCharacters;
    private bool _isUpdatingProfiles;
    private bool _isSwitchingCharacter;
    private bool _lastWizardCreatedNewCharacter;
    private string? _lastWizardSeriesId;
    private bool _showChoiceDetails;

    public MainViewModel()
    {
        var booksDirectory = FindBooksDirectory();
        _saveDirectory = GetSaveDirectory();
        _isDev = LoadDevSettings();
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
        ItemCategories = new ObservableCollection<string>
        {
            "general",
            "weapon",
            "backpack",
            "special",
            "meal",
            "ammo"
        };
        _rollRandomNumberCommand = new RelayCommand(RollRandomNumber);
        _confirmRandomNumberCommand = new RelayCommand(ConfirmRandomNumber, () => _resolvedRandomChoice is not null);
        _showRandomNumberTableCommand = new RelayCommand(ShowRandomNumberTable);
        _rollCombatNumberCommand = new RelayCommand(RollCombatNumber, () => IsProfileReady);
        _saveGameCommand = new RelayCommand(() => _ = SaveGameAsync());
        _loadGameCommand = new RelayCommand(() => _ = LoadGameAsync());
        _newSaveSlotCommand = new RelayCommand(CreateNewSaveSlot);
        _newProfileCommand = new RelayCommand(StartNewProfile);
        _newCharacterCommand = new RelayCommand(() => _ = CreateNewCharacterAsync(), () => CanCreateCharacter);
        _deleteProfilesCommand = new RelayCommand(DeleteAllProfiles, () => IsDev);
        _addSkillCommand = new RelayCommand(AddSkill, () => !string.IsNullOrWhiteSpace(SelectedAvailableSkill));
        _removeSkillCommand = new RelayCommand(RemoveSkill, () => SelectedSkill is not null);
        _addItemCommand = new RelayCommand(AddItem, () => !string.IsNullOrWhiteSpace(NewItemName));
        _removeItemCommand = new RelayCommand(RemoveItem, () => SelectedInventoryItem is not null);
        _addCounterCommand = new RelayCommand(AddCounter, () => !string.IsNullOrWhiteSpace(CounterNameInput));
        _browseImportInputCommand = new RelayCommand(() => SelectImportDirectory(true), () => !IsImporting);
        _browseImportOutputCommand = new RelayCommand(() => SelectImportDirectory(false), () => !IsImporting);
        _runImportCommand = new RelayCommand(() => _ = RunImportAsync(), CanRunImport);
        LoadBooks(booksDirectory);
        LoadSaveSlots();
        LoadProfiles();
        ActiveTabIndex = 1;
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
    public ObservableCollection<SeriesFilterOptionViewModel> CharacterSeriesOptions { get; } = new();
    public ObservableCollection<StatEntryViewModel> CoreStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> CoreSkills { get; } = new();
    public ObservableCollection<StatEntryViewModel> AttributeStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> SeriesStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> CombatSeriesStats { get; } = new();
    public ObservableCollection<StatEntryViewModel> InventoryCounters { get; } = new();
    public ObservableCollection<FlagEntryViewModel> FlagEntries { get; } = new();
    public ObservableCollection<string> SaveSlots { get; } = new();
    public ObservableCollection<string> AvailableSkills { get; } = new();
    public ObservableCollection<string> CharacterSkills { get; } = new();
    public ObservableCollection<ItemEntryViewModel> InventoryItems { get; } = new();
    public ObservableCollection<string> ItemCategories { get; } = new();
    public ObservableCollection<QuickActionViewModel> SuggestedActions { get; } = new();
    public ObservableCollection<string> RuleWarnings { get; } = new();
    public RelayCommand RollRandomNumberCommand => _rollRandomNumberCommand;
    public RelayCommand ConfirmRandomNumberCommand => _confirmRandomNumberCommand;
    public RelayCommand ShowRandomNumberTableCommand => _showRandomNumberTableCommand;
    public RelayCommand SaveGameCommand => _saveGameCommand;
    public RelayCommand LoadGameCommand => _loadGameCommand;
    public RelayCommand NewSaveSlotCommand => _newSaveSlotCommand;
    public RelayCommand NewProfileCommand => _newProfileCommand;
    public RelayCommand NewCharacterCommand => _newCharacterCommand;
    public RelayCommand DeleteProfilesCommand => _deleteProfilesCommand;
    public RelayCommand AddSkillCommand => _addSkillCommand;
    public RelayCommand RemoveSkillCommand => _removeSkillCommand;
    public RelayCommand AddItemCommand => _addItemCommand;
    public RelayCommand RemoveItemCommand => _removeItemCommand;
    public RelayCommand AddCounterCommand => _addCounterCommand;
    public bool HasCoreSkills => CoreSkills.Count > 0;
    public bool HasAvailableSkills => AvailableSkills.Count > 0;
    public bool HasAttributeStats => AttributeStats.Count > 0;
    public bool HasSeriesStats => SeriesStats.Count > 0;
    public bool HasCombatSeriesStats => CombatSeriesStats.Count > 0;
    public bool HasInventoryCounters => InventoryCounters.Count > 0;
    public bool HasFlags => FlagEntries.Count > 0;
    public bool CanCreateCharacter => SelectedProfile is not null;
    public bool IsProfileSelected => SelectedProfile is not null;
    public bool IsCharacterSeriesSelected => SelectedCharacterSeries is not null;
    public bool HasBonusSkillPoints => BonusSkillPoints.HasValue;
    public bool IsDev => _isDev;
    public bool HasSelectedProfileAndSeries => SelectedProfile is not null && SelectedCharacterSeries is not null;
    public bool HasSuggestedActions => SuggestedActions.Count > 0;
    public bool HasRuleWarnings => RuleWarnings.Count > 0;
    public string InventoryLabel => _currentProfile.InventoryLabel;
    public string SeriesId => _state.SeriesId;
    public string SeriesDisplayName => string.IsNullOrWhiteSpace(_state.SeriesId)
        ? "No series selected"
        : _currentProfile.Name;
    public string SeriesContextSummary => string.IsNullOrWhiteSpace(_state.SeriesId)
        ? "Select a book to see series-specific rules and mechanics."
        : GetSeriesContextSummary(_state.SeriesId);
    public string SeriesContextTooltip => string.IsNullOrWhiteSpace(_state.SeriesId)
        ? "Series rules appear once a book is selected."
        : GetSeriesContextTooltip(_state.SeriesId);
    public bool HasSeriesContext => !string.IsNullOrWhiteSpace(_state.SeriesId);

    private void ResetSuggestedActions()
    {
        SuggestedActions.Clear();
        OnPropertyChanged(nameof(HasSuggestedActions));
    }

    private void SetRuleWarnings(IEnumerable<string> warnings)
    {
        RuleWarnings.Clear();
        foreach (var warning in warnings)
        {
            RuleWarnings.Add(warning);
        }

        OnPropertyChanged(nameof(HasRuleWarnings));
    }
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
            OnPropertyChanged(nameof(ActiveProfileLabel));
            OnPropertyChanged(nameof(ActiveCharacterLabel));
            _newCharacterCommand.RaiseCanExecuteChanged();
            _rollCombatNumberCommand.RaiseCanExecuteChanged();
            if (!value)
            {
                ClearCombatContext();
            }
            RefreshCombatOutcome();
        }
    }

    private void NotifySeriesContextChanged()
    {
        OnPropertyChanged(nameof(SeriesId));
        OnPropertyChanged(nameof(SeriesDisplayName));
        OnPropertyChanged(nameof(SeriesContextSummary));
        OnPropertyChanged(nameof(SeriesContextTooltip));
        OnPropertyChanged(nameof(HasSeriesContext));
        OnPropertyChanged(nameof(InventoryLabel));
        OnPropertyChanged(nameof(CombatTableLabel));
        RefreshCombatOutcome();
    }

    private static string GetSeriesContextSummary(string seriesId)
    {
        return seriesId.Trim().ToLowerInvariant() switch
        {
            "lw" => "Kai Disciplines and the Combat Results Table drive encounters.",
            "gs" => "Sorcery disciplines and Willpower shape Grey Star encounters.",
            "fw" => "Driving/Shooting core skills plus Fuel/Bullets track survival.",
            _ => "Series-specific rules apply for this book."
        };
    }

    private static string GetSeriesContextTooltip(string seriesId)
    {
        return seriesId.Trim().ToLowerInvariant() switch
        {
            "lw" => "Lone Wolf: use Kai Disciplines, track Combat Skill & Endurance, and reference the Combat Results Table.",
            "gs" => "Grey Star: sorcery disciplines consume Willpower; combat and rewards follow Grey Star rules.",
            "fw" => "Freeway Warrior: driving actions, shooting checks, Fuel, and Bullets are core resources.",
            _ => "Rules vary by series; consult the book's front matter for specifics."
        };
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
            OnPropertyChanged(nameof(ActiveProfileLabel));
            OnPropertyChanged(nameof(ActiveCharacterLabel));
            OnPropertyChanged(nameof(IsCharacterSeriesSelected));
            OnPropertyChanged(nameof(HasSelectedProfileAndSeries));
            _newCharacterCommand.RaiseCanExecuteChanged();

            if (_isUpdatingProfiles)
            {
                return;
            }

            if (_selectedProfile is null)
            {
                ActiveTabIndex = 1;
                SetProfileSetupRequired("Select a profile to continue.");
                return;
            }

            SelectedCharacterSeries = null;
            PersistActiveCharacterState();
            ApplySelectedProfile(_selectedProfile.Profile);
            UpdateCharacterSeriesOptions(_selectedProfile.Profile, _state.SeriesId);
            OnPropertyChanged(nameof(HasSelectedProfileAndSeries));
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

    public int? BonusSkillPoints
    {
        get
        {
            if (!_state.Character.Attributes.TryGetValue("CoreSkillPoolTotal", out var poolTotal)
                || poolTotal <= 0)
            {
                return null;
            }

            var spent = _state.Character.CoreSkills.Values.Sum();
            return Math.Max(0, poolTotal - spent);
        }
    }

    public SeriesFilterOptionViewModel? SelectedCharacterSeries
    {
        get => _selectedCharacterSeries;
        set
        {
            if (_selectedCharacterSeries == value)
            {
                return;
            }

            _selectedCharacterSeries = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCharacterSeriesSelected));
            OnPropertyChanged(nameof(HasSelectedProfileAndSeries));

            if (_isUpdatingCharacters)
            {
                return;
            }

            if (_selectedProfile is null || _selectedCharacterSeries is null)
            {
                Characters.Clear();
                SelectedCharacter = null;
                return;
            }

            UpdateCharacterOptionsForProfile(_selectedProfile.Profile, seriesFilterId: _selectedCharacterSeries.Id);
        }
    }

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set
        {
            if (_activeTabIndex == value)
            {
                return;
            }

            _activeTabIndex = value;
            OnPropertyChanged();
        }
    }

    public string ActiveProfileLabel
    {
        get
        {
            if (!IsProfileSelected || string.IsNullOrWhiteSpace(_state.Profile?.Name))
            {
                return "Profile: none selected";
            }

            return $"Profile: {_state.Profile.Name}";
        }
    }

    public string ActiveCharacterLabel
    {
        get
        {
            if (!IsProfileReady)
            {
                return "Character: none selected";
            }

            var name = string.IsNullOrWhiteSpace(_state.Character.Name)
                ? _currentProfile.DefaultCharacterName
                : _state.Character.Name;

            return $"Character: {name}";
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

    public bool ShowChoiceDetails
    {
        get => _showChoiceDetails;
        set
        {
            if (_showChoiceDetails == value)
            {
                return;
            }

            _showChoiceDetails = value;
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

    public string RoundsToFireText
    {
        get => _roundsToFireText;
        set
        {
            if (_roundsToFireText == value)
            {
                return;
            }

            _roundsToFireText = value;
            OnPropertyChanged();
        }
    }

    public bool IsRoundsToFireVisible
    {
        get => _isRoundsToFireVisible;
        private set
        {
            if (_isRoundsToFireVisible == value)
            {
                return;
            }

            _isRoundsToFireVisible = value;
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

            var previousCharacter = _selectedCharacter;
            _selectedCharacter = value;
            OnPropertyChanged();

            if (_isUpdatingCharacters || _isSwitchingCharacter || _selectedCharacter is null)
            {
                return;
            }

            if (!IsProfileReady)
            {
                _ = LoadCharacterSelectionAsync(_selectedCharacter);
                ActiveTabIndex = 0;
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                "Switching characters will save the current character and update the book view. Continue?",
                "Switch Character",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                _isUpdatingCharacters = true;
                try
                {
                    _selectedCharacter = previousCharacter;
                    OnPropertyChanged(nameof(SelectedCharacter));
                }
                finally
                {
                    _isUpdatingCharacters = false;
                }
                return;
            }

            _ = SwitchCharacterAsync(_selectedCharacter);
            ActiveTabIndex = 0;
        }
    }
}
