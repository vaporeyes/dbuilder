// ABOUTME: Tests for MapSet spatial queries - NearestVertex/NearestLinedef/NearestSidedef/GetSectorAt.
// ABOUTME: Uses a CW-wound square sector (front faces inward) plus a two-sector divider to verify side selection.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapHitTestTests
{
    // CW-wound square (front sidedefs face inward), size x size, origin at (0,0).
    private static (MapSet map, Sector sector) BuildSquare(double size)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.FloorTexture = "F"; sector.CeilTexture = "C";
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(size, 0));
        var v2 = map.AddVertex(new Vector2D(size, size));
        var v3 = map.AddVertex(new Vector2D(0, size));

        void Edge(Vertex a, Vertex b)
        {
            var l = map.AddLinedef(a, b);
            map.AddSidedef(l, true, sector);
        }
        // CW: south E->W, west S->N, north W->E, east N->S.
        Edge(v1, v0); Edge(v0, v3); Edge(v3, v2); Edge(v2, v1);
        map.BuildIndexes();
        return (map, sector);
    }

    [Fact]
    public void NearestVertexFindsClosestCorner()
    {
        var (map, _) = BuildSquare(100);
        var v = map.NearestVertex(new Vector2D(5, 5));
        Assert.NotNull(v);
        Assert.Equal(new Vector2D(0, 0), v!.Position);
    }

    [Fact]
    public void NearestVertexRespectsMaxRange()
    {
        var (map, _) = BuildSquare(100);
        // Center of a 100x100 square is ~70.7 from every corner.
        Assert.Null(map.NearestVertex(new Vector2D(50, 50), maxRange: 50));
        Assert.NotNull(map.NearestVertex(new Vector2D(50, 50), maxRange: 80));
    }

    [Fact]
    public void NearestVertexEmptyMapReturnsNull()
    {
        var map = new MapSet();
        Assert.Null(map.NearestVertex(new Vector2D(0, 0)));
    }

    [Fact]
    public void StaticNearestVertexSearchesSelection()
    {
        var near = new Vertex(new Vector2D(3, 4));
        var far = new Vertex(new Vector2D(100, 0));
        var selection = new[] { far, near };

        Assert.Same(near, MapSet.NearestVertex(selection, new Vector2D(0, 0)));
        Assert.Null(MapSet.NearestVertex(Array.Empty<Vertex>(), new Vector2D(0, 0)));
    }

    [Fact]
    public void StaticNearestVertexSquareRangeUsesSquareBoundsAndManhattanDistance()
    {
        var diagonal = new Vertex(new Vector2D(4, 4));
        var outsideSquare = new Vertex(new Vector2D(6, 0));
        var selection = new[] { outsideSquare, diagonal };

        Assert.Same(diagonal, MapSet.NearestVertexSquareRange(selection, new Vector2D(0, 0), maxRange: 5));
        Assert.Null(MapSet.NearestVertexSquareRange(selection, new Vector2D(0, 0), maxRange: 3));
        Assert.Null(MapSet.NearestVertexSquareRange(Array.Empty<Vertex>(), new Vector2D(0, 0), maxRange: 5));
    }

    [Fact]
    public void NearestVertexSquareRangeSearchesMapVertices()
    {
        var map = new MapSet();
        var diagonal = map.AddVertex(new Vector2D(4, 4));
        map.AddVertex(new Vector2D(6, 0));

        Assert.Same(diagonal, map.NearestVertexSquareRange(new Vector2D(0, 0), maxRange: 5));
        Assert.Null(map.NearestVertexSquareRange(new Vector2D(0, 0), maxRange: 3));
    }

    [Fact]
    public void VertexDistanceHelpersMatchUdbSurface()
    {
        var vertex = new Vertex(new Vector2D(3, 4));

        Assert.Equal(25, vertex.DistanceToSq(new Vector2D(0, 0)), 1e-9);
        Assert.Equal(5, vertex.DistanceTo(new Vector2D(0, 0)), 1e-9);
    }

    [Fact]
    public void VertexMoveAndSnapToAccuracyMatchUdbSurface()
    {
        var vertex = new Vertex(new Vector2D(1.2345, -9.8765));

        vertex.Move(new Vector2D(2.3456, -8.7654));
        Assert.Equal(new Vector2D(2.3456, -8.7654), vertex.Position);

        vertex.SnapToAccuracy(vertexDecimals: 2);
        Assert.Equal(new Vector2D(2.35, -8.77), vertex.Position);

        vertex.Move(new Vector2D(2.3456, -8.7654));
        vertex.SnapToAccuracy(vertexDecimals: 2, usePrecisePosition: false);
        Assert.Equal(new Vector2D(2, -9), vertex.Position);
    }

    [Fact]
    public void VertexMoveRefreshesAttachedLinedefAngles()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(10, 0));
        var line = map.AddLinedef(start, end);
        map.BuildIndexes();

        end.Move(0, 10);

        Assert.Equal(new Vector2D(0, 10), end.Position);
        Assert.Equal(Linedef.ComputeAngle(start, end), line.Angle);
    }

    [Fact]
    public void VertexNearestLinedefSearchesAttachedLines()
    {
        var map = new MapSet();
        var center = map.AddVertex(new Vector2D(0, 0));
        var horizontal = map.AddLinedef(center, map.AddVertex(new Vector2D(10, 0)));
        var vertical = map.AddLinedef(center, map.AddVertex(new Vector2D(0, 10)));
        var detached = map.AddLinedef(map.AddVertex(new Vector2D(-5, 0)), map.AddVertex(new Vector2D(-5, 10)));
        map.BuildIndexes();

        Assert.Contains(horizontal, center.Linedefs);
        Assert.Contains(vertical, center.Linedefs);
        Assert.DoesNotContain(detached, center.Linedefs);
        Assert.Same(vertical, center.NearestLinedef(new Vector2D(-5, 0)));
        Assert.Same(vertical, MapSet.NearestLinedef(center.Linedefs, new Vector2D(-5, 0)));
        Assert.Null(MapSet.NearestLinedef(Array.Empty<Linedef>(), new Vector2D(0, 0)));
    }

    [Fact]
    public void NearestLinedefFindsClosestEdge()
    {
        var (map, _) = BuildSquare(100);
        // A point just inside the south edge (y small) should pick the south edge (v1->v0).
        var l = map.NearestLinedef(new Vector2D(50, 3));
        Assert.NotNull(l);
        Assert.Equal(new Vector2D(100, 0), l!.Start.Position);
        Assert.Equal(new Vector2D(0, 0),   l.End.Position);
    }

    [Fact]
    public void NearestLinedefRespectsMaxRange()
    {
        var (map, _) = BuildSquare(100);
        // Point far below the square - nearest edge (south) is 1000 away.
        Assert.Null(map.NearestLinedef(new Vector2D(50, -1000), maxRange: 100));
        Assert.NotNull(map.NearestLinedef(new Vector2D(50, -1000), maxRange: 1100));
    }

    [Fact]
    public void NearestLinedefUsesBoundedSegmentDistance()
    {
        var (map, _) = BuildSquare(100);
        // Point off the end of the south segment - bounded distance measures to the nearest endpoint,
        // so a point near the (0,0) corner from outside still resolves to a real nearest line.
        var l = map.NearestLinedef(new Vector2D(-5, -5));
        Assert.NotNull(l);
    }

    [Fact]
    public void NearestLinedefUsesUdbSafeDistanceForEndpointTies()
    {
        var map = new MapSet();
        var center = map.AddVertex(new Vector2D(0, 0));
        var horizontal = map.AddLinedef(center, map.AddVertex(new Vector2D(10, 0)));
        var vertical = map.AddLinedef(center, map.AddVertex(new Vector2D(0, 10)));

        var nearest = map.NearestLinedef(new Vector2D(-5, 0));

        Assert.Same(vertical, nearest);
        Assert.True(horizontal.DistanceToSq(new Vector2D(-5, 0), bounded: true) == vertical.DistanceToSq(new Vector2D(-5, 0), bounded: true));
        Assert.True(vertical.SafeDistanceToSq(new Vector2D(-5, 0), bounded: true) < horizontal.SafeDistanceToSq(new Vector2D(-5, 0), bounded: true));
    }

    [Fact]
    public void StaticNearestLinedefRangeSearchesSelectionWithinRange()
    {
        var near = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(10, 0)));
        var far = new Linedef(new Vertex(new Vector2D(100, 0)), new Vertex(new Vector2D(110, 0)));
        var selection = new[] { far, near };

        Assert.Same(near, MapSet.NearestLinedefRange(selection, new Vector2D(5, 3), maxRange: 4));
        Assert.Null(MapSet.NearestLinedefRange(selection, new Vector2D(5, 3), maxRange: 2));
        Assert.Null(MapSet.NearestLinedefRange(Array.Empty<Linedef>(), new Vector2D(5, 3), maxRange: 4));
    }

    [Fact]
    public void NearestLinedefRangeSearchesMapLinedefs()
    {
        var map = new MapSet();
        var near = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(10, 0)));
        map.AddLinedef(map.AddVertex(new Vector2D(100, 0)), map.AddVertex(new Vector2D(110, 0)));

        Assert.Same(near, map.NearestLinedefRange(new Vector2D(5, 3), maxRange: 4));
        Assert.Null(map.NearestLinedefRange(new Vector2D(5, 3), maxRange: 2));
    }

    [Fact]
    public void NearestUnselectedUnreferencedLinedefSkipsLinesConnectedToVertex()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        var connected = map.AddLinedef(vertex, map.AddVertex(new Vector2D(10, 0)));
        var unconnected = map.AddLinedef(map.AddVertex(new Vector2D(0, 2)), map.AddVertex(new Vector2D(10, 2)));

        var nearest = map.NearestUnselectedUnreferencedLinedef(new Vector2D(5, 0), maxRange: 8, vertex, out double distance);

        Assert.Same(unconnected, nearest);
        Assert.Equal(4, distance);
        Assert.True(connected.SafeDistanceToSq(new Vector2D(5, 0), bounded: true) < distance);
    }

    [Fact]
    public void NearestUnselectedUnreferencedLinedefPreservesUdbSelectedLineBehavior()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        map.AddLinedef(vertex, map.AddVertex(new Vector2D(10, 0)));
        var selected = map.AddLinedef(map.AddVertex(new Vector2D(0, 2)), map.AddVertex(new Vector2D(10, 2)));
        selected.Selected = true;

        var nearest = map.NearestUnselectedUnreferencedLinedef(new Vector2D(5, 2), maxRange: 2, vertex, out double distance);

        Assert.Same(selected, nearest);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void NearestUnselectedUnreferencedLinedefReturnsNullWhenOnlyConnectedLinesAreInRange()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        map.AddLinedef(vertex, map.AddVertex(new Vector2D(10, 0)));
        map.AddLinedef(map.AddVertex(new Vector2D(100, 0)), map.AddVertex(new Vector2D(110, 0)));

        var nearest = map.NearestUnselectedUnreferencedLinedef(new Vector2D(5, 0), maxRange: 8, vertex, out double distance);

        Assert.Null(nearest);
        Assert.Equal(double.MaxValue, distance);
    }

    [Fact]
    public void GetSectorAtReturnsSectorForInteriorPoint()
    {
        var (map, sector) = BuildSquare(100);
        Assert.Same(sector, map.GetSectorAt(new Vector2D(50, 20)));
        Assert.Same(sector, map.GetSectorAt(new Vector2D(20, 50)));
    }

    [Fact]
    public void GetSectorAtReturnsNullOutsideOneSidedWall()
    {
        var (map, _) = BuildSquare(100);
        // Just below the south wall: the facing side is the void (no back sector) -> null.
        Assert.Null(map.GetSectorAt(new Vector2D(50, -10)));
    }

    [Fact]
    public void NearestSidedefPicksFacingSide()
    {
        // Two sectors sharing a vertical two-sided divider at x=50.
        var map = new MapSet();
        var left = map.AddSector();  left.FloorTexture = "L";
        var right = map.AddSector(); right.FloorTexture = "R";

        var a = map.AddVertex(new Vector2D(50, 0));
        var b = map.AddVertex(new Vector2D(50, 100));
        var divider = map.AddLinedef(a, b); // start (50,0) -> end (50,100), pointing +y
        var front = map.AddSidedef(divider, true, right);  // front (right of +y) = +x side
        var back = map.AddSidedef(divider, false, left);   // back = -x side
        map.BuildIndexes();

        // A point to the right (+x) of the line is on the front side.
        var sdRight = map.NearestSidedef(new Vector2D(60, 50));
        Assert.Same(front, sdRight);
        // A point to the left (-x) is on the back side.
        var sdLeft = map.NearestSidedef(new Vector2D(40, 50));
        Assert.Same(back, sdLeft);
    }

    [Fact]
    public void NearestThingFindsClosestWithinRange()
    {
        var map = new MapSet();
        var t0 = map.AddThing(new Vector2D(0, 0), 1);
        var t1 = map.AddThing(new Vector2D(100, 0), 3001);

        Assert.Same(t0, map.NearestThing(new Vector2D(5, 5)));
        Assert.Same(t1, map.NearestThing(new Vector2D(95, 2)));
        Assert.Null(map.NearestThing(new Vector2D(50, 50), maxRange: 10));
        Assert.Null(new MapSet().NearestThing(new Vector2D(0, 0)));
    }

    [Fact]
    public void StaticNearestThingSearchesSelection()
    {
        var source = new Thing(new Vector2D(0, 0), 1);
        var near = new Thing(new Vector2D(3, 4), 2);
        var far = new Thing(new Vector2D(100, 0), 3);
        var selection = new[] { source, far, near };

        Assert.Same(near, MapSet.NearestThing(selection, new Vector2D(4, 4)));
        Assert.Same(near, MapSet.NearestThing(selection, source));
        Assert.Null(MapSet.NearestThing(new[] { source }, source));
        Assert.Null(MapSet.NearestThing(Array.Empty<Thing>(), new Vector2D(0, 0)));
    }

    [Fact]
    public void StaticNearestThingSquareRangeUsesThingSizeAndTieBreaksSmaller()
    {
        var large = new Thing(new Vector2D(4, 0), 1) { Size = 8 };
        var small = new Thing(new Vector2D(0, 4), 2) { Size = 4 };
        var sizeExtendsIntoRange = new Thing(new Vector2D(8, 0), 3) { Size = 4 };
        var selection = new[] { large, sizeExtendsIntoRange, small };

        Assert.Same(small, MapSet.NearestThingSquareRange(selection, new Vector2D(0, 0), maxRange: 5));
        Assert.Same(sizeExtendsIntoRange, MapSet.NearestThingSquareRange(new[] { sizeExtendsIntoRange }, new Vector2D(0, 0), maxRange: 5));
        Assert.Null(MapSet.NearestThingSquareRange(Array.Empty<Thing>(), new Vector2D(0, 0), maxRange: 5));
    }

    [Fact]
    public void NearestThingSquareRangeSearchesMapThings()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(100, 0), 1);
        var near = map.AddThing(new Vector2D(4, 0), 2);

        Assert.Same(near, map.NearestThingSquareRange(new Vector2D(0, 0), maxRange: 5));
        Assert.Null(map.NearestThingSquareRange(new Vector2D(0, 0), maxRange: 3));
    }

    [Fact]
    public void ThingDistanceHelpersMatchUdbSurface()
    {
        var thing = new Thing(new Vector2D(3, 4), 3001);

        Assert.Equal(25, thing.DistanceToSq(new Vector2D(0, 0)), 1e-9);
        Assert.Equal(5, thing.DistanceTo(new Vector2D(0, 0)), 1e-9);
    }

    [Fact]
    public void ThingMoveAndSnapToAccuracyMatchUdbSurface()
    {
        var thing = new Thing(new Vector2D(1.2345, -9.8765), 3001) { Height = 16.555 };

        thing.Move(new Vector2D(2.3456, -8.7654));
        Assert.Equal(new Vector2D(2.3456, -8.7654), thing.Position);

        thing.SnapToAccuracy(vertexDecimals: 2);
        Assert.Equal(new Vector2D(2.35, -8.77), thing.Position);
        Assert.Equal(16.56, thing.Height, 1e-9);

        thing.Move(new Vector2D(2.3456, -8.7654));
        thing.Height = 16.555;
        thing.SnapToAccuracy(vertexDecimals: 2, usePrecisePosition: false);
        Assert.Equal(new Vector2D(2, -9), thing.Position);
        Assert.Equal(17, thing.Height, 1e-9);
    }

    [Fact]
    public void Thing3DMoveRotationAndScaleHelpersMatchUdbSurface()
    {
        var thing = new Thing(new Vector2D(1, 2), 3001);

        thing.Move(new Vector3D(3, 4, 5));

        Assert.Equal(new Vector2D(3, 4), thing.Position);
        Assert.Equal(5, thing.Height, 1e-9);

        thing.Move(6, 7, 8);
        thing.Rotate(180);
        thing.SetPitch(450);
        thing.SetRoll(-90);
        thing.SetScale(1.25, 0.75);

        Assert.Equal(new Vector2D(6, 7), thing.Position);
        Assert.Equal(8, thing.Height, 1e-9);
        Assert.Equal(180, thing.Angle);
        Assert.Equal(90, thing.Pitch);
        Assert.Equal(270, thing.Roll);
        Assert.Equal(1.25, thing.ScaleX, 1e-9);
        Assert.Equal(0.75, thing.ScaleY, 1e-9);

        thing.Rotate(Angle2D.DoomToReal(90));

        Assert.Equal(90, thing.Angle);
    }

    [Fact]
    public void ThingUpdateHelperMatchesUdbSurface()
    {
        var thing = new Thing(new Vector2D(1, 2), 3001);

        thing.Update(
            type: 3002,
            x: 10,
            y: 20,
            zOffset: 32,
            angle: 90,
            pitch: 15,
            roll: 345,
            scaleX: 0,
            scaleY: 1.5,
            flags: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["skill1"] = true,
                ["ambush"] = false,
            },
            rawFlags: 33,
            tag: 7,
            action: 80,
            args: new[] { 1, 2, 3, 4, 5, 6 });

        Assert.Equal(3002, thing.Type);
        Assert.Equal(new Vector2D(10, 20), thing.Position);
        Assert.Equal(32, thing.Height, 1e-9);
        Assert.Equal(90, thing.Angle);
        Assert.Equal(15, thing.Pitch);
        Assert.Equal(345, thing.Roll);
        Assert.Equal(1.0, thing.ScaleX, 1e-9);
        Assert.Equal(1.5, thing.ScaleY, 1e-9);
        Assert.Equal(33, thing.Flags);
        Assert.Equal(7, thing.Tag);
        Assert.Equal(80, thing.Action);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, thing.Args);
        Assert.Contains("skill1", thing.UdmfFlags);
        Assert.DoesNotContain("ambush", thing.UdmfFlags);

        thing.Update(1, -1, -2, -3, 0, 0, 0, 2, 0, new Dictionary<string, bool>(), 0, 0, 0, new[] { 9 });

        Assert.Equal(new Vector2D(-1, -2), thing.Position);
        Assert.Equal(-3, thing.Height, 1e-9);
        Assert.Equal(2, thing.ScaleX, 1e-9);
        Assert.Equal(1, thing.ScaleY, 1e-9);
        Assert.Equal(new[] { 9, 0, 0, 0, 0 }, thing.Args);
        Assert.Empty(thing.UdmfFlags);
    }

    [Fact]
    public void SnapAllToAccuracySnapsVerticesAndThings()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(1.2345, -9.8765));
        var thing = map.AddThing(new Vector2D(2.3456, -8.7654), 3001);
        thing.Height = 16.555;

        map.SnapAllToAccuracy(vertexDecimals: 1);

        Assert.Equal(new Vector2D(1.2, -9.9), vertex.Position);
        Assert.Equal(new Vector2D(2.3, -8.8), thing.Position);
        Assert.Equal(16.6, thing.Height, 1e-9);
    }

    [Fact]
    public void GetSectorAtTwoSidedDividerPicksCorrectSector()
    {
        var map = new MapSet();
        var left = map.AddSector();
        var right = map.AddSector();
        var a = map.AddVertex(new Vector2D(50, 0));
        var b = map.AddVertex(new Vector2D(50, 100));
        var divider = map.AddLinedef(a, b);
        map.AddSidedef(divider, true, right);  // front = +x side
        map.AddSidedef(divider, false, left);  // back = -x side
        map.BuildIndexes();

        Assert.Same(right, map.GetSectorAt(new Vector2D(60, 50)));
        Assert.Same(left, map.GetSectorAt(new Vector2D(40, 50)));
    }

    [Fact]
    public void GetSectorContainingReturnsSectorForLineFullyInside()
    {
        var (map, sector) = BuildSquare(100);
        var line = new Linedef(
            new Vertex(new Vector2D(20, 20)),
            new Vertex(new Vector2D(80, 80)));

        Assert.Same(sector, map.GetSectorContaining(line));
    }

    [Fact]
    public void GetSectorContainingIgnoresCandidateLineAlreadyInMap()
    {
        var (map, sector) = BuildSquare(100);
        var line = map.AddLinedef(
            map.AddVertex(new Vector2D(20, 20)),
            map.AddVertex(new Vector2D(80, 80)));
        map.AddSidedef(line, true, sector);

        Assert.Same(sector, map.GetSectorContaining(line));
    }

    [Fact]
    public void GetSectorContainingReturnsNullForLineCrossingBoundary()
    {
        var (map, _) = BuildSquare(100);
        var line = new Linedef(
            new Vertex(new Vector2D(50, 50)),
            new Vertex(new Vector2D(150, 50)));

        Assert.Null(map.GetSectorContaining(line));
    }

    [Fact]
    public void BlockMapGetSectorContainingMatchesMapSet()
    {
        var (map, sector) = BuildSquare(100);
        var blockMap = new BlockMap(map, 64);
        var line = new Linedef(
            new Vertex(new Vector2D(20, 20)),
            new Vertex(new Vector2D(80, 20)));

        Assert.Same(sector, blockMap.GetSectorContaining(line));
        Assert.Same(map.GetSectorContaining(line), blockMap.GetSectorContaining(line));
    }
}
