using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aon.Rules;

public interface IRuleCatalog
{
    IReadOnlyList<RuleDefinition> Resolve(IEnumerable<string> ruleIds);
}

public sealed class RuleCatalog : IRuleCatalog
{
    private const string DefaultCatalogFileName = "RulesCatalog.json";
    private readonly IReadOnlyDictionary<string, RuleDefinition> _rules;

    public RuleCatalog(IEnumerable<RuleDefinition> rules)
    {
        _rules = rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id))
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public static RuleCatalog LoadDefault()
    {
        return LoadFromResource(DefaultCatalogFileName);
    }

    public static RuleCatalog Load(string? pathOrSeriesKey)
    {
        if (string.IsNullOrWhiteSpace(pathOrSeriesKey))
        {
            return LoadDefault();
        }

        var trimmed = pathOrSeriesKey.Trim();
        if (File.Exists(trimmed))
        {
            using var fileStream = File.OpenRead(trimmed);
            return LoadFromStream(fileStream);
        }

        var resourceFileName = ResolveCatalogFileName(trimmed) ?? trimmed;
        return LoadFromResource(resourceFileName);
    }

    public IReadOnlyList<RuleDefinition> Resolve(IEnumerable<string> ruleIds)
    {
        if (ruleIds is null)
        {
            return Array.Empty<RuleDefinition>();
        }

        var resolved = new List<RuleDefinition>();
        foreach (var ruleId in ruleIds)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                continue;
            }

            if (_rules.TryGetValue(ruleId.Trim(), out var rule))
            {
                resolved.Add(rule);
            }
        }

        return resolved;
    }

    private static RuleCatalog LoadFromResource(string resourceFileName)
    {
        var assembly = typeof(RuleCatalog).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return new RuleCatalog(Array.Empty<RuleDefinition>());
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new RuleCatalog(Array.Empty<RuleDefinition>());
        }

        return LoadFromStream(stream);
    }

    private static RuleCatalog LoadFromStream(Stream stream)
    {
        var document = JsonSerializer.Deserialize(
            stream,
            RuleCatalogDocumentContext.Default.RuleCatalogDocument);

        return new RuleCatalog(document?.Rules?.ToArray() ?? Array.Empty<RuleDefinition>());
    }

    private static string? ResolveCatalogFileName(string seriesKey)
    {
        if (string.IsNullOrWhiteSpace(seriesKey))
        {
            return null;
        }

        if (seriesKey.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return seriesKey;
        }

        var normalized = seriesKey
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        return normalized switch
        {
            "lw" => "RulesCatalog.LoneWolf.json",
            "lonewolf" => "RulesCatalog.LoneWolf.json",
            "gs" => "RulesCatalog.GreyStar.json",
            "greystar" => "RulesCatalog.GreyStar.json",
            "fw" => "RulesCatalog.FreewayWarrior.json",
            "freewaywarrior" => "RulesCatalog.FreewayWarrior.json",
            "default" => DefaultCatalogFileName,
            _ => null
        };
    }
}

public sealed record RuleCatalogDocument
{
    public List<RuleDefinition> Rules { get; init; } = new();
}

[JsonSerializable(typeof(RuleCatalogDocument))]
public sealed partial class RuleCatalogDocumentContext : JsonSerializerContext
{
}
