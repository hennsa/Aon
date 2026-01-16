using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using Aon.Content;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Aon.Tools.BookImporter <input-dir> <output-dir>");
    return;
}

var inputDirectory = args[0];
var outputDirectory = args[1];

if (!Directory.Exists(inputDirectory))
{
    Console.Error.WriteLine($"Input directory not found: {inputDirectory}");
    return;
}

Directory.CreateDirectory(outputDirectory);

var config = Configuration.Default;
var context = BrowsingContext.New(config);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var importedCount = 0;
var hadValidationErrors = false;

foreach (var file in Directory.EnumerateFiles(inputDirectory, "*.htm", SearchOption.AllDirectories))
{
    var html = await File.ReadAllTextAsync(file);
    var document = await context.OpenAsync(request => request.Content(html));
    var bookId = BuildBookId(inputDirectory, file);
    var book = ExtractBook(document, bookId);
    var validationErrors = ValidateBook(book);

    if (validationErrors.Count > 0)
    {
        hadValidationErrors = true;
        Console.Error.WriteLine($"Validation errors in {file}:");
        foreach (var error in validationErrors)
        {
            Console.Error.WriteLine($"  - {error}");
        }
    }

    var outputPath = Path.Combine(outputDirectory, $"{book.Id}.json");
    var json = JsonSerializer.Serialize(book, jsonOptions);
    await File.WriteAllTextAsync(outputPath, json);
    importedCount++;
}

Console.WriteLine($"Imported {importedCount} book file(s).");
if (hadValidationErrors)
{
    Console.Error.WriteLine("Import completed with validation errors.");
    Environment.ExitCode = 1;
}

static Book ExtractBook(IDocument document, string fallbackId)
{
    var title = document.QuerySelector("h1")?.TextContent?.Trim();
    if (string.IsNullOrWhiteSpace(title))
    {
        title = document.Title?.Trim();
    }

    var frontMatterSections = ExtractFrontMatter(document);
    var sections = ExtractSections(document);

    return new Book
    {
        Id = fallbackId,
        Title = title ?? fallbackId,
        FrontMatter = frontMatterSections,
        Sections = sections
    };
}

static string BuildBookId(string rootDirectory, string filePath)
{
    var relativePath = Path.GetRelativePath(rootDirectory, filePath);
    var withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
    var segments = withoutExtension
        .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
        .Select(Uri.EscapeDataString);
    return string.Join("__", segments);
}

static List<FrontMatterSection> ExtractFrontMatter(IDocument document)
{
    var sections = new List<FrontMatterSection>();
    var frontMatterNodes = document.QuerySelectorAll("div.frontmatter");
    var index = 1;

    foreach (var node in frontMatterNodes)
    {
        var heading = node.QuerySelector("h1, h2, h3");
        var headingText = heading?.TextContent?.Trim() ?? $"Frontmatter {index}";
        var anchor = heading?.QuerySelector("a[name]")?.GetAttribute("name");
        var id = !string.IsNullOrWhiteSpace(anchor)
            ? anchor
            : !string.IsNullOrWhiteSpace(node.Id)
                ? node.Id
                : $"frontmatter-{index}";

        sections.Add(new FrontMatterSection
        {
            Id = id,
            Title = headingText,
            Html = node.InnerHtml.Trim()
        });

        index++;
    }

    return sections;
}

static List<BookSection> ExtractSections(IDocument document)
{
    var sections = new List<BookSection>();
    var headings = document.QuerySelectorAll("h3");

    foreach (var heading in headings)
    {
        var sectionId = GetSectionId(heading);
        if (sectionId is null)
        {
            continue;
        }

        var title = heading.TextContent?.Trim() ?? sectionId;
        var nodes = new List<INode>();

        for (var node = heading.NextSibling; node is not null; node = node.NextSibling)
        {
            if (node is IElement element && element.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase))
            {
                if (GetSectionId(element) is not null)
                {
                    break;
                }
            }

            if (node is IElement frontMatterElement && frontMatterElement.ClassList.Contains("frontmatter"))
            {
                break;
            }

            nodes.Add(node);
        }

        var blocks = ExtractBlocks(nodes);
        var choices = ExtractChoices(nodes);

        sections.Add(new BookSection
        {
            Id = sectionId,
            Title = title,
            Blocks = blocks,
            Choices = choices
        });
    }

    return sections;
}

static List<ContentBlock> ExtractBlocks(IEnumerable<INode> nodes)
{
    var blocks = new List<ContentBlock>();

    foreach (var node in nodes)
    {
        if (node is IElement element && element.ClassList.Contains("choice"))
        {
            continue;
        }

        var text = node is IElement elementNode
            ? elementNode.TextContent
            : node.TextContent;

        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        var kind = node switch
        {
            IElement element => element.TagName.ToLowerInvariant(),
            _ => "text"
        };

        blocks.Add(new ContentBlock
        {
            Kind = kind,
            Text = text.Trim()
        });
    }

    return blocks;
}

static List<Choice> ExtractChoices(IEnumerable<INode> nodes)
{
    var choices = new List<Choice>();

    foreach (var element in nodes.OfType<IElement>())
    {
        if (!element.ClassList.Contains("choice"))
        {
            continue;
        }

        var link = element.QuerySelector("a[href^=\"#sect\"]");
        if (link is null)
        {
            continue;
        }

        var href = link.GetAttribute("href") ?? string.Empty;
        var targetId = href.StartsWith("#sect", StringComparison.OrdinalIgnoreCase)
            ? href["#sect".Length..]
            : href;

        if (string.IsNullOrWhiteSpace(targetId))
        {
            continue;
        }

        choices.Add(new Choice
        {
            Text = element.TextContent?.Trim() ?? string.Empty,
            TargetId = targetId
        });
    }

    return choices;
}

static List<string> ValidateBook(Book book)
{
    var errors = new List<string>();
    var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var duplicateSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var section in book.Sections)
    {
        if (!sectionIds.Add(section.Id))
        {
            duplicateSections.Add(section.Id);
        }
    }

    foreach (var duplicate in duplicateSections)
    {
        errors.Add($"Duplicate section id: {duplicate}");
    }

    foreach (var section in book.Sections)
    {
        foreach (var choice in section.Choices)
        {
            if (!sectionIds.Contains(choice.TargetId))
            {
                errors.Add($"Missing target section: {section.Id} -> {choice.TargetId}");
            }
        }
    }

    return errors;
}

static string? GetSectionId(IElement heading)
{
    var anchor = heading.QuerySelector("a[name]")?.GetAttribute("name")?.Trim();
    if (string.IsNullOrWhiteSpace(anchor))
    {
        return null;
    }

    if (!anchor.StartsWith("sect", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return anchor["sect".Length..];
}
