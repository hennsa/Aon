using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aon.Rules;

public sealed class CombatTable
{
    private const string DefaultTableFileName = "CombatTable.json";
    private readonly IReadOnlyList<CombatTableRatioBand> _ratios;

    public CombatTable(IEnumerable<CombatTableRatioBand> ratios)
    {
        _ratios = ratios
            ?.OrderBy(ratio => ratio.Min)
            .ToArray()
            ?? Array.Empty<CombatTableRatioBand>();
    }

    public static CombatTable LoadDefault()
    {
        return LoadFromResource(DefaultTableFileName);
    }

    public static CombatTable Load(string? pathOrSeriesKey)
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

        var resourceFileName = ResolveTableFileName(trimmed) ?? trimmed;
        return LoadFromResource(resourceFileName);
    }

    public CombatTableOutcome Resolve(int combatRatio, int roll)
    {
        if (_ratios.Count == 0)
        {
            return new CombatTableOutcome(0, 0);
        }

        var match = _ratios.FirstOrDefault(ratio => combatRatio >= ratio.Min && combatRatio <= ratio.Max);
        if (match is null)
        {
            match = combatRatio < _ratios[0].Min ? _ratios[0] : _ratios[^1];
        }

        return match.Resolve(roll);
    }

    private static CombatTable LoadFromResource(string resourceFileName)
    {
        var assembly = typeof(CombatTable).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return new CombatTable(Array.Empty<CombatTableRatioBand>());
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new CombatTable(Array.Empty<CombatTableRatioBand>());
        }

        return LoadFromStream(stream);
    }

    private static CombatTable LoadFromStream(Stream stream)
    {
        var document = JsonSerializer.Deserialize(
            stream,
            CombatTableDocumentContext.Default.CombatTableDocument);

        return new CombatTable(document?.Ratios?.ToArray() ?? Array.Empty<CombatTableRatioBand>());
    }

    private static string? ResolveTableFileName(string seriesKey)
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
            "lw" => "CombatTable.LoneWolf.json",
            "lonewolf" => "CombatTable.LoneWolf.json",
            "gs" => "CombatTable.GreyStar.json",
            "greystar" => "CombatTable.GreyStar.json",
            "fw" => "CombatTable.FreewayWarrior.json",
            "freewaywarrior" => "CombatTable.FreewayWarrior.json",
            "default" => DefaultTableFileName,
            _ => null
        };
    }
}

public sealed record CombatTableOutcome(int PlayerLoss, int EnemyLoss);

public sealed record CombatTableRoll
{
    public int Roll { get; init; }
    public int PlayerLoss { get; init; }
    public int EnemyLoss { get; init; }
}

public sealed record CombatTableRatioBand
{
    public int Min { get; init; }
    public int Max { get; init; }
    public List<CombatTableRoll> Rolls { get; init; } = new();

    public CombatTableOutcome Resolve(int roll)
    {
        if (Rolls.Count == 0)
        {
            return new CombatTableOutcome(0, 0);
        }

        var match = Rolls.FirstOrDefault(entry => entry.Roll == roll) ?? Rolls[0];
        return new CombatTableOutcome(match.PlayerLoss, match.EnemyLoss);
    }
}

public sealed record CombatTableDocument
{
    public List<CombatTableRatioBand> Ratios { get; init; } = new();
}

[JsonSerializable(typeof(CombatTableDocument))]
public sealed partial class CombatTableDocumentContext : JsonSerializerContext
{
}
