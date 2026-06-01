// ABOUTME: Tests for Tools.FindClosestPath - tracing a closed loop of linedef sides from a start line+side.
// ABOUTME: Uses known shapes (square, L-shape) and asserts the traced loop length, closure, and vertex set.

using System.Collections.Generic;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ToolsTraceTests
{
    [Fact]
    public void HermiteSpline2DMatchesUdbEndpointsAndMidpoint()
    {
        var p1 = new Vector2D(0, 0);
        var t1 = new Vector2D(10, 0);
        var p2 = new Vector2D(10, 10);
        var t2 = new Vector2D(0, 10);

        Assert.Equal(p1, Tools.HermiteSpline(p1, t1, p2, t2, 0));
        Assert.Equal(p2, Tools.HermiteSpline(p1, t1, p2, t2, 1));

        var midpoint = Tools.HermiteSpline(p1, t1, p2, t2, 0.5);
        Assert.Equal(6.25, midpoint.x, 1e-9);
        Assert.Equal(3.75, midpoint.y, 1e-9);
    }

    [Fact]
    public void HermiteSpline3DInterpolatesEveryAxis()
    {
        var p1 = new Vector3D(0, 0, 0);
        var t1 = new Vector3D(10, 0, 4);
        var p2 = new Vector3D(10, 10, 20);
        var t2 = new Vector3D(0, 10, -4);

        var midpoint = Tools.HermiteSpline(p1, t1, p2, t2, 0.5);

        Assert.Equal(6.25, midpoint.x, 1e-9);
        Assert.Equal(3.75, midpoint.y, 1e-9);
        Assert.Equal(11.0, midpoint.z, 1e-9);
    }

    [Fact]
    public void FindLabelPositionsUsesTriangleCentroidWhenNoInteriorCandidatesExist()
    {
        var (_, sector, _) = BuildSectorPolygon(new[]
        {
            new Vector2D(0, 0),
            new Vector2D(0, 100),
            new Vector2D(100, 0),
        });

        LabelPositionInfo label = Assert.Single(Tools.FindLabelPositions(sector));

        Assert.Equal(100.0 / 3.0, label.position.x, 1e-9);
        Assert.Equal(100.0 / 3.0, label.position.y, 1e-9);
        Assert.True(label.radius > 0);
    }

    [Fact]
    public void FindLabelPositionsPicksInteriorCandidateFarthestFromBoundary()
    {
        var (_, sector, _) = BuildSectorPolygon(new[]
        {
            new Vector2D(100, 0),
            new Vector2D(0, 0),
            new Vector2D(0, 100),
            new Vector2D(100, 100),
        });

        LabelPositionInfo label = Assert.Single(Tools.FindLabelPositions(sector));

        Assert.Equal(50, label.position.x, 1e-9);
        Assert.Equal(50, label.position.y, 1e-9);
        Assert.Equal(50, label.radius, 1e-9);
    }

    [Fact]
    public void RemoveMarkedActionsClearsMarkedThingsAndLinedefsOnly()
    {
        var map = new MapSet();
        var markedThing = map.AddThing(new Vector2D(0, 0), type: 1);
        markedThing.Marked = true;
        markedThing.Action = 80;
        markedThing.Args[0] = 12;
        markedThing.Args[4] = 99;

        var unmarkedThing = map.AddThing(new Vector2D(64, 0), type: 2);
        unmarkedThing.Action = 81;
        unmarkedThing.Args[1] = 7;

        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(128, 0));
        var markedLine = map.AddLinedef(v0, v1);
        markedLine.Marked = true;
        markedLine.Action = 12;
        markedLine.Args[2] = 34;

        var v2 = map.AddVertex(new Vector2D(128, 64));
        var unmarkedLine = map.AddLinedef(v1, v2);
        unmarkedLine.Action = 13;
        unmarkedLine.Args[3] = 35;

        Tools.RemoveMarkedActions(map);

        Assert.Equal(0, markedThing.Action);
        Assert.All(markedThing.Args, arg => Assert.Equal(0, arg));
        Assert.Equal(81, unmarkedThing.Action);
        Assert.Equal(7, unmarkedThing.Args[1]);

        Assert.Equal(0, markedLine.Action);
        Assert.All(markedLine.Args, arg => Assert.Equal(0, arg));
        Assert.Equal(13, unmarkedLine.Action);
        Assert.Equal(35, unmarkedLine.Args[3]);
    }

    [Fact]
    public void FlipSectorLinedefsFlipsBackLinesWhenFrontLinesDominate()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var other = map.AddSector();
        var frontA = AddTwoSidedLine(map, sector, other, targetOnFront: true);
        var frontB = AddTwoSidedLine(map, sector, other, targetOnFront: true);
        var back = AddTwoSidedLine(map, sector, other, targetOnFront: false);
        map.BuildIndexes();

        Tools.FlipSectorLinedefs(new[] { sector }, selectedLinesOnly: false);

        Assert.Same(sector, frontA.Front!.Sector);
        Assert.Same(sector, frontB.Front!.Sector);
        Assert.Same(sector, back.Front!.Sector);
    }

    [Fact]
    public void FlipSectorLinedefsSkipsSingleSidedFrontLines()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var line = AddOneSidedLine(map, sector);
        Vertex start = line.Start;
        Vertex end = line.End;
        map.BuildIndexes();

        Tools.FlipSectorLinedefs(new[] { sector }, selectedLinesOnly: false);

        Assert.Same(start, line.Start);
        Assert.Same(end, line.End);
        Assert.Same(sector, line.Front!.Sector);
        Assert.Null(line.Back);
    }

    [Fact]
    public void FlipSectorLinedefsUsesUnselectedLinesForSelectedOnlyDecision()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var other = map.AddSector();
        var unselectedFront = AddTwoSidedLine(map, sector, other, targetOnFront: true);
        var selectedFront = AddTwoSidedLine(map, sector, other, targetOnFront: true);
        var selectedBackA = AddTwoSidedLine(map, sector, other, targetOnFront: false);
        var selectedBackB = AddTwoSidedLine(map, sector, other, targetOnFront: false);
        selectedFront.Selected = true;
        selectedBackA.Selected = true;
        selectedBackB.Selected = true;
        map.BuildIndexes();

        Tools.FlipSectorLinedefs(new[] { sector }, selectedLinesOnly: true);

        Assert.Same(sector, unselectedFront.Front!.Sector);
        Assert.Same(sector, selectedFront.Back!.Sector);
        Assert.Same(sector, selectedBackA.Back!.Sector);
        Assert.Same(sector, selectedBackB.Back!.Sector);
    }

    [Fact]
    public void FlipSectorLinedefsProcessesSharedLinesOnceAcrossSectors()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var other = map.AddSector();
        var shared = AddTwoSidedLine(map, sector, other, targetOnFront: false);
        map.BuildIndexes();

        Tools.FlipSectorLinedefs(new[] { sector, other }, selectedLinesOnly: false);

        Assert.Same(sector, shared.Front!.Sector);
    }

    [Fact]
    public void FloodfillFlatsFillsConnectedMatchingFloors()
    {
        var (map, left, middle, right) = ThreeSectorChain();
        left.FloorTexture = "FLAT1";
        middle.FloorTexture = "FLAT1";
        right.FloorTexture = "FLAT2";

        Tools.FloodfillFlats(map, left, fillCeilings: false, new HashSet<string> { "FLAT1" }, "NEWFLAT", resetSectorMarks: true);

        Assert.Equal("NEWFLAT", left.FloorTexture);
        Assert.Equal("NEWFLAT", middle.FloorTexture);
        Assert.Equal("FLAT2", right.FloorTexture);
        Assert.True(left.Marked);
        Assert.True(middle.Marked);
        Assert.False(right.Marked);
    }

    [Fact]
    public void FloodfillFlatsCanFillCeilingsWithoutChangingFloors()
    {
        var (map, left, middle, _) = ThreeSectorChain();
        left.FloorTexture = middle.FloorTexture = "FLOOR";
        left.CeilTexture = middle.CeilTexture = "CEIL1";

        Tools.FloodfillFlats(map, left, fillCeilings: true, new HashSet<string> { "CEIL1" }, "NEWCEIL", resetSectorMarks: true);

        Assert.Equal("FLOOR", left.FloorTexture);
        Assert.Equal("FLOOR", middle.FloorTexture);
        Assert.Equal("NEWCEIL", left.CeilTexture);
        Assert.Equal("NEWCEIL", middle.CeilTexture);
    }

    [Fact]
    public void FloodfillFlatsRespectsPremarkedBoundariesWhenMarksAreNotReset()
    {
        var (map, left, middle, right) = ThreeSectorChain();
        left.FloorTexture = middle.FloorTexture = right.FloorTexture = "FLAT1";
        middle.Marked = true;

        Tools.FloodfillFlats(map, left, fillCeilings: false, new HashSet<string> { "FLAT1" }, "NEWFLAT", resetSectorMarks: false);

        Assert.Equal("NEWFLAT", left.FloorTexture);
        Assert.Equal("FLAT1", middle.FloorTexture);
        Assert.Equal("FLAT1", right.FloorTexture);
    }

    [Fact]
    public void FloodfillTexturesFillsVertexConnectedMatchingMiddleTextures()
    {
        var (map, first, second, third) = ThreeOneSidedLineChain();
        first.MidTexture = "STONE";
        second.MidTexture = "STONE";
        third.MidTexture = "BRICK";

        Tools.FloodfillTextures(map, first, new HashSet<string> { "STONE" }, "METAL", resetSideMarks: true);

        Assert.Equal("METAL", first.MidTexture);
        Assert.Equal("METAL", second.MidTexture);
        Assert.Equal("BRICK", third.MidTexture);
        Assert.True(first.Marked);
        Assert.True(second.Marked);
        Assert.False(third.Marked);
    }

    [Fact]
    public void FloodfillTexturesRespectsPremarkedSidedefBoundariesWhenMarksAreNotReset()
    {
        var (map, first, second, third) = ThreeOneSidedLineChain();
        first.MidTexture = second.MidTexture = third.MidTexture = "STONE";
        second.Marked = true;

        Tools.FloodfillTextures(map, first, new HashSet<string> { "STONE" }, "METAL", resetSideMarks: false);

        Assert.Equal("METAL", first.MidTexture);
        Assert.Equal("STONE", second.MidTexture);
        Assert.Equal("STONE", third.MidTexture);
    }

    [Fact]
    public void FloodfillTexturesUpdatesRequiredUpperLowerAndNonEmptyMiddleSlots()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        var backSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        backSector.FloorHeight = 32;
        backSector.CeilHeight = 64;

        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        Sidedef side = map.AddSidedef(line, isFront: true, frontSector);
        map.AddSidedef(line, isFront: false, backSector);
        side.HighTexture = side.MidTexture = side.LowTexture = "STONE";
        map.BuildIndexes();

        Tools.FloodfillTextures(map, side, new HashSet<string> { "STONE" }, "METAL", resetSideMarks: true);

        Assert.Equal("METAL", side.HighTexture);
        Assert.Equal("METAL", side.MidTexture);
        Assert.Equal("METAL", side.LowTexture);
        Assert.True(side.Marked);
    }

    [Fact]
    public void PointInPolygonUsesUdbCrossingRule()
    {
        var polygon = new[]
        {
            new Vector2D(0, 0),
            new Vector2D(10, 0),
            new Vector2D(10, 10),
            new Vector2D(0, 10),
        };

        Assert.True(Tools.PointInPolygon(polygon, new Vector2D(5, 5)));
        Assert.False(Tools.PointInPolygon(polygon, new Vector2D(15, 5)));
        Assert.True(Tools.PointInPolygon(polygon, new Vector2D(10, 5)));
        Assert.False(Tools.PointInPolygon(polygon, new Vector2D(5, 0)));
    }

    // Builds a closed polygon of linedefs (front sidedef inward, CW winding) and returns the map.
    private static (MapSet map, List<Linedef> lines) BuildPolygon(Vector2D[] cwLoop)
    {
        var (map, _, lines) = BuildSectorPolygon(cwLoop);
        return (map, lines);
    }

    private static Linedef AddOneSidedLine(MapSet map, Sector sector)
    {
        var start = map.AddVertex(new Vector2D(map.Linedefs.Count * 16, 0));
        var end = map.AddVertex(new Vector2D(map.Linedefs.Count * 16 + 8, 0));
        var line = map.AddLinedef(start, end);
        map.AddSidedef(line, isFront: true, sector);
        return line;
    }

    private static Linedef AddTwoSidedLine(MapSet map, Sector target, Sector other, bool targetOnFront)
    {
        var start = map.AddVertex(new Vector2D(map.Linedefs.Count * 16, 0));
        var end = map.AddVertex(new Vector2D(map.Linedefs.Count * 16 + 8, 0));
        var line = map.AddLinedef(start, end);
        map.AddSidedef(line, isFront: true, targetOnFront ? target : other);
        map.AddSidedef(line, isFront: false, targetOnFront ? other : target);
        return line;
    }

    private static (MapSet Map, Sector Left, Sector Middle, Sector Right) ThreeSectorChain()
    {
        var map = new MapSet();
        var left = map.AddSector();
        var middle = map.AddSector();
        var right = map.AddSector();
        AddTwoSidedLine(map, left, middle, targetOnFront: true);
        AddTwoSidedLine(map, middle, right, targetOnFront: true);
        map.BuildIndexes();
        return (map, left, middle, right);
    }

    private static (MapSet Map, Sidedef First, Sidedef Second, Sidedef Third) ThreeOneSidedLineChain()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(128, 0));
        var v3 = map.AddVertex(new Vector2D(192, 0));
        Sidedef first = map.AddSidedef(map.AddLinedef(v0, v1), isFront: true, sector);
        Sidedef second = map.AddSidedef(map.AddLinedef(v1, v2), isFront: true, sector);
        Sidedef third = map.AddSidedef(map.AddLinedef(v2, v3), isFront: true, sector);
        map.BuildIndexes();
        return (map, first, second, third);
    }

    private static (MapSet map, Sector sector, List<Linedef> lines) BuildSectorPolygon(Vector2D[] cwLoop)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var verts = cwLoop.Select(p => map.AddVertex(p)).ToList();
        var lines = new List<Linedef>();
        for (int i = 0; i < verts.Count; i++)
        {
            var l = map.AddLinedef(verts[i], verts[(i + 1) % verts.Count]);
            map.AddSidedef(l, true, sector);
            lines.Add(l);
        }
        map.BuildIndexes();
        return (map, sector, lines);
    }

    [Fact]
    public void TracesSquareLoop()
    {
        // CW square (front inward): (0,0)->(0,100)->(100,100)->(100,0).
        var (map, lines) = BuildPolygon(new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        });

        var path = Tools.FindClosestPath(lines[0], true, turnatends: true);
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count);
        // Every edge of the square is visited exactly once.
        var traced = new HashSet<Linedef>(path.Select(ls => ls.Line));
        Assert.Equal(4, traced.Count);
        foreach (var l in lines) Assert.Contains(l, traced);
    }

    [Fact]
    public void TracedLoopVerticesCloseTheRing()
    {
        var (map, lines) = BuildPolygon(new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        });
        var path = Tools.FindClosestPath(lines[0], true, true)!;
        var verts = Tools.LoopVertices(path);
        Assert.Equal(4, verts.Count);
        Assert.Equal(4, new HashSet<Vertex>(verts).Count); // 4 distinct corners
    }

    [Fact]
    public void FindClosestPathCanStopAtSpecificEndSide()
    {
        var (map, lines) = BuildPolygon(new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        });

        var path = Tools.FindClosestPath(lines[0], true, lines[2], true, turnatends: true);

        Assert.NotNull(path);
        Assert.Equal(3, path!.Count);
        Assert.Equal(new LinedefSide(lines[0], true), path[0]);
        Assert.Equal(new LinedefSide(lines[1], true), path[1]);
        Assert.Equal(new LinedefSide(lines[2], true), path[2]);
    }

    [Fact]
    public void FindClosestPathAddsDistinctEndSideAtDeadEnd()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(50, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, map.AddSector());
        map.BuildIndexes();

        var path = Tools.FindClosestPath(line, true, line, false, turnatends: true);

        Assert.NotNull(path);
        Assert.Equal(new[] { new LinedefSide(line, true), new LinedefSide(line, false) }, path);
    }

    [Fact]
    public void TracesLShapeLoop()
    {
        // 6-vertex L, wound CW so the front faces inward.
        var (map, lines) = BuildPolygon(new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 20), new Vector2D(10, 20),
            new Vector2D(10, 10), new Vector2D(20, 10), new Vector2D(20, 0),
        });
        var path = Tools.FindClosestPath(lines[0], true, true);
        Assert.NotNull(path);
        Assert.Equal(6, path!.Count);
        Assert.Equal(6, new HashSet<Linedef>(path.Select(ls => ls.Line)).Count);
    }

    [Fact]
    public void TraceThenCreateSectorAssignsAllSides()
    {
        // A closed square of sideless lines; tracing from the inside and building a sector should fill it.
        var map = new MapSet();
        var verts = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 100)),
            map.AddVertex(new Vector2D(100, 100)), map.AddVertex(new Vector2D(100, 0)),
        };
        var lines = new List<Linedef>();
        for (int i = 0; i < 4; i++) lines.Add(map.AddLinedef(verts[i], verts[(i + 1) % 4]));
        map.BuildIndexes();

        // Point inside (50,50): find nearest line, side facing the point, trace, build.
        var pos = new Vector2D(50, 50);
        var near = map.NearestLinedef(pos)!;
        bool front = Line2D.GetSideOfLine(near.Start.Position, near.End.Position, pos) <= 0;
        var path = Tools.FindClosestPath(near, front, true)!;
        var sector = SectorBuilder.CreateSectorFromSides(map, path)!;
        map.BuildIndexes();

        Assert.Equal(4, sector.Sidedefs.Count);
        var tri = Triangulation.Create(sector);
        double total = 0;
        for (int i = 0; i < tri.Vertices.Count; i += 3)
            total += TriArea(tri.Vertices[i], tri.Vertices[i + 1], tri.Vertices[i + 2]);
        Assert.Equal(10000.0, total, 1e-6);
    }

    [Fact]
    public void FindPotentialSectorDetectsHole()
    {
        // Outer 100x100 square + inner 20x20 hole at center. A point in the ring should yield outer + inner loops.
        var map = new MapSet();
        var outer = new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        };
        var hole = new[]
        {
            new Vector2D(40, 40), new Vector2D(60, 40), new Vector2D(60, 60), new Vector2D(40, 60),
        };
        void Ring(Vector2D[] pts)
        {
            var vs = pts.Select(p => map.AddVertex(p)).ToList();
            for (int i = 0; i < vs.Count; i++) map.AddLinedef(vs[i], vs[(i + 1) % vs.Count]);
        }
        Ring(outer); Ring(hole);
        map.BuildIndexes();

        var path = Tools.FindPotentialSectorAt(map, new Vector2D(10, 50)); // in the ring, outside the hole
        Assert.NotNull(path);
        // 4 outer + 4 inner = 8 sides.
        Assert.Equal(8, path!.Count);
        var lines = new HashSet<Linedef>(path.Select(ls => ls.Line));
        Assert.Equal(8, lines.Count);
    }

    [Fact]
    public void FindPotentialSectorRetracesOuterWhenNearestLineIsHole()
    {
        // Outer 100x100 square + inner 20x20 hole at center. Clicking just outside the hole edge (nearest
        // line is the hole boundary) must retrace outward and yield the full outer + inner side set.
        var map = new MapSet();
        var outer = new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        };
        var hole = new[]
        {
            new Vector2D(40, 40), new Vector2D(60, 40), new Vector2D(60, 60), new Vector2D(40, 60),
        };
        void Ring(Vector2D[] pts)
        {
            var vs = pts.Select(p => map.AddVertex(p)).ToList();
            for (int i = 0; i < vs.Count; i++) map.AddLinedef(vs[i], vs[(i + 1) % vs.Count]);
        }
        Ring(outer); Ring(hole);
        map.BuildIndexes();

        // Point at (50, 38): nearest line is the hole's bottom edge, but it lies in the ring (the outer sector).
        var path = Tools.FindPotentialSectorAt(map, new Vector2D(50, 38));
        Assert.NotNull(path);
        Assert.Equal(8, path!.Count);
        Assert.Equal(8, new HashSet<Linedef>(path.Select(ls => ls.Line)).Count);
    }

    [Fact]
    public void FindPotentialSectorSimpleSquareHasNoHoles()
    {
        var (map, lines) = BuildPolygon(new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 100), new Vector2D(100, 100), new Vector2D(100, 0),
        });
        var path = Tools.FindPotentialSectorAt(map, new Vector2D(50, 50));
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count);
    }

    [Fact]
    public void MakeSectorCreatesMissingSidedefsWithDefaultOptions()
    {
        var map = new MapSet();
        var lines = BuildSidelessSquare(map);
        var sides = lines.Select(line => new LinedefSide(line, true)).ToList();
        var options = new Tools.SectorCreationOptions
        {
            DefaultFloorHeight = -16,
            DefaultCeilingHeight = 160,
            DefaultBrightness = 192,
            DefaultFloorTexture = "FLOOR",
            DefaultCeilingTexture = "CEIL",
            DefaultMiddleTexture = "WALL",
        };

        Sector? sector = Tools.MakeSector(map, sides, options: options);
        map.BuildIndexes();

        Assert.NotNull(sector);
        Assert.Equal(-16, sector!.FloorHeight);
        Assert.Equal(160, sector.CeilHeight);
        Assert.Equal(192, sector.Brightness);
        Assert.Equal("FLOOR", sector.FloorTexture);
        Assert.Equal("CEIL", sector.CeilTexture);
        Assert.Equal(4, sector.Sidedefs.Count);
        Assert.All(lines, line => Assert.Same(sector, line.Front!.Sector));
        Assert.All(lines, line => Assert.Equal("WALL", line.Front!.MidTexture));
    }

    [Fact]
    public void MakeSectorCopiesSourceSectorAndSidedefDefaultsFromExistingSide()
    {
        var map = new MapSet();
        var lines = BuildSidelessSquare(map);
        Sector source = map.AddSector();
        source.FloorHeight = 24;
        source.CeilHeight = 96;
        source.Brightness = 144;
        source.SetFloorTexture("SRCFLAT");
        source.SetCeilTexture("SRCCEIL");
        Sidedef sourceSide = map.AddSidedef(lines[0], true, source);
        sourceSide.SetTextureMid("SRCWALL");
        map.BuildIndexes();

        Sector? sector = Tools.MakeSector(map, lines.Select(line => new LinedefSide(line, true)).ToList());
        map.BuildIndexes();

        Assert.NotNull(sector);
        Assert.NotSame(source, sector);
        Assert.Equal(24, sector!.FloorHeight);
        Assert.Equal(96, sector.CeilHeight);
        Assert.Equal(144, sector.Brightness);
        Assert.Equal("SRCFLAT", sector.FloorTexture);
        Assert.Equal("SRCCEIL", sector.CeilTexture);
        Assert.All(lines, line => Assert.Same(sector, line.Front!.Sector));
        Assert.All(lines, line => Assert.Equal("SRCWALL", line.Front!.MidTexture));
    }

    [Fact]
    public void MakeSectorAppliesOverridesAfterSourceCopy()
    {
        var map = new MapSet();
        var lines = BuildSidelessSquare(map);
        Sector source = map.AddSector();
        source.FloorHeight = 24;
        source.CeilHeight = 96;
        source.Brightness = 144;
        source.SetFloorTexture("SRCFLAT");
        source.SetCeilTexture("SRCCEIL");
        map.AddSidedef(lines[0], true, source);
        map.BuildIndexes();
        var options = new Tools.SectorCreationOptions
        {
            OverrideFloorTexture = true,
            OverrideCeilingTexture = true,
            OverrideFloorHeight = true,
            OverrideCeilingHeight = true,
            OverrideBrightness = true,
            DefaultFloorTexture = "OVRFLOOR",
            DefaultCeilingTexture = "OVRCEIL",
            CustomFloorHeight = -32,
            CustomCeilingHeight = 192,
            CustomBrightness = 208,
        };

        Sector? sector = Tools.MakeSector(map, lines.Select(line => new LinedefSide(line, true)).ToList(), useOverrides: true, options: options);

        Assert.NotNull(sector);
        Assert.Equal(-32, sector!.FloorHeight);
        Assert.Equal(192, sector.CeilHeight);
        Assert.Equal(208, sector.Brightness);
        Assert.Equal("OVRFLOOR", sector.FloorTexture);
        Assert.Equal("OVRCEIL", sector.CeilTexture);
    }

    [Fact]
    public void MakeSectorUsesNearestSectorWhenOnlyOppositeNearbySideExists()
    {
        var map = new MapSet();
        var lines = BuildSidelessSquare(map);
        Sector nearby = map.AddSector();
        nearby.FloorHeight = 12;
        nearby.CeilHeight = 136;
        nearby.Brightness = 176;
        nearby.SetFloorTexture("NEARFLAT");
        nearby.SetCeilTexture("NEARCEIL");

        Vertex a = map.AddVertex(new Vector2D(96, 0));
        Vertex b = map.AddVertex(new Vector2D(96, 64));
        Linedef nearbyLine = map.AddLinedef(a, b);
        Sidedef opposite = map.AddSidedef(nearbyLine, isFront: false, nearby);
        opposite.SetTextureMid("NEARWALL");
        map.BuildIndexes();

        Sector? sector = Tools.MakeSector(
            map,
            lines.Select(line => new LinedefSide(line, true)).ToList(),
            new[] { nearbyLine });
        map.BuildIndexes();

        Assert.NotNull(sector);
        Assert.Equal(12, sector!.FloorHeight);
        Assert.Equal(136, sector.CeilHeight);
        Assert.Equal(176, sector.Brightness);
        Assert.Equal("NEARFLAT", sector.FloorTexture);
        Assert.Equal("NEARCEIL", sector.CeilTexture);
        Assert.All(lines, line => Assert.Equal("NEARWALL", line.Front!.MidTexture));
    }

    [Fact]
    public void MakeSectorClampsInvalidCopiedCeilingWhenOverridesAreOff()
    {
        var map = new MapSet();
        var lines = BuildSidelessSquare(map);
        Sector source = map.AddSector();
        source.FloorHeight = 128;
        source.CeilHeight = 64;
        map.AddSidedef(lines[0], true, source);
        map.BuildIndexes();

        Sector? sector = Tools.MakeSector(map, lines.Select(line => new LinedefSide(line, true)).ToList(), useOverrides: false);

        Assert.NotNull(sector);
        Assert.Equal(128, sector!.FloorHeight);
        Assert.Equal(128, sector.CeilHeight);
    }

    [Fact]
    public void JoinSectorCreatesMissingSidedefsOnExistingSector()
    {
        var map = new MapSet();
        var vertices = new[]
        {
            new Vector2D(0, 0), new Vector2D(0, 64), new Vector2D(64, 64), new Vector2D(64, 0),
        }.Select(p => map.AddVertex(p)).ToList();
        var lines = new List<Linedef>();
        for (int i = 0; i < vertices.Count; i++)
            lines.Add(map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Count]));

        Sector sector = map.AddSector();
        Sidedef original = map.AddSidedef(lines[0], true, sector);
        original.SetTextureMid("STONE");

        var sides = lines.Select(line => new LinedefSide(line, true)).ToList();
        Sector? joined = Tools.JoinSector(map, sides, original, defaultMiddleTexture: "STARTAN");
        map.BuildIndexes();

        Assert.Same(sector, joined);
        Assert.All(lines, line => Assert.Same(sector, line.Front!.Sector));
        Assert.Equal(4, sector.Sidedefs.Count);
        Assert.All(lines.Select(line => line.Front!), side => Assert.Equal("STONE", side.MidTexture));
    }

    [Fact]
    public void JoinSectorCreatesTwoSidedGapsAndClearsOppositeMiddleTexture()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(a, b);
        Sector otherSector = map.AddSector();
        Sector sourceSector = map.AddSector();
        sourceSector.FloorHeight = -16;
        sourceSector.CeilHeight = 160;
        Sidedef other = map.AddSidedef(line, true, otherSector);
        other.SetTextureMid("OLDMID");

        var sourceLine = map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(192, 0)));
        Sidedef original = map.AddSidedef(sourceLine, true, sourceSector);
        original.SetTextureHigh("UPPER");
        original.SetTextureMid("MIDDLE");
        original.SetTextureLow("LOWER");
        map.BuildIndexes();

        Sector? joined = Tools.JoinSector(map, new[] { new LinedefSide(line, false) }, original);
        map.BuildIndexes();

        Assert.Same(sourceSector, joined);
        Assert.Same(sourceSector, line.Back!.Sector);
        Assert.Equal("UPPER", line.Back.HighTexture);
        Assert.Equal("-", line.Back.MidTexture);
        Assert.Equal("LOWER", line.Back.LowTexture);
        Assert.Equal("-", other.MidTexture);
    }

    private static double TriArea(Vector2D a, Vector2D b, Vector2D c)
        => System.Math.Abs(a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) * 0.5;

    [Fact]
    public void OpenGeometryTurnsAtDeadEnds()
    {
        // A single linedef (no closed loop). With turnatends it traces both sides and returns to start.
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(50, 0));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, true, map.AddSector());
        map.BuildIndexes();

        var path = Tools.FindClosestPath(l, true, turnatends: true);
        Assert.NotNull(path);
        // Front then back of the same line forms the (degenerate) closed walk.
        Assert.Equal(2, path!.Count);
        Assert.True(path[0].Front);
        Assert.False(path[1].Front);
    }

    private static List<Linedef> BuildSidelessSquare(MapSet map)
    {
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)),
            map.AddVertex(new Vector2D(64, 0)),
        };
        var lines = new List<Linedef>();
        for (int i = 0; i < vertices.Length; i++)
            lines.Add(map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]));
        map.BuildIndexes();
        return lines;
    }
}
