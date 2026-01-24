using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp;
using AngleSharp.Dom;
using Aon.Content;
using Aon.Rules;
using Json.Schema;
using System.Text.RegularExpressions;

namespace Aon.Tools.BookImporter;

public sealed record BookImportProgress(int ProcessedCount, int TotalCount, string CurrentFile);

public sealed record BookImportIssue(string FilePath, string Message);

public sealed class BookImportResult
{
    public int ImportedCount { get; init; }
    public IReadOnlyList<BookImportIssue> ValidationErrors { get; init; } = Array.Empty<BookImportIssue>();
    public IReadOnlyList<BookImportIssue> MetadataWarnings { get; init; } = Array.Empty<BookImportIssue>();
    public IReadOnlyList<string> GeneralWarnings { get; init; } = Array.Empty<string>();
}

public sealed class BookImportService
{
    public async Task<BookImportResult> ImportAsync(
        string inputDirectory,
        string outputDirectory,
        IProgress<BookImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory))
        {
            throw new ArgumentException("Input directory is required.", nameof(inputDirectory));
        }

        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(inputDirectory, "*.htm", SearchOption.AllDirectories).ToList();
        var totalCount = files.Count;
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var schemaPath = FindBookSchemaPath(Environment.CurrentDirectory);
        JsonSchema? bookSchema = null;
        var generalWarnings = new List<string>();
        if (schemaPath is null)
        {
            generalWarnings.Add("Warning: Unable to locate Book.schema.json for validation.");
        }
        else
        {
            var schemaText = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            bookSchema = JsonSchema.FromText(schemaText);
        }

        var importedCount = 0;
        var validationErrors = new List<BookImportIssue>();
        var metadataWarnings = new List<BookImportIssue>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new BookImportProgress(importedCount, totalCount, file));

            var html = await File.ReadAllTextAsync(file, cancellationToken);
            var document = await context.OpenAsync(request => request.Content(html), cancellationToken);
            var bookId = BuildBookId(inputDirectory, file);
            var book = ExtractBook(document, bookId);
            var bookValidationErrors = ValidateBook(book);
            bookValidationErrors.AddRange(ValidateBookSchema(book, bookSchema, jsonOptions));
            var ruleCatalog = RuleCatalog.Load(book.SeriesId);
            var bookMetadataWarnings = RuleMetadataValidator.ValidateBook(book, ruleCatalog);

            foreach (var error in bookValidationErrors)
            {
                validationErrors.Add(new BookImportIssue(file, error));
            }

            foreach (var warning in bookMetadataWarnings)
            {
                metadataWarnings.Add(new BookImportIssue(file, warning));
            }

            var outputPath = Path.Combine(outputDirectory, $"{book.Id}.json");
            var json = JsonSerializer.Serialize(book, jsonOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            importedCount++;
            progress?.Report(new BookImportProgress(importedCount, totalCount, file));
        }

        return new BookImportResult
        {
            ImportedCount = importedCount,
            ValidationErrors = validationErrors,
            MetadataWarnings = metadataWarnings,
            GeneralWarnings = generalWarnings
        };
    }

    private static Book ExtractBook(IDocument document, string fallbackId)
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
            SeriesId = seriesId,
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

    private static string BuildBookId(string rootDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, filePath);
        var withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
        var segments = withoutExtension
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        return string.Join("__", segments);
    }

    private static List<FrontMatterSection> ExtractFrontMatter(IDocument document)
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

    private static List<BookSection> ExtractSections(IDocument document)
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

    private static List<ContentBlock> ExtractBlocks(IEnumerable<INode> nodes)
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

    private static List<Choice> ExtractChoices(IEnumerable<INode> nodes)
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

    private static ChoiceRuleMetadata ExtractChoiceMetadata(IElement element)
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

    private static ChoiceRuleMetadata? DeserializeChoiceMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ChoiceRuleMetadata>(json, MetadataOptions.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> ParseDelimitedList(string? value)
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

    private static List<RandomOutcome> ParseRandomOutcomes(string? value)
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
            return new List<RandomOutcome>();
        }
    }

    private static List<string> ValidateBook(Book book)
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

    private static List<string> ValidateBookSchema(Book book, JsonSchema? schema, JsonSerializerOptions options)
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

    private static string? FindBookSchemaPath(string startDirectory)
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

    private static string? GetSectionId(IElement heading)
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

    private static string? GetChoiceTargetId(string href)
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

    private static string? GetFragmentTargetId(string fragment)
    {
        if (!fragment.StartsWith("#sect", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = fragment["#sect".Length..];
        return IsDigitsOnly(id) ? id : null;
    }

    private static string GetFileName(string value)
    {
        var lastSlash = value.LastIndexOfAny(new[] { '/', '\\' });
        return lastSlash >= 0 ? value[(lastSlash + 1)..] : value;
    }

    private static bool IsDigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.All(char.IsDigit);
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

    private static string ReplaceCharacterTokens(string seriesId, string text)
    {
        return CharacterTokenization.Replace(seriesId, text);
    }

    private static class CharacterTokenization
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

    private sealed class ChoiceRuleMetadata
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

    private static class MetadataOptions
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
