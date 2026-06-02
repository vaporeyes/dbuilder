// ABOUTME: Tests UDB-compatible grid setup persistence and snapping behavior.
// ABOUTME: Covers grid size clamping, rotation/origin transforms and background metadata settings.

using DBuilder.Geometry;
using DBuilder.IO;

namespace DBuilder.Tests;

public class GridSetupTests
{
    [Fact]
    public void DefaultsMatchUdbGridSetup()
    {
        var grid = new GridSetup();

        Assert.Equal(32, grid.GridSize);
        Assert.Equal(32.0, grid.GridSizeF);
        Assert.Equal(0.0, grid.GridRotate);
        Assert.Equal(0.0, grid.GridOriginX);
        Assert.Equal(0.0, grid.GridOriginY);
        Assert.Equal("", grid.BackgroundName);
        Assert.Equal(1.0, grid.BackgroundScaleX);
        Assert.Equal(1.0, grid.BackgroundScaleY);
    }

    [Fact]
    public void ReadAndWriteConfigUseUdbKeys()
    {
        var configuration = new Configuration(sorted: true);
        var grid = new GridSetup();
        grid.SetBackground("GRIDPIC", GridSetup.SourceTextures);
        grid.SetBackgroundView(10, -20, 1.5, 0.75);
        grid.SetGridSize(16.5);
        grid.SetGridRotation(0.25);
        grid.SetGridOrigin(4, 8);

        grid.WriteToConfig(configuration, "grid");
        var restored = new GridSetup();
        restored.ReadFromConfig(configuration, "grid");

        Assert.Equal("GRIDPIC", configuration.ReadSetting("grid.background", ""));
        Assert.Equal(GridSetup.SourceTextures, configuration.ReadSetting("grid.backsource", -1));
        Assert.Equal(10, configuration.ReadSetting("grid.backoffsetx", 0));
        Assert.Equal(-20, configuration.ReadSetting("grid.backoffsety", 0));
        Assert.Equal(150, configuration.ReadSetting("grid.backscalex", 0));
        Assert.Equal(75, configuration.ReadSetting("grid.backscaley", 0));
        Assert.Equal(16.5, restored.GridSizeF);
        Assert.Equal(16, restored.GridSize);
        Assert.Equal(0.25, restored.GridRotate);
        Assert.Equal(4, restored.GridOriginX);
        Assert.Equal(8, restored.GridOriginY);
        Assert.Equal(1.5, restored.BackgroundScaleX);
        Assert.Equal(0.75, restored.BackgroundScaleY);
    }

    [Fact]
    public void BackgroundScaleClampsToUdbPercentRange()
    {
        var grid = new GridSetup();

        grid.SetBackgroundView(0, 0, 0.0, 250.0);

        Assert.Equal(GridSetup.MinimumBackgroundScale, grid.BackgroundScaleX);
        Assert.Equal(GridSetup.MaximumBackgroundScale, grid.BackgroundScaleY);
    }

    [Fact]
    public void BackgroundScaleRecoversNonFiniteValues()
    {
        var grid = new GridSetup();

        grid.SetBackgroundView(0, 0, double.NaN, double.PositiveInfinity);

        Assert.Equal(1.0, grid.BackgroundScaleX);
        Assert.Equal(1.0, grid.BackgroundScaleY);
    }

    [Fact]
    public void ReadBackgroundScaleClampsToUdbPercentRange()
    {
        var configuration = new Configuration(sorted: true);
        configuration.WriteSetting("grid.backscalex", 0);
        configuration.WriteSetting("grid.backscaley", 25000);

        var grid = new GridSetup();
        grid.ReadFromConfig(configuration, "grid");

        Assert.Equal(GridSetup.MinimumBackgroundScale, grid.BackgroundScaleX);
        Assert.Equal(GridSetup.MaximumBackgroundScale, grid.BackgroundScaleY);
    }

