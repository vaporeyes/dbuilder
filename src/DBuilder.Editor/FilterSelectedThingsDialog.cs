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
        : base("Filter Selected Things", "Choose thing types to leave selected.")
    {
        foreach (int type in selectedTypes)
        {
            _checks[type] = AddCheckBox(Label(type, config), initial: true);
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

    private static string Label(int type, GameConfiguration? config)
    {
        if (config?.Things.TryGetValue(type, out ThingTypeInfo? info) == true && !string.IsNullOrWhiteSpace(info.Title))
            return type + " - " + info.Title;

        return type + " - Unknown thing";
    }
}
