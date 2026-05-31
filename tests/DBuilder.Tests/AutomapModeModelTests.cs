// ABOUTME: Tests the UDB-style AutomapMode model for line visibility, colors, secret sectors, and flag toggles.
// ABOUTME: Covers classic automap classification without requiring renderer or mode UI surfaces.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class AutomapModeModelTests
{
    private static (MapSet Map, Sector Front, Sector Back, Linedef Line) TwoSidedLine(
        int frontFloor = 0,
        int backFloor = 0,
        int frontCeil = 128,
        int backCeil = 128)
    {
        var map = new MapSet();
        var front = map.AddSector();
        front.FloorHeight = frontFloor;
        front.CeilHeight = frontCeil;
        var back = map.AddSector();
        back.FloorHeight = backFloor;
        back.CeilHeight = backCeil;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        map.BuildIndexes();
        return (map, front, back, line);
    }

    private static Linedef OneSidedLine()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        map.AddSidedef(line, true, sector);
        map.BuildIndexes();
        return line;
    }

    private static (MapSet Map, Sector Sector, Linedef[] Lines) SquareSector()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        Vertex v1 = map.AddVertex(new Vector2D(0, 0));
        Vertex v2 = map.AddVertex(new Vector2D(0, 64));
        Vertex v3 = map.AddVertex(new Vector2D(64, 64));
        Vertex v4 = map.AddVertex(new Vector2D(64, 0));
        Linedef l1 = map.AddLinedef(v1, v2);
        Linedef l2 = map.AddLinedef(v2, v3);
        Linedef l3 = map.AddLinedef(v3, v4);
        Linedef l4 = map.AddLinedef(v4, v1);
        map.AddSidedef(l1, true, sector);
        map.AddSidedef(l2, true, sector);
        map.AddSidedef(l3, true, sector);
        map.AddSidedef(l4, true, sector);
        map.BuildIndexes();
        return (map, sector, new[] { l1, l2, l3, l4 });
    }

    [Fact]
    public void ModeDescriptorAndDefaultsMatchUdbAutomapMode()
    {
        Assert.Equal("Automap Mode", AutomapModeModel.ModeDescriptor.DisplayName);
        Assert.Equal("automapmode", AutomapModeModel.ModeDescriptor.SwitchAction);
        Assert.Equal("automap.png", AutomapModeModel.ModeDescriptor.ButtonImage);
        Assert.Equal(int.MinValue + 503, AutomapModeModel.ModeDescriptor.ButtonOrder);
        Assert.Equal("000_editing", AutomapModeModel.ModeDescriptor.ButtonGroup);
        Assert.True(AutomapModeModel.ModeDescriptor.UseByDefault);

        Assert.Equal("automapmode.showhiddenlines", AutomapModeModel.ShowHiddenLinesSettingKey);
        Assert.Equal("automapmode.showsecretsectors", AutomapModeModel.ShowSecretSectorsSettingKey);
        Assert.Equal("automapmode.showlocks", AutomapModeModel.ShowLocksSettingKey);
        Assert.Equal("automapmode.showtextures", AutomapModeModel.ShowTexturesSettingKey);
        Assert.Equal("automapmode.colorpreset", AutomapModeModel.ColorPresetSettingKey);
        Assert.False(AutomapModeModel.DefaultSettings.ShowHiddenLines);
        Assert.False(AutomapModeModel.DefaultSettings.ShowSecretSectors);
        Assert.True(AutomapModeModel.DefaultSettings.ShowLocks);
        Assert.True(AutomapModeModel.DefaultSettings.ShowTextures);
        Assert.Equal(AutomapColorPreset.Doom, AutomapModeModel.DefaultSettings.ColorPreset);
        Assert.Equal(0.001, AutomapModeModel.LineLengthScaler);
    }

    [Fact]
    public void PresentationMatchesUdbAutomapEngageLayers()
    {
        AutomapPresentationDescriptor presentation = AutomapModeModel.Presentation;

        Assert.False(presentation.DrawMapCenter);
        Assert.True(presentation.SkipHiddenSectors);
        Assert.Collection(
            presentation.Layers,
            layer =>
            {
                Assert.Equal(AutomapPresentationLayerKind.Surface, layer.Kind);
                Assert.Equal(AutomapPresentationBlendMode.Mask, layer.BlendMode);
                Assert.False(layer.GeometryOnly);
            },
            layer =>
            {
                Assert.Equal(AutomapPresentationLayerKind.Overlay, layer.Kind);
                Assert.Equal(AutomapPresentationBlendMode.Mask, layer.BlendMode);
                Assert.False(layer.GeometryOnly);
            },
            layer =>
            {
                Assert.Equal(AutomapPresentationLayerKind.Grid, layer.Kind);
                Assert.Equal(AutomapPresentationBlendMode.Mask, layer.BlendMode);
                Assert.False(layer.GeometryOnly);
            },
            layer =>
            {
                Assert.Equal(AutomapPresentationLayerKind.Geometry, layer.Kind);
                Assert.Equal(AutomapPresentationBlendMode.Alpha, layer.BlendMode);
                Assert.Equal(1, layer.Alpha);
                Assert.True(layer.GeometryOnly);
            });
    }

    [Fact]
    public void ValidLinedefCollectionUsesAutomapVisibilityRules()
    {
        var map = new MapSet();
        var visible = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var hidden = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));
        hidden.SetFlag(AutomapModeModel.HiddenFlag, true);
        var front = map.AddSector();
        front.FloorHeight = 0;
        front.CeilHeight = 128;
        var back = map.AddSector();
        back.FloorHeight = 0;
        back.CeilHeight = 128;
        var matching = map.AddLinedef(map.AddVertex(new Vector2D(0, 128)), map.AddVertex(new Vector2D(64, 128)));
        map.AddSidedef(matching, true, front);
        map.AddSidedef(matching, false, back);

        List<Linedef> valid = AutomapModeModel.GetValidLinedefs(map, new AutomapModeOptions());

        Assert.Contains(visible, valid);
        Assert.DoesNotContain(hidden, valid);
        Assert.DoesNotContain(matching, valid);
        Assert.Contains(hidden, AutomapModeModel.GetValidLinedefs(map, new AutomapModeOptions(ShowHiddenLines: true)));
    }

    [Fact]
    public void PaletteMatchesUdbDoomPreset()
    {
        AutomapPalette palette = AutomapModeModel.Palette(AutomapColorPreset.Doom);

        Assert.Equal(new AutomapColor(255, 252, 0, 0), palette.SingleSided);
        Assert.Equal(new AutomapColor(255, 255, 0, 255), palette.Secret);
        Assert.Equal(new AutomapColor(255, 188, 120, 72), palette.FloorDifference);
        Assert.Equal(new AutomapColor(255, 252, 252, 0), palette.CeilingDifference);
        Assert.Equal(new AutomapColor(255, 192, 192, 192), palette.HiddenFlag);
        Assert.Equal(new AutomapColor(255, 108, 108, 108), palette.MatchingHeight);
        Assert.Equal(new AutomapColor(255, 0, 0, 0), palette.Background);
    }

    [Fact]
    public void ValidityMatchesUdbAutomapLineRules()
    {
        Linedef oneSided = OneSidedLine();
        var matching = TwoSidedLine().Line;
        var floorDiff = TwoSidedLine(backFloor: 24).Line;
        var hidden = TwoSidedLine(backFloor: 24).Line;
        hidden.SetFlag(AutomapModeModel.HiddenFlag, true);

        Assert.True(AutomapModeModel.IsLineValid(oneSided, new AutomapModeOptions()));
        Assert.False(AutomapModeModel.IsLineValid(matching, new AutomapModeOptions()));
        Assert.True(AutomapModeModel.IsLineValid(floorDiff, new AutomapModeOptions()));
        Assert.False(AutomapModeModel.IsLineValid(hidden, new AutomapModeOptions()));
        Assert.True(AutomapModeModel.IsLineValid(hidden, new AutomapModeOptions(ShowHiddenLines: true)));
        Assert.True(AutomapModeModel.IsLineValid(matching, new AutomapModeOptions(InvertLineVisibility: true)));
    }

    [Fact]
    public void HighlightPlanningPicksNearestValidLinedefWithinScaledRange()
    {
        var map = new MapSet();
        Linedef valid = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Linedef invalid = map.AddLinedef(map.AddVertex(new Vector2D(0, 8)), map.AddVertex(new Vector2D(64, 8)));
        invalid.SetFlag(AutomapModeModel.HiddenFlag, true);
        map.BuildIndexes();
        List<Linedef> validLines = AutomapModeModel.GetValidLinedefs(map, new AutomapModeOptions());

        AutomapHighlightResult result = AutomapModeModel.PlanHighlight(
            map,
            validLines,
            new Vector2D(32, 6),
            highlightRange: 16,
            rendererScale: 2,
            editSectors: false);

        Assert.Equal(AutomapHighlightKind.Linedef, result.Kind);
        Assert.Same(valid, result.Line);
        Assert.Null(result.Sector);
        Assert.Equal(new[] { valid }, result.Lines);

        AutomapHighlightResult tooFar = AutomapModeModel.PlanHighlight(
            map,
            validLines,
            new Vector2D(32, 9),
            highlightRange: 16,
            rendererScale: 2,
            editSectors: false);
        Assert.Equal(AutomapHighlightKind.None, tooFar.Kind);
    }

    [Fact]
    public void HighlightPlanningPicksSectorUnderCursorWithoutRangeLimit()
    {
        var (map, sector, lines) = SquareSector();

        AutomapHighlightResult result = AutomapModeModel.PlanHighlight(
            map,
            Array.Empty<Linedef>(),
            new Vector2D(32, 32),
            highlightRange: 1,
            rendererScale: 100,
            editSectors: true);

        Assert.Equal(AutomapHighlightKind.Sector, result.Kind);
        Assert.Null(result.Line);
        Assert.Same(sector, result.Sector);
        Assert.Equal(lines, result.Lines);
    }

    [Fact]
    public void DetermineLineStyleClassifiesAutomapLines()
    {
        AutomapPalette palette = AutomapModeModel.Palette(AutomapColorPreset.Doom);
        var options = new AutomapModeOptions();

        Assert.Equal(AutomapLineColorKind.SingleSided, AutomapModeModel.DetermineLineStyle(OneSidedLine(), options, palette).Kind);
        Assert.Equal(AutomapLineColorKind.FloorDifference, AutomapModeModel.DetermineLineStyle(TwoSidedLine(backFloor: 8).Line, options, palette).Kind);
        Assert.Equal(AutomapLineColorKind.CeilingDifference, AutomapModeModel.DetermineLineStyle(TwoSidedLine(backCeil: 96).Line, options, palette).Kind);
        Assert.Equal(AutomapLineColorKind.MatchingHeight, AutomapModeModel.DetermineLineStyle(TwoSidedLine().Line, options, palette).Kind);

        Linedef hidden = TwoSidedLine(backFloor: 8).Line;
        hidden.SetFlag(AutomapModeModel.HiddenFlag, true);
        AutomapLineStyle hiddenStyle = AutomapModeModel.DetermineLineStyle(hidden, options, palette);
        Assert.False(hiddenStyle.IsValid);
        Assert.Equal(AutomapLineColorKind.HiddenFlag, hiddenStyle.Kind);
        Assert.Equal(palette.HiddenFlag, hiddenStyle.Color);
    }

    [Fact]
    public void LockColorTakesPriorityOverOtherLineColors()
    {
        Linedef line = TwoSidedLine(backFloor: 8).Line;
        line.Action = 80;
        line.Args[1] = 3;
        var options = new AutomapModeOptions(
            LockableActionArgs: new Dictionary<int, int> { [80] = 1 },
            LockColors: new Dictionary<int, AutomapColor> { [3] = new(255, 1, 2, 3) });

        AutomapLineStyle style = AutomapModeModel.DetermineLineStyle(line, options, AutomapModeModel.Palette(AutomapColorPreset.Doom));

        Assert.Equal(AutomapLineColorKind.Lock, style.Kind);
        Assert.Equal(new AutomapColor(255, 1, 2, 3), style.Color);
    }

    [Fact]
    public void UdmfLockNumberCanDriveLockColor()
    {
        Linedef line = TwoSidedLine(backFloor: 8).Line;
        line.Fields["locknumber"] = 4;
        var options = new AutomapModeOptions(
            IsUdmf: true,
            LockColors: new Dictionary<int, AutomapColor> { [4] = new(255, 4, 5, 6) });

        AutomapLineStyle style = AutomapModeModel.DetermineLineStyle(line, options, AutomapModeModel.Palette(AutomapColorPreset.Doom));

        Assert.Equal(AutomapLineColorKind.Lock, style.Kind);
        Assert.Equal(new AutomapColor(255, 4, 5, 6), style.Color);
    }

    [Fact]
    public void SecretSectorDetectionMatchesDoomAndUdmfRules()
    {
        var sector = new Sector { Special = 9 };
        Assert.True(AutomapModeModel.IsSectorSecret(sector, isDoom: true));
        Assert.False(AutomapModeModel.IsSectorSecret(sector, isDoom: false));

        sector.Special = 0;
        Assert.True(AutomapModeModel.IsSectorSecret(
            sector,
            isDoom: true,
            _ => new AutomapSectorEffectData(0, new HashSet<int> { 128 })));
        Assert.True(AutomapModeModel.IsSectorSecret(
            sector,
            isDoom: false,
            _ => new AutomapSectorEffectData(0, new HashSet<int> { 1024 })));
    }

    [Fact]
    public void SecretSectorDisplayCanClassifyAdjacentLines()
    {
        var (_, front, _, line) = TwoSidedLine(backFloor: 8);
        var options = new AutomapModeOptions(
            ShowSecretSectors: true,
            SectorEffectData: sector => ReferenceEquals(sector, front)
                ? new AutomapSectorEffectData(9, new HashSet<int>())
                : new AutomapSectorEffectData(0, new HashSet<int>()));

        AutomapLineStyle style = AutomapModeModel.DetermineLineStyle(line, options, AutomapModeModel.Palette(AutomapColorPreset.Doom));

        Assert.Equal(AutomapLineColorKind.Secret, style.Kind);
    }

    [Fact]
    public void ToggleHelpersFlipAutomapFlags()
    {
        Linedef line = OneSidedLine();
        var sector = new Sector();

        AutomapModeModel.ToggleSecretFlag(line);
        AutomapModeModel.ToggleHiddenFlag(line);
        AutomapModeModel.ToggleTexturedAutomapHiddenFlag(sector);

        Assert.True(line.IsFlagSet(AutomapModeModel.SecretFlag));
        Assert.True(line.IsFlagSet(AutomapModeModel.HiddenFlag));
        Assert.False(AutomapModeModel.IsSectorVisible(sector));

        AutomapModeModel.ToggleSecretFlag(line);
        AutomapModeModel.ToggleHiddenFlag(line);
        AutomapModeModel.ToggleTexturedAutomapHiddenFlag(sector);

        Assert.False(line.IsFlagSet(AutomapModeModel.SecretFlag));
        Assert.False(line.IsFlagSet(AutomapModeModel.HiddenFlag));
        Assert.True(AutomapModeModel.IsSectorVisible(sector));
    }

    [Fact]
    public void ClassicFlagsDriveAutomapVisibilityAndClassification()
    {
        Linedef hidden = TwoSidedLine(backFloor: 24).Line;
        hidden.Flags = AutomapModeModel.ClassicHiddenFlagBit;
        Linedef secret = TwoSidedLine().Line;
        secret.Flags = AutomapModeModel.ClassicSecretFlagBit;

        var options = new AutomapModeOptions(IsUdmf: false);

        Assert.False(AutomapModeModel.IsLineValid(hidden, options));
        Assert.Equal(AutomapLineColorKind.HiddenFlag, AutomapModeModel.DetermineLineKind(hidden, options));
        Assert.True(AutomapModeModel.IsLineValid(secret, options));
        Assert.Equal(AutomapLineColorKind.SingleSided, AutomapModeModel.DetermineLineKind(secret, options));
    }

    [Fact]
    public void UdmfOptionsIgnoreClassicAutomapFlagBits()
    {
        Linedef hidden = TwoSidedLine(backFloor: 24).Line;
        hidden.Flags = AutomapModeModel.ClassicHiddenFlagBit;
        Linedef secret = TwoSidedLine().Line;
        secret.Flags = AutomapModeModel.ClassicSecretFlagBit;

        var options = new AutomapModeOptions(IsUdmf: true);

        Assert.True(AutomapModeModel.IsLineValid(hidden, options));
        Assert.Equal(AutomapLineColorKind.FloorDifference, AutomapModeModel.DetermineLineKind(hidden, options));
        Assert.False(AutomapModeModel.IsLineValid(secret, options));
        Assert.Equal(AutomapLineColorKind.MatchingHeight, AutomapModeModel.DetermineLineKind(secret, options));
    }

    [Fact]
    public void FormatAwareToggleHelpersFlipClassicOrUdmfFlags()
    {
        Linedef classic = OneSidedLine();
        Linedef udmf = OneSidedLine();

        AutomapModeModel.ToggleSecretFlag(classic, isUdmf: false);
        AutomapModeModel.ToggleHiddenFlag(classic, isUdmf: false);
        AutomapModeModel.ToggleSecretFlag(udmf, isUdmf: true);
        AutomapModeModel.ToggleHiddenFlag(udmf, isUdmf: true);

        Assert.Equal(
            AutomapModeModel.ClassicSecretFlagBit | AutomapModeModel.ClassicHiddenFlagBit,
            classic.Flags);
        Assert.Empty(classic.UdmfFlags);
        Assert.True(udmf.IsFlagSet(AutomapModeModel.SecretFlag));
        Assert.True(udmf.IsFlagSet(AutomapModeModel.HiddenFlag));

        AutomapModeModel.ToggleSecretFlag(classic, isUdmf: false);
        AutomapModeModel.ToggleHiddenFlag(classic, isUdmf: false);

        Assert.Equal(0, classic.Flags);
    }
}
