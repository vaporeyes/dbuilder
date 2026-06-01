// ABOUTME: Tests UDB-style selected-sector gradient helpers for heights and brightness.
// ABOUTME: Covers minimum selection rules, map-order interpolation, and non-linear modes.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorGradientTests
{
    [Fact]
    public void ApplyRequiresAtLeastThreeSectors()
    {
        var sectors = new[]
        {
            new Sector { FloorHeight = 0 },
            new Sector { FloorHeight = 128 }
        };

        SectorGradientResult result = SectorGradient.Apply(sectors, SectorGradientTarget.FloorHeight);

        Assert.False(result.Applied);
        Assert.Equal(2, result.SectorCount);
        Assert.Equal("Select at least 3 sectors first!", result.Message);
        Assert.Equal(0, sectors[0].FloorHeight);
        Assert.Equal(128, sectors[1].FloorHeight);
    }

    [Fact]
    public void ApplyFloorHeightGradientInterpolatesAcrossAllSelectedSectors()
    {
        var sectors = new[]
        {
            new Sector { FloorHeight = 0 },
            new Sector { FloorHeight = 1 },
            new Sector { FloorHeight = 2 },
            new Sector { FloorHeight = 96 }
        };

        SectorGradientResult result = SectorGradient.Apply(sectors, SectorGradientTarget.FloorHeight);

        Assert.True(result.Applied);
        Assert.Equal("Created gradient floor heights over selected sectors.", result.Message);
        Assert.Equal([0, 32, 64, 96], sectors.Select(sector => sector.FloorHeight).ToArray());
    }

    [Fact]
    public void ApplyCeilingHeightGradientUsesSelectedOrder()
    {
        var sectors = new[]
        {
            new Sector { CeilHeight = 192 },
            new Sector { CeilHeight = 0 },
            new Sector { CeilHeight = 0 },
            new Sector { CeilHeight = 0 }
        };

        SectorGradient.Apply(sectors, SectorGradientTarget.CeilingHeight);

        Assert.Equal([192, 128, 64, 0], sectors.Select(sector => sector.CeilHeight).ToArray());
    }

    [Fact]
    public void ApplyBrightnessGradientCanUseEaseModes()
    {
        var sectors = new[]
        {
            new Sector { Brightness = 0 },
            new Sector { Brightness = 0 },
            new Sector { Brightness = 0 },
            new Sector { Brightness = 0 },
            new Sector { Brightness = 255 }
        };

        SectorGradient.Apply(sectors, SectorGradientTarget.Brightness, InterpolationTools.Mode.EASE_IN_SINE);

        Assert.Equal([0, 19, 75, 157, 255], sectors.Select(sector => sector.Brightness).ToArray());
    }
}
