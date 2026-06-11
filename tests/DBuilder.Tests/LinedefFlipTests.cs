// ABOUTME: Tests for Linedef.FlipVertices/FlipSidedefs and the MapSet selection-flip helpers.
// ABOUTME: Verifies direction reversal, angle update, side swapping, IsFront flags, and geometry preservation.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefFlipTests
{
    [Fact]
    public void AddSidedefLinksOppositeSidesImmediately()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var line = map.AddLinedef(a, b);
        var frontSector = map.AddSector();
        var backSector = map.AddSector();

        var front = map.AddSidedef(line, true, frontSector);
        var back = map.AddSidedef(line, false, backSector);

        Assert.Same(back, front.Other);
        Assert.Same(front, back.Other);
        Assert.Same(front, line.Front);
        Assert.Same(back, line.Back);
    }

    [Fact]
    public void FlipVerticesReversesDirectionAndAngle()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        map.BuildIndexes();
        double before = l.Angle;

        l.FlipVertices();
        map.BuildIndexes();

        Assert.Same(b, l.Start);
        Assert.Same(a, l.End);
        Assert.NotEqual(before, l.Angle);
        // The reversed direction differs from the original by half a turn.
        double diff = Angle2D.Difference(before, l.Angle);
        Assert.Equal(Angle2D.PI, diff, 6);
    }

    [Fact]
    public void FlipSidedefsSwapsFrontAndBack()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        var s1 = map.AddSector();
        var s2 = map.AddSector();
        var front = map.AddSidedef(l, true, s1);
        var back = map.AddSidedef(l, false, s2);
        map.BuildIndexes();

        l.FlipSidedefs();
        map.BuildIndexes();

        Assert.Same(back, l.Front);
        Assert.Same(front, l.Back);
        Assert.True(l.Front!.IsFront);
        Assert.False(l.Back!.IsFront);
        Assert.Same(s2, l.Front.Sector);
        Assert.Same(s1, l.Back.Sector);
    }

    [Fact]
    public void FlipSidedefsHandlesMissingBack()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        var s1 = map.AddSector();
        var front = map.AddSidedef(l, true, s1);
        map.BuildIndexes();

        l.FlipSidedefs();

        Assert.Null(l.Front);
        Assert.Same(front, l.Back);
        Assert.False(l.Back!.IsFront);
    }

    [Fact]
    public void GetCenterPointReturnsLineMidpoint()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-10, 8));
        var b = map.AddVertex(new Vector2D(30, 24));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(10, 16), line.GetCenterPoint());
    }

    [Fact]
    public void GetSidePointOffsetsFromLineCenter()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(50, -0.01), line.GetSidePoint(front: true));
        Assert.Equal(new Vector2D(50, 0.01), line.GetSidePoint(front: false));
    }

    [Fact]
    public void GetSidePointHandlesDegenerateLinesLikeUdb()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(16, 32));
        var line = map.AddLinedef(a, a);

        Assert.Equal(a.Position, line.GetSidePoint(front: true));
        Assert.Equal(a.Position, line.GetSidePoint(front: false));
    }

    [Fact]
    public void GeometryPropertiesMatchUdbLineSurface()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(-4, 2));
        var b = map.AddVertex(new Vector2D(8, 7));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Line2D(a.Position, b.Position).v1, line.Line.v1);
        Assert.Equal(new Line2D(a.Position, b.Position).v2, line.Line.v2);
        Assert.Equal(169, line.LengthSq, 1e-9);
        Assert.Equal(13, line.Length, 1e-9);
        Assert.Equal(1.0 / 13.0, line.LengthInv, 1e-9);
        Assert.Equal((int)(line.Angle * Angle2D.PIDEG), line.AngleDeg);
        Assert.Equal(-4, line.Rect.Left);
        Assert.Equal(2, line.Rect.Top);
        Assert.Equal(12, line.Rect.Width);
        Assert.Equal(5, line.Rect.Height);
    }

    [Fact]
    public void GeometryMethodsMatchLine2DBehavior()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);

        Assert.Equal(new Vector2D(10, 0), line.NearestOnLine(new Vector2D(15, 4)));
        Assert.Equal(25, line.DistanceToSq(new Vector2D(15, 0), bounded: true), 1e-9);
        Assert.Equal(5, line.DistanceTo(new Vector2D(15, 0), bounded: true), 1e-9);
        Assert.Equal(0, line.DistanceTo(new Vector2D(15, 0), bounded: false), 1e-9);
        Assert.True(line.SideOfLine(new Vector2D(5, 1)) > 0);
        Assert.True(line.SideOfLine(new Vector2D(5, -1)) < 0);
    }

    [Fact]
    public void GridIntersectionsReturnUdbGridCrossings()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(96, 48));
        var line = map.AddLinedef(a, b);

        List<Vector2D> points = line.GetGridIntersections(gridSize: 32.0);

        Assert.Equal(5, points.Count);
        AssertVector(new Vector2D(0, 0), points[0]);
        AssertVector(new Vector2D(32, 16), points[1]);
        AssertVector(new Vector2D(64, 32), points[2]);
        AssertVector(new Vector2D(0, 0), points[3]);
        AssertVector(new Vector2D(64, 32), points[4]);
    }

    [Fact]
    public void GridIntersectionsIncludeStartButNotEndGridCrossing()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(a, b);

        List<Vector2D> points = line.GetGridIntersections(gridSize: 32.0);

        Assert.Equal(2, points.Count);
        AssertVector(new Vector2D(0, 0), points[0]);
        AssertVector(new Vector2D(32, 0), points[1]);
    }

    [Fact]
    public void GridIntersectionsHonorOffsetAndReverseDirection()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(70, 0));
        var b = map.AddVertex(new Vector2D(0, 0));
        var line = map.AddLinedef(a, b);

        List<Vector2D> points = line.GetGridIntersections(gridSize: 32.0, gridOffset: new Vector2D(8, 0));

        Assert.Equal(2, points.Count);
        AssertVector(new Vector2D(8, 0), points[0]);
        AssertVector(new Vector2D(40, 0), points[1]);
    }

    [Fact]
    public void GridIntersectionsHonorRotationAndOrigin()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(10, 0));
        var b = map.AddVertex(new Vector2D(10, 64));
        var line = map.AddLinedef(a, b);

        List<Vector2D> points = line.GetGridIntersections(
            gridSize: 32.0,
            gridRotation: Math.PI / 2.0,
            gridOriginX: 10.0,
            gridOriginY: 0.0);

        Assert.Equal(3, points.Count);
        AssertVector(new Vector2D(10, 0), points[0]);
        AssertVector(new Vector2D(10, 32), points[1]);
        AssertVector(new Vector2D(10, 0), points[2]);
    }

    [Fact]
    public void LinedefUpdateHelperMatchesUdbSurface()
    {
        var line = new Linedef();

        line.Update(
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["blocking"] = true,
                ["secret"] = false,
            },
            rawFlags: 17,
            activate: 3,
            tags: new List<int> { 5, 9 },
            action: 80,
            args: new[] { 1, 2, 3, 4, 5, 6 });

        Assert.Equal(17, line.Flags);
        Assert.Equal(3, line.Activate);
        Assert.Equal(80, line.Action);
        Assert.Equal(new[] { 5, 9 }, line.Tags);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, line.Args);
        Assert.Contains("blocking", line.UdmfFlags);
        Assert.DoesNotContain("secret", line.UdmfFlags);

        line.Update(
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            rawFlags: 0,
            activate: 0,
            tags: new List<int>(),
            action: 0,
            args: new[] { 7, 8 });

        Assert.Equal(0, line.Flags);
        Assert.Equal(0, line.Activate);
        Assert.Equal(0, line.Action);
        Assert.Empty(line.Tags);
        Assert.Equal(new[] { 7, 8, 0, 0, 0 }, line.Args);
        Assert.Empty(line.UdmfFlags);
    }

    [Fact]
    public void SafeDistanceAvoidsEqualEndpointDistanceForLongLines()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(a, b);

        Assert.True(line.SafeDistanceToSq(new Vector2D(-5, 0), bounded: true) > line.DistanceToSq(new Vector2D(-5, 0), bounded: true));
    }

    private static void AssertVector(Vector2D expected, Vector2D actual)
    {
        Assert.Equal(expected.x, actual.x, 1e-9);
        Assert.Equal(expected.y, actual.y, 1e-9);
    }

    [Fact]
    public void FlipSelectedLinedefsOnlyTouchesSelection()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(100, 100));
        var l1 = map.AddLinedef(a, b);
        var l2 = map.AddLinedef(b, c);
        map.BuildIndexes();
        l1.Selected = true;

        int n = map.FlipSelectedLinedefs();
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Same(b, l1.Start); // flipped
        Assert.Same(b, l2.Start); // untouched
        Assert.Same(c, l2.End);
    }

    [Fact]
    public void FlipSelectedLinedefsSwapsSidedefsAndSkipsFrontOnlyLines()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(0, 100));
        var d = map.AddVertex(new Vector2D(100, 100));
        var sector = map.AddSector();
        var flip = map.AddLinedef(a, b);
        var skipped = map.AddLinedef(c, d);
        var back = map.AddSidedef(flip, false, sector);
        var front = map.AddSidedef(skipped, true, sector);
        flip.Selected = true;
        skipped.Selected = true;
        map.BuildIndexes();

        int n = map.FlipSelectedLinedefs();
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Same(b, flip.Start);
        Assert.Same(a, flip.End);
        Assert.Same(back, flip.Front);
        Assert.Null(flip.Back);
        Assert.Same(c, skipped.Start);
        Assert.Same(d, skipped.End);
        Assert.Same(front, skipped.Front);
        Assert.Null(skipped.Back);
    }

    [Fact]
    public void FlipSelectedSidedefsOnlyTouchesTwoSidedLines()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(0, 100));
        var d = map.AddVertex(new Vector2D(100, 100));
        var frontSector = map.AddSector();
        var backSector = map.AddSector();
        var oneSidedSector = map.AddSector();
        var twoSided = map.AddLinedef(a, b);
        var oneSided = map.AddLinedef(c, d);
        var front = map.AddSidedef(twoSided, true, frontSector);
        var back = map.AddSidedef(twoSided, false, backSector);
        var oneSidedFront = map.AddSidedef(oneSided, true, oneSidedSector);
        twoSided.Selected = true;
        oneSided.Selected = true;
        map.BuildIndexes();

        int n = map.FlipSelectedSidedefs();
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Same(back, twoSided.Front);
        Assert.Same(front, twoSided.Back);
        Assert.Same(oneSidedFront, oneSided.Front);
        Assert.Null(oneSided.Back);
    }

    [Fact]
    public void FlipLinedefsOfSectorsFlipsBoundaryLinesOnce()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(0, 100));
        var d = map.AddVertex(new Vector2D(100, 100));
        var sector = map.AddSector();
        var flip = map.AddLinedef(a, b);
        var skipped = map.AddLinedef(c, d);
        var back = map.AddSidedef(flip, false, sector);
        var front = map.AddSidedef(skipped, true, sector);
        map.BuildIndexes();

        int n = map.FlipLinedefsOfSectors([sector]);
        map.BuildIndexes();

        Assert.Equal(1, n);
        Assert.Same(b, flip.Start);
        Assert.Same(a, flip.End);
        Assert.Same(back, flip.Front);
        Assert.Null(flip.Back);
        Assert.Same(c, skipped.Start);
        Assert.Same(d, skipped.End);
        Assert.Same(front, skipped.Front);
        Assert.Null(skipped.Back);
    }

    [Fact]
    public void FlipBackwardLinedefsFlipsLinesWithOnlyBackSide()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var back = map.AddSidedef(line, false, sector);

        int flips = MapSet.FlipBackwardLinedefs(new[] { line });

        Assert.Equal(1, flips);
        Assert.Same(b, line.Start);
        Assert.Same(a, line.End);
        Assert.Same(back, line.Front);
        Assert.Null(line.Back);
        Assert.True(back.IsFront);
    }

    [Fact]
    public void FlipBackwardLinedefsIgnoresLinesWithFrontSides()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var front = map.AddSidedef(line, true, sector);

        int flips = MapSet.FlipBackwardLinedefs(new[] { line });

        Assert.Equal(0, flips);
        Assert.Same(a, line.Start);
        Assert.Same(b, line.End);
        Assert.Same(front, line.Front);
        Assert.Null(line.Back);
    }

    [Fact]
    public void AlignSelectedLinedefsFlipsLinesToFaceDominantSector()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var back = map.AddSidedef(line, false, sector);
        line.Selected = true;
        map.BuildIndexes();

        int count = map.AlignSelectedLinedefs();
        map.BuildIndexes();

        Assert.Equal(1, count);
        Assert.Same(b, line.Start);
        Assert.Same(a, line.End);
        Assert.Same(back, line.Front);
        Assert.Null(line.Back);
        Assert.False(line.Selected);
    }

    [Fact]
    public void AlignLinedefsOfSectorsFlipsBackFacingSectorLines()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var sector = map.AddSector();
        var line = map.AddLinedef(a, b);
        var back = map.AddSidedef(line, false, sector);
        map.BuildIndexes();

        int count = map.AlignLinedefsOfSectors([sector]);
        map.BuildIndexes();

        Assert.Equal(1, count);
        Assert.Same(b, line.Start);
        Assert.Same(a, line.End);
        Assert.Same(back, line.Front);
        Assert.Null(line.Back);
    }
}
