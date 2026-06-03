// ABOUTME: Shapes discovered map entries for the Open Map picker without changing load order.
// ABOUTME: Keeps UDB-style map chooser ordering testable outside the Avalonia dialog.

namespace DBuilder.IO;

public static class MapPickerModel
{
    public static List<MapEntry> SortForPicker(IEnumerable<MapEntry> maps)
        => maps
            .OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(map => map.Format)
            .ToList();
}
