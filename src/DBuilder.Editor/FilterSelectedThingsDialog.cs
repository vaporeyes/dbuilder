// ABOUTME: Modal dialog for UDB-style filtering of the current thing selection by type.
// ABOUTME: Lists selected thing types and returns the checked type numbers to keep selected.

using Avalonia.Controls;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class FilterSelectedThingsDialog : PropertyDialog
{
    private readonly Dictionary<int, CheckBox> _checks = new();

    public IReadOnlyList<int> SelectedTypes { get; private set; } = [];

    public FilterSelectedThingsDialog(IReadOnlyList<int> selectedTypes, GameConfiguration? config)
        : this(selectedTypes.Distinct().ToDictionary(type => type, _ => 0), config)
    {
    }

    public FilterSelectedThingsDialog(IReadOnlyDictionary<int, int> selectedTypeCounts, GameConfiguration? config)
        : base("Filter Selected Things", "Choose thing types to leave selected.")
    {
        foreach (var pair in selectedTypeCounts.OrderBy(pair => pair.Key))
        {
            _checks[pair.Key] = AddCheckBox(Label(pair.Key, pair.Value, config), initial: true);
        }
    }

    protected override void OnConfirm()
    {
        SelectedTypes = _checks
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .OrderBy(type => type)
            .ToList();
    }

    private static string Label(int type, int count, GameConfiguration? config)
    {
        string countText = count > 0 ? " (" + count + ")" : "";
        if (config?.Things.TryGetValue(type, out ThingTypeInfo? info) == true && !string.IsNullOrWhiteSpace(info.Title))
            return type + " - " + info.Title + countText;

        return type + " - Unknown thing" + countText;
    }
}
