using Aon.Core;

namespace Aon.Application;

public interface IGameStateRepository
{
    Task<GameState?> LoadAsync(string slot, CancellationToken cancellationToken = default);
    Task SaveAsync(string slot, GameState state, CancellationToken cancellationToken = default);
}
