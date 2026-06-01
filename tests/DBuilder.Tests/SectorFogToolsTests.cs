// ABOUTME: Verifies UDB-style sector fade color selection behavior.
// ABOUTME: Covers explicit sector fadecolor, outside fog sky handling, MAPINFO fade, and black fallback.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorFogToolsTests
{
    [Fact]
    public void SectorFadeColorFieldWinsAndPreservesRawAlpha()
    {
        var sector = Sector();
        sector.Fields["fadecolor"] = 0x102030;
        var mapInfo = MapInfo(fade: (1, 2, 3), outsideFog: (4, 5, 6));

        int color = SectorFogTools.GetSectorFadeColorArgb(sector, mapInfo, "F_SKY1");

        Assert.Equal(0x102030, color);
    }

    [Fact]
    public void OutsideFogAppliesOnlyToSkyCeilings()
    {
        var sector = Sector();
        var mapInfo = MapInfo(fade: (1, 2, 3), outsideFog: (4, 5, 6));

        int withoutSky = SectorFogTools.GetSectorFadeColorArgb(sector, mapInfo, "F_SKY1");
        sector.CeilTexture = "F_SKY1";
        int withSky = SectorFogTools.GetSectorFadeColorArgb(sector, mapInfo, "F_SKY1");

        Assert.Equal(unchecked((int)0xFF010203), withoutSky);
        Assert.Equal(unchecked((int)0xFF040506), withSky);
    }

    [Fact]
    public void MapInfoFadeColorAppliesWhenOutsideFogDoesNot()
    {
        var sector = Sector();
        var mapInfo = MapInfo(fade: (0x20, 0x40, 0x60), outsideFog: null);

        int color = SectorFogTools.GetSectorFadeColorArgb(sector, mapInfo, "F_SKY1");

        Assert.Equal(unchecked((int)0xFF204060), color);
    }

    [Fact]
    public void MissingFogFallsBackToOpaqueBlack()
    {
        int color = SectorFogTools.GetSectorFadeColorArgb(Sector(), mapInfo: null, "F_SKY1");

        Assert.Equal(unchecked((int)0xFF000000), color);
    }

    [Fact]
    public void ConfigOverloadUsesConfiguredSkyFlatName()
    {
        var sector = Sector();
        sector.CeilTexture = "SKYFLAT";
        var mapInfo = MapInfo(fade: (1, 2, 3), outsideFog: (4, 5, 6));
        var config = GameConfiguration.FromText("""skyflatname = "skyflat";""");

        int color = SectorFogTools.GetSectorFadeColorArgb(sector, mapInfo, config);

        Assert.Equal(unchecked((int)0xFF040506), color);
    }

    private static Sector Sector()
        => new() { FloorHeight = 0, CeilHeight = 128, CeilTexture = "-" };

    private static MapInfoEntry MapInfo((byte R, byte G, byte B)? fade, (byte R, byte G, byte B)? outsideFog)
        => new()
        {
            FadeColor = fade,
            FogDensity = fade.HasValue ? 255 : 0,
            OutsideFogColor = outsideFog,
            OutsideFogDensity = outsideFog.HasValue ? 255 : 0,
        };
}
