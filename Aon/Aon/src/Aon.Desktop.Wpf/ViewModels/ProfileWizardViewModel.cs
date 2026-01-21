using System.Collections.ObjectModel;
using System.Linq;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public enum ProfileWizardMode
{
    Profile,
    Character
}

public sealed class ProfileWizardViewModel : ViewModelBase
{
    private readonly Func<int> _rollRandomNumber;
    private readonly Dictionary<string, int> _coreSkillMinimums = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISeriesProfile> _seriesProfiles;
    private readonly ProfileWizardMode _mode;
    private string _profileName = string.Empty;
    private string _characterName = string.Empty;
    private int _combatSkill;
    private int _endurance;
    private int _willpower;
    private int _bonusSkillPoints = 4;
    private int _selectedSkillCount;
    private string _selectionStatus = string.Empty;
    private string _seriesId = string.Empty;
    private string _seriesName = string.Empty;
    private string _skillSelectionTitle = string.Empty;
    private int _skillSelectionLimit;
    private ProfileOptionViewModel? _selectedExistingProfile;
    private CharacterOptionViewModel? _selectedExistingCharacter;
    private SeriesOptionViewModel? _selectedSeriesOption;
    private string _confirmActionLabel = "Create Profile";

    public ProfileWizardViewModel(
        Func<int> rollRandomNumber,
        IEnumerable<PlayerProfile> existingProfiles,
        ProfileWizardMode mode,
        string? initialSeriesId = null,
        bool isSeriesSelectionEnabled = true,
        bool isProfileSelectionEnabled = true)
    {
        _rollRandomNumber = rollRandomNumber;
        _mode = mode;
        _seriesProfiles = new Dictionary<string, ISeriesProfile>(StringComparer.OrdinalIgnoreCase)
        {
            { "lw", SeriesProfiles.LoneWolf },
            { "gs", SeriesProfiles.GreyStar },
            { "fw", SeriesProfiles.FreewayWarrior }
        };
        Skills = new ObservableCollection<SelectableSkillViewModel>();
        CoreSkills = new ObservableCollection<CoreSkillEntryViewModel>();
        Counters = new ObservableCollection<CounterEntryViewModel>();
        ExistingProfiles = new ObservableCollection<ProfileOptionViewModel>();
        ExistingCharacters = new ObservableCollection<CharacterOptionViewModel>();
        SeriesOptions = new ObservableCollection<SeriesOptionViewModel>(
            _seriesProfiles.Select(entry => new SeriesOptionViewModel(entry.Key, entry.Value.Name))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase));
        IsSeriesSelectionEnabled = isSeriesSelectionEnabled;
        IsProfileSelectionEnabled = isProfileSelectionEnabled;

        foreach (var profile in existingProfiles
                     .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
                     .DistinctBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            ExistingProfiles.Add(new ProfileOptionViewModel(profile));
        }

        RollCombatSkillCommand = new RelayCommand(RollCombatSkill);
        RollEnduranceCommand = new RelayCommand(RollEndurance);
        RollWillpowerCommand = new RelayCommand(RollWillpower, () => IsWillpowerAvailable);

        SelectedSeriesOption = SeriesOptions.FirstOrDefault(option => string.Equals(option.Id, initialSeriesId, StringComparison.OrdinalIgnoreCase))
            ?? SeriesOptions.FirstOrDefault();

        if (HasSkillSelection)
        {
            UpdateSelectionStatus();
        }

