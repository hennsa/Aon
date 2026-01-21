using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private bool EnsureSeriesProfile(string seriesId)
    {
        _currentProfile = ResolveSeriesProfile(seriesId);
        EnsureProfileContainer();

        var seriesState = EnsureSeriesState(_state.Profile, seriesId);
        _lastWizardCreatedNewCharacter = false;
        _lastWizardSeriesId = null;
        if (!seriesState.IsInitialized || seriesState.Characters.Count == 0)
        {
            if (!TryRunProfileWizard(ProfileWizardMode.Character, seriesId, _state.Profile, false, false, out var wizardResult))
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
                if (TryRunProfileWizard(ProfileWizardMode.Character, seriesId, _state.Profile, false, false, out var wizardResult))
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
        ResetSuggestedActions();
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
        CharacterSeriesOptions.Clear();
        SelectedCharacterSeries = null;
        Characters.Clear();
        SelectedCharacter = null;
        AreChoicesVisible = false;
        ClearBookDisplay();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();
        OnPropertyChanged(nameof(ActiveProfileLabel));
        OnPropertyChanged(nameof(ActiveCharacterLabel));
    }

    private void ApplySelectedProfile(PlayerProfile profile)
    {
        _state.Profile = profile;
        EnsureProfileContainer();
        _state.SeriesId = string.Empty;
        _currentCharacterState = null;
        IsProfileReady = false;
        CharacterSetupHint = "Select an existing character or create a new one to begin.";
        UpdateCharacterSeriesOptions(profile, _state.SeriesId);
        Characters.Clear();
        SelectedCharacter = null;
        ClearBookDisplay();
        RefreshCharacterPanels();
        OnPropertyChanged(nameof(ActiveProfileLabel));
        OnPropertyChanged(nameof(ActiveCharacterLabel));
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

    private bool TryRunProfileWizard(
        ProfileWizardMode mode,
        string? initialSeriesId,
        PlayerProfile? selectedProfile,
        bool isSeriesSelectionEnabled,
        bool isProfileSelectionEnabled,
        out ProfileWizardResult result)
    {
        var existingProfiles = LoadExistingProfiles();
        if (string.IsNullOrWhiteSpace(initialSeriesId) && selectedProfile is not null)
        {
            initialSeriesId = ResolvePreferredSeriesId(selectedProfile);
        }

        var viewModel = new ProfileWizardViewModel(
            _gameService.RollRandomNumber,
            existingProfiles,
            mode,
            initialSeriesId,
            isSeriesSelectionEnabled,
            isProfileSelectionEnabled);

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

            if (mode == ProfileWizardMode.Character
                && profileToSelect.SeriesStates.TryGetValue(viewModel.SeriesId, out var seriesState)
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

        var profileFromWizard = viewModel.SelectedExistingProfile?.Profile ?? new PlayerProfile();
        profileFromWizard.Name = viewModel.ProfileName.Trim();
        profileFromWizard.SeriesStates ??= new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);

        if (mode == ProfileWizardMode.Profile)
        {
            result = new ProfileWizardResult(profileFromWizard, string.Empty, new SeriesProfileState(), string.Empty, null, false);
            return true;
        }

        var seriesId = viewModel.SeriesId;
        var seriesProfile = ResolveSeriesProfile(seriesId);
        var selectedSeriesState = EnsureSeriesState(profileFromWizard, seriesId);

        if (!viewModel.IsCharacterCreationEnabled)
        {
            var selectedCharacterState = viewModel.SelectedExistingCharacter?.CharacterState;
            if (selectedCharacterState is null)
            {
                result = new ProfileWizardResult(profileFromWizard, seriesId, selectedSeriesState, string.Empty, null, false);
                return false;
            }

            var selectedName = selectedCharacterState.Character.Name;
            selectedSeriesState.ActiveCharacterName = selectedName;
            selectedSeriesState.IsInitialized = true;
            result = new ProfileWizardResult(profileFromWizard, seriesId, selectedSeriesState, selectedName, selectedCharacterState, false);
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
                result = new ProfileWizardResult(profileFromWizard, seriesId, selectedSeriesState, string.Empty, null, false);
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

        result = new ProfileWizardResult(profileFromWizard, seriesId, selectedSeriesState, characterName, characterState, true);
        return true;
    }

    private static string? ResolvePreferredSeriesId(PlayerProfile profile)
    {
        if (profile.SeriesStates is null)
        {
            return null;
        }

        foreach (var entry in profile.SeriesStates)
        {
            if (entry.Value.Characters is { Count: > 0 }
                && !string.IsNullOrWhiteSpace(entry.Value.ActiveCharacterName))
            {
                return entry.Key;
            }
        }

        foreach (var entry in profile.SeriesStates)
        {
            if (entry.Value.Characters is { Count: > 0 })
            {
                return entry.Key;
            }
        }

        return null;
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
        if (!TryRunProfileWizard(ProfileWizardMode.Profile, null, null, false, true, out var wizardResult))
        {
            SetProfileSetupRequired("Select a profile to continue.");
            return;
        }

        ApplySelectedProfile(wizardResult.Profile);
        ApplyProfileNameToSaveSlot();
        LoadProfiles();
        UpdateCharacterSeriesOptions(_state.Profile, _state.SeriesId);
        UpdateCharacterOptionsForProfile(_state.Profile, seriesFilterId: _state.SeriesId);
        _isUpdatingProfiles = true;
        try
        {
            SelectedProfile = Profiles.FirstOrDefault(option => string.Equals(option.Name, _state.Profile.Name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isUpdatingProfiles = false;
        }

        _ = SaveProfileStateAsync();
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
        ResetSuggestedActions();
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
            ResetSuggestedActions();
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
        ResetSuggestedActions();
        CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
        IsProfileReady = true;
        ApplyProfileNameToSaveSlot();
        RefreshCharacterPanels();
        UpdateBookProgressIndicators();
        UpdateCharacterSeriesOptions(_state.Profile, seriesId);
        UpdateCharacterOptionsForProfile(_state.Profile, seriesFilterId: seriesId);

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
        var initialSeriesId = SelectedCharacterSeries?.Id ?? _state.SeriesId;
        if (string.IsNullOrWhiteSpace(initialSeriesId))
        {
            MessageBox.Show("Select a series before creating a character.", "Character Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var existingProfile = SelectedProfile?.Profile ?? _state.Profile;
        if (SelectedProfile is null)
        {
            return;
        }

        if (!TryRunProfileWizard(ProfileWizardMode.Character, initialSeriesId, existingProfile, false, false, out var wizardResult))
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
            ResetSuggestedActions();
            CharacterSetupHint = $"Profile ready for {_currentProfile.Name}.";
            IsProfileReady = true;
            ApplyProfileNameToSaveSlot();
            RefreshCharacterPanels();
            UpdateBookProgressIndicators();
        }

        LoadProfiles();
        UpdateCharacterOptions(seriesState);
        UpdateCharacterSeriesOptions(_state.Profile, wizardResult.SeriesId);
        UpdateCharacterOptionsForProfile(_state.Profile, seriesFilterId: wizardResult.SeriesId);
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

        await SaveProfileStateAsync();
    }

    private void ApplyProfileNameToSaveSlot()
    {
        if (string.IsNullOrWhiteSpace(_state.Profile.Name))
        {
            return;
        }

        SaveSlot = _state.Profile.Name.Trim();
    }
}
