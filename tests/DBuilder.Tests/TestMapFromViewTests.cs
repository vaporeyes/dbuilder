// ABOUTME: Verifies UDB-style Test Map from current position player-start placement.
// ABOUTME: Keeps temporary test-map mutation rules covered without launching a source port.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class TestMapFromViewTests
{
    [Fact]
    public void PrepareMovesExistingPlayerStartOnClonedMap()
    {
        MapSet map = BuildBoxMap();
        Thing original = map.AddThing(new Vector2D(16, 16), TestMapFromView.PlayerStartType);

        TestMapFromViewResult result = TestMapFromView.Prepare(
            map,
            new TestMapFromViewPlacement(new Vector2D(64, 64), 0, null, VisualMode: false),
            usesHubPlayerStartArgs: false);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Map);
        Thing moved = Assert.Single(result.Map!.Things);
        Assert.Equal(new Vector2D(64, 64), moved.Position);
        Assert.Equal(0, moved.Height);
        Assert.Equal(new Vector2D(16, 16), original.Position);
    }

    [Fact]
    public void PrepareCreatesTemporaryPlayerStartWhenMissing()
    {
        MapSet map = BuildBoxMap();

        TestMapFromViewResult result = TestMapFromView.Prepare(
            map,
            new TestMapFromViewPlacement(new Vector2D(32, 48), 0, null, VisualMode: false),
            usesHubPlayerStartArgs: false);

        Assert.True(result.Success, result.Message);
        Thing start = Assert.Single(result.Map!.Things);
        Assert.Equal(TestMapFromView.PlayerStartType, start.Type);
        Assert.Empty(map.Things);
    }

    [Fact]
    public void PrepareUsesHighestValidHubPlayerStart()
    {
        MapSet map = BuildBoxMap();
        Thing skipped = map.AddThing(new Vector2D(8, 8), TestMapFromView.PlayerStartType);
        skipped.Args[0] = 2;
        Thing lower = map.AddThing(new Vector2D(16, 16), TestMapFromView.PlayerStartType);
        Thing higher = map.AddThing(new Vector2D(24, 24), TestMapFromView.PlayerStartType);

        TestMapFromViewResult result = TestMapFromView.Prepare(
            map,
            new TestMapFromViewPlacement(new Vector2D(40, 40), 0, null, VisualMode: false),
            usesHubPlayerStartArgs: true);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new Vector2D(8, 8), result.Map!.Things[0].Position);
        Assert.Equal(new Vector2D(16, 16), result.Map.Things[1].Position);
        Assert.Equal(new Vector2D(40, 40), result.Map.Things[2].Position);
        Assert.Equal(new Vector2D(24, 24), higher.Position);
        Assert.Equal(new Vector2D(16, 16), lower.Position);
    }

    [Fact]
    public void PrepareRejectsPointOutsideSector()
    {
        MapSet map = BuildBoxMap();

        TestMapFromViewResult result = TestMapFromView.Prepare(
            map,
            new TestMapFromViewPlacement(new Vector2D(400, 400), 0, null, VisualMode: false),
            usesHubPlayerStartArgs: false);

        Assert.False(result.Success);
        Assert.Null(result.Map);
        Assert.Equal("Can't test from current position: mouse cursor must be inside a sector!", result.Message);
    }

    [Fact]
    public void PrepareAppliesVisualHeightAndAngle()
    {
        MapSet map = BuildBoxMap(floor: 16, ceiling: 96);

        TestMapFromViewResult result = TestMapFromView.Prepare(
            map,
            new TestMapFromViewPlacement(
                new Vector2D(64, 64),
                120,
                Angle2D.DegToRad(90),
                VisualMode: true),
            usesHubPlayerStartArgs: false);

        Assert.True(result.Success, result.Message);
        Thing start = Assert.Single(result.Map!.Things);
        Assert.Equal(39, start.Height);
        Assert.Equal(180, start.Angle);
    }

    private static MapSet BuildBoxMap(int floor = 0, int ceiling = 128)
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.FloorHeight = floor;
        sector.CeilHeight = ceiling;
        Vertex a = map.AddVertex(new Vector2D(0, 0));
        Vertex b = map.AddVertex(new Vector2D(128, 0));
        Vertex c = map.AddVertex(new Vector2D(128, 128));
        Vertex d = map.AddVertex(new Vector2D(0, 128));
        AddLine(map, a, b, sector);
        AddLine(map, b, c, sector);
        AddLine(map, c, d, sector);
        AddLine(map, d, a, sector);
        map.BuildIndexes();
        return map;
    }

    private static void AddLine(MapSet map, Vertex start, Vertex end, Sector sector)
    {
        Linedef line = map.AddLinedef(start, end);
        map.AddSidedef(line, false, sector);
    }
}
