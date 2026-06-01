// ABOUTME: Tests UDB-style UDMF sidedef texture fit field calculations.
// ABOUTME: Covers scale, offset, automatic repeat, disabled-axis restore, and default field omission.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefTextureFittingTests
{
    [Fact]
    public void FitMiddleTextureWritesScaleAndOffsetFields()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);
        side.OffsetX = 8;
        side.OffsetY = 12;

        bool changed = SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(128, 64));

        Assert.True(changed);
        Assert.Equal(2.0, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(0.5, side.GetFloatField("scaley_mid", 1.0));
        Assert.Equal(-8.0, side.GetFloatField("offsetx_mid"));
        Assert.Equal(-12.0, side.GetFloatField("offsety_mid"));
    }

    [Fact]
    public void FitUsesTextureScaleAndRepeatOptions()
    {
        var (side, _) = OneSidedLine(length: 100, floor: 0, ceiling: 200);

        var options = new SidedefTextureFitOptions
        {
            HorizontalRepeat = 2.0,
            VerticalRepeat = 0.5,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(100, 100, ScaleX: 0.5, ScaleY: 2.0), options);

        Assert.Equal(1.0, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(0.5, side.GetFloatField("scaley_mid", 1.0));
    }

    [Fact]
    public void FitSubtractsControlSideOffsets()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);
        side.OffsetX = 8;
        side.OffsetY = 12;

        var options = new SidedefTextureFitOptions
        {
            ControlSideOffsetX = 5,
            ControlSideOffsetY = 7,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(128, 64), options);

        Assert.Equal(-13.0, side.GetFloatField("offsetx_mid"));
        Assert.Equal(-19.0, side.GetFloatField("offsety_mid"));
    }

    [Fact]
    public void FitAutoRepeatHonorsPatternSize()
    {
        var (side, _) = OneSidedLine(length: 96, floor: 0, ceiling: 160);

        var options = new SidedefTextureFitOptions
        {
            AutoWidth = true,
            AutoHeight = true,
            PatternWidth = 32,
            PatternHeight = 40,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(64, 80), options);

        Assert.Equal(1.0, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(1.0, side.GetFloatField("scaley_mid", 1.0));
    }

    [Fact]
    public void FitAcrossSurfacesUsesGlobalWidthAndPerSurfaceOffset()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);
        side.OffsetX = 4;

        var options = new SidedefTextureFitOptions
        {
            FitAcrossSurfaces = true,
            FitHeight = false,
            BoundsX = 64,
            GlobalBoundsWidth = 192,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(96, 64), options);

        Assert.Equal(0.5, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(28.0, side.GetFloatField("offsetx_mid"));
    }

    [Fact]
    public void FitAcrossSurfacesAutoRepeatUsesGlobalPatternWidth()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);

        var options = new SidedefTextureFitOptions
        {
            FitAcrossSurfaces = true,
            FitHeight = false,
            AutoWidth = true,
            PatternWidth = 64,
            GlobalBoundsWidth = 192,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(128, 64), options);

        Assert.Equal(1.0, side.GetFloatField("scalex_mid", 1.0));
    }

    [Fact]
    public void FitAcrossSurfacesWrapsXOffsetToTextureWidth()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);

        var options = new SidedefTextureFitOptions
        {
            FitAcrossSurfaces = true,
            FitHeight = false,
            BoundsX = 320,
            GlobalBoundsWidth = 640,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(128, 64), options);

        Assert.Equal(0.2, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(64.0, side.GetFloatField("offsetx_mid"));
    }

    [Fact]
    public void FitAcrossSurfacesUsesGlobalHeightAndPerSurfaceOffset()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);
        side.OffsetY = 10;

        var options = new SidedefTextureFitOptions
        {
            FitAcrossSurfaces = true,
            FitWidth = false,
            BoundsY = 64,
            GlobalBoundsHeight = 256,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(64, 128), options);

        Assert.Equal(0.5, side.GetFloatField("scaley_mid", 1.0));
        Assert.Equal(22.0, side.GetFloatField("offsety_mid"));
    }

    [Fact]
    public void FitHeightWrapsYOffsetToTextureHeight()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 32);
        side.OffsetY = 96;

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(64, 32));

        Assert.False(side.Fields.ContainsKey("scaley_mid"));
        Assert.Equal(0.0, side.GetFloatField("offsety_mid"));
    }

    [Fact]
    public void FitDisabledAxesRestoreInitialValues()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 128);
        side.SetFloatField("scalex_mid", 3.0, 1.0);
        side.SetFloatField("offsetx_mid", 15.0);
        side.SetFloatField("scaley_mid", 4.0, 1.0);
        side.SetFloatField("offsety_mid", 20.0);

        var options = new SidedefTextureFitOptions
        {
            FitWidth = false,
            FitHeight = false,
            InitialScaleX = 1.25,
            InitialOffsetX = 4.0,
            InitialScaleY = 0.75,
            InitialOffsetY = 6.0,
        };

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(128, 64), options);

        Assert.Equal(1.25, side.GetFloatField("scalex_mid", 1.0));
        Assert.Equal(4.0, side.GetFloatField("offsetx_mid"));
        Assert.Equal(0.75, side.GetFloatField("scaley_mid", 1.0));
        Assert.Equal(6.0, side.GetFloatField("offsety_mid"));
    }

    [Fact]
    public void FitOmitsDefaultFieldsWhenValuesReturnToDefaults()
    {
        var (side, _) = OneSidedLine(length: 64, floor: 0, ceiling: 64);
        side.SetFloatField("scalex_mid", 3.0, 1.0);
        side.SetFloatField("scaley_mid", 4.0, 1.0);

        SidedefTextureFitting.Fit(side, SidedefPart.Middle, new TextureFitImage(64, 64));

        Assert.False(side.Fields.ContainsKey("scalex_mid"));
        Assert.False(side.Fields.ContainsKey("scaley_mid"));
    }

    private static (Sidedef Side, Sector Sector) OneSidedLine(int length, int floor, int ceiling)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.FloorHeight = floor;
        sector.CeilHeight = ceiling;
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(length, 0));
        var line = map.AddLinedef(start, end);
        var side = map.AddSidedef(line, isFront: true, sector);
        map.BuildIndexes();
        return (side, sector);
    }
}
