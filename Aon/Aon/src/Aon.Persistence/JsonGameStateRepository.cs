using System.Text.Json;
using Aon.Application;
using Aon.Core;

namespace Aon.Persistence;

public sealed class JsonGameStateRepository : IGameStateRepository
{
    private readonly string _saveDirectory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public JsonGameStateRepository(string saveDirectory)
    {
        _saveDirectory = saveDirectory;
    }

    public async Task<GameState?> LoadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var path = GetSlotPath(slot);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<GameState>(stream, _options, cancellationToken);
    }

    public async Task SaveAsync(string slot, GameState state, CancellationToken cancellationToken = default)
    {
        var path = GetSlotPath(slot);
        Directory.CreateDirectory(_saveDirectory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, state, _options, cancellationToken);
    }

    private string GetSlotPath(string slot)
    {
        var safeSlot = SanitizeSlot(slot);
        return Path.Combine(_saveDirectory, $"{safeSlot}.json");
    }

    private static string SanitizeSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            return "default";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new char[slot.Length];
        for (var i = 0; i < slot.Length; i++)
        {
            var character = slot[i];
            sanitized[i] = invalidChars.Contains(character) ? '_' : character;
        }

        var result = new string(sanitized).Trim();
        return string.IsNullOrWhiteSpace(result) ? "default" : result;
    }
}
