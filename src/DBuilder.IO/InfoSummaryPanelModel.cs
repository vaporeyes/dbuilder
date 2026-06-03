// ABOUTME: Builds text summaries for the editor info panel when no single element is selected.
// ABOUTME: Keeps map overview and selection summary wording testable outside Avalonia controls.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed record InfoPanelSelectionCounts(int Vertices, int Linedefs, int Sidedefs, int Sectors, int Things)
{
    public int Total => Vertices + Linedefs + Sidedefs + Sectors + Things;
}

public static class InfoSummaryPanelModel
{
    public static string NoMapLoadedText() => "No map loaded.";

    public static InfoPanelSelectionCounts SelectionCounts(MapSet map)
        => new(
            map.SelectedVerticesCount,
            map.SelectedLinedefsCount,
            map.SelectedSidedefsCount,
            map.SelectedSectorsCount,
            map.SelectedThingsCount);

    public static string MapOverviewText(
        MapSet map,
        string configName,
        string editMode,
        string modeHints,
        string toggle3DHint)
        => $"Map: {CountLabel(map.Vertices.Count, "vertex", "vertices")}, {CountLabel(map.Linedefs.Count, "linedef")}, {CountLabel(map.Sectors.Count, "sector")}, {CountLabel(map.Things.Count, "thing")}." +
           $"   Config: {configName}.   Mode: {editMode}.   {modeHints}.   {toggle3DHint}.   See Help > Shortcuts for all controls.";

    public static string SelectionSummaryText(
        InfoPanelSelectionCounts counts,
        string? nextUndoDescription = null,
        string? nextRedoDescription = null)
    {
        string text = $"Selected: {CountLabel(counts.Vertices, "vertex", "vertices")}, {CountLabel(counts.Linedefs, "linedef")}, {CountLabel(counts.Sidedefs, "sidedef")}, {CountLabel(counts.Sectors, "sector")}, {CountLabel(counts.Things, "thing")}.";
        if (nextUndoDescription is null && nextRedoDescription is null) return text;

        string undo = string.IsNullOrWhiteSpace(nextUndoDescription) ? "-" : nextUndoDescription;
        string redo = string.IsNullOrWhiteSpace(nextRedoDescription) ? "-" : nextRedoDescription;
        return text + $"   Undo: {undo}  Redo: {redo}";
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";
}
