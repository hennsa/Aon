using System;
using Aon.Core;
using Aon.Rules;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private int _enemyCombatSkill;
    private int _enemyEndurance;
    private int _combatRoll;
    private int _combatRatio;
    private int _combatPlayerLoss;
    private int _combatEnemyLoss;
    private bool _combatPlayerDefeated;
    private bool _combatEnemyDefeated;
    private string _combatOutcomeSummary = "Enter enemy stats and a roll to resolve the combat round.";

    public int EnemyCombatSkill
    {
        get => _enemyCombatSkill;
        set
        {
            if (_enemyCombatSkill == value)
            {
                return;
            }

            _enemyCombatSkill = value;
            OnPropertyChanged();
            RefreshCombatOutcome();
        }
    }

    public int EnemyEndurance
    {
        get => _enemyEndurance;
        set
        {
            if (_enemyEndurance == value)
            {
                return;
            }

            _enemyEndurance = value;
            OnPropertyChanged();
            RefreshCombatOutcome();
        }
    }

    public int CombatRoll
    {
        get => _combatRoll;
        set
        {
            if (_combatRoll == value)
            {
                return;
            }

            _combatRoll = value;
            OnPropertyChanged();
            RefreshCombatOutcome();
        }
    }

    public int CombatRatio
    {
        get => _combatRatio;
        private set
        {
            if (_combatRatio == value)
            {
                return;
            }

            _combatRatio = value;
            OnPropertyChanged();
        }
    }

    public int CombatPlayerLoss
    {
        get => _combatPlayerLoss;
        private set
        {
            if (_combatPlayerLoss == value)
            {
                return;
            }

            _combatPlayerLoss = value;
            OnPropertyChanged();
        }
    }

    public int CombatEnemyLoss
    {
        get => _combatEnemyLoss;
        private set
        {
            if (_combatEnemyLoss == value)
            {
                return;
            }

            _combatEnemyLoss = value;
            OnPropertyChanged();
        }
    }

    public bool CombatPlayerDefeated
    {
        get => _combatPlayerDefeated;
        private set
        {
            if (_combatPlayerDefeated == value)
            {
                return;
            }

            _combatPlayerDefeated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CombatPlayerDefeatedLabel));
        }
    }

    public bool CombatEnemyDefeated
    {
        get => _combatEnemyDefeated;
        private set
        {
            if (_combatEnemyDefeated == value)
            {
                return;
            }

            _combatEnemyDefeated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CombatEnemyDefeatedLabel));
        }
    }

    public string CombatOutcomeSummary
    {
        get => _combatOutcomeSummary;
        private set
        {
            if (_combatOutcomeSummary == value)
            {
                return;
            }

            _combatOutcomeSummary = value;
            OnPropertyChanged();
        }
    }

    public string CombatTableLabel => string.IsNullOrWhiteSpace(_state.SeriesId)
        ? "Default Combat Table"
        : $"{ResolveSeriesName(_state.SeriesId)} Combat Table";

    public int PlayerCombatSkill => _state.Character.CombatSkill;
    public int PlayerEffectiveCombatSkill => _state.Character.GetEffectiveCombatSkill();
    public int PlayerEndurance => _state.Character.Endurance;
    public int CombatSkillBonus => _state.Character.Attributes.TryGetValue(Character.CombatSkillBonusAttribute, out var value) ? value : 0;
    public string CombatPlayerDefeatedLabel => CombatPlayerDefeated ? "Yes" : "No";
    public string CombatEnemyDefeatedLabel => CombatEnemyDefeated ? "Yes" : "No";

    public RelayCommand RollCombatNumberCommand => _rollCombatNumberCommand;

    private void RollCombatNumber()
    {
        CombatRoll = _gameService.RollRandomNumber();
    }

    private void RefreshCombatSeriesStats()
    {
        CombatSeriesStats.Clear();

        if (_state.Character.Attributes.TryGetValue("Willpower", out var willpower))
        {
            CombatSeriesStats.Add(new StatEntryViewModel("Willpower", willpower));
        }

        if (_state.Character.Attributes.TryGetValue("CoreSkillPoolTotal", out var poolTotal))
        {
            CombatSeriesStats.Add(new StatEntryViewModel("Core Skill Pool Total", poolTotal));
        }

        OnPropertyChanged(nameof(HasCombatSeriesStats));
    }

    private void RefreshCombatOutcome()
    {
        if (!IsProfileReady)
        {
            CombatOutcomeSummary = "Select a profile and character to resolve combat.";
            CombatRatio = 0;
            CombatPlayerLoss = 0;
            CombatEnemyLoss = 0;
            CombatPlayerDefeated = false;
            CombatEnemyDefeated = false;
            CombatSeriesStats.Clear();
            OnPropertyChanged(nameof(HasCombatSeriesStats));
            return;
        }

        RefreshCombatSeriesStats();

        if (EnemyCombatSkill <= 0 || EnemyEndurance <= 0)
        {
            CombatOutcomeSummary = "Enter enemy combat skill and endurance to resolve the round.";
            CombatRatio = 0;
            CombatPlayerLoss = 0;
            CombatEnemyLoss = 0;
            CombatPlayerDefeated = false;
            CombatEnemyDefeated = false;
            return;
        }

        var roll = Math.Clamp(CombatRoll, 0, 9);
        var rollNote = CombatRoll == roll ? $"roll {roll}" : $"roll {roll} (clamped from {CombatRoll})";
        var effectiveCombatSkill = _state.Character.GetEffectiveCombatSkill();
        var combatRatio = effectiveCombatSkill - EnemyCombatSkill;
        var combatTable = CombatTable.Load(_state.SeriesId);
        var outcome = combatTable.Resolve(combatRatio, roll);
        var playerLoss = outcome.PlayerLoss;
        var enemyLoss = outcome.EnemyLoss;
        var playerDefeated = _state.Character.Endurance - playerLoss <= 0;
        var enemyDefeated = EnemyEndurance - enemyLoss <= 0;

        CombatRatio = combatRatio;
        CombatPlayerLoss = playerLoss;
        CombatEnemyLoss = enemyLoss;
        CombatPlayerDefeated = playerDefeated;
        CombatEnemyDefeated = enemyDefeated;

        CombatOutcomeSummary = $"{CombatTableLabel}: {rollNote}. Combat ratio {combatRatio}. "
            + $"Player loses {playerLoss} Endurance; enemy loses {enemyLoss}. "
            + $"Player defeated: {CombatPlayerDefeatedLabel}. Enemy defeated: {CombatEnemyDefeatedLabel}.";
    }
}
