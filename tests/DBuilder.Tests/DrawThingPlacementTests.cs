// ABOUTME: Tests UDB-style thing placement from draw-mode generated vertices.
// ABOUTME: Covers duplicate point filtering and selected thing creation without editor UI dependencies.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class DrawThingPlacementTests
{
    [Fact]
    public void UniquePositionsKeepsFirstOccurrenceOrder()
    {
        var positions = DrawThingPlacement.UniquePositions(
            [
                new Vector2D(0, 0),
                new Vector2D(64, 0),
                new Vector2D(0, 0),
                new Vector2D(64, 64)
            ]);

        Assert.Equal(
            [
                new Vector2D(0, 0),
                new Vector2D(64, 0),
                new Vector2D(64, 64)
            ],
            positions);
    }

    [Fact]
    public void PlaceAtPositionsCreatesSelectedThingsAndClearsSelection()
    {
        var map = new MapSet();
        Thing existing = map.AddThing(new Vector2D(-32, -32), 3001);
        existing.Selected = true;

        int count = DrawThingPlacement.PlaceAtPositions(
            map,
            [
                new Vector2D(0, 0),
                new Vector2D(64, 0),
                new Vector2D(0, 0)
            ],
            thingType: 1);

        Assert.Equal(2, count);
        Assert.False(existing.Selected);
        Assert.Equal(3, map.Things.Count);
        Assert.Equal(new Vector2D(0, 0), map.Things[1].Position);
        Assert.Equal(new Vector2D(64, 0), map.Things[2].Position);
        Assert.All(map.Things.Skip(1), thing =>
        {
            Assert.Equal(1, thing.Type);
            Assert.True(thing.Selected);
        });
    }

    [Fact]
    public void PlaceAtPositionsReturnsZeroForEmptyPositions()
    {
        var map = new MapSet();

        int count = DrawThingPlacement.PlaceAtPositions(map, [], thingType: 3001);

        Assert.Equal(0, count);
        Assert.Empty(map.Things);
    }

    [Fact]
    public void PositionsFromVerticesDeduplicatesSelectedVertexPositions()
    {
        var map = new MapSet();
        Vertex first = map.AddVertex(new Vector2D(0, 0));
        Vertex duplicate = map.AddVertex(new Vector2D(0, 0));
        Vertex second = map.AddVertex(new Vector2D(64, 0));

        IReadOnlyList<Vector2D> positions = DrawThingPlacement.PositionsFromVertices(
            [first, duplicate, second]);

        Assert.Equal([new Vector2D(0, 0), new Vector2D(64, 0)], positions);
    }

    [Fact]
    public void PositionsFromLinedefsUsesUniqueEndpointsInSelectionOrder()
    {
        var map = new MapSet();
        Vertex a = map.AddVertex(new Vector2D(0, 0));
        Vertex b = map.AddVertex(new Vector2D(64, 0));
        Vertex c = map.AddVertex(new Vector2D(64, 64));
        Linedef first = map.AddLinedef(a, b);
        Linedef second = map.AddLinedef(b, c);

        IReadOnlyList<Vector2D> positions = DrawThingPlacement.PositionsFromLinedefs(
            [first, second]);

        Assert.Equal(
            [new Vector2D(0, 0), new Vector2D(64, 0), new Vector2D(64, 64)],
            positions);
    }

    [Fact]
    public void PositionsFromSectorsUsesUdbLabelPositionBeforeBoundsCenter()
    {
        (Sector sector, _) = BuildTriangleSector();

        IReadOnlyList<Vector2D> positions = DrawThingPlacement.PositionsFromSectors([sector]);

        Vector2D position = Assert.Single(positions);
        Assert.Equal(Tools.FindLabelPositions(sector)[0].position, position);
        Assert.NotEqual(new Vector2D(48, 32), position);
    }

    [Fact]
    public void PositionsFromSectorsFallsBackToBoundsCenterForEmptySector()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.UpdateBBox();

        IReadOnlyList<Vector2D> positions = DrawThingPlacement.PositionsFromSectors([sector]);

        Assert.Equal([new Vector2D(0, 0)], positions);
    }

    private static (Sector Sector, MapSet Map) BuildTriangleSector()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex[] vertices =
        [
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(96, 0)),
            map.AddVertex(new Vector2D(0, 64)),
        ];

        for (int i = 0; i < vertices.Length; i++)
        {
            Linedef line = map.AddLinedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            map.AddSidedef(line, true, sector);
        }

        map.BuildIndexes();
        return (sector, map);
    }
}
