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

    [Fact]
    public void ApplyLightFogFlagsCountsFrontAndBackChanges()
    {
        var line = new Linedef();
        var front = Side(line, isFront: true);
        var back = Side(line, isFront: false);
        front.Fields["light"] = 144;
        front.Sector!.Fields["fadecolor"] = 0x102030;
        back.SetFlag("lightfog", true);

        SidedefLightFogFlagResult result = SidedefFogTools.ApplyLightFogFlags([line], mapInfo: null, config: null);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.RemovedCount);
        Assert.True(result.Changed);
        Assert.Equal("Added 'lightfog' flag to 1 sidedefs, removed it from 1 sidedefs.", result.Message);
        Assert.True(front.IsFlagSet("lightfog"));
        Assert.False(back.IsFlagSet("lightfog"));
    }

    private static Sidedef Side()
        => Side(new Linedef(), isFront: true);

    private static Sidedef Side(Linedef line, bool isFront)
    {
        var sector = new Sector { FloorHeight = 0, CeilHeight = 128, CeilTexture = "-" };
        var side = new Sidedef(line, isFront) { Sector = sector };
        if (isFront) line.Front = side;
        else line.Back = side;
        return side;
    }
}
