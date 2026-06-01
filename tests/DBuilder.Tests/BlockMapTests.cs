// ABOUTME: Tests for BlockMap - the uniform-grid spatial index, validated against MapSet brute-force queries.
// ABOUTME: Builds a seeded pseudo-random map and asserts identical nearest distances for many sample points.

using System;
using System.Drawing;
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
        return l.SafeDistanceToSq(p, bounded: true);
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
    public void GetBlockAtReturnsCellContentsForPosition()
    {
        var (map, sector) = SquareSector(0, 0, 64);
        var thing = map.AddThing(new Vector2D(20, 20), 3001);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        var cell = bm.GetBlockAt(new Vector2D(20, 20));

        Assert.NotNull(cell);
        Assert.Contains(thing, cell.Value.Things);
        Assert.Contains(sector, cell.Value.Sectors);
        Assert.Contains(map.Vertices[0], cell.Value.Vertices);
        Assert.NotEmpty(cell.Value.Lines);
        Assert.Null(bm.GetBlockAt(new Vector2D(-1000, -1000)));
    }

    [Fact]
    public void GetBlocksReturnsPointBlockWhenNonEmpty()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var thing = new Thing(new Vector2D(20, 20), 3001);
        bm.AddThing(thing);

        var blocks = bm.GetBlocks(new Vector2D(20, 20));

        var block = Assert.Single(blocks);
        Assert.Contains(thing, block.Things);
        Assert.Empty(bm.GetBlocks(new Vector2D(96, 96)));
        Assert.Empty(bm.GetBlocks(new Vector2D(-1, -1)));
    }

    [Fact]
    public void GetBlocksRectangleUsesUdbFloorAndRightPlusOneBounds()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var edgeThing = new Thing(new Vector2D(64, 16), 3001);
        var outside = new Thing(new Vector2D(96, 96), 3001);
        bm.AddThings(new[] { edgeThing, outside });

        var blocks = bm.GetBlocks(new RectangleF(0, 0, 64, 16));

        Assert.Contains(blocks, block => block.Things.Contains(edgeThing));
        Assert.DoesNotContain(blocks, block => block.Things.Contains(outside));
    }

    [Fact]
    public void GetLineBlocksUsesBoundingRectangleLikeVisualBlockMap()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var diagonalCell = new Thing(new Vector2D(96, 96), 3001);
        var boundingOnlyCell = new Thing(new Vector2D(96, 16), 3001);
        bm.AddThings(new[] { diagonalCell, boundingOnlyCell });

        var blocks = bm.GetLineBlocks(new Vector2D(0, 0), new Vector2D(128, 128));
        var traversed = bm.GetLineCellCoordinates(new Vector2D(0, 0), new Vector2D(128, 128));

        Assert.Contains(blocks, block => block.Things.Contains(diagonalCell));
        Assert.Contains(blocks, block => block.Things.Contains(boundingOnlyCell));
        Assert.DoesNotContain((1, 0), traversed);
    }

    [Fact]
    public void GetFrustumBlocksReturnsVisibleNonEmptyBlocks()
    {
        var bm = new BlockMap(new RectangleF(-128, -128, 256, 256), 64);
        var inside = new Thing(new Vector2D(0, -64), 3001);
        var behind = new Thing(new Vector2D(0, 96), 3001);
        bm.AddThings(new[] { inside, behind });
        var frustum = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 8, far: 128, fov: (float)(Math.PI / 2));

        var blocks = bm.GetFrustumBlocks(frustum);

        Assert.Contains(blocks, block => block.Things.Contains(inside));
        Assert.DoesNotContain(blocks, block => block.Things.Contains(behind));
    }

    [Fact]
    public void ClearRemovesIndexedContentsButKeepsRange()
    {
        var (map, _) = SquareSector(0, 0, 64);
        map.AddThing(new Vector2D(20, 20), 3001);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        bm.Clear();

        Assert.True(bm.IsInRange(new Vector2D(20, 20)));
        var cell = bm.GetBlockAt(new Vector2D(20, 20));
        Assert.NotNull(cell);
        Assert.Empty(cell.Value.Lines);
        Assert.Empty(cell.Value.Things);
        Assert.Empty(cell.Value.Sectors);
        Assert.Empty(cell.Value.Vertices);
    }

    [Fact]
    public void ExplicitRangeConstructorCreatesEmptyBlockMapForPopulation()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var thing = new Thing(new Vector2D(32, 32), 3001);
        var outside = new Thing(new Vector2D(256, 256), 3001);

        bm.AddThings(new[] { thing, outside });

        Assert.Equal(3, bm.Columns);
        Assert.Equal(3, bm.Rows);
        Assert.Same(thing, bm.NearestThing(new Vector2D(30, 30)));
        Assert.DoesNotContain(outside, bm.GetThingsNear(new Vector2D(128, 128), 256));
    }

    [Fact]
    public void IsInRangeUsesExclusiveWorldRange()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);

        Assert.True(bm.IsInRange(new Vector2D(0, 0)));
        Assert.True(bm.IsInRange(new Vector2D(127.999, 127.999)));
        Assert.False(bm.IsInRange(new Vector2D(128, 64)));
        Assert.False(bm.IsInRange(new Vector2D(64, 128)));
        Assert.NotNull(bm.GetBlockAt(new Vector2D(128, 64)));
    }

    [Fact]
    public void AddMethodsPopulateExistingBlockMapRange()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0));
        map.AddVertex(new Vector2D(128, 128));
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        var vertex = new Vertex(new Vector2D(32, 32));
        var thing = new Thing(new Vector2D(96, 96), 3001);
        var line = new Linedef(new Vertex(new Vector2D(0, 64)), new Vertex(new Vector2D(128, 64)));
        var sectorMap = new MapSet();
        var sector = AddSquareSector(sectorMap, 32, 32, 32);
        sectorMap.BuildIndexes();

        bm.AddVertex(vertex);
        bm.AddThing(thing);
        bm.AddLinedef(line);
        bm.AddSector(sector);

        Assert.Contains(vertex, bm.GetVerticesAt(0, 0));
        Assert.Contains(thing, bm.GetThingsAt(1, 1));
        Assert.Contains(line, bm.GetLinedefsAt(0, 1));
        Assert.Contains(line, bm.GetLinedefsAt(1, 1));
        Assert.Contains(line, bm.GetLinedefsAt(2, 1));
        Assert.Contains(sector, bm.GetSectorsAt(0, 0));
    }

    [Fact]
    public void AddThingIndexesEditorRadiusLikeVisualBlockMap()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var thing = new Thing(new Vector2D(63, 32), 3001) { Size = 8 };

        bm.AddThing(thing);

        Assert.Contains(thing, bm.GetThingsAt(0, 0));
        Assert.Contains(thing, bm.GetThingsAt(1, 0));
        Assert.Contains(thing, bm.GetThingsNear(new Vector2D(70, 32), 1));
    }

    [Fact]
    public void AddThingCropsRadiusToExistingBlockMapRange()
    {
        var bm = new BlockMap(new RectangleF(0, 0, 128, 128), 64);
        var partiallyInside = new Thing(new Vector2D(-4, 32), 3001) { Size = 8 };
        var outside = new Thing(new Vector2D(-32, 32), 3001) { Size = 8 };

        bm.AddThings(new[] { partiallyInside, outside });

        Assert.Contains(partiallyInside, bm.GetThingsAt(0, 0));
        Assert.DoesNotContain(outside, bm.GetThingsAt(0, 0));
    }

    [Fact]
    public void AddMethodsIgnoreItemsOutsideExistingBlockMapRange()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0));
        map.AddVertex(new Vector2D(128, 128));
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);
        var vertex = new Vertex(new Vector2D(1000, 1000));
        var thing = new Thing(new Vector2D(1000, 1000), 3001);
        var line = new Linedef(new Vertex(new Vector2D(1000, 1000)), new Vertex(new Vector2D(1100, 1100)));
        var sectorMap = new MapSet();
        var sector = AddSquareSector(sectorMap, 1000, 1000, 64);
        sectorMap.BuildIndexes();

        bm.AddVertices(new[] { vertex });
        bm.AddThings(new[] { thing });
        bm.AddLinedefs(new[] { line });
        bm.AddSectors(new[] { sector });

        Assert.DoesNotContain(vertex, bm.GetVerticesNear(new Vector2D(128, 128), 256));
        Assert.DoesNotContain(thing, bm.GetThingsNear(new Vector2D(128, 128), 256));
        Assert.DoesNotContain(line, bm.GetLinedefsNear(new Vector2D(128, 128), 256));
        Assert.DoesNotContain(sector, bm.GetSectorsNear(new Vector2D(128, 128), 256));
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
    public void ThingDetermineSectorUsesBlockMapContainingSector()
    {
        var (map, sector) = SquareSector(0, 0, 128);
        var inside = map.AddThing(new Vector2D(64, 64), 3001);
        var outside = map.AddThing(new Vector2D(-16, 64), 3001);
        map.BuildIndexes();
        var bm = new BlockMap(map, 64);

        inside.DetermineSector(bm);
        outside.DetermineSector(bm);

        Assert.Same(sector, inside.Sector);
        Assert.Null(outside.Sector);
    }

    [Fact]
    public void ThingDetermineSectorUsesMapSetSectorQuery()
    {
        var (map, sector) = SquareSector(0, 0, 128);
        var thing = map.AddThing(new Vector2D(64, 64), 3001);
        map.BuildIndexes();

        thing.DetermineSector(map);

        Assert.Same(sector, thing.Sector);
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
