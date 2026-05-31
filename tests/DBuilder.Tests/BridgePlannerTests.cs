// ABOUTME: Tests UDB-style bridge mode geometry planning from selected linedef chains.
// ABOUTME: Covers validation, cubic bridge curves, quadrilateral loops, and sector property interpolation.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class BridgePlannerTests
{
    [Fact]
    public void TryCreateBuildsBridgeShapesBetweenTwoLineGroups()
    {
        var map = new MapSet();
        Linedef first = AddLineWithSector(
            map,
            new Vector2D(0, 0),
            new Vector2D(100, 0),
            floor: 0,
            ceiling: 128,
            brightness: 100,
            high: "-",
            middle: "MID1",
            low: "LOW1");
        Linedef second = AddLineWithSector(
            map,
            new Vector2D(0, 100),
            new Vector2D(100, 100),
            floor: 100,
            ceiling: 228,
            brightness: 200,
            high: "HIGH2",
            middle: "MID2",
            low: "-");

        BridgePlan? plan = BridgePlanner.TryCreate(new[] { first, second }, new BridgePlanOptions { Subdivisions = 2 });

        Assert.NotNull(plan);
        Assert.Equal(2, plan.Curves.Count);
        Assert.Equal(3, plan.Curves[0].Count);
        Assert.Equal(2, plan.Shapes.Count);
        Assert.All(plan.Shapes, shape => Assert.Equal(5, shape.Loop.Count));
        Assert.Equal(new Vector2D(0, 0), plan.Curves[0][0]);
        Assert.Equal(new Vector2D(0, 100), plan.Curves[0][^1]);
        Assert.Equal(new Vector2D(100, 0), plan.Curves[1][0]);
        Assert.Equal(new Vector2D(100, 100), plan.Curves[1][^1]);

        BridgeSectorProperties firstShape = plan.Shapes[0].Properties;
        Assert.Equal(75, firstShape.FloorHeight);
        Assert.Equal(203, firstShape.CeilingHeight);
        Assert.Equal(175, firstShape.Brightness);
        Assert.Equal("MID1", firstShape.HighTexture);
        Assert.Equal("LOW1", firstShape.LowTexture);

        BridgeSectorProperties secondShape = plan.Shapes[1].Properties;
        Assert.Equal(100, secondShape.FloorHeight);
        Assert.Equal(228, secondShape.CeilingHeight);
        Assert.Equal(200, secondShape.Brightness);
    }

    [Fact]
    public void TryCreateSupportsHighestLowestAndCeilingFloorClamp()
    {
        var map = new MapSet();
        Linedef first = AddLineWithSector(map, new Vector2D(0, 0), new Vector2D(64, 0), 64, 80, 96, "H1", "M1", "L1");
        Linedef second = AddLineWithSector(map, new Vector2D(0, 64), new Vector2D(64, 64), 96, 88, 160, "H2", "M2", "L2");

        BridgePlan? plan = BridgePlanner.TryCreate(
            new[] { first, second },
            new BridgePlanOptions
            {
                Subdivisions = 1,
                FloorMode = BridgeInterpolation.Lowest,
                CeilingMode = BridgeInterpolation.Highest,
                BrightnessMode = BridgeInterpolation.Highest,
            });

        Assert.NotNull(plan);
        BridgeSectorProperties properties = Assert.Single(plan.Shapes).Properties;
        Assert.Equal(64, properties.FloorHeight);
        Assert.Equal(88, properties.CeilingHeight);
        Assert.Equal(160, properties.Brightness);
    }

    [Fact]
    public void TryCreateRejectsUnequalLineGroups()
    {
        var map = new MapSet();
        Linedef first = AddLineWithSector(map, new Vector2D(0, 0), new Vector2D(64, 0), 0, 128, 160, "H", "M", "L");
        Linedef second = AddLineWithSector(map, new Vector2D(64, 0), new Vector2D(128, 0), 0, 128, 160, "H", "M", "L");
        Linedef third = AddLineWithSector(map, new Vector2D(0, 64), new Vector2D(64, 64), 0, 128, 160, "H", "M", "L");

        Assert.Null(BridgePlanner.TryCreate(new[] { first, second, third }));
    }

    [Fact]
    public void TryCreateRejectsIntersectingLineGroups()
    {
        var map = new MapSet();
        Linedef first = AddLineWithSector(map, new Vector2D(0, 0), new Vector2D(100, 100), 0, 128, 160, "H", "M", "L");
        Linedef second = AddLineWithSector(map, new Vector2D(0, 100), new Vector2D(100, 0), 0, 128, 160, "H", "M", "L");

        Assert.Null(BridgePlanner.TryCreate(new[] { first, second }));
    }

    private static Linedef AddLineWithSector(
        MapSet map,
        Vector2D start,
        Vector2D end,
        int floor,
        int ceiling,
        int brightness,
        string high,
        string middle,
        string low)
    {
        Sector sector = map.AddSector();
        sector.FloorHeight = floor;
        sector.CeilHeight = ceiling;
        sector.Brightness = brightness;
        Linedef line = map.AddLinedef(map.AddVertex(start), map.AddVertex(end));
        Sidedef side = map.AddSidedef(line, true, sector);
        side.HighTexture = high;
        side.MidTexture = middle;
        side.LowTexture = low;
        map.BuildIndexes();
        return line;
    }
}
