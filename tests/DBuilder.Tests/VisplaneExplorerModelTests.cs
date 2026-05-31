// ABOUTME: Tests the UDB-style VisplaneExplorer tile model and packed point statistics.
// ABOUTME: Covers progressive sampling order, result storage, compression, and sentinel point states.

using DBuilder.Map;

namespace DBuilder.Tests;

public class VisplaneExplorerModelTests
{
    [Fact]
    public void PaletteCopiesRowColorsAndSupportsUdbStyleOverride()
    {
        uint[] source = MakePalette(0xFF100000u);
        var palette = new VisplanePalette(source);

        source[5] = 0xFFFFFFFFu;
        palette.SetColor(5, 0x80123456u);

        Assert.Equal(0x80123456u, palette[5]);
        Assert.Equal(0x80123456u, palette.ColorForByte(5));
        Assert.NotEqual(source[5], palette[5]);
    }

    [Fact]
    public void PaletteSetSelectsStatPaletteUnlessHeatmapRenderingIsEnabled()
    {
        var visplanes = new VisplanePalette(MakePalette(0xFF000000u));
        var drawsegs = new VisplanePalette(MakePalette(0xFF100000u));
        var solidsegs = new VisplanePalette(MakePalette(0xFF200000u));
        var openings = new VisplanePalette(MakePalette(0xFF300000u));
        var heatmap = new VisplanePalette(MakePalette(0xFF400000u));
        var palettes = new VisplanePaletteSet(visplanes, drawsegs, solidsegs, openings, heatmap);

        Assert.Same(solidsegs, palettes.PaletteFor(VisplaneExplorerStat.Solidsegs, showHeatmap: false));
        Assert.Same(heatmap, palettes.PaletteFor(VisplaneExplorerStat.Solidsegs, showHeatmap: true));
        Assert.Same(heatmap, palettes[VisplaneExplorerStat.Heatmap]);
    }

    [Fact]
    public void PaletteSetAppliesVoidColorOverrideToAllPalettes()
    {
        var palettes = new VisplanePaletteSet(
            new VisplanePalette(MakePalette(0xFF000000u)),
            new VisplanePalette(MakePalette(0xFF100000u)),
            new VisplanePalette(MakePalette(0xFF200000u)),
            new VisplanePalette(MakePalette(0xFF300000u)),
            new VisplanePalette(MakePalette(0xFF400000u)));

        palettes.SetVoidColor(0x00000000u);

        Assert.Equal(0x00000000u, palettes[VisplaneExplorerStat.Visplanes][VisplaneTile.PointVoidByte]);
        Assert.Equal(0x00000000u, palettes[VisplaneExplorerStat.Drawsegs][VisplaneTile.PointVoidByte]);
        Assert.Equal(0x00000000u, palettes[VisplaneExplorerStat.Solidsegs][VisplaneTile.PointVoidByte]);
        Assert.Equal(0x00000000u, palettes[VisplaneExplorerStat.Openings][VisplaneTile.PointVoidByte]);
        Assert.Equal(0x00000000u, palettes[VisplaneExplorerStat.Heatmap][VisplaneTile.PointVoidByte]);
    }

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
    public void GetHeatmapByteInterpolatesVisplanesAgainstConfiguredStaticLimit()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 1),
            VisplanePointResult.Ok,
            Visplanes: 64,
            Drawsegs: 66,
            Solidsegs: 17,
            Openings: 160));

        Assert.Equal(64, tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Visplanes));
        Assert.Equal(32, tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Visplanes, configuredVisplaneLimit: 256));
        Assert.Equal(33, tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Drawsegs, configuredVisplaneLimit: 256));
        Assert.Equal(1, tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Openings, configuredVisplaneLimit: 256));
    }

    [Fact]
    public void GetHeatmapByteLeavesVoidAndZeroVisplanesUninterpolated()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 1),
            VisplanePointResult.Void,
            0,
            0,
            0,
            0));

        Assert.Equal(VisplaneTile.PointVoidByte, tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Visplanes, 256));
        Assert.Equal(0, tile.GetHeatmapByte(1, 1, VisplaneExplorerStat.Visplanes, 256));
    }

    [Fact]
    public void PackedPointAccessRejectsHeatmapViewMode()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => tile.GetPointByte(0, 0, VisplaneExplorerStat.Heatmap));
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.GetPointValue(0, 0, VisplaneExplorerStat.Heatmap));
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.GetHeatmapByte(0, 0, VisplaneExplorerStat.Heatmap));
    }

    [Fact]
    public void TileReportsCompleteAfterAllPointsAreIssued()
    {
        var tile = new VisplaneTile(new VisplaneTilePosition(0, 0));

        for (int i = 0; i < VisplaneTile.TileSize * VisplaneTile.TileSize; i++)
            tile.GetNextPoint();

        Assert.True(tile.IsComplete);
    }

    private static uint[] MakePalette(uint baseColor)
    {
        var colors = new uint[256];
        for (uint i = 0; i < colors.Length; i++)
            colors[i] = baseColor + i;
        return colors;
    }
}
