// ABOUTME: Tests for SectorBuilder.CreateSector - building a sector from an ordered vertex loop.
// ABOUTME: Verifies linedef/sidedef creation, interior-facing front (via triangulation area), winding normalization, and reuse.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorBuilderTests
{
    private static List<Vertex> Square(MapSet map, double size, bool ccw)
    {
        // CCW order in math y-up: (0,0)->(size,0)->(size,size)->(0,size).
        var vs = new List<Vertex>
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(size, 0)),
            map.AddVertex(new Vector2D(size, size)),
            map.AddVertex(new Vector2D(0, size)),
        };
        if (!ccw) vs.Reverse();
        return vs;
    }

    [Fact]
    public void CreatesLinedefsAndSidedefsForLoop()
    {
        var map = new MapSet();
        var loop = Square(map, 100, ccw: true);
        var sector = SectorBuilder.CreateSector(map, loop);
        map.BuildIndexes();

        Assert.NotNull(sector);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Equal(4, map.Sidedefs.Count);
        Assert.Equal(4, sector!.Sidedefs.Count);
        Assert.All(map.Sidedefs, sd => Assert.Same(sector, sd.Sector));
    }

    [Fact]
    public void FrontFacesInteriorSoItTriangulates()
    {
        var map = new MapSet();
        var sector = SectorBuilder.CreateSector(map, Square(map, 100, ccw: true))!;
        map.BuildIndexes();

        // If the front faces inward, the sector triangulates to its true area (100x100 = 10000).
        var tri = Triangulation.Create(sector);
        double total = 0;
        for (int i = 0; i < tri.Vertices.Count; i += 3)
            total += TriArea(tri.Vertices[i], tri.Vertices[i + 1], tri.Vertices[i + 2]);
        Assert.Equal(10000.0, total, 1e-6);
        Assert.False(tri.IsApproximate);
    }

    [Fact]
    public void ClockwiseInputAlsoFacesInterior()
    {
        var map = new MapSet();
        var sector = SectorBuilder.CreateSector(map, Square(map, 80, ccw: false))!;
        map.BuildIndexes();
        var tri = Triangulation.Create(sector);
        double total = 0;
        for (int i = 0; i < tri.Vertices.Count; i += 3)
            total += TriArea(tri.Vertices[i], tri.Vertices[i + 1], tri.Vertices[i + 2]);
        Assert.Equal(6400.0, total, 1e-6); // 80*80
    }

    [Fact]
    public void ReusesExistingLinedefs()
    {
        var map = new MapSet();
        var loop = Square(map, 100, ccw: true);
        // Pre-create one edge of the loop (v0->v1).
        var pre = map.AddLinedef(loop[0], loop[1]);
        map.BuildIndexes();

        SectorBuilder.CreateSector(map, loop);
        map.BuildIndexes();

        Assert.Equal(4, map.Linedefs.Count); // pre + 3 new, not 5
        Assert.Contains(pre, map.Linedefs);
    }

    [Fact]
    public void TooFewVerticesReturnsNull()
    {
        var map = new MapSet();
        var two = new List<Vertex> { map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(10, 0)) };
        Assert.Null(SectorBuilder.CreateSector(map, two));
    }

    [Fact]
    public void CopyFromAppliesProperties()
    {
        var map = new MapSet();
        var src = new Sector { FloorHeight = 8, CeilHeight = 120, FloorTexture = "FLOOR4_8", CeilTexture = "CEIL3_5", Brightness = 144, Special = 9 };
        var sector = SectorBuilder.CreateSector(map, Square(map, 50, ccw: true), src)!;
        Assert.Equal(8, sector.FloorHeight);
        Assert.Equal(120, sector.CeilHeight);
        Assert.Equal("FLOOR4_8", sector.FloorTexture);
        Assert.Equal(144, sector.Brightness);
        Assert.Equal(9, sector.Special);
    }

    [Fact]
    public void CopyFromPreservesTagsSlopesAndFields()
    {
        var map = new MapSet();
        var src = new Sector
        {
            FloorSlope = new Vector3D(1, 0, 2),
            FloorSlopeOffset = 16,
            CeilSlope = new Vector3D(0, 1, 2),
            CeilSlopeOffset = 128,
        };
        src.Tags.AddRange(new[] { 5, 7 });
        src.SetIntegerField("lightcolor", 16711680);
        src.SetStringField("comment", "copied");

        var sector = SectorBuilder.CreateSector(map, Square(map, 50, ccw: true), src)!;

        Assert.Equal(src.FloorSlope, sector.FloorSlope);
        Assert.Equal(16, sector.FloorSlopeOffset);
        Assert.Equal(src.CeilSlope, sector.CeilSlope);
        Assert.Equal(128, sector.CeilSlopeOffset);
        Assert.Equal(new[] { 5, 7 }, sector.Tags);
        Assert.Equal(16711680, sector.GetIntegerField("lightcolor"));
        Assert.Equal("copied", sector.GetStringField("comment"));
    }

    [Fact]
    public void BuildingASectorIsUndoable()
    {
        var map = new MapSet();
        var loop = Square(map, 100, ccw: true);
        var undo = new UndoManager(map);

        undo.CreateUndo("Draw sector");
        SectorBuilder.CreateSector(map, loop);
        map.BuildIndexes();
        Assert.Single(map.Sectors);
        Assert.Equal(4, map.Linedefs.Count);

        undo.Undo();
        Assert.Empty(map.Sectors);
        Assert.Empty(map.Linedefs);
        Assert.Equal(4, map.Vertices.Count); // vertices predate the undo snapshot
    }

    private static double TriArea(Vector2D a, Vector2D b, Vector2D c)
        => System.Math.Abs(a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) * 0.5;
}
