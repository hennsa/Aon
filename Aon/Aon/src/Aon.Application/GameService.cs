using Aon.Content;
using Aon.Core;
using Aon.Rules;

namespace Aon.Application;

public sealed class GameService
{
    private readonly IBookRepository _bookRepository;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IRulesEngine _rulesEngine;

    public GameService(
        IBookRepository bookRepository,
        IGameStateRepository gameStateRepository,
        IRulesEngine rulesEngine)
    {
        _bookRepository = bookRepository;
        _gameStateRepository = gameStateRepository;
        _rulesEngine = rulesEngine;
    }

    public async Task<GameState> StartNewGameAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var book = await _bookRepository.GetBookAsync(bookId, cancellationToken);
        var firstSection = book.Sections.FirstOrDefault()?.Id ?? string.Empty;

        return new GameState
        {
            BookId = book.Id,
            SeriesId = book.SeriesId,
            SectionId = firstSection
        };
    }

    public Task<GameState?> LoadGameAsync(string slot, CancellationToken cancellationToken = default)
    {
        return _gameStateRepository.LoadAsync(slot, cancellationToken);
    }

    public Task SaveGameAsync(string slot, GameState state, CancellationToken cancellationToken = default)
    {
        return _gameStateRepository.SaveAsync(slot, state, cancellationToken);
    }

    public async Task<BookSection?> ApplyChoiceAsync(
        GameState state,
        Choice choice,
        int? randomNumber = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);

        var book = await _bookRepository.GetBookAsync(state.BookId, cancellationToken);
        var randomOutcome = ResolveRandomOutcome(choice, randomNumber);
        var section = book.Sections.FirstOrDefault(item => item.Id == randomOutcome.TargetId);
        if (section is null)
        {
            return null;
        }

        var context = new RuleContext(state);
        var evaluation = _rulesEngine.EvaluateChoice(choice, context);
        if (!evaluation.IsAvailable)
        {
            return null;
        }

        var effects = _rulesEngine.ResolveChoiceRules(choice, context).Effects
            .Concat(randomOutcome.Effects)
            .ToList();
        _rulesEngine.ApplyEffects(effects, context);

        state.SectionId = section.Id;
        return section;
    }

    private static RandomOutcomeResolution ResolveRandomOutcome(Choice choice, int? randomNumber)
    {
        var targetId = choice.TargetId;
        var effects = new List<Effect>();

        if (!randomNumber.HasValue)
        {
            return new RandomOutcomeResolution(targetId, effects);
        }

        var metadata = ChoiceRollMetadata.FromChoice(choice);
        if (!metadata.RequiresRoll)
        {
            return new RandomOutcomeResolution(targetId, effects);
        }

        var outcome = RollOutcomeResolver.ResolveOutcomes(metadata, randomNumber.Value)
            .FirstOrDefault();
        if (outcome is null)
        {
            return new RandomOutcomeResolution(targetId, effects);
        }

        if (!string.IsNullOrWhiteSpace(outcome.TargetId))
        {
            targetId = outcome.TargetId;
        }

        foreach (var effect in outcome.Effects)
        {
            effects.Add(EffectParser.Parse(effect));
        }

        return new RandomOutcomeResolution(targetId, effects);
    }

    private sealed record RandomOutcomeResolution(string TargetId, List<Effect> Effects);

    public ChoiceEvaluationResult EvaluateChoice(GameState state, Choice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);

        var context = new RuleContext(state);
        return _rulesEngine.EvaluateChoice(choice, context);
    }

    public ChoiceRuleSet ResolveChoiceRules(GameState state, Choice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);

        var context = new RuleContext(state);
        return _rulesEngine.ResolveChoiceRules(choice, context);
    }

    public void ApplyEffects(GameState state, IEnumerable<Effect> effects)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(effects);

        var context = new RuleContext(state);
        _rulesEngine.ApplyEffects(effects, context);
    }

    public int RollRandomNumber()
    {
        return _rulesEngine.RollRandomNumber();
    }

    public CombatResult ResolveCombatRound(
        Character player,
        int enemyCombatSkill,
        int enemyEndurance,
        int randomNumber,
        string? seriesId)
    {
        return _rulesEngine.ResolveCombatRound(player, enemyCombatSkill, enemyEndurance, randomNumber, seriesId);
    }
}
