namespace Aon.Content;

public sealed class BookSection
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<ContentBlock> Blocks { get; init; } = new();
    public List<Choice> Choices { get; init; } = new();
}
