// ABOUTME: Verifies visual-mode lighting tints for renderable map surfaces.
// ABOUTME: Covers UDMF sidedef light fields and sector lightcolor behavior used by 3D rendering.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class VisualSurfaceLightingTests
{
    [Fact]
    public void WallRenderTintUsesRelativeSidedefLight()
    {
        Sidedef side = Side(new Sector { Brightness = 96 });
        side.SetIntegerField("light", 32);

        int tint = VisualSurfaceLighting.WallRenderTint(side, fullBrightness: false, scale: 1.0);

        Assert.Equal(unchecked((int)0xff808080), tint);
    }

    [Fact]
    public void WallRenderTintUsesAbsoluteSidedefLight()
    {
        Sidedef side = Side(new Sector { Brightness = 96 });
        side.SetField("lightabsolute", true);
        side.SetIntegerField("light", 192);

        int tint = VisualSurfaceLighting.WallRenderTint(side, fullBrightness: false, scale: 1.0);

        Assert.Equal(unchecked((int)0xffc0c0c0), tint);
    }

    [Fact]
    public void WallRenderTintUsesSectorLightColor()
    {
        var sector = new Sector { Brightness = 128 };
        sector.SetIntegerField("lightcolor", 0x804020);
        Sidedef side = Side(sector);

        int tint = VisualSurfaceLighting.WallRenderTint(side, fullBrightness: false, scale: 1.0);

        Assert.Equal(unchecked((int)0xff402010), tint);
    }

    [Fact]
    public void WallRenderTintUsesFullBrightnessOverride()
    {
        Sidedef side = Side(new Sector { Brightness = 32 });

        int tint = VisualSurfaceLighting.WallRenderTint(side, fullBrightness: true, scale: 0.6);

        Assert.Equal(unchecked((int)0xff999999), tint);
    }

    private static Sidedef Side(Sector sector)
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        return new Sidedef(line, isFront: true) { Sector = sector };
    }
}
