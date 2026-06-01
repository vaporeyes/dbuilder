// ABOUTME: Verifies UDB-style sidedef lightfog flag maintenance behavior.
// ABOUTME: Covers MAPINFO fade, outside fog sky checks, sector fadecolor, and light field removal.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefFogToolsTests
{
    [Fact]
    public void AddsLightFogWhenSidedefLightAndMapFadeColorExist()
    {
        var side = Side();
        side.Fields["light"] = 144;

        int result = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: true, mapHasOutsideFogColor: false);

        Assert.Equal(1, result);
        Assert.True(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void RemovesLightFogWhenSidedefLightFieldIsMissing()
    {
        var side = Side();
        side.SetFlag("lightfog", true);

        int result = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: true, mapHasOutsideFogColor: true);

        Assert.Equal(-1, result);
        Assert.False(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void OutsideFogOnlyAddsLightFogForSkyCeiling()
    {
        var side = Side();
        side.Fields["light"] = 144;

        int withoutSky = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: false, mapHasOutsideFogColor: true);

        side.Sector!.CeilTexture = "F_SKY1";
        int withSky = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: false, mapHasOutsideFogColor: true);

        Assert.Equal(0, withoutSky);
        Assert.Equal(1, withSky);
        Assert.True(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void SectorFadeColorAddsLightFogWithoutMapFog()
    {
        var side = Side();
        side.Fields["light"] = 144;
        side.Sector!.Fields["fadecolor"] = 0x102030;

        int result = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: false, mapHasOutsideFogColor: false);

        Assert.Equal(1, result);
        Assert.True(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void RemovesExistingLightFogWhenFogNoLongerApplies()
    {
        var side = Side();
        side.Fields["light"] = 144;
        side.SetFlag("lightfog", true);

        int result = SidedefFogTools.UpdateLightFogFlag(side, mapHasFadeColor: false, mapHasOutsideFogColor: false);

        Assert.Equal(-1, result);
        Assert.False(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void MapInfoOverloadUsesConfiguredSkyFlat()
    {
        var mapInfo = new MapInfoEntry
        {
            FadeColor = (0, 0, 0),
            FogDensity = 0,
            OutsideFogColor = (1, 2, 3),
            OutsideFogDensity = 255,
        };
        var config = GameConfiguration.FromText("""skyflatname = "SKYFLAT";""");
        var side = Side();
        side.Fields["light"] = 144;
        side.Sector!.CeilTexture = "skyflat";

        int result = SidedefFogTools.UpdateLightFogFlag(side, mapInfo, config);

        Assert.Equal(1, result);
        Assert.True(side.IsFlagSet("lightfog"));
    }

    private static Sidedef Side()
    {
        var line = new Linedef();
        var sector = new Sector { FloorHeight = 0, CeilHeight = 128, CeilTexture = "-" };
        var side = new Sidedef(line, isFront: true) { Sector = sector };
        line.Front = side;
        return side;
    }
}
