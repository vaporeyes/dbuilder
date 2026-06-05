// ABOUTME: Formats UDB-style status bar labels for the editor shell.
// ABOUTME: Keeps config, mode, and snap-grid text rules testable outside Avalonia.

using System.Globalization;

namespace DBuilder.IO;

public static class StatusBarModel
{
    public static string ConfigLabel(string configName, string? gameName)
        => string.IsNullOrWhiteSpace(gameName)
            ? configName
            : $"{gameName} ({configName})";

    public static string ConfigText(string configName, string? gameName)
        => "Config: " + ConfigLabel(configName, gameName);

    public static string ModeText(
        string editMode,
        bool in3DMode = false,
        bool automapMode = false,
        bool wadAuthorMode = false,
        bool imageExampleMode = false,
        bool drawMode = false)
    {
        if (in3DMode) return "Mode: 3D";
        if (automapMode) return "Mode: Automap";
        if (wadAuthorMode) return "Mode: WadAuthor";
        if (imageExampleMode) return "Mode: Image Example";
        return drawMode ? $"Mode: {editMode} (draw)" : $"Mode: {editMode}";
    }

    public static string GridText(bool snapToGrid, double gridSize)
    {
        string formatted = gridSize % 1.0 == 0.0
            ? ((int)Math.Round(gridSize)).ToString(CultureInfo.InvariantCulture)
            : gridSize.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{(snapToGrid ? "Snap" : "Free")}: {formatted}";
    }

    public static string SelectionText(int selectedCount)
        => $"Selected: {selectedCount}";

    public static string CoordinateText(double x, double y)
        => $"{x.ToString("0", CultureInfo.InvariantCulture)} , {y.ToString("0", CultureInfo.InvariantCulture)}";
}
