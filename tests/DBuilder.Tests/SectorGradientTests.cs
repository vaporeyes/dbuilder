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

    [Fact]
    public void ApplyGradientNormalizesUnknownInterpolationModes()
    {
        var sectors = new[]
        {
            new Sector { Brightness = 0 },
            new Sector { Brightness = 0 },
            new Sector { Brightness = 100 }
        };

        SectorGradient.Apply(sectors, SectorGradientTarget.Brightness, (InterpolationTools.Mode)999);

        Assert.Equal([0, 50, 100], sectors.Select(sector => sector.Brightness).ToArray());
    }

    [Fact]
    public void ApplyFloorLightGradientUsesAbsoluteAndRelativeBrightnessLikeUdb()
    {
        var sectors = new[]
        {
            new Sector { Brightness = 100 },
            new Sector { Brightness = 160 },
            new Sector { Brightness = 100 },
        };
        sectors[0].SetIntegerField("lightfloor", 20);
        sectors[2].SetField("lightfloorabsolute", true);
        sectors[2].SetIntegerField("lightfloor", 220);

        SectorGradient.Apply(sectors, SectorGradientTarget.FloorLight);

        Assert.Equal(20, sectors[0].GetIntegerField("lightfloor"));
        Assert.Equal(10, sectors[1].GetIntegerField("lightfloor"));
        Assert.Equal(220, sectors[2].GetIntegerField("lightfloor"));
    }

    [Fact]
    public void ApplyCeilingLightGradientWritesAbsoluteFieldsDirectly()
    {
        var sectors = new[]
        {
            new Sector { Brightness = 96 },
            new Sector { Brightness = 160 },
            new Sector { Brightness = 64 },
        };
        foreach (Sector sector in sectors) sector.SetField("lightceilingabsolute", true);
        sectors[0].SetIntegerField("lightceiling", 32);
        sectors[2].SetIntegerField("lightceiling", 128);

        SectorGradient.Apply(sectors, SectorGradientTarget.CeilingLight);

        Assert.Equal([32, 80, 128], sectors.Select(sector => sector.GetIntegerField("lightceiling")).ToArray());
    }

    [Fact]
    public void ApplyColorGradientRequiresFirstOrLastColorField()
    {
        var sectors = new[]
        {
            new Sector(),
            new Sector(),
            new Sector()
        };

        SectorGradientResult result = SectorGradient.Apply(sectors, SectorGradientTarget.LightColor);

        Assert.False(result.Applied);
        Assert.Equal("First or last selected sector must have the \"lightcolor\" property!", result.Message);
        Assert.DoesNotContain("lightcolor", sectors[1].Fields.Keys);
    }

    [Fact]
    public void ApplyLightColorGradientWritesInteriorSectorsOnly()
    {
        var sectors = new[]
        {
            new Sector(),
            new Sector(),
            new Sector()
        };
        sectors[0].SetIntegerField("lightcolor", 0xFF0000, 0xFFFFFF);
        sectors[2].SetIntegerField("lightcolor", 0x0000FF, 0xFFFFFF);

        SectorGradient.Apply(sectors, SectorGradientTarget.LightColor);

        Assert.Equal(0xFF0000, sectors[0].GetIntegerField("lightcolor", 0xFFFFFF));
        Assert.Equal(0x800080, sectors[1].GetIntegerField("lightcolor", 0xFFFFFF));
        Assert.Equal(0x0000FF, sectors[2].GetIntegerField("lightcolor", 0xFFFFFF));
    }

    [Fact]
    public void ApplyLightAndFadeColorGradientAppliesBothColorFields()
    {
        var sectors = new[]
        {
            new Sector(),
            new Sector(),
            new Sector()
        };
        sectors[0].SetIntegerField("lightcolor", 0x000000, 0xFFFFFF);
        sectors[2].SetIntegerField("lightcolor", 0xFFFFFF, 0xFFFFFF);
        sectors[0].SetIntegerField("fadecolor", 0x000000);
        sectors[2].SetIntegerField("fadecolor", 0x0000FF);

        SectorGradientResult result = SectorGradient.Apply(sectors, SectorGradientTarget.LightAndFadeColor);

        Assert.True(result.Applied);
        Assert.Equal(0x808080, sectors[1].GetIntegerField("lightcolor", 0xFFFFFF));
        Assert.Equal(0x000080, sectors[1].GetIntegerField("fadecolor"));
    }

    [Fact]
    public void ApplyLightAndFadeColorGradientReportsMissingEndpointFields()
    {
        var sectors = new[]
        {
            new Sector(),
            new Sector(),
            new Sector()
        };

        SectorGradientResult result = SectorGradient.Apply(sectors, SectorGradientTarget.LightAndFadeColor);

        Assert.False(result.Applied);
        Assert.Equal("First or last selected sector must have the \"fadecolor\" or \"lightcolor\" property!", result.Message);
    }
}
