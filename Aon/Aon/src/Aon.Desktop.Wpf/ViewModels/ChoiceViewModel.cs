using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed class ChoiceViewModel
{
    public ChoiceViewModel(
        string text,
        ICommand command,
        bool isEnabled,
        IEnumerable<string>? requirements = null,
        IEnumerable<string>? effects = null,
        IEnumerable<string>? ruleIds = null,
        IEnumerable<string>? validationWarnings = null)
    {
        Text = text;
        Command = command;
        IsEnabled = isEnabled;
        RequirementsDisplay = FormatList(requirements);
        EffectsDisplay = FormatList(effects);
        RuleIdsDisplay = FormatList(ruleIds);
        ValidationWarnings = (validationWarnings ?? Array.Empty<string>())
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .ToArray();
    }

    public string Text { get; }
    public ICommand Command { get; }
    public bool IsEnabled { get; }
    public string RequirementsDisplay { get; }
    public string EffectsDisplay { get; }
    public string RuleIdsDisplay { get; }
    public IReadOnlyList<string> ValidationWarnings { get; }
    public bool HasValidationWarnings => ValidationWarnings.Count > 0;

    private static string FormatList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return "None";
        }

        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        return items.Length == 0 ? "None" : string.Join(", ", items);
    }
}
