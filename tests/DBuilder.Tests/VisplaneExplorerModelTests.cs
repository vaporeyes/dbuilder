// ABOUTME: Tests the UDB-style VisplaneExplorer tile model and packed point statistics.
// ABOUTME: Covers progressive sampling order, result storage, compression, and sentinel point states.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisplaneExplorerModelTests
{
    [Fact]
    public void ModeDescriptorMatchesUdbEditModeAttribute()
    {
        VisplaneExplorerModeDescriptor descriptor = VisplaneExplorerInterfaceModel.ModeDescriptor;

        Assert.Equal("Visplane Explorer", descriptor.DisplayName);
        Assert.Equal("visplaneexplorermode", descriptor.SwitchAction);
        Assert.Equal("Gauge.png", descriptor.ButtonImage);
        Assert.Equal(300, descriptor.ButtonOrder);
        Assert.Equal("002_tools", descriptor.ButtonGroup);
        Assert.True(descriptor.Volatile);
        Assert.True(descriptor.UseByDefault);
        Assert.Equal(new[] { "DoomMapSetIO", "HexenMapSetIO" }, descriptor.SupportedMapFormats);
        Assert.False(descriptor.AllowCopyPaste);
    }

    [Fact]
    public void InterfaceSettingsDefaultsMatchUdbPluginSettings()
    {
        VisplaneExplorerInterfaceSettings settings = VisplaneExplorerInterfaceModel.CreateSettings(
            new Dictionary<string, object?>(),
            viewHeightDefault: 41);

        Assert.False(settings.OpenDoors);
        Assert.False(settings.ShowHeatmap);
        Assert.Equal(41, settings.ViewHeight);
        Assert.Equal(0, settings.ViewHeightCustom);
    }

    [Fact]
    public void StatMenuItemsMatchUdbToolbarDropdown()
    {
        IReadOnlyList<VisplaneExplorerStatMenuItem> items =
            VisplaneExplorerInterfaceModel.StatMenuItems(VisplaneExplorerStat.Solidsegs);

        Assert.Equal(4, items.Count);
        Assert.Equal(new VisplaneExplorerStatMenuItem(VisplaneExplorerStat.Visplanes, "Visplanes", "Visplanes", "0", Checked: false), items[0]);
        Assert.Equal(new VisplaneExplorerStatMenuItem(VisplaneExplorerStat.Drawsegs, "Drawsegs", "Drawsegs", "1", Checked: false), items[1]);
        Assert.Equal(new VisplaneExplorerStatMenuItem(VisplaneExplorerStat.Solidsegs, "Solidsegs", "Solidsegs", "2", Checked: true), items[2]);
        Assert.Equal(new VisplaneExplorerStatMenuItem(VisplaneExplorerStat.Openings, "Openings", "Openings", "3", Checked: false), items[3]);
    }

    [Fact]
    public void InterfaceSettingsReadStoredPluginValues()
    {
        VisplaneExplorerInterfaceSettings settings = VisplaneExplorerInterfaceModel.CreateSettings(
            new Dictionary<string, object?>
            {
                [VisplaneExplorerInterfaceModel.OpenDoorsSettingsKey] = true,
                [VisplaneExplorerInterfaceModel.ShowHeatmapSettingsKey] = "true",
                [VisplaneExplorerInterfaceModel.ViewHeightSettingsKey] = "64",
                [VisplaneExplorerInterfaceModel.ViewHeightCustomSettingsKey] = 72,
            },
            viewHeightDefault: 41);

        Assert.True(settings.OpenDoors);
        Assert.True(settings.ShowHeatmap);
        Assert.Equal(64, settings.ViewHeight);
        Assert.Equal(72, settings.ViewHeightCustom);
    }

    [Fact]
    public void ViewHeightStateFormatsButtonAndCustomMenuLikeUdb()
    {
        VisplaneExplorerViewHeightState state = VisplaneExplorerInterfaceModel.ViewHeightState(
            viewHeight: 72,
            viewHeightCustom: 72);

        Assert.Equal("View Height (72)", state.ButtonText);
        Assert.True(state.CustomItemVisible);
        Assert.Equal("72 - Custom", state.CustomItemText);
        Assert.False(state.SettingsChanged);
    }

    [Fact]
    public void SelectViewHeightClearsCustomHeightAndReportsChanges()
    {
        VisplaneExplorerViewHeightState changed = VisplaneExplorerInterfaceModel.SelectViewHeight(
            currentViewHeight: 72,
            selectedViewHeight: 41);
        VisplaneExplorerViewHeightState unchanged = VisplaneExplorerInterfaceModel.SelectViewHeight(
            currentViewHeight: 41,
            selectedViewHeight: 41);

        Assert.Equal(41, changed.ViewHeight);
        Assert.Equal(0, changed.ViewHeightCustom);
        Assert.False(changed.CustomItemVisible);
        Assert.True(changed.SettingsChanged);
        Assert.False(unchanged.SettingsChanged);
    }

    [Fact]
    public void ApplyCustomViewHeightMatchesUdbDialogCommitBehavior()
    {
        VisplaneExplorerViewHeightState state = VisplaneExplorerInterfaceModel.ApplyCustomViewHeight(
            currentViewHeight: 41,
            currentCustomHeight: 0,
            customInput: "72",
            viewHeightDefault: 41,
            configuredViewHeights: new Dictionary<string, string>());

        Assert.Equal(72, state.ViewHeight);
        Assert.Equal(72, state.ViewHeightCustom);
        Assert.Equal("72 - Custom", state.CustomItemText);
        Assert.True(state.SettingsChanged);
    }

    [Fact]
    public void ApplyCustomViewHeightHidesCustomMenuWhenValueMatchesConfiguredPreset()
    {
        VisplaneExplorerViewHeightState state = VisplaneExplorerInterfaceModel.ApplyCustomViewHeight(
            currentViewHeight: 41,
            currentCustomHeight: 72,
            customInput: "41",
            viewHeightDefault: 41,
            configuredViewHeights: new Dictionary<string, string> { ["41"] = "Default" });

        Assert.Equal(41, state.ViewHeight);
        Assert.Equal(0, state.ViewHeightCustom);
        Assert.False(state.CustomItemVisible);
        Assert.Equal("", state.CustomItemText);
        Assert.True(state.SettingsChanged);
    }

    [Fact]
    public void ApplyCustomViewHeightFallsBackToDefaultWhenInputNormalizesToZero()
    {
        VisplaneExplorerViewHeightState state = VisplaneExplorerInterfaceModel.ApplyCustomViewHeight(
            currentViewHeight: 72,
            currentCustomHeight: 72,
            customInput: "32768",
            viewHeightDefault: 41,
            configuredViewHeights: new Dictionary<string, string>());

        Assert.Equal(41, state.ViewHeight);
        Assert.Equal(0, state.ViewHeightCustom);
        Assert.Equal("View Height (41)", state.ButtonText);
        Assert.True(state.SettingsChanged);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("41", 41)]
    [InlineData("32767", 32767)]
    [InlineData("32768", 0)]
    [InlineData("-1", 0)]
    [InlineData("not a number", 0)]
    [InlineData("++64", 64)]
    [InlineData("+++8", 8)]
    [InlineData("--8", 0)]
    [InlineData("*4", 0)]
    [InlineData("/2", 0)]
    public void CustomViewHeightInputMatchesUdbDialogNormalization(string input, int expected)
        => Assert.Equal(expected, VisplaneExplorerViewHeight.NormalizeCustomHeightInput(input));

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
    public void TileForPointMatchesUdbFlooringForPositiveAndNegativeCoordinates()
    {
        Assert.Equal(new VisplaneTilePosition(0, 0), VisplaneTileScan.TileForPoint(0, 0));
        Assert.Equal(new VisplaneTilePosition(0, 0), VisplaneTileScan.TileForPoint(63.9, 63.9));
        Assert.Equal(new VisplaneTilePosition(64, 64), VisplaneTileScan.TileForPoint(64, 64));
        Assert.Equal(new VisplaneTilePosition(-64, -64), VisplaneTileScan.TileForPoint(-0.1, -0.1));
        Assert.Equal(new VisplaneTilePosition(-64, -64), VisplaneTileScan.TileForPoint(-64, -64));
        Assert.Equal(new VisplaneTilePosition(-128, -128), VisplaneTileScan.TileForPoint(-64.1, -64.1));
    }

    [Fact]
    public void CollectNextPointBatchPrefersIncompleteTilesInCurrentView()
    {
        var scan = new VisplaneTileScan();
        scan.AddTile(new VisplaneTilePosition(0, 0));
        scan.AddTile(new VisplaneTilePosition(128, 0));

        IReadOnlyList<VisplaneTilePoint> points = scan.CollectNextPointBatch(new VisplaneMapRectangle(-64, -64, 128, 128));

        Assert.Equal(new VisplaneTilePoint(0, 0, 64), Assert.Single(points));
    }

    [Fact]
    public void CollectNextPointBatchFallsBackToAllIncompleteTilesWhenViewIsComplete()
    {
        var scan = new VisplaneTileScan();
        scan.AddTile(new VisplaneTilePosition(0, 0));
        scan.AddTile(new VisplaneTilePosition(128, 0));

        IReadOnlyList<VisplaneTilePoint> points = scan.CollectNextPointBatch(new VisplaneMapRectangle(512, 512, 64, 64));

        Assert.Equal(
            new[] { new VisplaneTilePoint(0, 0, 64), new VisplaneTilePoint(128, 0, 64) },
            points);
    }

    [Fact]
    public void QueuePointsRepeatsBatchesUntilTargetQueuedCountIsReached()
    {
        var scan = new VisplaneTileScan();
        scan.AddTile(new VisplaneTilePosition(0, 0));
        scan.AddTile(new VisplaneTilePosition(64, 0));

        IReadOnlyList<VisplaneTilePoint> points = scan.QueuePoints(
            new VisplaneMapRectangle(-64, -64, 256, 128),
            currentQueuedPoints: 1,
            targetQueuedPoints: 5);

        Assert.Equal(4, points.Count);
        Assert.Equal(new VisplaneTilePoint(0, 0, 64), points[0]);
        Assert.Equal(new VisplaneTilePoint(64, 0, 64), points[1]);
        Assert.Equal(new VisplaneTilePoint(32, 32, 32), points[2]);
        Assert.Equal(new VisplaneTilePoint(96, 32, 32), points[3]);
    }

    [Fact]
    public void QueuePointsReturnsNoPointsWhenCurrentQueueAlreadyMeetsTarget()
    {
        var scan = new VisplaneTileScan();
        scan.AddTile(new VisplaneTilePosition(0, 0));

        IReadOnlyList<VisplaneTilePoint> points = scan.QueuePoints(
            new VisplaneMapRectangle(-64, -64, 256, 128),
            currentQueuedPoints: 5,
            targetQueuedPoints: 5);

        Assert.Empty(points);
        Assert.False(scan.Tiles[new VisplaneTilePosition(0, 0)].IsComplete);
    }

    [Fact]
    public void HoverInfoFormatsUdbStylePointValueAndStaticLimit()
    {
        var scan = new VisplaneTileScan();
        VisplaneTile tile = scan.AddTile(new VisplaneTilePosition(-64, 0));
        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(-64, 0, 64),
            VisplanePointResult.Ok,
            Visplanes: 65,
            Drawsegs: 67,
            Solidsegs: 17,
            Openings: 320));

        VisplaneHoverInfo? info = scan.GetHoverInfo(-0.1, 63.9, VisplaneExplorerStat.Drawsegs, staticLimit: 256);

        Assert.Equal(new VisplaneHoverInfo(68, 256, Overflow: false), info);
        Assert.Equal("68 / 256", info?.FormatLabel());
    }

    [Fact]
    public void HoverInfoReturnsNullForVoidOrMissingTilesLikeUdbTooltip()
    {
        var scan = new VisplaneTileScan();
        VisplaneTile tile = scan.AddTile(new VisplaneTilePosition(0, 0));
        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 64),
            VisplanePointResult.Void,
            0,
            0,
            0,
            0));

        Assert.Null(scan.GetHoverInfo(4, 4, VisplaneExplorerStat.Visplanes, staticLimit: 128));
        Assert.Null(scan.GetHoverInfo(128, 4, VisplaneExplorerStat.Visplanes, staticLimit: 128));
    }

    [Fact]
    public void HoverInfoAppendsOverflowMarkerLikeUdbTooltip()
    {
        var scan = new VisplaneTileScan();
        VisplaneTile tile = scan.AddTile(new VisplaneTilePosition(0, 0));
        tile.StorePointData(new VisplanePointData(
            new VisplaneTilePoint(0, 0, 64),
            VisplanePointResult.Overflow,
            0,
            0,
            0,
            0));

        VisplaneHoverInfo? info = scan.GetHoverInfo(1, 1, VisplaneExplorerStat.Openings, staticLimit: 32768);

        Assert.Equal(new VisplaneHoverInfo(40640, 32768, Overflow: true), info);
        Assert.Equal("40640+ / 32768", info?.FormatLabel());
    }

    [Fact]
    public void CreateForMapExpandsVertexBoundsByOneTileLikeUdb()
    {
        MapSet map = ClockwiseSquareMap();

        VisplaneTileScan scan = VisplaneTileScan.CreateForMap(map);

        Assert.Contains(new VisplaneTilePosition(-64, -64), scan.Tiles.Keys);
        Assert.Contains(new VisplaneTilePosition(0, 0), scan.Tiles.Keys);
        Assert.Contains(new VisplaneTilePosition(128, 128), scan.Tiles.Keys);
        Assert.DoesNotContain(new VisplaneTilePosition(192, 192), scan.Tiles.Keys);
    }

    [Fact]
    public void CreateForMapSkipsObviousBackSideOutsideTiles()
    {
        MapSet map = ClockwiseSquareMap();

        VisplaneTileScan scan = VisplaneTileScan.CreateForMap(map);

        Assert.DoesNotContain(new VisplaneTilePosition(0, -128), scan.Tiles.Keys);
        Assert.Contains(new VisplaneTilePosition(0, -64), scan.Tiles.Keys);
    }

    [Fact]
    public void CreateForMapReturnsEmptyScanForMapsWithoutLines()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(0, 0));

        VisplaneTileScan scan = VisplaneTileScan.CreateForMap(map);

        Assert.Empty(scan.Tiles);
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

    private static MapSet ClockwiseSquareMap()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex bottomLeft = map.AddVertex(new Vector2D(0, 0));
        Vertex bottomRight = map.AddVertex(new Vector2D(128, 0));
        Vertex topRight = map.AddVertex(new Vector2D(128, 128));
        Vertex topLeft = map.AddVertex(new Vector2D(0, 128));

        AddLine(bottomRight, bottomLeft);
        AddLine(topRight, bottomRight);
        AddLine(topLeft, topRight);
        AddLine(bottomLeft, topLeft);
        map.BuildIndexes();
        return map;

        void AddLine(Vertex start, Vertex end)
        {
            Linedef line = map.AddLinedef(start, end);
            map.AddSidedef(line, true, sector);
        }
    }
}
