namespace Aon.Content;

public sealed class Book
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<FrontMatterSection> FrontMatter { get; init; } = new();
    public List<BookSection> Sections { get; init; } = new();
}
