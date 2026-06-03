// ABOUTME: Provides UDB-style helpers for filtering selected things by thing type.
// ABOUTME: Keeps selection filtering testable without depending on editor dialogs.

using System.Globalization;

namespace DBuilder.Map;

public static class ThingSelectionFilter
{
    public static IReadOnlyList<int> SelectedTypes(MapSet map)
    {
        return map.GetSelectedThings()
            .Select(thing => thing.Type)
            .Distinct()
            .OrderBy(type => type)
            .ToList();
    }

    public static int KeepSelectedTypes(MapSet map, IEnumerable<int> allowedTypes)
    {
        var allowed = new HashSet<int>(allowedTypes);
        int kept = 0;
        foreach (Thing thing in map.GetSelectedThings())
        {
            if (allowed.Contains(thing.Type))
            {
                kept++;
            }
            else
            {
                thing.Selected = false;
            }
        }

        return kept;
    }

    public static string FilterStatusText(int selectedThingCount)
        => $"Filtered selected things: {CountLabel(selectedThingCount, "thing")} {(selectedThingCount == 1 ? "remains" : "remain")} selected.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";
}
