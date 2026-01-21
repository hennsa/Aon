using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Aon.Content;
using Aon.Core;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
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
            var isEnabled = IsChoiceAvailable(choice);
            Choices.Add(new ChoiceViewModel(ReplaceCharacterTokens(choice.Text), command, isEnabled));
        }

        AreChoicesVisible = Choices.Count > 0;
    }

    private bool IsChoiceAvailable(Choice choice)
    {
        if (string.IsNullOrWhiteSpace(choice.Text))
        {
            return true;
        }

        if (!TryGetRequiredSkill(choice.Text, out var requiredSkill))
        {
            return true;
        }

        return _state.Character.Disciplines.Contains(requiredSkill, StringComparer.OrdinalIgnoreCase);
    }

    private bool TryGetRequiredSkill(string text, out string requiredSkill)
    {
        requiredSkill = string.Empty;
        if (_currentProfile.SkillNames.Count == 0)
        {
            return false;
        }

        var requiresKaiDiscipline = _state.SeriesId.Equals("lw", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Kai Discipline", StringComparison.OrdinalIgnoreCase);
        var requiresMagicalPower = _state.SeriesId.Equals("gs", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("Magical Power", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Power of", StringComparison.OrdinalIgnoreCase));

        if (!requiresKaiDiscipline && !requiresMagicalPower)
        {
            return false;
        }

        foreach (var skill in _currentProfile.SkillNames)
        {
            if (text.Contains(skill, StringComparison.OrdinalIgnoreCase))
            {
                requiredSkill = skill;
                return true;
            }
        }

        return false;
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

    private sealed record ProfileWizardResult(
        PlayerProfile Profile,
        string SeriesId,
        SeriesProfileState SeriesState,
        string CharacterName,
        CharacterProfileState? CharacterState,
        bool IsNewCharacter);

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
