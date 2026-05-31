// ABOUTME: Tests WadAuthorMode hover selection priority and side-based sector highlighting.
// ABOUTME: Covers the UDB classic mode hit-test order for vertices, things, linedefs, and sectors.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class WadAuthorModeModelTests
{
    [Fact]
    public void ModeDescriptorMatchesUdbEditModeAttribute()
    {
        WadAuthorModeDescriptor descriptor = WadAuthorModeModel.ModeDescriptor;

        Assert.Equal("WadAuthor Mode", descriptor.DisplayName);
        Assert.Equal("wadauthormode", descriptor.SwitchAction);
        Assert.Equal("WAuthor.png", descriptor.ButtonImage);
        Assert.Equal(int.MinValue + 400, descriptor.ButtonOrder);
        Assert.Equal("000_editing", descriptor.ButtonGroup);
        Assert.True(descriptor.UseByDefault);
        Assert.True(descriptor.SafeStartMode);
    }

    [Fact]
    public void LinedefPopupItemsMatchUdbOrder()
    {
        var items = WadAuthorModeModel.LinedefPopupItems;

        Assert.Equal(
            new[] { "Properties...", "", "Delete", "Split", "Flip", "Curve..." },
            items.Select(i => i.Title));
        Assert.Equal(
            new WadAuthorLinedefPopupAction?[]
            {
                WadAuthorLinedefPopupAction.Properties,
                null,
                WadAuthorLinedefPopupAction.Delete,
                WadAuthorLinedefPopupAction.Split,
                WadAuthorLinedefPopupAction.Flip,
                WadAuthorLinedefPopupAction.Curve,
            },
            items.Select(i => i.Action));
    }

    [Fact]
    public void LinedefPopupExecutableStateMatchesPortedEditorActions()
    {
        Assert.True(WadAuthorModeModel.CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction.Properties));
        Assert.True(WadAuthorModeModel.CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction.Delete));
        Assert.True(WadAuthorModeModel.CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction.Split));
        Assert.True(WadAuthorModeModel.CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction.Flip));
        Assert.False(WadAuthorModeModel.CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction.Curve));
    }

    [Fact]
    public void EnterModeConvertsSelectedSectorsToLinedefsLikeUdb()
    {
        var (map, sector, lines) = SquareSectorMap();
        sector.Selected = true;

        WadAuthorModeModel.EnterMode(map);

        Assert.False(sector.Selected);
        Assert.All(lines, line => Assert.True(line.Selected));
    }

    [Fact]
    public void LeaveModeClearsAllSelectedElementsLikeUdb()
    {
        var (map, sector, lines) = SquareSectorMap();
        Vertex vertex = map.Vertices[0];
        Thing thing = map.AddThing(new Vector2D(16, 16), 3001);
        vertex.Selected = true;
        lines[0].Selected = true;
        sector.Selected = true;
        thing.Selected = true;

        WadAuthorModeModel.LeaveMode(map);

        Assert.False(vertex.Selected);
        Assert.False(lines[0].Selected);
        Assert.False(sector.Selected);
        Assert.False(thing.Selected);
    }

    [Fact]
    public void LinedefPopupPropertiesSelectsOnlyTargetLine()
    {
        var map = EmptyLineMap();
        Linedef line = map.Linedefs[0];
        Vertex vertex = map.Vertices[0];
        vertex.Selected = true;

        WadAuthorLinedefPopupResult result = WadAuthorModeModel.ExecuteLinedefPopupAction(
            map,
            line,
            WadAuthorLinedefPopupAction.Properties,
            new Vector2D(50, 0));

        Assert.False(result.Changed);
        Assert.True(line.Selected);
        Assert.False(vertex.Selected);
        Assert.Equal("Edit linedef properties.", result.Status);
    }

    [Fact]
    public void LinedefPopupDeleteRemovesTargetLine()
    {
        var map = EmptyLineMap();
        Linedef line = map.Linedefs[0];

        WadAuthorLinedefPopupResult result = WadAuthorModeModel.ExecuteLinedefPopupAction(
            map,
            line,
            WadAuthorLinedefPopupAction.Delete,
            new Vector2D(50, 0));

        Assert.True(result.Changed);
        Assert.DoesNotContain(line, map.Linedefs);
        Assert.Equal("Deleted linedef.", result.Status);
    }

    [Fact]
    public void LinedefPopupSplitSplitsTargetLineAtCursorPosition()
    {
        var map = EmptyLineMap();
        Linedef line = map.Linedefs[0];

        WadAuthorLinedefPopupResult result = WadAuthorModeModel.ExecuteLinedefPopupAction(
            map,
            line,
            WadAuthorLinedefPopupAction.Split,
            new Vector2D(40, 0));

        Assert.True(result.Changed);
        Assert.Equal(2, map.Linedefs.Count);
        Assert.Equal(new Vector2D(40, 0), line.End.Position);
        Assert.Equal("Split linedef.", result.Status);
    }

    [Fact]
    public void LinedefPopupFlipReversesTargetLine()
    {
        var map = EmptyLineMap();
        Linedef line = map.Linedefs[0];
        Vertex start = line.Start;
        Vertex end = line.End;

        WadAuthorLinedefPopupResult result = WadAuthorModeModel.ExecuteLinedefPopupAction(
            map,
            line,
            WadAuthorLinedefPopupAction.Flip,
            new Vector2D(50, 0));

        Assert.True(result.Changed);
        Assert.Same(end, line.Start);
        Assert.Same(start, line.End);
        Assert.Equal("Flipped linedef.", result.Status);
    }

    private static MapSet EmptyLineMap()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        map.BuildIndexes();
        return map;
    }

    private static (MapSet Map, Sector Front, Sector Back, Linedef Line) TwoSidedLineMap()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var back = map.AddSector();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        map.BuildIndexes();
        return (map, front, back, line);
    }

    private static (MapSet Map, Sector Sector, Linedef[] Lines) SquareSectorMap()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex a = map.AddVertex(new Vector2D(0, 0));
        Vertex b = map.AddVertex(new Vector2D(64, 0));
        Vertex c = map.AddVertex(new Vector2D(64, 64));
        Vertex d = map.AddVertex(new Vector2D(0, 64));
        Linedef ab = map.AddLinedef(a, b);
        Linedef bc = map.AddLinedef(b, c);
        Linedef cd = map.AddLinedef(c, d);
        Linedef da = map.AddLinedef(d, a);
        map.AddSidedef(ab, true, sector);
        map.AddSidedef(bc, true, sector);
        map.AddSidedef(cd, true, sector);
        map.AddSidedef(da, true, sector);
        map.BuildIndexes();
        return (map, sector, new[] { ab, bc, cd, da });
    }

    [Fact]
    public void VertexBeatsThingWhenBothAreInRangeAndVertexIsCloser()
    {
        var map = new MapSet();
        Vertex vertex = map.AddVertex(new Vector2D(1, 0));
        map.AddThing(new Vector2D(3, 0), 1);

        WadAuthorHighlight highlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(0, 0));

        Assert.Equal(WadAuthorHighlightKind.Vertex, highlight.Kind);
        Assert.Same(vertex, highlight.Target);
    }

    [Fact]
    public void ThingBeatsVertexWhenBothAreInRangeAndThingIsCloser()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(6, 0));
        Thing thing = map.AddThing(new Vector2D(1, 0), 1);

        WadAuthorHighlight highlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(0, 0));

        Assert.Equal(WadAuthorHighlightKind.Thing, highlight.Kind);
        Assert.Same(thing, highlight.Target);
    }

    [Fact]
    public void LinedefBeatsSectorWhenMouseIsWithinLineRange()
    {
        var (map, _, _, line) = TwoSidedLineMap();

        WadAuthorHighlight highlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, 3));

        Assert.Equal(WadAuthorHighlightKind.Linedef, highlight.Kind);
        Assert.Same(line, highlight.Target);
    }

    [Fact]
    public void SectorComesFromNearestLineSideWhenLineIsOutOfRange()
    {
        var (map, front, back, line) = TwoSidedLineMap();

        WadAuthorHighlight frontHighlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, -12));
        WadAuthorHighlight backHighlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, 12));

        Assert.Equal(WadAuthorHighlightKind.Sector, frontHighlight.Kind);
        Assert.Same(front, frontHighlight.Target);
        Assert.Equal(WadAuthorHighlightKind.Sector, backHighlight.Kind);
        Assert.Same(back, backHighlight.Target);
        Assert.Same(front, WadAuthorModeModel.SectorFromNearestLineSide(line, new Vector2D(50, -12)));
        Assert.Same(back, WadAuthorModeModel.SectorFromNearestLineSide(line, new Vector2D(50, 12)));
    }

    [Fact]
    public void OneSidedLineOutOfRangeCanReturnNoSectorOnMissingSide()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        map.AddSidedef(line, true, front);
        map.BuildIndexes();

        WadAuthorHighlight frontHighlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, -12));
        WadAuthorHighlight missingBackHighlight = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, 12));

        Assert.Equal(WadAuthorHighlightKind.Sector, frontHighlight.Kind);
        Assert.Same(front, frontHighlight.Target);
        Assert.Equal(WadAuthorHighlightKind.None, missingBackHighlight.Kind);
        Assert.Null(missingBackHighlight.Target);
    }

    [Fact]
    public void RendererScaleShrinksHighlightRangesLikeUdb()
    {
        var map = EmptyLineMap();

        WadAuthorHighlight normal = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, 6), rendererScale: 1.0);
        WadAuthorHighlight zoomed = WadAuthorModeModel.PickHighlight(map, new Vector2D(50, 6), rendererScale: 2.0);

        Assert.Equal(WadAuthorHighlightKind.Linedef, normal.Kind);
        Assert.Equal(WadAuthorHighlightKind.None, zoomed.Kind);
    }

    [Fact]
    public void EmptyMapReturnsNoHighlight()
    {
        WadAuthorHighlight highlight = WadAuthorModeModel.PickHighlight(new MapSet(), new Vector2D(0, 0));

        Assert.Equal(WadAuthorHighlightKind.None, highlight.Kind);
        Assert.Null(highlight.Target);
    }
}
