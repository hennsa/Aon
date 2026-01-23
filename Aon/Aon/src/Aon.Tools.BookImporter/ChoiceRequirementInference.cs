using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace Aon.Tools.BookImporter;

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
