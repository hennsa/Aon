using Aon.Content;

namespace Aon.Application;

public interface IBookRepository
{
    Task<Book> GetBookAsync(string bookId, CancellationToken cancellationToken = default);
}
