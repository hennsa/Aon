using System;
using System.Linq;
using System.Text.RegularExpressions;
using Aon.Content;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
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
        OnPropertyChanged(nameof(ActiveCharacterLabel));
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
}
