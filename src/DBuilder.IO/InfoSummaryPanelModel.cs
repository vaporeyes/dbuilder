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
        => $"Map: {map.Vertices.Count.ToString(CultureInfo.InvariantCulture)} vertices, {map.Linedefs.Count.ToString(CultureInfo.InvariantCulture)} linedefs, {map.Sectors.Count.ToString(CultureInfo.InvariantCulture)} sectors, {map.Things.Count.ToString(CultureInfo.InvariantCulture)} things." +
           $"   Config: {configName}.   Mode: {editMode}.   {modeHints}.   {toggle3DHint}.   See Help > Shortcuts for all controls.";

    public static string SelectionSummaryText(
        InfoPanelSelectionCounts counts,
        string? nextUndoDescription = null,
        string? nextRedoDescription = null)
    {
        string text = $"Selected: {counts.Vertices.ToString(CultureInfo.InvariantCulture)} vertices, {counts.Linedefs.ToString(CultureInfo.InvariantCulture)} linedefs, {counts.Sidedefs.ToString(CultureInfo.InvariantCulture)} sidedefs, {counts.Sectors.ToString(CultureInfo.InvariantCulture)} sectors, {counts.Things.ToString(CultureInfo.InvariantCulture)} things.";
        if (nextUndoDescription is null && nextRedoDescription is null) return text;

        string undo = string.IsNullOrWhiteSpace(nextUndoDescription) ? "-" : nextUndoDescription;
        string redo = string.IsNullOrWhiteSpace(nextRedoDescription) ? "-" : nextRedoDescription;
        return text + $"   Undo: {undo}  Redo: {redo}";
    }
}
