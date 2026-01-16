using System.Text.Json;
using Aon.Application;
using Aon.Core;

namespace Aon.Persistence;

public sealed class JsonGameStateRepository : IGameStateRepository
{
    private readonly string _rootDirectory;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonGameStateRepository(string rootDirectory, JsonSerializerOptions? serializerOptions = null)
    {
        _rootDirectory = rootDirectory;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task<GameState?> LoadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var path = GetSlotPath(slot);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<GameState>(json, _serializerOptions);
    }

    public async Task SaveAsync(string slot, GameState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootDirectory);
        var path = GetSlotPath(slot);
        var json = JsonSerializer.Serialize(state, _serializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private string GetSlotPath(string slot)
    {
        var sanitized = new string(slot.Select(ch => InvalidFileNameChars.Contains(ch) ? '_' : ch).ToArray());
        return Path.Combine(_rootDirectory, $"{sanitized}.json");
    }

    private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());
}
