// ABOUTME: Verifies UDB-style sector point intersection behavior.
// ABOUTME: Covers point-in-sector checks, boundary toggles, and self-referencing sectors.

using DBuilder.Geometry;
using DBuilder.Map;

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
