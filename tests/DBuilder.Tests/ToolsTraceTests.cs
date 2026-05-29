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
        return (map, lines);
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
}
