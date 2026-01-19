using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Aon.Tools.BookImporter;

public static class Program
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LoneWolfNameRegex = new(@"\bLone\s+Wolf\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GreyStarNameRegex = new(@"\bGrey\s+Star\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalPhoenixNameRegex = new(@"\bCal\s+Phoenix\b", RegexOptions.Compiled);
    private static readonly Regex CalNameRegex = new(@"\bCal\b", RegexOptions.Compiled);
    private const string CharacterNameToken = "{{characterName}}";
    private static string _currentSeriesId = string.Empty;

    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepoRoot(Environment.CurrentDirectory);
        if (repoRoot is null)
        {
            Console.Error.WriteLine("Unable to locate repo root containing the Aon/BookSource directory.");
            return 1;
        }

        var defaultInputRoot = Path.Combine(repoRoot, "Aon", "BookSource", "all-books-simple", "en", "xhtml-simple");
        var defaultOutputRoot = Path.Combine(repoRoot, "Aon.Content", "Books");

        var inputRoot = args.Length > 0 ? args[0] : defaultInputRoot;
        var outputRoot = args.Length > 1 ? args[1] : defaultOutputRoot;

        if (!Directory.Exists(inputRoot))
        {
            Console.Error.WriteLine($"Input root not found: {inputRoot}");
            return 1;
        }

        Directory.CreateDirectory(outputRoot);

        var htmlParser = new HtmlParser(new HtmlParserOptions
        {
            IsEmbedded = true,
            IsStrictMode = false
        });

        var bookFiles = Directory.EnumerateFiles(inputRoot, "*.htm", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validationErrors = new List<string>();

        foreach (var filePath in bookFiles)
        {
            var html = await File.ReadAllTextAsync(filePath);
            var document = await htmlParser.ParseDocumentAsync(html);
            var bookId = Path.GetFileNameWithoutExtension(filePath);
            _currentSeriesId = GetSeriesId(bookId);
            var title = NormalizeText(document.QuerySelector("head > title")?.TextContent
                                       ?? document.QuerySelector("h1")?.TextContent
                                       ?? bookId);

            var toc = ExtractTableOfContents(document);
            var numberedSectionRoot = document.QuerySelector("div.numbered");

            var frontMatterSections = ExtractFrontMatterSections(document, numberedSectionRoot);
            var numberedSections = ExtractNumberedSections(numberedSectionRoot);

            var allSections = frontMatterSections.Concat(numberedSections).ToList();
            var sectionIds = new HashSet<string>(allSections.Select(section => section.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var section in allSections)
            {
                foreach (var choice in section.Choices)
                {
                    if (string.IsNullOrWhiteSpace(choice.TargetSectionId))
                    {
                        continue;
                    }

                    if (!sectionIds.Contains(choice.TargetSectionId))
                    {
                        validationErrors.Add($"{bookId}: section '{section.Id}' has choice targeting missing section '{choice.TargetSectionId}'.");
                    }
                }
            }

            var anchors = document.QuerySelectorAll("a[name]")
                .Select(anchor => NormalizeText(anchor.GetAttribute("name") ?? string.Empty))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var book = new BookOutput(
                bookId,
                title,
                toc,
                frontMatterSections,
                numberedSections,
                anchors);

            var outputPath = Path.Combine(outputRoot, $"{bookId}.json");
            var json = JsonSerializer.Serialize(book, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
            Console.WriteLine($"Wrote {outputPath}");
        }

        if (validationErrors.Count > 0)
        {
            Console.Error.WriteLine("Choice validation failed:");
            foreach (var error in validationErrors)
            {
                Console.Error.WriteLine($" - {error}");
            }

            return 1;
        }

        return 0;
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var bookSourcePath = Path.Combine(current.FullName, "Aon", "BookSource");
            if (Directory.Exists(bookSourcePath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static List<TocEntry> ExtractTableOfContents(IDocument document)
    {
        var heading = document.QuerySelectorAll("div.frontmatter h2")
            .FirstOrDefault(h2 => string.Equals(NormalizeText(h2.TextContent), "Table of Contents", StringComparison.OrdinalIgnoreCase));

        if (heading is null)
        {
            return new List<TocEntry>();
        }

        var tocList = heading.ParentElement?.QuerySelector("ul");
        if (tocList is null)
        {
            return new List<TocEntry>();
        }

        return ParseTocList(tocList);
    }

    private static List<TocEntry> ParseTocList(IElement listElement)
    {
        var entries = new List<TocEntry>();
        foreach (var item in listElement.Children.Where(child => child.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase)))
        {
            var link = item.QuerySelector(":scope > a");
            var title = NormalizeDisplayText(link?.TextContent ?? item.TextContent);
            var href = link?.GetAttribute("href") ?? string.Empty;
            var target = href.StartsWith('#') ? href[1..] : null;

            var childList = item.QuerySelector(":scope > ul");
            var children = childList is null ? new List<TocEntry>() : ParseTocList(childList);

            entries.Add(new TocEntry(title, target, children));
        }

        return entries;
    }

    private static List<SectionOutput> ExtractFrontMatterSections(IDocument document, IElement? numberedRoot)
    {
        var sections = new List<SectionOutput>();
        var bodyChildren = document.Body?.Children ?? Array.Empty<IElement>();
        var frontMatterDivs = new List<IElement>();

        foreach (var element in bodyChildren)
        {
            if (element == numberedRoot)
            {
                break;
            }

            if (element.ClassList.Contains("frontmatter"))
            {
                frontMatterDivs.Add(element);
            }
        }

        for (var index = 0; index < frontMatterDivs.Count; index++)
        {
            var div = frontMatterDivs[index];
            var heading = div.QuerySelector("h1, h2, h3, h4, h5, h6");
            var title = NormalizeDisplayText(heading?.TextContent ?? $"Frontmatter {index + 1}");
            var anchorName = heading?.QuerySelector("a[name]")?.GetAttribute("name");
            var sectionId = NormalizeId(anchorName, $"frontmatter-{index + 1}");

            var anchors = div.QuerySelectorAll("a[name]")
                .Select(anchor => NormalizeText(anchor.GetAttribute("name") ?? string.Empty))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var content = ExtractContentBlocks(div, excludeHeading: heading);

            sections.Add(new SectionOutput(
                sectionId,
                title,
                content,
                new List<ChoiceOutput>(),
                new List<ActionOutput>(),
                anchors));
        }

        return sections;
    }

    private static List<SectionOutput> ExtractNumberedSections(IElement? numberedRoot)
    {
        var sections = new List<SectionOutput>();
        if (numberedRoot is null)
        {
            return sections;
        }

        SectionOutputBuilder? current = null;
        foreach (var node in numberedRoot.Children)
        {
            if (node.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    sections.Add(current.Build());
                }

                var anchor = node.QuerySelector("a[name]");
                var id = NormalizeId(anchor?.GetAttribute("name"), Guid.NewGuid().ToString("N"));
                var title = NormalizeDisplayText(node.TextContent);
                current = new SectionOutputBuilder(id, title);
                current.AddAnchors(node.QuerySelectorAll("a[name]"));
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (node.ClassList.Contains("choice"))
            {
                var link = node.QuerySelector("a[href^='#']");
                var target = link?.GetAttribute("href");
                var targetId = target?.StartsWith('#') == true ? target[1..] : null;

                current.Choices.Add(new ChoiceOutput(NormalizeDisplayText(node.TextContent), targetId));
                continue;
            }

            if (node.ClassList.Contains("action"))
            {
                current.Actions.Add(new ActionOutput(NormalizeDisplayText(node.TextContent)));
                continue;
            }

            if (node.TagName.Equals("H2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            current.Content.Add(NormalizeDisplayText(node.TextContent));
            current.AddAnchors(node.QuerySelectorAll("a[name]"));
        }

        if (current is not null)
        {
            sections.Add(current.Build());
        }

        return sections;
    }

    private static List<string> ExtractContentBlocks(IElement container, IElement? excludeHeading)
    {
        var content = new List<string>();
        foreach (var child in container.Children)
        {
            if (excludeHeading is not null && child == excludeHeading)
            {
                continue;
            }

            if (child.ClassList.Contains("choice") || child.ClassList.Contains("action"))
            {
                continue;
            }

            var text = NormalizeDisplayText(child.TextContent);
            if (!string.IsNullOrWhiteSpace(text))
            {
                content.Add(text);
            }
        }

        return content;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(value.Trim(), " ");
    }

    private static string NormalizeDisplayText(string? value)
    {
        var normalized = NormalizeText(value);
        return ReplaceCharacterNameTokens(normalized);
    }

    private static string ReplaceCharacterNameTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return _currentSeriesId switch
        {
            "lw" => LoneWolfNameRegex.Replace(value, CharacterNameToken),
            "gs" => GreyStarNameRegex.Replace(value, CharacterNameToken),
            "fw" => CalNameRegex.Replace(CalPhoenixNameRegex.Replace(value, CharacterNameToken), CharacterNameToken),
            _ => value
        };
    }

    private static string NormalizeId(string? value, string fallback)
    {
        var normalized = NormalizeText(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string GetSeriesId(string bookId)
    {
        if (bookId.StartsWith("lw", StringComparison.OrdinalIgnoreCase))
        {
            return "lw";
        }

        if (bookId.StartsWith("gs", StringComparison.OrdinalIgnoreCase))
        {
            return "gs";
        }

        if (bookId.StartsWith("fw", StringComparison.OrdinalIgnoreCase))
        {
            return "fw";
        }

        return "unknown";
    }

    private sealed class SectionOutputBuilder
    {
        public SectionOutputBuilder(string id, string title)
        {
            Id = id;
            Title = title;
            Content = new List<string>();
            Choices = new List<ChoiceOutput>();
            Actions = new List<ActionOutput>();
            Anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; }
        public string Title { get; }
        public List<string> Content { get; }
        public List<ChoiceOutput> Choices { get; }
        public List<ActionOutput> Actions { get; }
        public HashSet<string> Anchors { get; }

        public void AddAnchors(IEnumerable<IElement> anchors)
        {
            foreach (var anchor in anchors)
            {
                var name = NormalizeText(anchor.GetAttribute("name"));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Anchors.Add(name);
                }
            }
        }

        public SectionOutput Build()
        {
            return new SectionOutput(
                Id,
                Title,
                Content,
                Choices,
                Actions,
                Anchors.OrderBy(anchor => anchor, StringComparer.OrdinalIgnoreCase).ToList());
        }
    }

    public sealed record BookOutput(
        string Id,
        string Title,
        List<TocEntry> Toc,
        List<SectionOutput> FrontMatter,
        List<SectionOutput> Sections,
        List<string> Anchors);

    public sealed record TocEntry(
        string Title,
        string? TargetId,
        List<TocEntry> Children);

    public sealed record SectionOutput(
        string Id,
        string Title,
        List<string> Content,
        List<ChoiceOutput> Choices,
        List<ActionOutput> Actions,
        List<string> Anchors);

    public sealed record ChoiceOutput(
        string Text,
        string? TargetSectionId);

    public sealed record ActionOutput(
        string Text);
}
