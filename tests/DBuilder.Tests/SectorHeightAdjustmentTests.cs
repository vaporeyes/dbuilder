// ABOUTME: Covers UDB-style fixed-step selected-sector height actions.
// ABOUTME: Verifies floor and ceiling mutation metadata for editor command integration.

using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorHeightAdjustmentTests
{
    [Fact]
    public void ApplyLowersFloorHeights()
    {
        var first = new Sector { FloorHeight = 16, CeilHeight = 128 };
        var second = new Sector { FloorHeight = -8, CeilHeight = 96 };

        SectorHeightAdjustmentResult result = SectorHeightAdjustment.Apply(
            [first, second],
            SectorHeightPart.Floor,
            -8);

        Assert.Equal(8, first.FloorHeight);
        Assert.Equal(-16, second.FloorHeight);
        Assert.Equal(128, first.CeilHeight);
        Assert.Equal(96, second.CeilHeight);
        Assert.Equal(2, result.ChangedCount);
        Assert.Equal("Floor heights change", result.UndoDescription);
        Assert.Equal("Lowered floor heights by 8mp.", result.StatusMessage);
    }

    [Fact]
    public void ApplyRaisesCeilingHeights()
    {
        var first = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var second = new Sector { FloorHeight = 24, CeilHeight = 72 };

        SectorHeightAdjustmentResult result = SectorHeightAdjustment.Apply(
            [first, second],
            SectorHeightPart.Ceiling,
            8);

        Assert.Equal(136, first.CeilHeight);
        Assert.Equal(80, second.CeilHeight);
        Assert.Equal(0, first.FloorHeight);
        Assert.Equal(24, second.FloorHeight);
        Assert.Equal(2, result.ChangedCount);
        Assert.Equal("Ceiling heights change", result.UndoDescription);
        Assert.Equal("Raised ceiling heights by 8mp.", result.StatusMessage);
    }
}
