namespace Aon.Content;

public sealed class BookSection
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Html { get; init; } = string.Empty;
    public List<SectionLink> Links { get; init; } = new();
}
