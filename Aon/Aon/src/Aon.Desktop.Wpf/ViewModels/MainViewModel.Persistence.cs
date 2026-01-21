using Aon.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
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

        var matchedProfile = Profiles.FirstOrDefault(option =>
            string.Equals(option.Name, _state.Profile?.Name, StringComparison.OrdinalIgnoreCase));
        var selectedProfile = matchedProfile;

        _isUpdatingProfiles = true;
        try
        {
            SelectedProfile = selectedProfile;
        }
        finally
        {
            _isUpdatingProfiles = false;
        }

        if (shouldSetProfileRequired && selectedProfile is not null && !IsProfileReady)
        {
            ApplySelectedProfile(selectedProfile.Profile);
            return;
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
}
