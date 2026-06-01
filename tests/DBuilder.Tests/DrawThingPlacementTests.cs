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
}
