// ABOUTME: Verifies UDB-style glowing flat display resolution for UDMF fields and GLDEFS entries.
// ABOUTME: Covers fullbright surface lighting overrides used by classic and visual render planning.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class GlowingFlatDisplayTests
{
    [Fact]
    public void UdmfFloorGlowFieldsOverrideGldefs()
    {
        var sector = new Sector { FloorTexture = "GLDFLAT" };
        sector.Fields["floorglowcolor"] = 0x2040ff;
        sector.Fields["floorglowheight"] = 72.0;
        Gldefs gldefs = GldefsParser.Parse("""
            glow { flats { GLDFLAT } }
            """);

        GlowingFlatDisplayState? state = GlowingFlatDisplay.Resolve(sector, GlowingFlatSurface.Floor, gldefs, isUdmf: true);

        Assert.NotNull(state);
        Assert.Equal(0x2040ff, state!.Color);
        Assert.Equal(72.0, state.Height);
        Assert.Equal((0x20 + 0x40 + 0xff) / 3, state.Brightness);
        Assert.False(state.Fullbright);
        Assert.False(state.CalculateTextureColor);
    }

    [Fact]
    public void UdmfDisabledGlowSuppressesGldefsFallback()
    {
        var sector = new Sector { CeilTexture = "GLOWCEIL" };
        sector.Fields["ceilingglowcolor"] = -1;
        Gldefs gldefs = GldefsParser.Parse("""
            glow { flats { GLOWCEIL } }
            """);

        Assert.Null(GlowingFlatDisplay.Resolve(sector, GlowingFlatSurface.Ceiling, gldefs, isUdmf: true));
    }

    [Fact]
    public void GldefsGlowUsesTextureColorCalculationDefaults()
    {
        var sector = new Sector { FloorTexture = "NUKAGE1" };
        Gldefs gldefs = GldefsParser.Parse("""
            glow { flats { NUKAGE1 } }
            """);

        GlowingFlatDisplayState? state = GlowingFlatDisplay.Resolve(sector, GlowingFlatSurface.Floor, gldefs, isUdmf: false);

        Assert.NotNull(state);
        Assert.Equal(0xffffff, state!.Color);
        Assert.Equal(128, state.Height);
        Assert.Equal(255, state.Brightness);
        Assert.True(state.Fullbright);
        Assert.True(state.CalculateTextureColor);
    }

    [Fact]
    public void GldefsTextureGlowKeepsConfiguredColorHeightAndBrightness()
    {
        var sector = new Sector { CeilTexture = "GLOWHEX" };
        Gldefs gldefs = GldefsParser.Parse("""
            glow { texture GLOWHEX, "#2040ff", 32 }
            """);

        GlowingFlatDisplayState? state = GlowingFlatDisplay.Resolve(sector, GlowingFlatSurface.Ceiling, gldefs, isUdmf: false);

        Assert.NotNull(state);
        Assert.Equal(0x2040ff, state!.Color);
        Assert.Equal(64, state.Height);
        Assert.Equal((0x20 + 0x40 + 0xff) / 3, state.Brightness);
        Assert.False(state.Fullbright);
        Assert.False(state.CalculateTextureColor);
    }

    [Fact]
    public void FullbrightGldefsGlowForcesSurfaceLightingOverride()
    {
        var sector = new Sector { FloorTexture = "BRIGHT" };
        sector.Fields["lightcolor"] = 0x2040ff;
        sector.Fields["color_floor"] = 0x808080;
        sector.Fields["lightfloor"] = -64;
        sector.Fields["lightfloorabsolute"] = false;
        Gldefs gldefs = GldefsParser.Parse("""
            glow { texture BRIGHT, "#2040ff", fullbright }
            """);

        GlowingFlatSurfaceLighting lighting = GlowingFlatDisplay.SurfaceLighting(sector, GlowingFlatSurface.Floor, gldefs);

        Assert.Equal(-1, lighting.Color);
        Assert.Equal(255, lighting.Light);
        Assert.True(lighting.Absolute);
    }

    [Fact]
    public void NonFullbrightSurfaceLightingUsesModulatedUdmfLightingFields()
    {
        var sector = new Sector { CeilTexture = "DIM" };
        sector.Fields["lightcolor"] = 0x804020;
        sector.Fields["color_ceiling"] = 0x80ff40;
        sector.Fields["lightceiling"] = 32;
        sector.Fields["lightceilingabsolute"] = true;
        Gldefs gldefs = GldefsParser.Parse("""
            glow { texture DIM, "#2040ff" }
            """);

        GlowingFlatSurfaceLighting lighting = GlowingFlatDisplay.SurfaceLighting(sector, GlowingFlatSurface.Ceiling, gldefs);

        Assert.Equal(0x404008, lighting.Color);
        Assert.Equal(32, lighting.Light);
        Assert.True(lighting.Absolute);
    }

    [Fact]
    public void SurfaceRenderTintUsesFullbrightOverride()
    {
        var lighting = new GlowingFlatSurfaceLighting(
            GlowingFlatDisplay.NoColorOverride,
            GlowingFlatDisplay.DefaultGlowBrightness,
            Absolute: true);

        int tint = GlowingFlatDisplay.SurfaceRenderTint(64, lighting, fullBrightness: false, scale: 0.85);

        Assert.Equal(unchecked((int)0xffd8d8d8), tint);
    }

    [Fact]
    public void SurfaceRenderTintAppliesRelativeLight()
    {
        var lighting = new GlowingFlatSurfaceLighting(
            GlowingFlatDisplay.NoColorOverride,
            Light: 32,
            Absolute: false);

        int tint = GlowingFlatDisplay.SurfaceRenderTint(96, lighting, fullBrightness: false, scale: 1.0);

        Assert.Equal(unchecked((int)0xff808080), tint);
    }

    [Fact]
    public void SurfaceRenderTintUsesClassicBrightnessBands()
    {
        var lighting = new GlowingFlatSurfaceLighting(
            GlowingFlatDisplay.NoColorOverride,
            Light: 0,
            Absolute: false);

        int tint = GlowingFlatDisplay.SurfaceRenderTint(101, lighting, fullBrightness: false, scale: 1.0, classicRendering: true);

        Assert.Equal(unchecked((int)0xff606060), tint);
    }

    [Fact]
    public void SurfaceRenderTintUsesSurfaceColor()
    {
        var lighting = new GlowingFlatSurfaceLighting(
            Color: 0x804020,
            Light: 128,
            Absolute: true);

        int tint = GlowingFlatDisplay.SurfaceRenderTint(255, lighting, fullBrightness: false, scale: 1.0);

        Assert.Equal(unchecked((int)0xff402010), tint);
    }
}