        UpdateConfirmActionLabel();
        if (IsCharacterWizard)
        {
            LoadExistingCharacters(null);
        }
    }

    public string SeriesId
    {
        get => _seriesId;
        private set
        {
            if (_seriesId == value)
            {
                return;
            }

            _seriesId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWillpowerAvailable));
        }
    }

    public string SeriesName
    {
        get => _seriesName;
        private set
        {
            if (_seriesName == value)
            {
                return;
            }

            _seriesName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderTitle));
        }
    }

    public string SkillSelectionTitle
    {
        get => _skillSelectionTitle;
        private set
        {
            if (_skillSelectionTitle == value)
            {
                return;
            }

            _skillSelectionTitle = value;
            OnPropertyChanged();
        }
    }

    public int SkillSelectionLimit
    {
        get => _skillSelectionLimit;
        private set
        {
            if (_skillSelectionLimit == value)
            {
                return;
            }

            _skillSelectionLimit = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<SelectableSkillViewModel> Skills { get; }
    public ObservableCollection<CoreSkillEntryViewModel> CoreSkills { get; }
    public ObservableCollection<CounterEntryViewModel> Counters { get; }
    public ObservableCollection<ProfileOptionViewModel> ExistingProfiles { get; }
    public ObservableCollection<CharacterOptionViewModel> ExistingCharacters { get; }
    public ObservableCollection<SeriesOptionViewModel> SeriesOptions { get; }
    public List<Item> DefaultItems { get; } = new();
    public RelayCommand RollCombatSkillCommand { get; }
    public RelayCommand RollEnduranceCommand { get; }
    public RelayCommand RollWillpowerCommand { get; }
    public bool IsSeriesSelectionEnabled { get; }
    public bool IsProfileSelectionEnabled { get; }
    public bool IsProfileNameReadOnly => !IsProfileSelectionEnabled;
    public bool IsCharacterWizard => _mode == ProfileWizardMode.Character;
    public bool IsProfileWizard => _mode == ProfileWizardMode.Profile;
    public bool ShowSeriesSelection => IsCharacterWizard;
    public bool ShowCharacterSection => IsCharacterWizard;
    public string WindowTitle => IsProfileWizard ? "Profile Setup" : "Character Setup";
    public string HeaderTitle => IsProfileWizard ? "Profile Details" : SeriesName;
    public bool HasSkillSelection => Skills.Count > 0;
    public bool HasCoreSkills => CoreSkills.Count > 0;
    public bool HasCounters => Counters.Count > 0;
    public bool IsWillpowerAvailable => SeriesId is "gs";
    public bool HasExistingProfiles => ExistingProfiles.Count > 0;
    public bool ShowExistingProfiles => IsProfileSelectionEnabled && HasExistingProfiles;
    public bool HasExistingCharacters => IsCharacterWizard && ExistingCharacters.Count > 1;
    public bool IsCharacterCreationEnabled => IsCharacterWizard && (SelectedExistingCharacter?.IsNew ?? true);
    public string ConfirmActionLabel
    {
        get => _confirmActionLabel;
        private set
        {
            if (_confirmActionLabel == value)
            {
                return;
            }

            _confirmActionLabel = value;
            OnPropertyChanged();
        }
    }

    public ProfileOptionViewModel? SelectedExistingProfile
    {
        get => _selectedExistingProfile;
        set
        {
            if (_selectedExistingProfile == value)
            {
                return;
            }

            _selectedExistingProfile = value;
            OnPropertyChanged();
            if (_selectedExistingProfile is not null)
            {
                ProfileName = _selectedExistingProfile.Name;
            }

            if (IsCharacterWizard)
            {
                LoadExistingCharacters(_selectedExistingProfile?.Profile);
                OnPropertyChanged(nameof(HasExistingCharacters));
            }
            OnPropertyChanged(nameof(ShowExistingProfiles));
            UpdateConfirmActionLabel();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public SeriesOptionViewModel? SelectedSeriesOption
    {
        get => _selectedSeriesOption;
        set
        {
            if (_selectedSeriesOption == value)
            {
                return;
            }

            _selectedSeriesOption = value;
            OnPropertyChanged();
            if (_selectedSeriesOption is not null)
            {
                ApplySeriesProfile(_selectedSeriesOption.Id);
                if (IsCharacterWizard)
                {
                    LoadExistingCharacters(_selectedExistingProfile?.Profile);
                }
            }

            OnPropertyChanged(nameof(IsValid));
        }
    }

    public CharacterOptionViewModel? SelectedExistingCharacter
    {
        get => _selectedExistingCharacter;
        set
        {
            if (_selectedExistingCharacter == value)
            {
                return;
            }

            _selectedExistingCharacter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCharacterCreationEnabled));
            OnPropertyChanged(nameof(IsValid));
            UpdateConfirmActionLabel();

            if (_selectedExistingCharacter is null || _selectedExistingCharacter.IsNew)
            {
                return;
            }

            ApplyExistingCharacter(_selectedExistingCharacter.CharacterState);
        }
    }

    public string ProfileName
    {
        get => _profileName;
        set
        {
            if (_profileName == value)
            {
                return;
            }

            _profileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public string CharacterName
    {
        get => _characterName;
        set
        {
            if (_characterName == value)
            {
                return;
            }

            _characterName = value;
            OnPropertyChanged();
        }
    }

    public int CombatSkill
    {
        get => _combatSkill;
        set
        {
            if (_combatSkill == value)
            {
                return;
            }

            _combatSkill = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public int Endurance
    {
        get => _endurance;
        set
        {
            if (_endurance == value)
            {
                return;
            }

            _endurance = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public int Willpower
    {
        get => _willpower;
        set
        {
            if (_willpower == value)
            {
                return;
            }

            _willpower = value;
            OnPropertyChanged();
        }
    }

    public int BonusSkillPoints
    {
        get => _bonusSkillPoints;
        set
        {
            if (_bonusSkillPoints == value)
            {
                return;
            }

            _bonusSkillPoints = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CoreSkillPoolTotal));
            OnPropertyChanged(nameof(RemainingCoreSkillPoints));
        }
    }

    public int CoreSkillPoolTotal => _coreSkillMinimums.Values.Sum() + BonusSkillPoints;

    public int RemainingCoreSkillPoints => Math.Max(0, CoreSkillPoolTotal - CoreSkills.Sum(entry => entry.Value));

    public int SelectedSkillCount
    {
        get => _selectedSkillCount;
        private set
        {
            if (_selectedSkillCount == value)
            {
                return;
            }

            _selectedSkillCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public string SelectionStatus
    {
        get => _selectionStatus;
        private set
        {
            if (_selectionStatus == value)
            {
                return;
            }

            _selectionStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                return false;
            }

            if (IsProfileWizard)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(SeriesId))
            {
                return false;
            }

            if (!IsCharacterCreationEnabled)
            {
                return true;
            }

            if (CombatSkill <= 0 || Endurance <= 0)
            {
                return false;
            }

            if (IsWillpowerAvailable && Willpower <= 0)
            {
                return false;
            }

            if (HasSkillSelection && SelectedSkillCount != SkillSelectionLimit)
            {
                return false;
            }

            if (HasCoreSkills && RemainingCoreSkillPoints != 0)
            {
                return false;
            }

            return true;
        }
    }

    public Character BuildCharacter()
    {
        var character = new Character
        {
            CombatSkill = CombatSkill,
            Endurance = Endurance
        };

        if (!string.IsNullOrWhiteSpace(CharacterName))
        {
            character.Name = CharacterName.Trim();
        }

        character.Attributes[Character.CombatSkillBonusAttribute] = 0;

        if (IsWillpowerAvailable)
        {
            character.Attributes["Willpower"] = Willpower;
        }

        if (HasCoreSkills)
        {
            foreach (var entry in CoreSkills)
            {
                character.CoreSkills[entry.Label] = entry.Value;
            }

            character.Attributes["CoreSkillPoolTotal"] = CoreSkillPoolTotal;
        }

        if (HasSkillSelection)
        {
            foreach (var skill in Skills.Where(skill => skill.IsSelected))
            {
                character.Disciplines.Add(skill.Name);
            }
        }

        foreach (var counter in Counters)
        {
            character.Inventory.Counters[counter.Name] = counter.Value;
        }

        foreach (var item in DefaultItems)
        {
            character.Inventory.Items.Add(item);
        }

        return character;
    }

    public void RefreshCoreSkillStatus()
    {
        OnPropertyChanged(nameof(RemainingCoreSkillPoints));
        OnPropertyChanged(nameof(IsValid));
    }

    private void LoadExistingCharacters(PlayerProfile? profile)
    {
        if (!IsCharacterWizard)
        {
            return;
        }

        ExistingCharacters.Clear();
        ExistingCharacters.Add(CharacterOptionViewModel.CreateNew());

        if (profile is not null
            && profile.SeriesStates.TryGetValue(SeriesId, out var seriesState))
        {
            foreach (var entry in seriesState.Characters.Values
                         .Where(state => !string.IsNullOrWhiteSpace(state.Character.Name))
                         .OrderBy(state => state.Character.Name, StringComparer.OrdinalIgnoreCase))
            {
                ExistingCharacters.Add(new CharacterOptionViewModel(entry.Character.Name, entry, SeriesId, SeriesName));
            }
        }

        SelectedExistingCharacter = ExistingCharacters.FirstOrDefault();
        OnPropertyChanged(nameof(HasExistingCharacters));
        UpdateConfirmActionLabel();
    }

    private void ApplySeriesProfile(string seriesId)
    {
        if (!_seriesProfiles.TryGetValue(seriesId, out var seriesProfile))
        {
            return;
        }

        SeriesId = seriesId;
        SeriesName = seriesProfile.Name;

        Skills.Clear();
        CoreSkills.Clear();
        Counters.Clear();
        DefaultItems.Clear();
        _coreSkillMinimums.Clear();

        SkillSelectionTitle = string.Empty;
        SkillSelectionLimit = 0;

        foreach (var entry in seriesProfile.DefaultCounters)
        {
            Counters.Add(new CounterEntryViewModel(entry.Key, entry.Value));
        }

        foreach (var item in seriesProfile.DefaultItems)
        {
            DefaultItems.Add(item);
        }

        if (seriesId is "lw")
        {
            SkillSelectionLimit = 5;
            SkillSelectionTitle = "Kai Disciplines";
            foreach (var skill in seriesProfile.SkillNames)
            {
                Skills.Add(new SelectableSkillViewModel(skill, TryUpdateSkillSelection));
            }
        }
        else if (seriesId is "gs")
        {
            SkillSelectionLimit = 5;
            SkillSelectionTitle = "Magical Powers";
            foreach (var skill in seriesProfile.SkillNames)
            {
                Skills.Add(new SelectableSkillViewModel(skill, TryUpdateSkillSelection));
            }
        }
        else if (seriesId is "fw")
        {
            foreach (var entry in seriesProfile.CoreSkills)
            {
                _coreSkillMinimums[entry.Key] = entry.Value;
                CoreSkills.Add(new CoreSkillEntryViewModel(entry.Key, entry.Value, () => TryAdjustCoreSkill(entry.Key, 1), () => TryAdjustCoreSkill(entry.Key, -1)));
            }
        }

        CharacterName = string.Empty;
        CombatSkill = 0;
        Endurance = 0;
        Willpower = 0;
        BonusSkillPoints = 4;
        SelectedSkillCount = 0;
        UpdateSelectionStatus();
        OnPropertyChanged(nameof(HasSkillSelection));
        OnPropertyChanged(nameof(HasCoreSkills));
        OnPropertyChanged(nameof(HasCounters));
    }

    private void ApplyExistingCharacter(CharacterProfileState? characterState)
    {
        if (characterState is null)
        {
            return;
        }

        var character = characterState.Character;
        CharacterName = character.Name;
        CombatSkill = character.CombatSkill;
        Endurance = character.Endurance;
        Willpower = character.Attributes.TryGetValue("Willpower", out var willpower) ? willpower : 0;

        if (HasSkillSelection)
        {
            foreach (var skill in Skills)
            {
                skill.IsSelected = character.Disciplines.Contains(skill.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (HasCoreSkills)
        {
            foreach (var entry in CoreSkills)
            {
                entry.Value = character.CoreSkills.TryGetValue(entry.Label, out var value) ? value : entry.Value;
            }

            if (character.Attributes.TryGetValue("CoreSkillPoolTotal", out var poolTotal))
            {
                var minimumTotal = CoreSkills.Sum(entry => entry.Value);
                BonusSkillPoints = Math.Max(0, poolTotal - minimumTotal);
            }
        }

        foreach (var counter in Counters)
        {
            counter.Value = character.Inventory.Counters.TryGetValue(counter.Name, out var value) ? value : counter.Value;
        }
    }

    private void RollCombatSkill()
    {
        CombatSkill = _rollRandomNumber() + 10;
    }

    private void RollEndurance()
    {
        Endurance = _rollRandomNumber() + 20;
    }

    private void RollWillpower()
    {
        if (!IsWillpowerAvailable)
        {
            return;
        }

        Willpower = _rollRandomNumber() + 20;
    }

    private bool TryUpdateSkillSelection(SelectableSkillViewModel skill, bool isSelected)
    {
        if (!isSelected)
        {
            SelectedSkillCount = Math.Max(0, SelectedSkillCount - 1);
            UpdateSelectionStatus();
            return true;
        }

        if (SelectedSkillCount >= SkillSelectionLimit)
        {
            UpdateSelectionStatus();
            return false;
        }

        SelectedSkillCount++;
        UpdateSelectionStatus();
        return true;
    }

    private void UpdateSelectionStatus()
    {
        if (SkillSelectionLimit <= 0)
        {
            SelectionStatus = string.Empty;
            return;
        }

        SelectionStatus = $"Selected {SelectedSkillCount} of {SkillSelectionLimit}.";
    }

    private void UpdateConfirmActionLabel()
    {
        if (IsProfileWizard)
        {
            ConfirmActionLabel = SelectedExistingProfile is not null ? "Update Profile" : "Create Profile";
            return;
        }

        if (!IsProfileSelectionEnabled)
        {
            ConfirmActionLabel = IsCharacterCreationEnabled ? "Create Character" : "Use Character";
            return;
        }

        if (SelectedExistingProfile is not null)
        {
            ConfirmActionLabel = IsCharacterCreationEnabled ? "Create Character" : "Use Character";
            return;
        }

        ConfirmActionLabel = "Create Profile";
    }

    private void TryAdjustCoreSkill(string skillName, int delta)
    {
        var entry = CoreSkills.FirstOrDefault(item => string.Equals(item.Label, skillName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return;
        }

        var minimum = _coreSkillMinimums.GetValueOrDefault(skillName, 0);
        if (delta < 0 && entry.Value <= minimum)
        {
            return;
        }

        if (delta > 0 && RemainingCoreSkillPoints <= 0)
        {
            return;
        }

        entry.Value = Math.Max(minimum, entry.Value + delta);
        OnPropertyChanged(nameof(RemainingCoreSkillPoints));
        OnPropertyChanged(nameof(IsValid));
    }
}

public sealed class SelectableSkillViewModel : ViewModelBase
{
    private readonly Func<SelectableSkillViewModel, bool, bool> _selectionHandler;
    private bool _isSelected;

    public SelectableSkillViewModel(string name, Func<SelectableSkillViewModel, bool, bool> selectionHandler)
    {
        Name = name;
        _selectionHandler = selectionHandler;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            if (!_selectionHandler(this, value))
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }
}

public sealed class CoreSkillEntryViewModel : ViewModelBase
{
    private int _value;

    public CoreSkillEntryViewModel(string label, int value, Action increaseAction, Action decreaseAction)
    {
        Label = label;
        _value = value;
        IncreaseCommand = new RelayCommand(increaseAction);
        DecreaseCommand = new RelayCommand(decreaseAction);
    }

    public string Label { get; }
    public RelayCommand IncreaseCommand { get; }
    public RelayCommand DecreaseCommand { get; }

    public int Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }
}

public sealed class CounterEntryViewModel : ViewModelBase
{
    private int _value;

    public CounterEntryViewModel(string name, int value)
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }

    public int Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = Math.Max(0, value);
            OnPropertyChanged();
        }
    }
}

public sealed class ProfileOptionViewModel
{
    public ProfileOptionViewModel(PlayerProfile profile)
    {
        Profile = profile;
        Name = profile.Name;
    }

    public string Name { get; }
    public PlayerProfile Profile { get; }
}

public sealed class CharacterOptionViewModel
{
    public CharacterOptionViewModel(string name, CharacterProfileState characterState, string? seriesId = null, string? seriesName = null)
    {
        Name = name;
        CharacterState = characterState;
        SeriesId = seriesId;
        SeriesName = seriesName;
        DisplayName = string.IsNullOrWhiteSpace(seriesName) || string.Equals(seriesName, "Other", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name} ({seriesName})";
    }

    private CharacterOptionViewModel(string name)
    {
        Name = name;
        IsNew = true;
        DisplayName = name;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public bool IsNew { get; }
    public CharacterProfileState? CharacterState { get; }
    public string? SeriesId { get; }
    public string? SeriesName { get; }

    public static CharacterOptionViewModel CreateNew()
    {
        return new CharacterOptionViewModel("Create new character");
    }
}

public sealed class SeriesOptionViewModel
{
    public SeriesOptionViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }
}
