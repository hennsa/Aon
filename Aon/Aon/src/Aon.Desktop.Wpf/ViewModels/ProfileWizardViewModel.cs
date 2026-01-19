using System.Collections.ObjectModel;
using System.Linq;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ProfileWizardViewModel : ViewModelBase
{
    private readonly Func<int> _rollRandomNumber;
    private readonly Dictionary<string, int> _coreSkillMinimums = new(StringComparer.OrdinalIgnoreCase);
    private string _profileName = string.Empty;
    private string _characterName = string.Empty;
    private int _combatSkill;
    private int _endurance;
    private int _willpower;
    private int _bonusSkillPoints = 4;
    private int _selectedSkillCount;
    private string _selectionStatus = string.Empty;
    private ProfileOptionViewModel? _selectedExistingProfile;
    private CharacterOptionViewModel? _selectedExistingCharacter;

    public ProfileWizardViewModel(
        string seriesId,
        ISeriesProfile seriesProfile,
        Func<int> rollRandomNumber,
        IEnumerable<PlayerProfile> existingProfiles)
    {
        SeriesId = seriesId;
        SeriesName = seriesProfile.Name;
        _rollRandomNumber = rollRandomNumber;
        Skills = new ObservableCollection<SelectableSkillViewModel>();
        CoreSkills = new ObservableCollection<CoreSkillEntryViewModel>();
        Counters = new ObservableCollection<CounterEntryViewModel>();
        ExistingProfiles = new ObservableCollection<ProfileOptionViewModel>();
        ExistingCharacters = new ObservableCollection<CharacterOptionViewModel>();

        foreach (var profile in existingProfiles
                     .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
                     .DistinctBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            ExistingProfiles.Add(new ProfileOptionViewModel(profile));
        }

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

        RollCombatSkillCommand = new RelayCommand(RollCombatSkill);
        RollEnduranceCommand = new RelayCommand(RollEndurance);
        RollWillpowerCommand = new RelayCommand(RollWillpower, () => IsWillpowerAvailable);

        if (HasSkillSelection)
        {
            UpdateSelectionStatus();
        }

        LoadExistingCharacters(null);
    }

    public string SeriesId { get; }
    public string SeriesName { get; }
    public string SkillSelectionTitle { get; } = string.Empty;
    public int SkillSelectionLimit { get; }
    public ObservableCollection<SelectableSkillViewModel> Skills { get; }
    public ObservableCollection<CoreSkillEntryViewModel> CoreSkills { get; }
    public ObservableCollection<CounterEntryViewModel> Counters { get; }
    public ObservableCollection<ProfileOptionViewModel> ExistingProfiles { get; }
    public ObservableCollection<CharacterOptionViewModel> ExistingCharacters { get; }
    public List<Item> DefaultItems { get; } = new();
    public RelayCommand RollCombatSkillCommand { get; }
    public RelayCommand RollEnduranceCommand { get; }
    public RelayCommand RollWillpowerCommand { get; }
    public bool HasSkillSelection => Skills.Count > 0;
    public bool HasCoreSkills => CoreSkills.Count > 0;
    public bool HasCounters => Counters.Count > 0;
    public bool IsWillpowerAvailable => SeriesId is "gs";
    public bool HasExistingProfiles => ExistingProfiles.Count > 0;
    public bool HasExistingCharacters => ExistingCharacters.Count > 1;
    public bool IsCharacterCreationEnabled => SelectedExistingCharacter?.IsNew ?? true;

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

            LoadExistingCharacters(_selectedExistingProfile?.Profile);
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
        ExistingCharacters.Clear();
        ExistingCharacters.Add(CharacterOptionViewModel.CreateNew());

        if (profile is not null
            && profile.SeriesStates.TryGetValue(SeriesId, out var seriesState))
        {
            foreach (var entry in seriesState.Characters.Values
                         .Where(state => !string.IsNullOrWhiteSpace(state.Character.Name))
                         .OrderBy(state => state.Character.Name, StringComparer.OrdinalIgnoreCase))
            {
                ExistingCharacters.Add(new CharacterOptionViewModel(entry.Character.Name, entry));
            }
        }

        SelectedExistingCharacter = ExistingCharacters.FirstOrDefault();
        OnPropertyChanged(nameof(HasExistingCharacters));
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
    public CharacterOptionViewModel(string name, CharacterProfileState characterState)
    {
        Name = name;
        CharacterState = characterState;
    }

    private CharacterOptionViewModel(string name)
    {
        Name = name;
        IsNew = true;
    }

    public string Name { get; }
    public bool IsNew { get; }
    public CharacterProfileState? CharacterState { get; }

    public static CharacterOptionViewModel CreateNew()
    {
        return new CharacterOptionViewModel("Create new character");
    }
}
