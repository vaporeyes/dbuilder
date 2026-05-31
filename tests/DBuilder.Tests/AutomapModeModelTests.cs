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
}
