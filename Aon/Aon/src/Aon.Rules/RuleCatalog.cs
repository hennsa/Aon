using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aon.Rules;

public interface IRuleCatalog
{
    IReadOnlyList<RuleDefinition> Resolve(IEnumerable<string> ruleIds);
}

public sealed class RuleCatalog : IRuleCatalog
{
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
        var assembly = typeof(RuleCatalog).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("RulesCatalog.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return new RuleCatalog(Array.Empty<RuleDefinition>());
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new RuleCatalog(Array.Empty<RuleDefinition>());
        }

        var document = JsonSerializer.Deserialize(
            stream,
            RuleCatalogDocumentContext.Default.RuleCatalogDocument);

        // Fix: Convert List<RuleDefinition> to array for constructor
        return new RuleCatalog(document?.Rules?.ToArray() ?? Array.Empty<RuleDefinition>());
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
}

public sealed record RuleCatalogDocument
{
    public List<RuleDefinition> Rules { get; init; } = new();
}

[JsonSerializable(typeof(RuleCatalogDocument))]
public sealed partial class RuleCatalogDocumentContext : JsonSerializerContext
{
}
