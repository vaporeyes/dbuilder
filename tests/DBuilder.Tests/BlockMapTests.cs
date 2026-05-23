// ABOUTME: Tests for BlockMap - the uniform-grid spatial index, validated against MapSet brute-force queries.
// ABOUTME: Builds a seeded pseudo-random map and asserts identical nearest distances for many sample points.

using System;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class BlockMapTests
{
    private static MapSet RandomMap(int seed)
    {
        var rng = new Random(seed);
        var map = new MapSet();
        var verts = new System.Collections.Generic.List<Vertex>();
        for (int i = 0; i < 80; i++)
            verts.Add(map.AddVertex(new Vector2D(rng.Next(0, 2000), rng.Next(0, 2000))));
        for (int i = 0; i < 120; i++)
        {
            var a = verts[rng.Next(verts.Count)];
            var b = verts[rng.Next(verts.Count)];
            if (!ReferenceEquals(a, b)) map.AddLinedef(a, b);
        }
        for (int i = 0; i < 40; i++)
            map.AddThing(new Vector2D(rng.Next(-200, 2200), rng.Next(-200, 2200)), 1);
        map.BuildIndexes();
        return map;
    }

    private static double SegDistSq(Linedef l, Vector2D p)
    {
        var a = l.Start.Position; var b = l.End.Position;
        return Line2D.GetDistanceToLineSq(a, b, p, bounded: true);
    }

    [Fact]
    public void NearestLinedefMatchesBruteForce()
    {
        var map = RandomMap(1234);
        var bm = new BlockMap(map, 128);
        var rng = new Random(99);
        for (int i = 0; i < 500; i++)
        {
            var p = new Vector2D(rng.Next(-300, 2300), rng.Next(-300, 2300));
            var brute = map.NearestLinedef(p);
            var fast = bm.NearestLinedef(p);
            Assert.NotNull(brute);
            Assert.NotNull(fast);
            // Ties may pick different lines; the minimal distance must agree.
            Assert.Equal(SegDistSq(brute!, p), SegDistSq(fast!, p), 6);
        }
    }

    [Fact]
    public void NearestThingMatchesBruteForce()
    {
        var map = RandomMap(555);
        var bm = new BlockMap(map, 96);
        var rng = new Random(7);
        for (int i = 0; i < 300; i++)
        {
            var p = new Vector2D(rng.Next(-400, 2400), rng.Next(-400, 2400));
            var brute = map.NearestThing(p)!;
            var fast = bm.NearestThing(p)!;
            double db = (brute.Position - p).GetLengthSq();
            double df = (fast.Position - p).GetLengthSq();
            Assert.Equal(db, df, 6);
        }
    }

    [Fact]
    public void NearestVertexMatchesBruteForce()
    {
        var map = RandomMap(42);
        var bm = new BlockMap(map, 200);
        var rng = new Random(8675309);
        for (int i = 0; i < 300; i++)
        {
            var p = new Vector2D(rng.Next(-100, 2100), rng.Next(-100, 2100));
            var brute = map.NearestVertex(p)!;
            var fast = bm.NearestVertex(p)!;
            double db = (brute.Position - p).GetLengthSq();
            double df = (fast.Position - p).GetLengthSq();
            Assert.Equal(db, df, 6);
        }
    }

    [Fact]
    public void MaxRangeExcludesDistantGeometry()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, b);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Null(bm.NearestLinedef(new Vector2D(50, 5000), maxRange: 100));
        Assert.NotNull(bm.NearestLinedef(new Vector2D(50, 10), maxRange: 100));
    }

    [Fact]
    public void RangeQueryReturnsSupersetOfNearest()
    {
        var map = RandomMap(2024);
        var bm = new BlockMap(map, 128);
        var p = new Vector2D(1000, 1000);
        var nearest = bm.NearestLinedef(p)!;
        // A range covering the nearest line's distance (plus a cell) guarantees its bucket is queried.
        double range = Math.Sqrt(SegDistSq(nearest, p)) + bm.BlockSize;
        var near = bm.GetLinedefsNear(p, range);
        Assert.Contains(nearest, near);
    }

    [Fact]
    public void EmptyMapYieldsNoResults()
    {
        var map = new MapSet();
        var bm = new BlockMap(map);
        Assert.Null(bm.NearestLinedef(new Vector2D(0, 0)));
        Assert.Null(bm.NearestThing(new Vector2D(0, 0)));
        Assert.Null(bm.NearestVertex(new Vector2D(0, 0)));
    }

    [Fact]
    public void OriginAndOccupancyForOverlay()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(10, 10));
        var b = map.AddVertex(new Vector2D(40, 40)); // both inside the first 128-block
        map.AddLinedef(a, b);
        map.BuildIndexes();
        var bm = new BlockMap(map, 128);

        Assert.Equal(10, bm.OriginX, 6); // origin = min bounds
        Assert.Equal(10, bm.OriginY, 6);
        Assert.True(bm.LinedefCountAt(0, 0) >= 1); // the line occupies block (0,0)
        Assert.Equal(0, bm.LinedefCountAt(99, 99)); // out of range -> 0
    }
}
