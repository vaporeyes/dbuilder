// ABOUTME: Tests for MapSet.SplitLinedef - inserting a vertex on a linedef and splitting it into two segments.
// ABOUTME: Verifies topology (start..v / v..end), property + sidedef inheritance, two-sided handling, and undo.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefSplitTests
{
    private static (MapSet map, Linedef line, Sector sector) BuildOneSidedLine()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.FloorTexture = "F"; sector.CeilTexture = "C";
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(v0, v1);
        l.Flags = 0x0001; l.Action = 11; l.Tag = 5;
        var sd = map.AddSidedef(l, true, sector);
        sd.OffsetX = 4;
        sd.OffsetY = 8;
        sd.MidTexture = "WALL";
        sd.LongMiddleTexture = 100;
        map.BuildIndexes();
        return (map, l, sector);
    }

    [Fact]
    public void SplitCreatesTwoSegmentsSharingNewVertex()
    {
        var (map, l, _) = BuildOneSidedLine();
        var v = map.SplitLinedef(l, new Vector2D(40, 0));
        map.BuildIndexes();

        Assert.Equal(3, map.Vertices.Count);
        Assert.Equal(2, map.Linedefs.Count);
        // Original is now start..v.
        Assert.Equal(new Vector2D(0, 0), l.Start.Position);
        Assert.Same(v, l.End);
        // The new segment is v..oldEnd.
        var nl = map.Linedefs[1];
        Assert.Same(v, nl.Start);
        Assert.Equal(new Vector2D(100, 0), nl.End.Position);
        Assert.Equal(new Vector2D(40, 0), v.Position);
    }

    [Fact]
    public void SplitCopiesLinedefProperties()
    {
        var (map, l, _) = BuildOneSidedLine();
        map.SplitLinedef(l, new Vector2D(50, 0));
        var nl = map.Linedefs[1];
        Assert.Equal(l.Flags, nl.Flags);
        Assert.Equal(l.Action, nl.Action);
        Assert.Equal(l.Tag, nl.Tag);
    }

    [Fact]
    public void SplitCopiesFrontSidedefIntoSameSector()
    {
        var (map, l, sector) = BuildOneSidedLine();
        map.SplitLinedef(l, new Vector2D(50, 0));
        map.BuildIndexes();

        var nl = map.Linedefs[1];
        Assert.NotNull(nl.Front);
        Assert.Same(sector, nl.Front!.Sector);
        Assert.Equal("WALL", nl.Front.MidTexture);
        Assert.Equal(8, nl.Front.OffsetY);
        // Sector now owns both front sidedefs.
        Assert.Equal(2, sector.Sidedefs.Count);
    }

    [Fact]
    public void SplitAdvancesFrontXOffsetByFirstHalfLength()
    {
        var (map, l, _) = BuildOneSidedLine();
        map.SplitLinedef(l, new Vector2D(40, 0)); // first half length = 40
        var nl = map.Linedefs[1];
        Assert.Equal(4 + 40, nl.Front!.OffsetX); // original offset 4 + 40
    }

    [Fact]
    public void SplitAdvancesBackXOffsetBySecondHalfLength()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        Linedef line = map.AddLinedef(v0, v1);
        Sidedef back = map.AddSidedef(line, false, sector);
        back.OffsetX = 6;
        back.OffsetY = 9;
        back.MidTexture = "BACKWALL";
        back.LongMiddleTexture = 200;
        map.BuildIndexes();

        map.SplitLinedef(line, new Vector2D(40, 0));
        Linedef split = map.Linedefs[1];

        Assert.Equal(6 + 60, line.Back!.OffsetX);
        Assert.Equal(6, split.Back!.OffsetX);
        Assert.Equal(9, split.Back.OffsetY);
    }

    [Fact]
    public void SplitTwoSidedLineCopiesBothSidedefs()
    {
        var map = new MapSet();
        var sA = map.AddSector(); var sB = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(v0, v1);
        var front = map.AddSidedef(l, true, sA);
        var back = map.AddSidedef(l, false, sB);
        back.MidTexture = "BK";
        map.BuildIndexes();

        map.SplitLinedef(l, new Vector2D(50, 0));
        map.BuildIndexes();

        Assert.Equal(4, map.Sidedefs.Count); // 2 original + 2 copies
        var nl = map.Linedefs[1];
        Assert.NotNull(nl.Front);
        Assert.NotNull(nl.Back);
        Assert.Same(sA, nl.Front!.Sector);
        Assert.Same(sB, nl.Back!.Sector);
        Assert.Equal("BK", nl.Back.MidTexture);
    }

    [Fact]
    public void SplitVertexLiesOnSegment()
    {
        var map = new MapSet();
        map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(80, 60));
        var l = map.AddLinedef(v0, v1);
        map.AddSidedef(l, true, map.Sectors[0]);
        map.BuildIndexes();

        var v = map.SplitLinedef(l, new Vector2D(40, 30)); // midpoint of the diagonal
        Assert.Equal(new Vector2D(40, 30), v.Position);
        Assert.Same(v, l.End);
        Assert.Same(v, map.Linedefs[1].Start);
    }

    [Fact]
    public void SplitIsUndoable()
    {
        var (map, l, _) = BuildOneSidedLine();
        var undo = new UndoManager(map);

        undo.CreateUndo("Split linedef");
        map.SplitLinedef(l, new Vector2D(50, 0));
        map.BuildIndexes();
        Assert.Equal(2, map.Linedefs.Count);
        Assert.Equal(3, map.Vertices.Count);

        undo.Undo();
        Assert.Single(map.Linedefs);
        Assert.Equal(2, map.Vertices.Count);
        Assert.Equal(new Vector2D(100, 0), map.Linedefs[0].End.Position);
    }

    [Fact]
    public void SplitLinedefsAtMidpointsSplitsEachSelectedLineAtCenter()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(100, 100));
        Linedef first = map.AddLinedef(a, b);
        Linedef second = map.AddLinedef(b, c);
        map.BuildIndexes();

        int count = map.SplitLinedefsAtMidpoints([first, second]);
        map.BuildIndexes();

        Assert.Equal(2, count);
        Assert.Equal(5, map.Vertices.Count);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Equal(new Vector2D(50, 0), first.End.Position);
        Assert.Equal(new Vector2D(100, 50), second.End.Position);
    }

    [Fact]
    public void SplitLinedefsAtMidpointsIgnoresDuplicateTargets()
    {
        var (map, line, _) = BuildOneSidedLine();

        int count = map.SplitLinedefsAtMidpoints([line, line]);

        Assert.Equal(1, count);
        Assert.Equal(2, map.Linedefs.Count);
        Assert.Equal(3, map.Vertices.Count);
    }
}
