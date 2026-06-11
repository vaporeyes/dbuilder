// ABOUTME: Tests for the centroid-fan fallback that kicks in when the main trace+cut+earclip yields no triangles.
// ABOUTME: Constructs sectors with intentionally pathological topology to force the fallback path.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class TriangulationFallbackTests
{
    [Fact]
    public void ConvexSectorStillUsesMainAlgorithm()
    {
        // Square sector wound CW around its interior - main algorithm handles this trivially.
        var (_, sector) = BuildCwSquare(100);
        var tri = Triangulation.Create(sector);
        Assert.False(tri.IsApproximate);
        Assert.Equal(6, tri.Vertices.Count); // 2 triangles
    }

    [Fact]
    public void SectorWithUntraceableTopologyUsesFallback()
    {
        // Construct a sector where the boundary doesn't close: 4 linedefs sharing a sector but
        // the sidedefs aren't wound in a way the trace can close (front-facing-out instead of front-facing-in).
        var map = new MapSet();
        var sector = new Sector { Index = 0 };
        map.Sectors.Add(sector);

        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        var v2 = new Vertex(new Vector2D(10, 10));
        var v3 = new Vertex(new Vector2D(0, 10));
        map.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        // Wind CCW (front faces outward) - the main trace algorithm relies on CW orientation.
        Linedef Make(Vertex a, Vertex b)
        {
            var l = new Linedef(a, b);
            var sd = new Sidedef(l, true) { Sector = sector };
            l.Front = sd;
            map.Sidedefs.Add(sd);
            map.Linedefs.Add(l);
            return l;
        }
        Make(v0, v1); Make(v1, v2); Make(v2, v3); Make(v3, v0);

        map.BuildIndexes();
        var tri = Triangulation.Create(sector);

        // Even though the main algorithm bails on wrong winding, the fallback produces some triangles
        // from the unique vertex set so the viewer can still render the sector.
        Assert.NotEmpty(tri.Vertices);
        // The fallback flag should signal the consumer that this geometry is approximate.
        // For an actually-convex shape with bad winding, the fan from centroid still covers the square exactly.
    }

    [Fact]
    public void SectorWithNoSidedefsProducesNoTriangles()
    {
        var sector = new Sector { Index = 0 };
        var tri = Triangulation.Create(sector);
        Assert.Empty(tri.Vertices);
        Assert.False(tri.IsApproximate);
    }

    [Fact]
    public void FallbackIgnoresSameSectorTwoSidedReferences()
    {
        var map = new MapSet();
        var sector = new Sector { Index = 0 };
        map.Sectors.Add(sector);
        var a = new Vertex(new Vector2D(0, 0));
        var b = new Vertex(new Vector2D(64, 0));
        var c = new Vertex(new Vector2D(64, 64));
        map.Vertices.AddRange(new[] { a, b, c });

        void AddInternal(Vertex start, Vertex end)
        {
            var line = new Linedef(start, end);
            var front = new Sidedef(line, true) { Sector = sector };
            var back = new Sidedef(line, false) { Sector = sector };
            line.AttachFront(front);
            line.AttachBack(back);
            map.Linedefs.Add(line);
            map.Sidedefs.Add(front);
            map.Sidedefs.Add(back);
        }

        AddInternal(a, b);
        AddInternal(b, c);
        AddInternal(c, a);
        map.BuildIndexes();

        var tri = Triangulation.Create(sector);

        Assert.Empty(tri.Vertices);
        Assert.False(tri.IsApproximate);
    }

    [Fact]
    public void FallbackEmitsThreeVerticesPerTriangle()
    {
        // Force the fallback with a CCW-wound square (same as above).
        var map = new MapSet();
        var sector = new Sector { Index = 0 };
        map.Sectors.Add(sector);
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(20, 0));
        var v2 = new Vertex(new Vector2D(20, 20));
        var v3 = new Vertex(new Vector2D(0, 20));
        map.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        Linedef Make(Vertex a, Vertex b)
        {
            var l = new Linedef(a, b);
            var sd = new Sidedef(l, true) { Sector = sector };
            l.Front = sd;
            map.Sidedefs.Add(sd);
            map.Linedefs.Add(l);
            return l;
        }
        Make(v0, v1); Make(v1, v2); Make(v2, v3); Make(v3, v0);
        map.BuildIndexes();

        var tri = Triangulation.Create(sector);
        Assert.True(tri.IsApproximate);
        Assert.Equal(0, tri.Vertices.Count % 3); // valid triangle list
        Assert.True(tri.Vertices.Count >= 12);   // at least 4 fan triangles from 4 unique verts
    }

    [Fact]
    public void FallbackFanArea_ApproximatesConvexShape()
    {
        // For a convex shape, the centroid fan recovers exactly the polygon area.
        var map = new MapSet();
        var sector = new Sector { Index = 0 };
        map.Sectors.Add(sector);
        // Triangle wound CCW (forces fallback).  Vertices at (0,0), (30,0), (15,30).
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(30, 0));
        var v2 = new Vertex(new Vector2D(15, 30));
        map.Vertices.AddRange(new[] { v0, v1, v2 });
        Linedef Make(Vertex a, Vertex b)
        {
            var l = new Linedef(a, b);
            var sd = new Sidedef(l, true) { Sector = sector };
            l.Front = sd;
            map.Sidedefs.Add(sd);
            map.Linedefs.Add(l);
            return l;
        }
        Make(v0, v1); Make(v1, v2); Make(v2, v0);
        map.BuildIndexes();

        var tri = Triangulation.Create(sector);
        Assert.True(tri.IsApproximate);

        // Triangle area = 0.5 * base * height = 0.5 * 30 * 30 = 450.
        double total = 0;
        for (int i = 0; i < tri.Vertices.Count; i += 3)
            total += TriangleArea(tri.Vertices[i], tri.Vertices[i + 1], tri.Vertices[i + 2]);
        Assert.Equal(450.0, total, 1e-6);
    }

    private static (MapSet map, Sector sector) BuildCwSquare(double size)
    {
        var map = new MapSet();
        var sector = new Sector { Index = 0 };
        map.Sectors.Add(sector);
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(size, 0));
        var v2 = new Vertex(new Vector2D(size, size));
        var v3 = new Vertex(new Vector2D(0, size));
        map.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        Linedef Make(Vertex a, Vertex b)
        {
            var l = new Linedef(a, b);
            var sd = new Sidedef(l, true) { Sector = sector };
            l.Front = sd;
            map.Sidedefs.Add(sd);
            map.Linedefs.Add(l);
            return l;
        }
        // CW winding (front faces inward): south W<-E, west S->N, north W->E, east N->S.
        Make(v1, v0); Make(v0, v3); Make(v3, v2); Make(v2, v1);
        map.BuildIndexes();
        return (map, sector);
    }

    private static double TriangleArea(Vector2D a, Vector2D b, Vector2D c)
        => System.Math.Abs(a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) * 0.5;
}
