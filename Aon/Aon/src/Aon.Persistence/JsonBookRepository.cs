using System.Text.Json;
using Aon.Application;
using Aon.Content;

namespace Aon.Persistence;

public sealed class JsonBookRepository : IBookRepository
{
    private readonly string _rootDirectory;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonBookRepository(string rootDirectory, JsonSerializerOptions? serializerOptions = null)
    {
        _rootDirectory = rootDirectory;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions();
    }

    public async Task<Book> GetBookAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_rootDirectory, $"{bookId}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Book JSON not found for id '{bookId}'.", path);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var book = JsonSerializer.Deserialize<Book>(json, _serializerOptions);
        if (book is null)
        {
            throw new InvalidOperationException($"Failed to deserialize book '{bookId}'.");
        }

        return book;
    }
}
