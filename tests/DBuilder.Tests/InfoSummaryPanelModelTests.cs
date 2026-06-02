// ABOUTME: Verifies text summaries used by the editor info panel outside single-element selection.
// ABOUTME: Covers no-map text, map overview details, selection counts, and undo/redo labels.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class InfoSummaryPanelModelTests
{
    [Fact]
    public void BuildsNoMapLoadedText()
    {
        Assert.Equal("No map loaded.", InfoSummaryPanelModel.NoMapLoadedText());
    }

    [Fact]
    public void BuildsMapOverviewText()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        map.AddSidedef(line, isFront: true, sector);
        map.AddThing(new Vector2D(8, 8), 3001);

        string text = InfoSummaryPanelModel.MapOverviewText(
            map,
            "Doom_Doom2Doom",
            "Linedefs",
            "1 / NumPad1 Vertices mode; 2 / NumPad2 Linedefs mode",
            "Tab Enter 3D mode");

        Assert.Equal(
            "Map: 2 vertices, 1 linedefs, 1 sectors, 1 things.   Config: Doom_Doom2Doom.   Mode: Linedefs.   1 / NumPad1 Vertices mode; 2 / NumPad2 Linedefs mode.   Tab Enter 3D mode.   See Help > Shortcuts for all controls.",
            text);
    }

    [Fact]
    public void CountsSelectionFromMap()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef side = map.AddSidedef(line, isFront: true, sector);
        Thing thing = map.AddThing(new Vector2D(8, 8), 3001);
        map.Vertices[0].Selected = true;
        line.Selected = true;
        side.Selected = true;
        sector.Selected = true;
        thing.Selected = true;

        InfoPanelSelectionCounts counts = InfoSummaryPanelModel.SelectionCounts(map);

        Assert.Equal(1, counts.Vertices);
        Assert.Equal(1, counts.Linedefs);
        Assert.Equal(1, counts.Sidedefs);
        Assert.Equal(1, counts.Sectors);
        Assert.Equal(1, counts.Things);
        Assert.Equal(5, counts.Total);
    }

    [Fact]
    public void BuildsSelectionSummaryWithoutUndoManager()
    {
        var counts = new InfoPanelSelectionCounts(1, 2, 3, 4, 5);

        string text = InfoSummaryPanelModel.SelectionSummaryText(counts);

        Assert.Equal("Selected: 1 vertices, 2 linedefs, 3 sidedefs, 4 sectors, 5 things.", text);
    }

    [Fact]
    public void BuildsSelectionSummaryWithUndoAndRedoLabels()
    {
        var counts = new InfoPanelSelectionCounts(0, 2, 0, 1, 0);

        string text = InfoSummaryPanelModel.SelectionSummaryText(counts, "Draw sector", "");

        Assert.Equal("Selected: 0 vertices, 2 linedefs, 0 sidedefs, 1 sectors, 0 things.   Undo: Draw sector  Redo: -", text);
    }
}
