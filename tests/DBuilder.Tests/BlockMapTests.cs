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
    public void GetSectorAtMatchesMapSetNearestLinedefRule()
    {
        var map = new MapSet();
        var left = map.AddSector();
        var right = map.AddSector();
        var a = map.AddVertex(new Vector2D(50, 0));
        var b = map.AddVertex(new Vector2D(50, 100));
        var divider = map.AddLinedef(a, b);
        map.AddSidedef(divider, true, right);
        map.AddSidedef(divider, false, left);
        map.BuildIndexes();
        var bm = new BlockMap(map, 32);

        Assert.Same(map.GetSectorAt(new Vector2D(60, 50)), bm.GetSectorAt(new Vector2D(60, 50)));
        Assert.Same(map.GetSectorAt(new Vector2D(40, 50)), bm.GetSectorAt(new Vector2D(40, 50)));
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
    public void NearestLinedefRangeLargeRangeMatchesBoundedNearestDistance()
    {
        var map = RandomMap(5150);
        var bm = new BlockMap(map, 128);
        var rng = new Random(44);

        for (int i = 0; i < 300; i++)
        {
            var p = new Vector2D(rng.Next(-300, 2300), rng.Next(-300, 2300));
            double range = rng.Next(129, 380);
            var brute = map.NearestLinedef(p, range);
            var fast = bm.NearestLinedefRange(p, range);

            if (brute == null)
            {
                Assert.Null(fast);
                continue;
            }

            Assert.NotNull(fast);
            Assert.Equal(SegDistSq(brute, p), SegDistSq(fast!, p), 6);
        }
    }

    [Fact]
    public void NearestLinedefRangeSmallRangeUsesCenterAndCornerBlocks()
    {
        var map = new MapSet();
        var near = map.AddLinedef(map.AddVertex(new Vector2D(60, 60)), map.AddVertex(new Vector2D(60, 70)));
        map.AddLinedef(map.AddVertex(new Vector2D(120, 60)), map.AddVertex(new Vector2D(120, 70)));
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        var found = bm.NearestLinedefRange(new Vector2D(63, 63), 8);

        Assert.Same(near, found);
    }

    [Fact]
    public void NearestLinedefRangeReturnsNullForNegativeRange()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(100, 0)));
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Null(bm.NearestLinedefRange(new Vector2D(0, 0), -1));
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

    [Fact]
    public void CellHelpersExposeCoordinatesRangeCenterAndContents()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(10, 10));
        var b = map.AddVertex(new Vector2D(140, 10));
        var line = map.AddLinedef(a, b);
        var thing = map.AddThing(new Vector2D(20, 20), 3001);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Equal((0, 0), bm.GetCellCoordinates(new Vector2D(10, 10)));
        Assert.Equal((1, 0), bm.GetCellCoordinates(new Vector2D(74, 10)));
        Assert.True(bm.IsCellInRange(0, 0));
        Assert.True(bm.IsInRange(new Vector2D(20, 20)));
        Assert.False(bm.IsCellInRange(-1, 0));
        Assert.False(bm.IsInRange(new Vector2D(-1000, -1000)));

        Assert.Equal(new Vector2D(42, 42), bm.GetCellCenter(0, 0));
        Assert.Contains(line, bm.GetLinedefsAt(0, 0));
        Assert.Contains(a, bm.GetVerticesAt(0, 0));
        Assert.Contains(thing, bm.GetThingsAt(0, 0));
        Assert.Empty(bm.GetLinedefsAt(-1, 0));
    }

    [Fact]
    public void SectorCellsUseSectorBounds()
    {
        var (map, sector) = SquareSector(0, 0, 128);
        var other = map.AddSector();
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Contains(sector, bm.GetSectorsAt(0, 0));
        Assert.Contains(sector, bm.GetSectorsAt(1, 1));
        Assert.Contains(sector, bm.GetSectorsAt(2, 2));
        Assert.DoesNotContain(other, bm.GetSectorsAt(0, 0));
    }

    [Fact]
    public void SectorRangeQueryReturnsDistinctSectors()
    {
        var (map, first) = SquareSector(0, 0, 64);
        var second = AddSquareSector(map, 96, 0, 64);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        var sectors = bm.GetSectorsNear(new Vector2D(80, 32), 96);

        Assert.Contains(first, sectors);
        Assert.Contains(second, sectors);
        Assert.Equal(2, sectors.Count);
    }

    [Fact]
    public void ContainingSectorsFiltersCellCandidatesByPolygon()
    {
        var (map, sector) = SquareSector(0, 0, 128);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Equal(new[] { sector }, bm.GetContainingSectors(new Vector2D(32, 32)));
        Assert.Empty(bm.GetContainingSectors(new Vector2D(200, 32)));
    }

    [Fact]
    public void ContainingSectorReturnsSingleMatchingSector()
    {
        var (map, sector) = SquareSector(0, 0, 128);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        Assert.Same(sector, bm.GetContainingSector(new Vector2D(64, 64)));
        Assert.Null(bm.GetContainingSector(new Vector2D(-16, 64)));
    }

    [Fact]
    public void CellRangeCropsToBlockMapBounds()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 128)));
        map.BuildIndexes();

        var bm = new BlockMap(map, 64);

        var cells = bm.GetCellRange(-64, -64, 160, 160);

        Assert.Equal(new[] { (0, 0), (0, 1), (1, 0), (1, 1) }, cells);
    }

    [Fact]
    public void LineCellCoordinatesExposeTraversalOrder()
    {
        var map = new MapSet();
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 128)));
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        var cells = bm.GetLineCellCoordinates(new Vector2D(0, 0), new Vector2D(128, 128));

        Assert.Equal(new[] { (0, 0), (0, 1), (1, 1), (1, 2), (2, 2) }, cells);
    }

    [Fact]
    public void LinedefCellsFollowCrossedBlocksInsteadOfBoundingBox()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 128)));
        map.BuildIndexes();

        var bm = new BlockMap(map, 64);

        Assert.Contains(line, bm.GetLinedefsAt(0, 0));
        Assert.Contains(line, bm.GetLinedefsAt(1, 1));
        Assert.Contains(line, bm.GetLinedefsAt(2, 2));
        Assert.DoesNotContain(line, bm.GetLinedefsAt(1, 0));
        Assert.DoesNotContain(line, bm.GetLinedefsAt(2, 0));
        Assert.DoesNotContain(line, bm.GetLinedefsAt(0, 2));
    }

    [Fact]
    public void HorizontalLinedefCellsIncludeOnlyTraversedRow()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(160, 0)));
        map.AddVertex(new Vector2D(0, 96));
        map.BuildIndexes();

        var bm = new BlockMap(map, 64);

        Assert.Contains(line, bm.GetLinedefsAt(0, 0));
        Assert.Contains(line, bm.GetLinedefsAt(1, 0));
        Assert.Contains(line, bm.GetLinedefsAt(2, 0));
        Assert.Empty(bm.GetLinedefsAt(0, 1));
        Assert.Empty(bm.GetLinedefsAt(1, 1));
        Assert.Empty(bm.GetLinedefsAt(2, 1));
    }

    private static (MapSet Map, Sector Sector) SquareSector(double x, double y, double size)
    {
        var map = new MapSet();
        var sector = AddSquareSector(map, x, y, size);
        return (map, sector);
    }

    private static Sector AddSquareSector(MapSet map, double x, double y, double size)
    {
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(x, y));
        var v1 = map.AddVertex(new Vector2D(x + size, y));
        var v2 = map.AddVertex(new Vector2D(x + size, y + size));
        var v3 = map.AddVertex(new Vector2D(x, y + size));

        map.AddSidedef(map.AddLinedef(v1, v0), true, sector);
        map.AddSidedef(map.AddLinedef(v0, v3), true, sector);
        map.AddSidedef(map.AddLinedef(v3, v2), true, sector);
        map.AddSidedef(map.AddLinedef(v2, v1), true, sector);
        return sector;
    }
}
