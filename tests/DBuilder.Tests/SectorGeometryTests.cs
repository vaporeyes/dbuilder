// ABOUTME: Verifies UDB-style sector point intersection behavior.
// ABOUTME: Covers point-in-sector checks, boundary toggles, and self-referencing sectors.

using DBuilder.Geometry;
using DBuilder.Map;
using System.Drawing;

namespace DBuilder.Tests;

public class SectorGeometryTests
{
    [Fact]
    public void IntersectFindsPointsInsideAndOutsideSector()
    {
        var (sector, _) = BuildSquareSector(selfReferencing: false);

        Assert.True(sector.Intersect(new Vector2D(32, 32)));
        Assert.False(sector.Intersect(new Vector2D(96, 32)));
        Assert.False(sector.Intersect(new Vector2D(32, 96)));
    }

    [Fact]
    public void IntersectUsesBoundaryOptionForVertices()
    {
        var (sector, _) = BuildSquareSector(selfReferencing: false);

        Assert.True(sector.Intersect(new Vector2D(0, 0), countOnTopAsTrue: true));
        Assert.False(sector.Intersect(new Vector2D(0, 0), countOnTopAsTrue: false));
    }

    [Fact]
    public void IntersectTreatsSelfReferencingSectorLikeUdb()
    {
        var (sector, _) = BuildSquareSector(selfReferencing: true);

        Assert.True(sector.Intersect(new Vector2D(32, 32)));
        Assert.False(sector.Intersect(new Vector2D(96, 32)));
    }

    [Fact]
    public void IntersectReturnsFalseForEmptySector()
    {
        var sector = new Sector();

        Assert.False(sector.Intersect(new Vector2D(0, 0)));
    }

    [Fact]
    public void BuildIndexesUpdatesSectorBoundingBox()
    {
        var (sector, _) = BuildSquareSector(selfReferencing: false);

        Assert.Equal(new RectangleF(0, 0, 64, 64), sector.BBox);
    }

    [Fact]
    public void UpdateBBoxCreatesUniqueVertexBounds()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(-8, 4));
        var b = map.AddVertex(new Vector2D(24, 4));
        var c = map.AddVertex(new Vector2D(24, 40));
        var d = map.AddVertex(new Vector2D(-8, 40));
        map.AddSidedef(map.AddLinedef(a, b), true, sector);
        map.AddSidedef(map.AddLinedef(b, c), true, sector);
        map.AddSidedef(map.AddLinedef(c, d), true, sector);
        map.AddSidedef(map.AddLinedef(d, a), true, sector);
        map.BuildIndexes();

        Assert.Equal(new RectangleF(-8, 4, 32, 36), sector.BBox);
    }

    [Fact]
    public void EmptySectorBoundingBoxMatchesUdbDefault()
    {
        var sector = new Sector();

        sector.UpdateBBox();

        Assert.Equal(new RectangleF(), sector.BBox);
    }

    private static (Sector Sector, MapSet Map) BuildSquareSector(bool selfReferencing)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)),
            map.AddVertex(new Vector2D(64, 64)),
            map.AddVertex(new Vector2D(0, 64)),
        };

        for (int i = 0; i < vertices.Length; i++)
        {
            var line = map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            map.AddSidedef(line, true, sector);
            if (selfReferencing) map.AddSidedef(line, false, sector);
        }

        map.BuildIndexes();
        return (sector, map);
    }
}
