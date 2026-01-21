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

            PersistActiveCharacterState();
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
}
