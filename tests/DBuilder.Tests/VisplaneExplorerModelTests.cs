// ABOUTME: Tests the UDB-style VisplaneExplorer tile model and packed point statistics.
// ABOUTME: Covers progressive sampling order, result storage, compression, and sentinel point states.

using DBuilder.Map;

namespace DBuilder.Tests;

public class VisplaneExplorerModelTests
{
    [Fact]
    public void PointByIndexMatchesUdbButterflySamplingOrder()
    {
        Assert.Equal(new VisplaneTilePoint(0, 0, 64), VisplaneTile.PointByIndex(0));
        Assert.Equal(new VisplaneTilePoint(32, 32, 32), VisplaneTile.PointByIndex(1));
        Assert.Equal(new VisplaneTilePoint(0, 32, 32), VisplaneTile.PointByIndex(2));
        Assert.Equal(new VisplaneTilePoint(32, 0, 32), VisplaneTile.PointByIndex(3));
        Assert.Equal(new VisplaneTilePoint(16, 16, 16), VisplaneTile.PointByIndex(4));
        Assert.Equal(new VisplaneTilePoint(48, 48, 16), VisplaneTile.PointByIndex(5));
        Assert.Equal(new VisplaneTilePoint(8, 8, 8), VisplaneTile.PointByIndex(16));
        Assert.Equal(new VisplaneTilePoint(4, 4, 4), VisplaneTile.PointByIndex(64));
        Assert.Equal(new VisplaneTilePoint(2, 2, 2), VisplaneTile.PointByIndex(256));
        Assert.Equal(new VisplaneTilePoint(1, 1, 1), VisplaneTile.PointByIndex(1024));
    }

    [Fact]
    public void GetNextPointOffsetsPointsByTilePosition()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(128, -64));

        Assert.Equal(new VisplaneTilePoint(128, -64, 64), tile.GetNextPoint());
        Assert.Equal(new VisplaneTilePoint(160, -32, 32), tile.GetNextPoint());
    }

    [Fact]
    public void StoreOkPointCompressesAndFillsGranularityArea()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 4),
            VisplanePointResult.Ok,
            Visplanes: 129,
            Drawsegs: 65,
            Solidsegs: 17,
            Openings: 161));

        Assert.Equal(129, tile.GetPointValue(0, 0, VisplaneExplorerStat.Visplanes));
        Assert.Equal(66, tile.GetPointValue(3, 3, VisplaneExplorerStat.Drawsegs));
        Assert.Equal(17, tile.GetPointValue(2, 1, VisplaneExplorerStat.Solidsegs));
        Assert.Equal(320, tile.GetPointValue(1, 2, VisplaneExplorerStat.Openings));
        Assert.Equal(0, tile.GetPointValue(4, 4, VisplaneExplorerStat.Visplanes));
    }

    [Fact]
    public void StorePointDataUsesUdbSentinelValues()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 1),
            VisplanePointResult.Void,
            0,
            0,
            0,
            0));
        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(1, 0, 1),
            VisplanePointResult.Overflow,
            0,
            0,
            0,
            0));
        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(2, 0, 1),
            VisplanePointResult.BadZ,
            0,
            0,
            0,
            0));

        Assert.Equal(VisplaneTile.PointVoidByte, tile.GetPointByte(0, 0, VisplaneExplorerStat.Visplanes));
        Assert.Equal(VisplaneTile.PointOverflowByte, tile.GetPointByte(1, 0, VisplaneExplorerStat.Drawsegs));
        Assert.Equal(1, tile.GetPointByte(2, 0, VisplaneExplorerStat.Visplanes));
        Assert.Equal(0, tile.GetPointByte(2, 0, VisplaneExplorerStat.Drawsegs));
    }

    [Fact]
    public void PackedPointAccessRejectsHeatmapViewMode()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => tile.GetPointByte(0, 0, VisplaneExplorerStat.Heatmap));
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.GetPointValue(0, 0, VisplaneExplorerStat.Heatmap));
    }

    [Fact]
    public void TileReportsCompleteAfterAllPointsAreIssued()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        for (int i = 0; i < VisplaneTile.TileSize * VisplaneTile.TileSize; i++)
            tile.GetNextPoint();

        Assert.True(tile.IsComplete);
    }
}
