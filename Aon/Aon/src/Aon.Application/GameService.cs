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

    public async Task<BookSection?> ApplyChoiceAsync(GameState state, Choice choice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);

        var book = await _bookRepository.GetBookAsync(state.BookId, cancellationToken);
        var section = book.Sections.FirstOrDefault(item => item.Id == choice.TargetId);
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

        var effects = _rulesEngine.ResolveChoiceRules(choice).Effects;
        _rulesEngine.ApplyEffects(effects, context);

        state.SectionId = section.Id;
        return section;
    }

    public ChoiceEvaluationResult EvaluateChoice(GameState state, Choice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);

        var context = new RuleContext(state);
        return _rulesEngine.EvaluateChoice(choice, context);
    }

    public ChoiceRuleSet ResolveChoiceRules(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        return _rulesEngine.ResolveChoiceRules(choice);
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
        int randomNumber)
    {
        return _rulesEngine.ResolveCombatRound(player, enemyCombatSkill, enemyEndurance, randomNumber);
    }
}
