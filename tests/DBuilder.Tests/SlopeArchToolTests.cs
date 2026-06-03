// ABOUTME: Tests UDB-style slope arch plane calculation over selected sectors.
// ABOUTME: Covers floor, ceiling, height offsets, and degenerate handle input.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SlopeArchToolTests
{
    [Theory]
    [InlineData(0, "No sectors slope-arched.")]
    [InlineData(1, "Applied floor slope arch to 1 sector.")]
    [InlineData(2, "Applied floor slope arch to 2 sectors.")]
    public void ApplyStatusTextFormatsSingularAndPluralSectorCounts(int sectorCount, string expected)
        => Assert.Equal(expected, SlopeArchTool.ApplyStatusText(sectorCount));

    [Fact]
    public void ApplyCreatesFloorArchPlanesAcrossSectorProjection()
    {
        var (map, left, right) = TwoHorizontalSectors();
        var options = new SlopeArchOptions
        {
            Theta = Angle2D.PIHALF,
            OffsetAngle = 0.0,
            BaseHeight = 128,
        };

        int changed = SlopeArchTool.Apply(new[] { left, right }, new Vector2D(0, 0), new Vector2D(100, 0), options);

        Assert.Equal(2, changed);
        Assert.True(left.HasFloorSlope);
        Assert.True(right.HasFloorSlope);
        Assert.Equal(128.0, left.GetFloorZ(new Vector2D(0, 5)), 2);
        Assert.Equal(114.6, left.GetFloorZ(new Vector2D(50, 5)), 1);
        Assert.Equal(28.0, right.GetFloorZ(new Vector2D(100, 5)), 1);
        _ = map;
    }

    [Fact]
    public void ApplyCanTargetCeilingAndPreserveSampledHeights()
    {
        var (_, left, _) = TwoHorizontalSectors();
        var options = new SlopeArchOptions
        {
            Theta = Angle2D.PIHALF,
            OffsetAngle = 0.0,
            BaseHeight = 256,
            HeightOffset = 16,
            ApplyToCeiling = true,
        };

        int changed = SlopeArchTool.Apply(new[] { left }, new Vector2D(0, 0), new Vector2D(100, 0), options);

        Assert.Equal(1, changed);
        Assert.True(left.HasCeilSlope);
        Assert.False(left.HasFloorSlope);
        Assert.Equal(272.0, left.GetCeilZ(new Vector2D(0, 5)), 2);
        Assert.Equal(258.6, left.GetCeilZ(new Vector2D(50, 5)), 1);
    }

    [Fact]
    public void ApplyReturnsZeroForDegenerateHandleLine()
    {
        var (_, left, _) = TwoHorizontalSectors();
        var options = new SlopeArchOptions
        {
            Theta = Angle2D.PIHALF,
            BaseHeight = 128,
        };

        Assert.Equal(0, SlopeArchTool.Apply(new[] { left }, new Vector2D(0, 0), new Vector2D(0, 0), options));
        Assert.False(left.HasFloorSlope);
    }

    [Fact]
    public void ApplyReturnsZeroWhenArcCannotBeSolved()
    {
        var (_, left, _) = TwoHorizontalSectors();
        var options = new SlopeArchOptions
        {
            Theta = 0.0,
            OffsetAngle = 0.0,
            BaseHeight = 128,
        };

        Assert.Equal(0, SlopeArchTool.Apply(new[] { left }, new Vector2D(0, 0), new Vector2D(100, 0), options));
        Assert.False(left.HasFloorSlope);
    }

    private static (MapSet Map, Sector Left, Sector Right) TwoHorizontalSectors()
    {
        var map = new MapSet();
        Sector left = AddRectSector(map, 0, 0, 50, 10);
        Sector right = AddRectSector(map, 50, 0, 100, 10);
        map.BuildIndexes();
        return (map, left, right);
    }

    private static Sector AddRectSector(MapSet map, double left, double top, double right, double bottom)
    {
        Sector sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;

        Vertex a = map.AddVertex(new Vector2D(left, top));
        Vertex b = map.AddVertex(new Vector2D(right, top));
        Vertex c = map.AddVertex(new Vector2D(right, bottom));
        Vertex d = map.AddVertex(new Vector2D(left, bottom));

        map.AddSidedef(map.AddLinedef(a, b), true, sector);
        map.AddSidedef(map.AddLinedef(b, c), true, sector);
        map.AddSidedef(map.AddLinedef(c, d), true, sector);
        map.AddSidedef(map.AddLinedef(d, a), true, sector);
        return sector;
    }
}