    [Fact]
    public void DialogModelFormatsBackgroundScaleAsPercent()
    {
        Assert.Equal("150", GridSetupDialogModel.FormatBackgroundScalePercent(1.5));
        Assert.Equal("75", GridSetupDialogModel.FormatBackgroundScalePercent(0.75));
    }

    [Fact]
    public void DialogModelParsesBackgroundScalePercentToScaleFactor()
    {
        Assert.Equal(1.5, GridSetupDialogModel.ParseBackgroundScalePercent("150", 1.0));
        Assert.Equal(0.75, GridSetupDialogModel.ParseBackgroundScalePercent("75", 1.0));
        Assert.Equal(1.25, GridSetupDialogModel.ParseBackgroundScalePercent("125", 1.0));
    }

    [Fact]
    public void DialogModelClampsAndFallsBackForInvalidBackgroundScalePercent()
    {
        Assert.Equal(GridSetup.MinimumBackgroundScale, GridSetupDialogModel.ParseBackgroundScalePercent("0", 1.0));
        Assert.Equal(GridSetup.MaximumBackgroundScale, GridSetupDialogModel.ParseBackgroundScalePercent("25000", 1.0));
        Assert.Equal(1.5, GridSetupDialogModel.ParseBackgroundScalePercent("not a number", 1.5));
    }

    [Fact]
    public void DialogModelClearsBackgroundWhenShowBackgroundIsDisabled()
    {
        var result = GridSetupDialogModel.BackgroundSelection(false, "GRIDPIC", GridSetup.SourceTextures);

        Assert.Equal("", result.Name);
        Assert.Equal(0, result.Source);
    }

    [Fact]
    public void DialogModelKeepsTrimmedBackgroundWhenShowBackgroundIsEnabled()
    {
        var result = GridSetupDialogModel.BackgroundSelection(true, " GRIDPIC ", GridSetup.SourceTextures);

        Assert.Equal("GRIDPIC", result.Name);
        Assert.Equal(GridSetup.SourceTextures, result.Source);
    }

    [Fact]
    public void SetGridSizeClampsToMinimumAndRoundsDisplaySize()
    {
        var grid = new GridSetup();

        grid.SetGridSize(0);

        Assert.Equal(1.0, grid.GridSizeF);
        Assert.Equal(1, grid.GridSize);
    }

    [Fact]
    public void UdmfGridAllowsFractionalMinimum()
    {
        var grid = new GridSetup(udmf: true);

        grid.SetGridSize(0);

        Assert.Equal(0.125, grid.GridSizeF);
        Assert.Equal(1, grid.GridSize);
    }

    [Fact]
    public void StepGridSizeStopsAtUdbBounds()
    {
        var grid = new GridSetup();

        grid.SetGridSize(GridSetup.MaximumGridSize);
        Assert.False(grid.TryStepGridSize(larger: true));
        Assert.Equal(GridSetup.MaximumGridSize, grid.GridSizeF);

        Assert.True(grid.TryStepGridSize(larger: false));
        Assert.Equal(512.0, grid.GridSizeF);

        grid.SetGridSize(GridSetup.MinimumGridSize);
        Assert.False(grid.TryStepGridSize(larger: false));
        Assert.Equal(GridSetup.MinimumGridSize, grid.GridSizeF);
    }

    [Fact]
    public void StepGridSizeCanReduceOversizedGridValues()
    {
        var grid = new GridSetup();

        grid.SetGridSize(GridSetup.MaximumGridSize * 4.0);

        Assert.False(grid.TryStepGridSize(larger: true));
        Assert.Equal(GridSetup.MaximumGridSize * 4.0, grid.GridSizeF);
        Assert.True(grid.TryStepGridSize(larger: false));
        Assert.Equal(GridSetup.MaximumGridSize * 2.0, grid.GridSizeF);
        Assert.True(grid.TryStepGridSize(larger: false));
        Assert.Equal(GridSetup.MaximumGridSize, grid.GridSizeF);
    }

