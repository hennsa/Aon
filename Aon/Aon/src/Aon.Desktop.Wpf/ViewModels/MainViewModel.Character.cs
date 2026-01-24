using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aon.Content;
using Aon.Core;
using Aon.Rules;

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
        SeriesStats.Clear();
        var seriesStatKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Character.CombatSkillBonusAttribute,
            "CoreSkillPoolTotal",
            "Willpower"
        };
        foreach (var entry in _state.Character.Attributes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (seriesStatKeys.Contains(entry.Key))
            {
                SeriesStats.Add(new StatEntryViewModel(entry.Key, entry.Value));
            }
            else
            {
                AttributeStats.Add(new StatEntryViewModel(entry.Key, entry.Value));
            }
        }
        OnPropertyChanged(nameof(HasAttributeStats));
        OnPropertyChanged(nameof(HasSeriesStats));

        InventoryCounters.Clear();
        foreach (var entry in _state.Character.Inventory.Counters.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            InventoryCounters.Add(new StatEntryViewModel(
                entry.Key,
                entry.Value,
                () => AdjustCounter(entry.Key, 1),
                () => AdjustCounter(entry.Key, -1)));
        }
        OnPropertyChanged(nameof(HasInventoryCounters));

        FlagEntries.Clear();
        foreach (var entry in _state.Flags.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            FlagEntries.Add(new FlagEntryViewModel(entry.Key, entry.Value));
        }
        OnPropertyChanged(nameof(HasFlags));

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

    private void AddOrIncrementCounter(string name, int delta)
    {
        var existingKey = _state.Character.Inventory.Counters.Keys
            .FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existingKey is null)
        {
            _state.Character.Inventory.Counters[name] = delta;
            return;
        }

        _state.Character.Inventory.Counters[existingKey] += delta;
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

            var rules = _gameService.ResolveChoiceRules(_state, choice);
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
            var hasItem = _state.Character.Inventory.Items.Any(existing =>
                existing.Name.Equals(suggestion.Name, StringComparison.OrdinalIgnoreCase)
                && existing.Category.Equals(suggestion.Category, StringComparison.OrdinalIgnoreCase));
            var hasAmmo = suggestion.AmmoCount > 0 && !string.IsNullOrWhiteSpace(suggestion.AmmoCounterName);
            if (hasItem && !hasAmmo)
            {
                continue;
            }

            var labelPrefix = suggestion.Category.Equals("weapon", StringComparison.OrdinalIgnoreCase)
                ? "Add weapon"
                : "Add item";
            var label = $"{labelPrefix}: {suggestion.Name}";
            if (hasAmmo)
            {
                label = hasItem
                    ? $"Add ammo: +{suggestion.AmmoCount} {suggestion.AmmoCounterName}"
                    : $"{label} (+{suggestion.AmmoCount} {suggestion.AmmoCounterName})";
            }

            SuggestedActions.Add(new QuickActionViewModel(label, () =>
            {
                if (!hasItem)
                {
                    _state.Character.Inventory.Items.Add(new Item(suggestion.Name, suggestion.Category));
                }

                if (hasAmmo && suggestion.AmmoCounterName is not null)
                {
                    AddOrIncrementCounter(suggestion.AmmoCounterName, suggestion.AmmoCount);
                }

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
                    if (TryParseWeaponWithAmmo(entry, out var weaponName, out var ammoName, out var ammoCount))
                    {
                        AddSuggestion(items, weaponName, "weapon", ammoName, ammoCount);
                        continue;
                    }

                    var item = NormalizeItemText(entry);
                    if (!string.IsNullOrWhiteSpace(item)
                        && LooksLikeItemName(item, block.Text)
                        && IsLikelyItemPhrase(item))
                    {
                        AddSuggestion(items, item, InferItemCategory(item, block.Text));
                    }
                }

                continue;
            }

            foreach (Match match in Regex.Matches(
                block.Text,
                "\\b(?:you (?:find|found|discover|discovered|notice|noticed|spot|spotted|locate|located|recover|recovered|take|took|pick up|picked up|gain|gained|receive|received))\\s+(?:an?|the)\\s+(?<item>[^.]+)",
                RegexOptions.IgnoreCase))
            {
                var item = NormalizeItemText(match.Groups["item"].Value);
                if (!string.IsNullOrWhiteSpace(item)
                    && LooksLikeItemName(item, block.Text)
                    && IsLikelyItemPhrase(item))
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
                    if (!string.IsNullOrWhiteSpace(item) && LooksLikeItemName(item, block.Text))
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

        var actionChartMatch = Regex.Match(
            trimmed,
            "mark this on your Action Chart as (?:a|an) (?<item>[^;,)]+)",
            RegexOptions.IgnoreCase);
        if (actionChartMatch.Success)
        {
            return actionChartMatch.Groups["item"].Value.Trim().TrimEnd('.', ';', ':');
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
        string category,
        string? ammoCounterName = null,
        int ammoCount = 0)
    {
        var key = $"{name}|{category}|{ammoCounterName}|{ammoCount}";
        if (!items.ContainsKey(key))
        {
            items[key] = new ItemSuggestion(name, category, ammoCounterName, ammoCount);
        }
    }

    private static bool LooksLikeItemName(string item, string context)
    {
        if (Regex.IsMatch(item, "\\(\\d+\\)"))
        {
            return true;
        }

        if (ContainsItemKeyword(item))
        {
            return true;
        }

        if (Regex.IsMatch(item, "\\b[A-Z][A-Za-z]"))
        {
            return true;
        }

        return context.Contains("weapon", StringComparison.OrdinalIgnoreCase)
            || context.Contains("Weapons List", StringComparison.OrdinalIgnoreCase)
            || context.Contains("Backpack", StringComparison.OrdinalIgnoreCase)
            || (context.Contains("item", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(item, "\\b[A-Za-z]{3,}"));
    }

    private static bool ContainsItemKeyword(string item)
    {
        return Regex.IsMatch(
            item,
            "\\b(weapon|sword|axe|dagger|knife|pistol|rifle|gun|bow|arrow|spear|mace|club|staff|shield|helmet|armou?r|pack|backpack|satchel|bag|kit|medi-?kit|potion|meal|ration|food|ammo|bullet|bolt|canteen|water|map|key|rope|torch|lantern|gem|ring|amulet|scroll|herb|bomb|grenade|chest|box)\\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsLikelyItemPhrase(string item)
    {
        var wordMatches = Regex.Matches(item, "[A-Za-z0-9]+");
        if (wordMatches.Count == 0 || wordMatches.Count > 7)
        {
            return false;
        }

        if (ContainsItemKeyword(item) || Regex.IsMatch(item, "\\(\\d+\\)") || Regex.IsMatch(item, "\\b[A-Z][A-Za-z]"))
        {
            return true;
        }

        return !Regex.IsMatch(
            item,
            "\\b(sound|noise|cry|scream|shout|voice|song|silence|light|darkness|shadow|sight|smell|feeling|glow|heat|cold|movement)\\b",
            RegexOptions.IgnoreCase);
    }

    private static bool TryParseWeaponWithAmmo(string raw, out string weaponName, out string ammoName, out int ammoCount)
    {
        weaponName = string.Empty;
        ammoName = string.Empty;
        ammoCount = 0;

        var match = Regex.Match(
            raw,
            "^(?<weapon>[^.]+?)\\s+plus\\s+(?<count>[A-Za-z0-9-]+)\\s+rounds?\\s+of\\s+(?<ammo>[^.]+?)(?:\\s+ammunition)?\\s*$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        if (!TryParseNumberToken(match.Groups["count"].Value, out ammoCount) || ammoCount <= 0)
        {
            return false;
        }

        weaponName = NormalizeItemText(match.Groups["weapon"].Value);
        var rawAmmo = match.Groups["ammo"].Value.Trim();
        ammoName = NormalizeAmmoText(rawAmmo);
        if (!ammoName.EndsWith("ammunition", StringComparison.OrdinalIgnoreCase))
        {
            ammoName = $"{ammoName} ammunition";
        }

        return !string.IsNullOrWhiteSpace(weaponName) && !string.IsNullOrWhiteSpace(ammoName);
    }

    private static string NormalizeAmmoText(string raw)
    {
        return raw.Trim().TrimEnd('.', ';', ':');
    }

    private static bool TryParseNumberToken(string token, out int number)
    {
        number = 0;
        if (int.TryParse(token, out number))
        {
            return true;
        }

        return token.ToLowerInvariant() switch
        {
            "one" => ReturnNumber(1, out number),
            "two" => ReturnNumber(2, out number),
            "three" => ReturnNumber(3, out number),
            "four" => ReturnNumber(4, out number),
            "five" => ReturnNumber(5, out number),
            "six" => ReturnNumber(6, out number),
            "seven" => ReturnNumber(7, out number),
            "eight" => ReturnNumber(8, out number),
            "nine" => ReturnNumber(9, out number),
            "ten" => ReturnNumber(10, out number),
            "eleven" => ReturnNumber(11, out number),
            "twelve" => ReturnNumber(12, out number),
            "thirteen" => ReturnNumber(13, out number),
            "fourteen" => ReturnNumber(14, out number),
            "fifteen" => ReturnNumber(15, out number),
            "sixteen" => ReturnNumber(16, out number),
            "seventeen" => ReturnNumber(17, out number),
            "eighteen" => ReturnNumber(18, out number),
            "nineteen" => ReturnNumber(19, out number),
            "twenty" => ReturnNumber(20, out number),
            _ => false
        };
    }

    private static bool ReturnNumber(int value, out int number)
    {
        number = value;
        return true;
    }

    private readonly record struct ItemSuggestion(
        string Name,
        string Category,
        string? AmmoCounterName,
        int AmmoCount);
}
