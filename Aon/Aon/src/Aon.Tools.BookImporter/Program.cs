using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp;
using AngleSharp.Dom;
using Aon.Content;
using Json.Schema;
using System.Text.RegularExpressions;


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
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
var schemaPath = FindBookSchemaPath(Environment.CurrentDirectory);
JsonSchema? bookSchema = null;
if (schemaPath is null)
{
    Console.Error.WriteLine("Warning: Unable to locate Book.schema.json for validation.");
}
else
{
    var schemaText = await File.ReadAllTextAsync(schemaPath);
    bookSchema = JsonSchema.FromText(schemaText);
}
var importedCount = 0;
var hadValidationErrors = false;

foreach (var file in Directory.EnumerateFiles(inputDirectory, "*.htm", SearchOption.AllDirectories))
{
    var html = await File.ReadAllTextAsync(file);
    var document = await context.OpenAsync(request => request.Content(html));
    var bookId = BuildBookId(inputDirectory, file);
    var book = ExtractBook(document, bookId);
    var validationErrors = ValidateBook(book);
    validationErrors.AddRange(ValidateBookSchema(book, bookSchema, jsonOptions));

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

    var seriesId = GetSeriesId(fallbackId);
    var frontMatterSections = ExtractFrontMatter(document);
    var sections = ExtractSections(document);

    return new Book
    {
        Id = fallbackId,
        Title = ReplaceCharacterTokens(seriesId, title ?? fallbackId),
        FrontMatter = frontMatterSections
            .Select(section => new FrontMatterSection
            {
                Id = section.Id,
                Title = ReplaceCharacterTokens(seriesId, section.Title),
                Html = ReplaceCharacterTokens(seriesId, section.Html)
            })
            .ToList(),
        Sections = sections
            .Select(section => new BookSection
            {
                Id = section.Id,
                Title = ReplaceCharacterTokens(seriesId, section.Title),
                Blocks = section.Blocks
                    .Select(block => new ContentBlock
                    {
                        Kind = block.Kind,
                        Text = ReplaceCharacterTokens(seriesId, block.Text)
                    })
                    .ToList(),
                Choices = section.Choices
                    .Select(choice => new Choice
                    {
                        Text = ReplaceCharacterTokens(seriesId, choice.Text),
                        TargetId = choice.TargetId,
                        Requirements = choice.Requirements.ToList(),
                        Effects = choice.Effects.ToList(),
                        RandomOutcomes = choice.RandomOutcomes
                            .Select(outcome => new RandomOutcome
                            {
                                Min = outcome.Min,
                                Max = outcome.Max,
                                TargetId = outcome.TargetId,
                                Effects = outcome.Effects.ToList()
                            })
                            .ToList(),
                        RuleIds = choice.RuleIds.ToList()
                    })
                    .ToList()
            })
            .ToList()
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
        if (node is IElement elem && elem.ClassList.Contains("choice"))
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

    foreach (var el in nodes.OfType<IElement>())
    {
        if (!el.ClassList.Contains("choice"))
        {
            continue;
        }

        var targetId = el.QuerySelectorAll("a[href]")
            .Select(anchor => GetChoiceTargetId(anchor.GetAttribute("href") ?? string.Empty))
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        if (string.IsNullOrWhiteSpace(targetId))
        {
            continue;
        }

        var metadata = ExtractChoiceMetadata(el);

        choices.Add(new Choice
        {
            Text = el.TextContent?.Trim() ?? string.Empty,
            TargetId = targetId,
            Requirements = metadata.Requirements,
            Effects = metadata.Effects,
            RandomOutcomes = metadata.RandomOutcomes,
            RuleIds = metadata.RuleIds
        });
    }

    return choices;
}

static ChoiceRuleMetadata ExtractChoiceMetadata(IElement element)
{
    var metadata = new ChoiceRuleMetadata();

    var rulesJson = element.GetAttribute("data-rules");
    if (!string.IsNullOrWhiteSpace(rulesJson))
    {
        var parsed = DeserializeChoiceMetadata(rulesJson);
        if (parsed is not null)
        {
            metadata.Merge(parsed);
        }
    }

    var requirements = ParseDelimitedList(element.GetAttribute("data-requirements"));
    var effects = ParseDelimitedList(element.GetAttribute("data-effects"));
    var ruleIds = ParseDelimitedList(element.GetAttribute("data-rule-ids"));
    var randomOutcomes = ParseRandomOutcomes(element.GetAttribute("data-random-outcomes"));

    metadata.AddRequirements(requirements);
    metadata.AddEffects(effects);
    metadata.AddRuleIds(ruleIds);
    metadata.AddRandomOutcomes(randomOutcomes);
    if (metadata.Requirements.Count == 0)
    {
        metadata.AddRequirements(ChoiceRequirementInference.InferChoiceRequirements(element));
    }

    return metadata;
}

static IEnumerable<string> InferChoiceRequirements(IElement element)
{
    var text = element.TextContent?.Trim() ?? string.Empty;
    if (!StartsWithPossessClause(text))
    {
        return Array.Empty<string>();
    }

    var emphasizedItem = ExtractEmphasizedItem(element);
    if (!string.IsNullOrWhiteSpace(emphasizedItem))
    {
        return new[] { $"item:{emphasizedItem}" };
    }

    if (ContainsSkillOrRankLanguage(text))
    {
        return Array.Empty<string>();
    }

    var itemName = ExtractItemNameFromPossessClause(text);
    if (string.IsNullOrWhiteSpace(itemName))
    {
        return Array.Empty<string>();
    }

    return new[] { $"item:{itemName}" };
}

static bool StartsWithPossessClause(string text)
{
    return text.StartsWith("If you possess", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("If you have", StringComparison.OrdinalIgnoreCase);
}

static string? ExtractEmphasizedItem(IElement element)
{
    var emphasized = element.QuerySelector("strong, b, em, i");
    return emphasized?.TextContent?.Trim();
}

static bool ContainsSkillOrRankLanguage(string text)
{
    return text.Contains("discipline", StringComparison.OrdinalIgnoreCase)
        || text.Contains("skill", StringComparison.OrdinalIgnoreCase)
        || text.Contains("rank", StringComparison.OrdinalIgnoreCase);
}

static string? ExtractItemNameFromPossessClause(string text)
{
    var match = PossessClauseRegex.Match(text);
    if (!match.Success)
    {
        return null;
    }

    var item = match.Groups["item"].Value.Trim().TrimEnd('.');
    if (string.IsNullOrWhiteSpace(item))
    {
        return null;
    }

    if (item.Contains(" or ", StringComparison.OrdinalIgnoreCase)
        || item.Contains(" and ", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return item;
}

static readonly Regex PossessClauseRegex = new(
    @"^\s*If you (?:possess|have)(?:\s+an|\s+a|\s+the)?\s+(?<item>[^,.]+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

static ChoiceRuleMetadata? DeserializeChoiceMetadata(string json)
{
    try
    {
        return JsonSerializer.Deserialize<ChoiceRuleMetadata>(json, MetadataOptions.Options);
    }
    catch (JsonException)
    {
        Console.Error.WriteLine("Warning: Unable to parse choice rule metadata JSON.");
        return null;
    }
}

static class ChoiceRequirementInference
{
    private static readonly Regex PossessClauseRegex = new(
        @"^\s*If you (?:possess|have)(?:\s+an|\s+a|\s+the)?\s+(?<item>[^,.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IEnumerable<string> InferChoiceRequirements(IElement element)
    {
        var text = element.TextContent?.Trim() ?? string.Empty;
        if (!StartsWithPossessClause(text))
        {
            return Array.Empty<string>();
        }

        var emphasizedItem = ExtractEmphasizedItem(element);
        if (!string.IsNullOrWhiteSpace(emphasizedItem))
        {
            return new[] { $"item:{emphasizedItem}" };
        }

        if (ContainsSkillOrRankLanguage(text))
        {
            return Array.Empty<string>();
        }

        var itemName = ExtractItemNameFromPossessClause(text);
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return Array.Empty<string>();
        }

        return new[] { $"item:{itemName}" };
    }

    private static bool StartsWithPossessClause(string text)
    {
        return text.StartsWith("If you possess", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("If you have", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractEmphasizedItem(IElement element)
    {
        var emphasized = element.QuerySelector("strong, b, em, i");
        return emphasized?.TextContent?.Trim();
    }

    private static bool ContainsSkillOrRankLanguage(string text)
    {
        return text.Contains("discipline", StringComparison.OrdinalIgnoreCase)
            || text.Contains("skill", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rank", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractItemNameFromPossessClause(string text)
    {
        var match = PossessClauseRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var item = match.Groups["item"].Value.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(item))
        {
            return null;
        }

        if (item.Contains(" or ", StringComparison.OrdinalIgnoreCase)
            || item.Contains(" and ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return item;
    }
}

static List<string> ParseDelimitedList(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return new List<string>();
    }

    return value
        .Split(new[] { '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .ToList();
}

static List<RandomOutcome> ParseRandomOutcomes(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return new List<RandomOutcome>();
    }

    try
    {
        return JsonSerializer.Deserialize<List<RandomOutcome>>(value, MetadataOptions.Options) ?? new List<RandomOutcome>();
    }
    catch (JsonException)
    {
        Console.Error.WriteLine("Warning: Unable to parse choice random outcomes JSON.");
        return new List<RandomOutcome>();
    }
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

static List<string> ValidateBookSchema(Book book, JsonSchema? schema, JsonSerializerOptions options)
{
    var errors = new List<string>();
    if (schema is null)
    {
        return errors;
    }

    JsonNode? instance = JsonSerializer.SerializeToNode(book, options);
    if (instance is null)
    {
        errors.Add("Schema validation failed: unable to serialize book.");
        return errors;
    }

    var results = schema.Evaluate(instance, new EvaluationOptions());

    if (!results.IsValid)
    {
        errors.Add("Schema validation failed: output does not conform to Book.schema.json.");
    }

    return errors;
}

static string? FindBookSchemaPath(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);
    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "Aon.Content", "Book.schema.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}

static string? GetSectionId(IElement heading)
{
    var anchor = heading.QuerySelector("a[name]")?.GetAttribute("name")?.Trim();
    if (!string.IsNullOrWhiteSpace(anchor) && anchor.StartsWith("sect", StringComparison.OrdinalIgnoreCase))
    {
        return anchor["sect".Length..];
    }

    var headingText = heading.TextContent?.Trim();
    if (string.IsNullOrWhiteSpace(headingText))
    {
        return null;
    }

    return headingText.All(char.IsDigit) ? headingText : null;
}

static string? GetChoiceTargetId(string href)
{
    if (string.IsNullOrWhiteSpace(href))
    {
        return null;
    }

    var trimmed = href.Trim();
    if (trimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase))
    {
        return GetFragmentTargetId(trimmed);
    }

    var fragmentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
    if (fragmentIndex >= 0)
    {
        var fragmentId = GetFragmentTargetId(trimmed[fragmentIndex..]);
        if (!string.IsNullOrWhiteSpace(fragmentId))
        {
            return fragmentId;
        }

        trimmed = trimmed[..fragmentIndex];
    }

    var fileName = GetFileName(trimmed);
    if (fileName.StartsWith("sect", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
    {
        var id = fileName["sect".Length..^".htm".Length];
        return IsDigitsOnly(id) ? id : null;
    }

    if (fileName.StartsWith("sect", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
    {
        var id = fileName["sect".Length..^".html".Length];
        return IsDigitsOnly(id) ? id : null;
    }

    return null;
}

static string? GetFragmentTargetId(string fragment)
{
    if (!fragment.StartsWith("#sect", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var id = fragment["#sect".Length..];
    return IsDigitsOnly(id) ? id : null;
}

static string GetFileName(string value)
{
    var lastSlash = value.LastIndexOfAny(new[] { '/', '\\' });
    return lastSlash >= 0 ? value[(lastSlash + 1)..] : value;
}

static bool IsDigitsOnly(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    return value.All(char.IsDigit);
}

static string GetSeriesId(string bookId)
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

static string ReplaceCharacterTokens(string seriesId, string text)
{
    return CharacterTokenization.Replace(seriesId, text);
}

static class CharacterTokenization
{
    private const string CharacterNameToken = "{{characterName}}";
    private static readonly Regex LoneWolfNameRegex = new(@"\bLone\s+Wolf\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GreyStarNameRegex = new(@"\bGrey\s+Star\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalPhoenixNameRegex = new(@"\bCal\s+Phoenix\b", RegexOptions.Compiled);
    private static readonly Regex CalNameRegex = new(@"\bCal\b", RegexOptions.Compiled);

    public static string Replace(string seriesId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return seriesId switch
        {
            "lw" => LoneWolfNameRegex.Replace(text, CharacterNameToken),
            "gs" => GreyStarNameRegex.Replace(text, CharacterNameToken),
            "fw" => CalNameRegex.Replace(CalPhoenixNameRegex.Replace(text, CharacterNameToken), CharacterNameToken),
            _ => text
        };
    }
}

sealed class ChoiceRuleMetadata
{
    public List<string> Requirements { get; } = new();
    public List<string> Effects { get; } = new();
    public List<RandomOutcome> RandomOutcomes { get; } = new();
    public List<string> RuleIds { get; } = new();

    public void Merge(ChoiceRuleMetadata other)
    {
        AddRequirements(other.Requirements);
        AddEffects(other.Effects);
        AddRandomOutcomes(other.RandomOutcomes);
        AddRuleIds(other.RuleIds);
    }

    public void AddRequirements(IEnumerable<string> values) => AddUnique(Requirements, values);
    public void AddEffects(IEnumerable<string> values) => AddUnique(Effects, values);
    public void AddRuleIds(IEnumerable<string> values) => AddUnique(RuleIds, values);

    public void AddRandomOutcomes(IEnumerable<RandomOutcome> values)
    {
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            var hasTarget = !string.IsNullOrWhiteSpace(value.TargetId);
            var hasEffects = value.Effects.Any(effect => !string.IsNullOrWhiteSpace(effect));
            if (!hasTarget && !hasEffects)
            {
                continue;
            }

            RandomOutcomes.Add(new RandomOutcome
            {
                Min = value.Min,
                Max = value.Max,
                TargetId = value.TargetId?.Trim() ?? string.Empty,
                Effects = value.Effects.Where(effect => !string.IsNullOrWhiteSpace(effect)).ToList()
            });
        }
    }

    private static void AddUnique(List<string> target, IEnumerable<string> values)
    {
        var seen = new HashSet<string>(target, StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                target.Add(trimmed);
            }
        }
    }
}

static class ChoiceRequirementInference
{
    private static readonly Regex PossessClauseRegex = new(
        @"^\s*If you (?:possess|have)(?:\s+an|\s+a|\s+the)?\s+(?<item>[^,.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IEnumerable<string> InferChoiceRequirements(IElement element)
    {
        var text = element.TextContent?.Trim() ?? string.Empty;
        if (!StartsWithPossessClause(text))
        {
            return Array.Empty<string>();
        }

        var emphasizedItem = ExtractEmphasizedItem(element);
        if (!string.IsNullOrWhiteSpace(emphasizedItem))
        {
            return new[] { $"item:{emphasizedItem}" };
        }

        if (ContainsSkillOrRankLanguage(text))
        {
            return Array.Empty<string>();
        }

        var itemName = ExtractItemNameFromPossessClause(text);
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return Array.Empty<string>();
        }

        return new[] { $"item:{itemName}" };
    }

    private static bool StartsWithPossessClause(string text)
    {
        return text.StartsWith("If you possess", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("If you have", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractEmphasizedItem(IElement element)
    {
        var emphasized = element.QuerySelector("strong, b, em, i");
        return emphasized?.TextContent?.Trim();
    }

    private static bool ContainsSkillOrRankLanguage(string text)
    {
        return text.Contains("discipline", StringComparison.OrdinalIgnoreCase)
            || text.Contains("skill", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rank", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractItemNameFromPossessClause(string text)
    {
        var match = PossessClauseRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var item = match.Groups["item"].Value.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(item))
        {
            return null;
        }

        if (item.Contains(" or ", StringComparison.OrdinalIgnoreCase)
            || item.Contains(" and ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return item;
    }
}

static class MetadataOptions
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}