    [Fact]
    public void SetGridSizeRecoversNonFiniteValuesToMaximum()
    {
        var grid = new GridSetup();

        grid.SetGridSize(double.PositiveInfinity);

        Assert.Equal(GridSetup.MaximumGridSize, grid.GridSizeF);
        Assert.False(grid.TryStepGridSize(larger: true));
        Assert.True(grid.TryStepGridSize(larger: false));
        Assert.Equal(GridSetup.MaximumGridSize * 0.5, grid.GridSizeF);
    }

    [Fact]
    public void SetGridSizeSaturatesDisplaySizeForHugeFiniteValues()
    {
        var grid = new GridSetup();

        grid.SetGridSize((double)int.MaxValue * 2.0);

        Assert.Equal((double)int.MaxValue * 2.0, grid.GridSizeF);
        Assert.Equal(int.MaxValue, grid.GridSize);
        Assert.True(grid.TryStepGridSize(larger: false));
        Assert.Equal((double)int.MaxValue, grid.GridSizeF);
        Assert.Equal(int.MaxValue, grid.GridSize);
    }

    [Fact]
    public void DynamicGridSizeTargetsPowerOfTwoDisplayDensity()
    {
        Assert.Equal(32.0, GridSetup.DynamicGridSizeForView(1024, 768, 1.0));
        Assert.Equal(4.0, GridSetup.DynamicGridSizeForView(1024, 768, 0.1));
        Assert.Equal(256.0, GridSetup.DynamicGridSizeForView(1024, 768, 10.0));
    }

    [Fact]
    public void MatchSizeToDisplayScaleUpdatesOnlyWhenSizeChanges()
    {
        var grid = new GridSetup();

        Assert.False(grid.MatchSizeToDisplayScale(1024, 768, 1.0));
        Assert.Equal(32.0, grid.GridSizeF);
        Assert.True(grid.MatchSizeToDisplayScale(1024, 768, 10.0));
        Assert.Equal(256.0, grid.GridSizeF);
    }

    [Fact]
    public void GetHigherAndLowerMatchGridSize()
    {
        var grid = new GridSetup();
        grid.SetGridSize(16);

        Assert.Equal(32, grid.GetHigher(24));
        Assert.Equal(16, grid.GetLower(24));
    }

    [Fact]
    public void SnappedToGridRoundsToNearestGridPoint()
    {
        var grid = new GridSetup();
        grid.SetGridSize(16);

        var snapped = grid.SnappedToGrid(new Vector2D(23, 25));

        Assert.Equal(new Vector2D(16, 32), snapped);
    }

    [Fact]
    public void SnappedToGridHandlesOriginAndRotation()
    {
        var grid = new GridSetup();
        grid.SetGridSize(10);
        grid.SetGridOrigin(100, 50);
        grid.SetGridRotation(Angle2D.PIHALF);

        var snapped = grid.SnappedToGrid(new Vector2D(100, 61));

        Assert.Equal(100, snapped.x, 6);
        Assert.Equal(60, snapped.y, 6);
    }

    [Fact]
    public void StaticSnapClampsToBoundaries()
    {
        var snapped = GridSetup.SnappedToGrid(
            new Vector2D(100, -100),
            gridSize: 32,
            gridSizeInv: 1.0 / 32.0,
            leftBoundary: -64,
            rightBoundary: 64,
            bottomBoundary: -32,
            topBoundary: 32);

        Assert.Equal(new Vector2D(64, -32), snapped);
    }

    [Fact]
    public void MapOptionsReadsAndWritesGridSetup()
    {
        var options = new MapOptions();
        var grid = new GridSetup();
        grid.SetGridSize(64);
        grid.SetGridOrigin(8, 16);

        options.WriteGridSetup(grid);
        var restored = new GridSetup();
        options.ReadGridSetup(restored);

        Assert.Equal(64, restored.GridSizeF);
        Assert.Equal(8, restored.GridOriginX);
        Assert.Equal(16, restored.GridOriginY);
    }
}
