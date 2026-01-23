using System.Text.Json;
using Aon.Application;
using Aon.Content;

namespace Aon.Persistence;

public sealed class JsonBookRepository : IBookRepository
{
    private readonly string _booksDirectory;
    private readonly JsonSerializerOptions _options;

    public JsonBookRepository(string booksDirectory)
    {
        _booksDirectory = booksDirectory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Book> GetBookAsync(string bookId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_booksDirectory, $"{bookId}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Book file not found: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var book = await JsonSerializer.DeserializeAsync<Book>(stream, _options, cancellationToken);
        if (book is null)
        {
            throw new InvalidOperationException($"Unable to deserialize book data from {path}.");
        }

        if (string.IsNullOrWhiteSpace(book.SeriesId))
        {
            throw new InvalidOperationException($"Book data from {path} is missing a seriesId.");
        }

        return book;
    }
}
