// ABOUTME: Tests for Tools.FindClosestPath - tracing a closed loop of linedef sides from a start line+side.
// ABOUTME: Uses known shapes (square, L-shape) and asserts the traced loop length, closure, and vertex set.

using System.Collections.Generic;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ToolsTraceTests
{
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
