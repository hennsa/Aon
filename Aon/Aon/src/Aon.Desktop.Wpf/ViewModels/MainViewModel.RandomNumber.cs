using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Aon.Content;
using Aon.Core;
using Aon.Rules;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private void ResetRandomNumberState()
    {
        _pendingRandomChoices.Clear();
        _randomNumberResult = null;
        _resolvedRandomChoice = null;
        RandomNumberStatus = string.Empty;
        IsRandomNumberVisible = false;
        IsRoundsToFireVisible = false;
        AreChoicesVisible = true;
        OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
        _confirmRandomNumberCommand.RaiseCanExecuteChanged();
    }

    private void PrepareRandomNumberSection(BookSection section)
    {
        _pendingRandomChoices.Clear();
        _pendingRandomChoices.AddRange(section.Choices);
        _randomNumberResult = null;
        _resolvedRandomChoice = null;
        ResetRollHistory();
        RandomNumberStatus = "Roll a number from the Random Number Table (0–9).";
        RollModifierText = GetSuggestedRollModifier(section).ToString(CultureInfo.InvariantCulture);
        IsRoundsToFireVisible = RequiresRoundsToFireInput(section);
        if (IsRoundsToFireVisible)
        {
            RoundsToFireText = "1";
        }
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
        return _gameService.EvaluateChoice(_state, choice).IsAvailable;
    }

    private void RollRandomNumber()
    {
        if (_book is null)
        {
            return;
        }

        var roll = _gameService.RollRandomNumber();
        TrackRoll(roll);
        var modifier = GetRollModifier();
        var roundsBonus = GetRoundsToFireBonus();
        var totalScore = roll + modifier + roundsBonus;
        var effectiveRoll = Math.Clamp(roll + modifier + roundsBonus, 0, 9);
        _randomNumberResult = effectiveRoll;

        if (IsManualRandomMode)
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, roundsBonus, effectiveRoll, $"Select the correct outcome below. Total score: {totalScore}.");
            ShowChoices(_pendingRandomChoices);
            OnPropertyChanged(nameof(IsRandomNumberConfirmVisible));
            _confirmRandomNumberCommand.RaiseCanExecuteChanged();
            return;
        }

        var matches = RollOutcomeResolver.ResolveChoices(_pendingRandomChoices, effectiveRoll);
        if (matches.Count == 0)
        {
            matches = FilterChoicesByTotalScore(_pendingRandomChoices, totalScore);
        }

        if (matches.Count == 1)
        {
            _resolvedRandomChoice = matches[0];
            var targetId = GetOutcomeTargetId(_resolvedRandomChoice, effectiveRoll);
            var suffix = string.IsNullOrWhiteSpace(targetId)
                ? "This resolves your roll outcome."
                : $"This directs you to section {targetId}.";
            RandomNumberStatus = BuildRollStatus(roll, modifier, roundsBonus, effectiveRoll, $"{suffix} Total score: {totalScore}.");
            AreChoicesVisible = false;
        }
        else if (matches.Count > 1)
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, roundsBonus, effectiveRoll, $"Multiple outcomes match—choose the correct option below. Total score: {totalScore}.");
            ShowChoices(matches);
        }
        else
        {
            _resolvedRandomChoice = null;
            RandomNumberStatus = BuildRollStatus(roll, modifier, roundsBonus, effectiveRoll, $"Choose the correct option below. Total score: {totalScore}.");
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
        return section.Choices.Any(choice => ChoiceRollMetadata.FromChoice(choice).RequiresRoll)
            || RequiresRandomNumberFromText(section);
    }

    private static bool RequiresRandomNumberFromText(BookSection section)
    {
        if (section.Blocks.Count == 0)
        {
            return false;
        }

        var text = string.Join(" ", section.Blocks.Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Random Number Table", StringComparison.OrdinalIgnoreCase)
            || text.Contains("random number", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresRoundsToFireInput(BookSection section)
    {
        if (section.Blocks.Count == 0)
        {
            return false;
        }

        var text = string.Join(" ", section.Blocks.Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("round", StringComparison.OrdinalIgnoreCase)
            && text.Contains("add", StringComparison.OrdinalIgnoreCase)
            && text.Contains("point", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Choice> FilterChoicesByTotalScore(IEnumerable<Choice> choices, int totalScore)
    {
        var matches = new List<Choice>();
        foreach (var choice in choices)
        {
            if (string.IsNullOrWhiteSpace(choice.Text))
            {
                continue;
            }

            var match = Regex.Match(
                choice.Text,
                "\\btotal score[^\\d]*(?<value>\\d+)\\b[^.]*?(?<comparison>or less|or more|or higher)\\b",
                RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups["value"].Value, out var threshold))
            {
                continue;
            }

            var comparison = match.Groups["comparison"].Value;
            if (comparison.Contains("less", StringComparison.OrdinalIgnoreCase) && totalScore <= threshold)
            {
                matches.Add(choice);
            }
            else if ((comparison.Contains("more", StringComparison.OrdinalIgnoreCase)
                    || comparison.Contains("higher", StringComparison.OrdinalIgnoreCase))
                && totalScore >= threshold)
            {
                matches.Add(choice);
            }
        }

        return matches;
    }

    private static string GetOutcomeTargetId(Choice choice, int roll)
    {
        var metadata = ChoiceRollMetadata.FromChoice(choice);
        var outcome = RollOutcomeResolver.ResolveOutcomes(metadata, roll)
            .FirstOrDefault();
        return outcome?.TargetId ?? choice.TargetId;
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

    private int GetRoundsToFireBonus()
    {
        if (!IsRoundsToFireVisible)
        {
            return 0;
        }

        var rounds = GetRoundsToFire();
        return Math.Clamp(rounds, 1, 5);
    }

    private int GetRoundsToFire()
    {
        if (int.TryParse(RoundsToFireText, out var rounds))
        {
            return rounds;
        }

        RoundsToFireText = "1";
        return 1;
    }

    private int GetSuggestedRollModifier(BookSection section)
    {
        if (_state.Character.CoreSkills.Count == 0)
        {
            return 0;
        }

        var text = string.Join(" ", section.Blocks.Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var modifierMatch = Regex.Match(
            text,
            "\\badd to your current (?<skills>[^.]+?) skill total\\b",
            RegexOptions.IgnoreCase);
        if (modifierMatch.Success)
        {
            var initialSkillsSegment = modifierMatch.Groups["skills"].Value;
            var total = GetSkillTotalFromSegment(initialSkillsSegment);
            if (total > 0)
            {
                return total;
            }
        }

        modifierMatch = Regex.Match(
            text,
            "\\bif your current (?<skills>[^.]+?) points[^.]*?total (?<threshold>\\d+) or more, add (?<modifier>\\d+)\\b",
            RegexOptions.IgnoreCase);

        if (!modifierMatch.Success)
        {
            modifierMatch = Regex.Match(
                text,
                "\\bif your current (?<skills>[^.]+?) points[^.]*?total (?<threshold>\\d+) or less, add (?<modifier>\\d+)\\b",
                RegexOptions.IgnoreCase);
        }

        if (!modifierMatch.Success)
        {
            return 0;
        }

        if (!int.TryParse(modifierMatch.Groups["threshold"].Value, out var threshold)
            || !int.TryParse(modifierMatch.Groups["modifier"].Value, out var modifier))
        {
            return 0;
        }

        var skillsSegment = modifierMatch.Groups["skills"].Value;
        var sum = GetSkillTotalFromSegment(skillsSegment);
        if (sum == 0)
        {
            return 0;
        }

        return sum >= threshold ? modifier : 0;
    }

    private int GetSkillTotalFromSegment(string skillsSegment)
    {
        if (string.IsNullOrWhiteSpace(skillsSegment))
        {
            return 0;
        }

        var sum = 0;
        foreach (var entry in _state.Character.CoreSkills)
        {
            if (Regex.IsMatch(skillsSegment, $"\\b{Regex.Escape(entry.Key)}\\b", RegexOptions.IgnoreCase))
            {
                sum += entry.Value;
            }
        }

        return sum;
    }

    private static string BuildRollStatus(int roll, int modifier, int roundsBonus, int effectiveRoll, string suffix)
    {
        if (modifier == 0 && roundsBonus == 0)
        {
            return $"You rolled {roll}. {suffix}";
        }

        if (roundsBonus == 0)
        {
            return $"You rolled {roll} + {modifier} = {effectiveRoll}. {suffix}";
        }

        if (modifier == 0)
        {
            return $"You rolled {roll} + {roundsBonus} = {effectiveRoll}. {suffix}";
        }

        return $"You rolled {roll} + {modifier} + {roundsBonus} = {effectiveRoll}. {suffix}";
    }

    private sealed record ProfileWizardResult(
        PlayerProfile Profile,
        string SeriesId,
        SeriesProfileState SeriesState,
        string CharacterName,
        CharacterProfileState? CharacterState,
        bool IsNewCharacter);
}
