using Aon.Content;
using Aon.Core;

namespace Aon.Application;

public sealed class GameService
{
    private readonly IBookRepository _bookRepository;
    private readonly IGameStateRepository _gameStateRepository;

    public GameService(IBookRepository bookRepository, IGameStateRepository gameStateRepository)
    {
        _bookRepository = bookRepository;
        _gameStateRepository = gameStateRepository;
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
        var book = await _bookRepository.GetBookAsync(state.BookId, cancellationToken);
        var section = book.Sections.FirstOrDefault(item => item.Id == choice.TargetId);
        if (section is null)
        {
            return null;
        }

        state.SectionId = section.Id;
        return section;
    }
}
