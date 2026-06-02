// ABOUTME: Covers UDB-style selected-sector brightness step actions.
// ABOUTME: Verifies configured step-list behavior, boundary handling, and status metadata.

using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorBrightnessAdjustmentTests
{
    [Fact]
    public void ApplyRaisesBrightnessToNextConfiguredLevel()
    {
        var first = new Sector { Brightness = 128 };
        var second = new Sector { Brightness = 160 };

        SectorBrightnessAdjustmentResult result = SectorBrightnessAdjustment.Apply(
            [first, second],
            [0, 128, 160, 192, 255],
            raise: true);

        Assert.Equal(160, first.Brightness);
        Assert.Equal(192, second.Brightness);
        Assert.Equal(2, result.ChangedCount);
        Assert.Equal(32, result.Delta);
        Assert.Equal("Sector brightness change", result.UndoDescription);
        Assert.Equal("Raised sector brightness by 32.", result.StatusMessage);
    }

    [Fact]
    public void ApplyLowersBrightnessToNextConfiguredLevel()
    {
        var first = new Sector { Brightness = 192 };
        var second = new Sector { Brightness = 129 };

        SectorBrightnessAdjustmentResult result = SectorBrightnessAdjustment.Apply(
            [first, second],
            [0, 128, 160, 192, 255],
            raise: false);

        Assert.Equal(160, first.Brightness);
        Assert.Equal(128, second.Brightness);
        Assert.Equal(32, result.Delta);
        Assert.Equal("Lowered sector brightness by 32.", result.StatusMessage);
    }

    [Fact]
    public void ApplyReportsZeroDeltaAtBoundaryLikeUdbStepsList()
    {
        var sector = new Sector { Brightness = 255 };

        SectorBrightnessAdjustmentResult result = SectorBrightnessAdjustment.Apply(
            [sector],
            [0, 128, 255],
            raise: true);

        Assert.Equal(255, sector.Brightness);
        Assert.Equal(0, result.Delta);
        Assert.Equal("Raised sector brightness by 0.", result.StatusMessage);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 16)]
    [InlineData(255, 255)]
    public void NextHigherUsesFallbackLevelsWhenConfigHasNoSteps(int current, int expected)
        => Assert.Equal(expected, SectorBrightnessAdjustment.NextHigher([], current));

    [Theory]
    [InlineData(255, 224)]
    [InlineData(224, 192)]
    [InlineData(0, 0)]
    public void NextLowerUsesFallbackLevelsWhenConfigHasNoSteps(int current, int expected)
        => Assert.Equal(expected, SectorBrightnessAdjustment.NextLower([], current));
}
