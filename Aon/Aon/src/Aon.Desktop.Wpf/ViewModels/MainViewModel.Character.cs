using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aon.Content;
using Aon.Core;
using Aon.Rules;
using Aon.Core;
using System.Text.RegularExpressions;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    public sealed class SeriesFilterOptionViewModel
    {
        public SeriesFilterOptionViewModel(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
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
        OnPropertyChanged(nameof(ActiveCharacterLabel));
        OnPropertyChanged(nameof(BonusSkillPoints));
        OnPropertyChanged(nameof(HasBonusSkillPoints));
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

    private void UpdateCharacterOptionsForProfile(
        PlayerProfile profile,
        string? activeSeriesId = null,
        string? activeCharacterName = null,
        string? seriesFilterId = null)
    {
        _isUpdatingCharacters = true;
        Characters.Clear();

        if (profile.SeriesStates is null)
        {
            profile.SeriesStates = new Dictionary<string, SeriesProfileState>(StringComparer.OrdinalIgnoreCase);
            SelectedCharacter = null;
            _isUpdatingCharacters = false;
            return;
        }

        foreach (var seriesEntry in profile.SeriesStates.OrderBy(entry => ResolveSeriesSortOrder(entry.Key)))
        {
            var seriesId = seriesEntry.Key;
            var seriesState = seriesEntry.Value;
            if (seriesState.Characters.Count == 0 && HasLegacyCharacterData(seriesState.Character))
            {
                var seriesProfile = ResolveSeriesProfile(seriesId);
                var legacyName = string.IsNullOrWhiteSpace(seriesState.Character.Name)
                    ? seriesProfile.DefaultCharacterName
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
            if (!string.IsNullOrWhiteSpace(seriesFilterId)
                && !string.Equals(seriesFilterId, seriesId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var seriesName = ResolveSeriesName(seriesId);
            foreach (var entry in seriesState.Characters.Values
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

    private void UpdateCharacterSeriesOptions(PlayerProfile profile, string? preferredSeriesId = null)
    {
        _isUpdatingCharacters = true;
        CharacterSeriesOptions.Clear();

        var seriesIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lw",
            "gs",
            "fw"
        };

        if (profile.SeriesStates is not null)
        {
            foreach (var entry in profile.SeriesStates.Keys)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    seriesIds.Add(entry);
                }
            }
        }

        foreach (var seriesId in seriesIds.OrderBy(ResolveSeriesSortOrder))
        {
            CharacterSeriesOptions.Add(new SeriesFilterOptionViewModel(seriesId, ResolveSeriesName(seriesId)));
        }

        _isUpdatingCharacters = false;

        if (!string.IsNullOrWhiteSpace(preferredSeriesId))
        {
            SelectedCharacterSeries = CharacterSeriesOptions.FirstOrDefault(option =>
                string.Equals(option.Id, preferredSeriesId, StringComparison.OrdinalIgnoreCase));
            return;
        }

        SelectedCharacterSeries = null;
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
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
        _ = SaveProfileStateAsync();
    }

    private void UpdateSuggestedActions(BookSection section)
    {
        ResetSuggestedActions();
        var effects = new List<Effect>();
        foreach (var choice in section.Choices)
        {
            var evaluation = _gameService.EvaluateChoice(_state, choice);
            if (!evaluation.IsAvailable)
            {
                continue;
            }

            var rules = _gameService.ResolveChoiceRules(choice);
            effects.AddRange(rules.Effects);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects)
        {
            var label = GetSuggestedActionLabel(effect);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var key = GetSuggestedActionKey(effect);
            if (!seen.Add(key))
            {
                continue;
            }

            SuggestedActions.Add(new QuickActionViewModel(label, () =>
            {
                _gameService.ApplyEffects(_state, new[] { effect });
                RefreshCharacterPanels();
                _ = SaveProfileStateAsync();
            }));
        }

        foreach (var suggestion in ExtractItemSuggestions(section))
        {
            if (_state.Character.Inventory.Items.Any(existing =>
                    existing.Name.Equals(suggestion.Name, StringComparison.OrdinalIgnoreCase)
                    && existing.Category.Equals(suggestion.Category, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var labelPrefix = suggestion.Category.Equals("weapon", StringComparison.OrdinalIgnoreCase)
                ? "Add weapon"
                : "Add item";
            SuggestedActions.Add(new QuickActionViewModel($"{labelPrefix}: {suggestion.Name}", () =>
            {
                _state.Character.Inventory.Items.Add(new Item(suggestion.Name, suggestion.Category));
                RefreshCharacterPanels();
                _ = SaveProfileStateAsync();
            }));
        }

        OnPropertyChanged(nameof(HasSuggestedActions));
    }

    private static string GetSuggestedActionKey(Effect effect)
    {
        return effect switch
        {
            AdjustStatEffect stat => $"stat:{stat.StatName}:{stat.Delta}",
            AddItemEffect addItem => $"item:add:{addItem.ItemName}:{addItem.Category}",
            RemoveItemEffect removeItem => $"item:remove:{removeItem.ItemName}",
            SetFlagEffect flag => $"flag:{flag.FlagName}:{flag.Value}",
            GrantDisciplineEffect discipline => $"discipline:{discipline.DisciplineName}",
            UpdateCounterEffect counter => $"counter:{counter.CounterName}:{counter.Value}:{counter.IsAbsolute}",
            _ => $"unsupported:{effect.Raw}"
        };
    }

    private static string? GetSuggestedActionLabel(Effect effect)
    {
        return effect switch
        {
            AdjustStatEffect stat => stat.Delta >= 0
                ? $"Add {stat.Delta} {stat.StatName}"
                : $"Remove {Math.Abs(stat.Delta)} {stat.StatName}",
            AddItemEffect addItem => $"Add item: {addItem.ItemName}",
            RemoveItemEffect removeItem => $"Remove item: {removeItem.ItemName}",
            SetFlagEffect flag => $"Set flag: {flag.FlagName} = {flag.Value}",
            GrantDisciplineEffect discipline => $"Add skill: {discipline.DisciplineName}",
            UpdateCounterEffect counter when counter.IsAbsolute => $"Set {counter.CounterName} to {counter.Value}",
            UpdateCounterEffect counter => counter.Value >= 0
                ? $"Add {counter.Value} {counter.CounterName}"
                : $"Remove {Math.Abs(counter.Value)} {counter.CounterName}",
            UnsupportedEffect => null,
            _ => null
        };
    }

    private static IEnumerable<ItemSuggestion> ExtractItemSuggestions(BookSection section)
    {
        var items = new Dictionary<string, ItemSuggestion>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in section.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Text))
            {
                continue;
            }

            if (string.Equals(block.Kind, "ul", StringComparison.OrdinalIgnoreCase)
                || string.Equals(block.Kind, "ol", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in block.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var item = NormalizeItemText(entry);
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        AddSuggestion(items, item, InferItemCategory(item, block.Text));
                    }
                }

                continue;
            }

            foreach (Match match in Regex.Matches(
                block.Text,
                "\\b(?:it is|it was|you (?:find|found|discover|discovered|notice|noticed|spot|spotted|locate|located|pick up|take))\\s+(?:an?|the)\\s+(?<item>[^.]+)",
                RegexOptions.IgnoreCase))
            {
                var item = NormalizeItemText(match.Groups["item"].Value);
                if (!string.IsNullOrWhiteSpace(item))
                {
                    AddSuggestion(items, item, InferItemCategory(item, block.Text));
                }
            }

            foreach (Match match in Regex.Matches(
                block.Text,
                "\\b(?<item>[A-Z][A-Za-z0-9'’\\- ]+\\(\\d+\\))",
                RegexOptions.IgnoreCase))
            {
                var item = NormalizeItemText(match.Groups["item"].Value);
                if (!string.IsNullOrWhiteSpace(item))
                {
                    AddSuggestion(items, item, "weapon");
                }
            }

            foreach (Match match in Regex.Matches(
                block.Text,
                "\\bitems? you (?:find|found|discover|discovered|notice|noticed|spot|spotted|locate|located)[^.]*? are (?<items>[^.]+)",
                RegexOptions.IgnoreCase))
            {
                foreach (var item in SplitItemList(match.Groups["items"].Value, block.Text))
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        AddSuggestion(items, item, InferItemCategory(item, block.Text));
                    }
                }
            }
        }

        return items.Values;
    }

    private static IEnumerable<string> SplitItemList(string text, string context)
    {
        var parts = text.Split(new[] { ",", " and " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var item = NormalizeItemText(part);
            if (!string.IsNullOrWhiteSpace(item))
            {
                yield return item;
            }
        }
    }

    private static string NormalizeItemText(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var hasWeaponBonus = Regex.IsMatch(trimmed, "\\(\\d+\\)");
        trimmed = Regex.Replace(trimmed, "^\\d+\\.\\s*", string.Empty);
        trimmed = Regex.Replace(trimmed, "^(?:a|an|the)\\s+", string.Empty, RegexOptions.IgnoreCase);

        var separators = hasWeaponBonus
            ? new[] { " with ", " containing ", " sufficient for ", " that ", " which " }
            : new[] { " (", " with ", " containing ", " sufficient for ", " that ", " which " };
        foreach (var separator in separators)
        {
            var index = trimmed.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                trimmed = trimmed[..index];
                break;
            }
        }

        return trimmed.Trim().TrimEnd('.', ';', ':');
    }

    private static string InferItemCategory(string item, string context)
    {
        if (Regex.IsMatch(item, "\\(\\d+\\)"))
        {
            return "weapon";
        }

        if (context.Contains("Weapons List", StringComparison.OrdinalIgnoreCase)
            || context.Contains("weapon", StringComparison.OrdinalIgnoreCase))
        {
            return "weapon";
        }

        return "general";
    }

    private static void AddSuggestion(
        IDictionary<string, ItemSuggestion> items,
        string name,
        string category)
    {
        var key = $"{name}|{category}";
        if (!items.ContainsKey(key))
        {
            items[key] = new ItemSuggestion(name, category);
        }
    }

    private readonly record struct ItemSuggestion(string Name, string Category);
}
