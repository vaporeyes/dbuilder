// ABOUTME: Tests for MapInfo - parsing ZDoom new (brace) and old (line) MAPINFO formats into map entries.
// ABOUTME: Covers field extraction, lookup titles, multiple maps, comments, and unknown directive skipping.

using DBuilder.IO;

namespace DBuilder.Tests;

public class MapInfoTests
{
    [Fact]
    public void ParsesNewBraceFormat()
    {
        const string text = @"
map MAP01 ""Entryway""
{
    next = ""MAP02""
    secretnext = ""MAP31""
    music = ""D_RUNNIN""
    sky1 = ""SKY1"", 0
    cluster = 1
    levelnum = 1
    par = 30
}";
        var mi = MapInfo.Parse(text);
        var m = mi.GetMap("MAP01");
        Assert.NotNull(m);
        Assert.Equal("Entryway", m!.Title);
        Assert.False(m.TitleIsLookup);
        Assert.Equal("MAP02", m.Next);
        Assert.Equal("MAP31", m.SecretNext);
        Assert.Equal("D_RUNNIN", m.Music);
        Assert.Equal("SKY1", m.Sky1);
        Assert.Equal(1, m.Cluster);
        Assert.Equal(1, m.LevelNum);
        Assert.Equal(30, m.Par);
    }

    [Fact]
    public void ParsesOldLineFormat()
    {
        const string text = @"
map MAP01 ""Entryway""
    next MAP02
    secretnext MAP31
    sky1 SKY1
    cluster 1
    par 30
    music D_RUNNIN

map MAP02 ""Underhalls""
    next MAP03
    cluster 1";
        var mi = MapInfo.Parse(text);
        Assert.Equal(2, mi.Maps.Count);

        var m1 = mi.GetMap("MAP01")!;
        Assert.Equal("Entryway", m1.Title);
        Assert.Equal("MAP02", m1.Next);
        Assert.Equal("MAP31", m1.SecretNext);
        Assert.Equal("SKY1", m1.Sky1);
        Assert.Equal(30, m1.Par);

        var m2 = mi.GetMap("MAP02")!;
        Assert.Equal("Underhalls", m2.Title);
        Assert.Equal("MAP03", m2.Next);
        Assert.Null(m2.Par);
    }

    [Fact]
    public void ParsesLookupTitle()
    {
        const string text = "map MAP01 lookup \"HUSTR_1\"\n{\n next = \"MAP02\"\n}";
        var m = MapInfo.Parse(text).GetMap("MAP01")!;
        Assert.True(m.TitleIsLookup);
        Assert.Equal("HUSTR_1", m.Title);
        Assert.Equal("MAP02", m.Next);
    }

    [Fact]
    public void SkipsUnknownDirectivesAndComments()
    {
        const string text = @"
// a leading comment
gameinfo
{
    bordertexture = ""GRNROCK""
}

cluster 1
{
    flat = ""SLIME16""
    music = ""D_READ_M""
}

/* block comment
   spanning lines */
map MAP01 ""Entryway""
{
    next = ""MAP02""
}";
        var mi = MapInfo.Parse(text);
        Assert.Single(mi.Maps);
        var m = mi.GetMap("MAP01")!;
        Assert.Equal("Entryway", m.Title);
        Assert.Equal("MAP02", m.Next);
    }

    [Fact]
    public void StopsAtGzdbSkipDirective()
    {
        const string text = @"
map MAP01 ""Before"" { next = MAP02 }
$gzdb_skip
map MAP02 ""After"" { next = MAP03 }";

        var mi = MapInfo.Parse(text);

        Assert.NotNull(mi.GetMap("MAP01"));
        Assert.Null(mi.GetMap("MAP02"));
    }

    [Fact]
    public void ParsesGameInfoSkyFlatName()
    {
        const string text = @"
gameinfo
{
    SkyFlatName = ""skyflat""
    bordertexture = ""GRNROCK""
}
map MAP01 ""Entryway"" { next = MAP02 }";

        var mi = MapInfo.Parse(text);

        Assert.Equal("SKYFLAT", mi.SkyFlatName);
        Assert.Equal("MAP02", mi.GetMap("MAP01")!.Next);
    }

    [Fact]
    public void UnknownPropertiesLandInDictionary()
    {
        const string text = "map MAP01 \"X\"\n{\n titlepatch = \"CWILV00\"\n flags = \"foo\"\n}";
        var m = MapInfo.Parse(text).GetMap("MAP01")!;
        Assert.Equal("CWILV00", m.TitlePatch);
        Assert.True(m.Properties.ContainsKey("flags"));
        Assert.Equal("foo", m.Properties["flags"]);
    }

    [Fact]
    public void HandlesNumericHexenMapName()
    {
        const string text = @"
map 1 ""Winnowing Hall""
    next 2
    cluster 1
    music WINNOWR";
        var m = MapInfo.Parse(text).GetMap("1")!;
        Assert.Equal("Winnowing Hall", m.Title);
        Assert.Equal("2", m.Next);
        Assert.Equal("WINNOWR", m.Music);
    }

    [Fact]
    public void ParsesDoomEdNumsAndSpawnNums()
    {
        const string text = @"
DoomEdNums
{
    0 = IgnoredActor
    3004 = Zombieman
    32000 = CustomActor, 1, 2
}
SpawnNums
{
    4 = DoomImp
    255 = BossBrain
}";

        var mi = MapInfo.Parse(text);

        Assert.False(mi.DoomEdNums.ContainsKey(0));
        Assert.Equal("zombieman", mi.DoomEdNums[3004]);
        Assert.Equal("customactor", mi.DoomEdNums[32000]);
        Assert.Equal("doomimp", mi.SpawnNums[4]);
        Assert.Equal("bossbrain", mi.SpawnNums[255]);
    }

    [Fact]
    public void ParsesIncludesOnce()
    {
        const string text = @"
include ""mapinfo/maps.txt""
include ""mapinfo/maps.txt""
DoomEdNums { 9001 = LocalActor }";

        var mi = MapInfo.Parse(text, include => include == "mapinfo/maps.txt"
            ? "map MAP01 \"Entryway\" { next = MAP02 }\nDoomEdNums { 9000 = IncludedActor }"
            : null);

        Assert.Single(mi.Maps);
        Assert.Equal("MAP02", mi.GetMap("MAP01")!.Next);
        Assert.Equal("includedactor", mi.DoomEdNums[9000]);
        Assert.Equal("localactor", mi.DoomEdNums[9001]);
    }

    [Fact]
    public void MergesIncludedGameInfo()
    {
        const string text = @"include ""mapinfo/gameinfo.txt""";

        var mi = MapInfo.Parse(text, include => include == "mapinfo/gameinfo.txt"
            ? "gameinfo { SkyFlatName = F_SKY2 }"
            : null);

        Assert.Equal("F_SKY2", mi.SkyFlatName);
    }

    [Fact]
    public void AppliesDefaultMapAndAddDefaultMap()
    {
        const string text = @"
defaultmap
{
    sky1 = SKY1
    music = D_RUNNIN
    cluster = 1
}
map MAP01 ""Entryway""
{
    next = MAP02
}
adddefaultmap
{
    par = 45
}
map MAP02 ""Underhalls""
{
    sky1 = SKY2
}";

        var mi = MapInfo.Parse(text);

        var map01 = mi.GetMap("MAP01")!;
        Assert.Equal("SKY1", map01.Sky1);
        Assert.Equal("D_RUNNIN", map01.Music);
        Assert.Equal(1, map01.Cluster);
        Assert.Null(map01.Par);

        var map02 = mi.GetMap("MAP02")!;
        Assert.Equal("SKY2", map02.Sky1);
        Assert.Equal("D_RUNNIN", map02.Music);
        Assert.Equal(1, map02.Cluster);
        Assert.Equal(45, map02.Par);
    }

    [Fact]
    public void ParsesVisualDisplayProperties()
    {
        const string text = @"
map MAP01 ""Entryway""
{
    sky1 = sky1, 0.25
    sky2 = sky2, -0.5
    doublesky
    fade = ""204060""
    outsidefog = ""102030""
    fogdensity = 64
    outsidefogdensity = 32
    evenlighting
    smoothlighting
    forceworldpanning
    horizwallshade = 8
    vertwallshade = -16
    lightmode = 3
    lightattenuationmode = ""linear""
    pixelratio = 1.2
}";

        var map = MapInfo.Parse(text).GetMap("MAP01")!;

        Assert.Equal("SKY1", map.Sky1);
        Assert.Equal("SKY2", map.Sky2);
        Assert.Equal(0.25f, map.Sky1ScrollSpeed);
        Assert.Equal(-0.5f, map.Sky2ScrollSpeed);
        Assert.True(map.DoubleSky);
        Assert.True(map.EvenLighting);
        Assert.True(map.SmoothLighting);
        Assert.True(map.ForceWorldPanning);
        Assert.Equal("204060", map.Fade);
        Assert.Equal(((byte)0x20, (byte)0x40, (byte)0x60), map.FadeColor);
        Assert.True(map.HasFadeColor);
        Assert.Equal("102030", map.OutsideFog);
        Assert.Equal(((byte)0x10, (byte)0x20, (byte)0x30), map.OutsideFogColor);
        Assert.True(map.HasOutsideFogColor);
        Assert.Equal(64, map.FogDensity);
        Assert.Equal(32, map.OutsideFogDensity);
        Assert.Equal(8, map.HorizWallShade);
        Assert.Equal(-16, map.VertWallShade);
        Assert.Equal("3", map.LightMode);
        Assert.Equal("linear", map.LightAttenuationMode);
        Assert.Equal(1.2, map.PixelRatio);
    }

    [Fact]
    public void NormalizesSkyboxTextureName()
    {
        const string text = @"map MAP01 ""Entryway"" { skybox = ""skybox01"" }";

        var map = MapInfo.Parse(text).GetMap("MAP01")!;

        Assert.Equal("SKYBOX01", map.Sky1);
        Assert.Equal(0, map.Sky1ScrollSpeed);
    }

    [Fact]
    public void RequiresEqualsForNewFormatOnlyVisualProperties()
    {
        const string text = @"
map MAP01 ""OldStyle""
{
    skybox skybox01
    fogdensity 64
    outsidefogdensity 32
    lightmode 3
    lightattenuationmode linear
    pixelratio 1.0
}
map MAP02 ""NewStyle""
{
    skybox = skybox02
    fogdensity = 64
    outsidefogdensity = 32
    lightmode = 3
    lightattenuationmode = linear
    pixelratio = 1.0
}";

        var oldStyle = MapInfo.Parse(text).GetMap("MAP01")!;
        var newStyle = MapInfo.Parse(text).GetMap("MAP02")!;

        Assert.Null(oldStyle.Sky1);
        Assert.Equal(255, oldStyle.FogDensity);
        Assert.Equal(255, oldStyle.OutsideFogDensity);
        Assert.Null(oldStyle.LightMode);
        Assert.Null(oldStyle.LightAttenuationMode);
        Assert.Equal(1.2, oldStyle.PixelRatio);
        Assert.Equal("SKYBOX02", newStyle.Sky1);
        Assert.Equal(64, newStyle.FogDensity);
        Assert.Equal(32, newStyle.OutsideFogDensity);
        Assert.Equal("3", newStyle.LightMode);
        Assert.Equal("linear", newStyle.LightAttenuationMode);
        Assert.Equal(1.0, newStyle.PixelRatio);
    }

    [Fact]
    public void ParsesSpacedMapInfoFadeColors()
    {
        const string text = @"
map MAP01 ""Entryway""
{
    fade = ""20 40 60""
    outsidefog = ""10 20 30""
    outsidefogdensity = 32
}";

        var map = MapInfo.Parse(text).GetMap("MAP01")!;

        Assert.Equal("204060", map.Fade);
        Assert.Equal(((byte)0x20, (byte)0x40, (byte)0x60), map.FadeColor);
        Assert.True(map.HasFadeColor);
        Assert.Equal("102030", map.OutsideFog);
        Assert.Equal(((byte)0x10, (byte)0x20, (byte)0x30), map.OutsideFogColor);
        Assert.True(map.HasOutsideFogColor);
    }

    [Fact]
    public void StripsAttachedCommasFromMapInfoSkyTextures()
    {
        const string text = @"
map MAP01 ""Entryway""
{
    sky1 = ""sky1,"" 0.25
    sky2 = sky2, -0.5
}";

        var map = MapInfo.Parse(text).GetMap("MAP01")!;

        Assert.Equal("SKY1", map.Sky1);
        Assert.Equal(0.25f, map.Sky1ScrollSpeed);
        Assert.Equal("SKY2", map.Sky2);
        Assert.Equal(-0.5f, map.Sky2ScrollSpeed);
    }

    [Fact]
    public void ClearsDoubleSkyWithoutSecondSky()
    {
        const string text = @"
map MAP01 ""Entryway"" { doublesky }
map MAP02 ""Underhalls""
{
    sky2 = SKY2
    doublesky
}";

        var mi = MapInfo.Parse(text);

        Assert.False(mi.GetMap("MAP01")!.DoubleSky);
        Assert.True(mi.GetMap("MAP02")!.DoubleSky);
    }

    [Fact]
    public void ParsesMultipleBracePropertiesOnOneLine()
    {
        const string text = @"map MAP01 ""Entryway"" { next = MAP02 secretnext = MAP31 sky1 = SKY1 par = 30 doublesky }";

        var map = MapInfo.Parse(text).GetMap("MAP01")!;

        Assert.Equal("MAP02", map.Next);
        Assert.Equal("MAP31", map.SecretNext);
        Assert.Equal("SKY1", map.Sky1);
        Assert.Equal(30, map.Par);
        Assert.False(map.DoubleSky);
    }

    [Fact]
    public void UsesUdbVisualDefaultsAndClampsWallShades()
    {
        const string text = @"
map MAP01 ""Defaults"" { fade = 204060 }
map MAP02 ""Clamped""
{
    horizwallshade = -999
    vertwallshade = 999
}";

        var mi = MapInfo.Parse(text);
        var defaults = mi.GetMap("MAP01")!;
        var clamped = mi.GetMap("MAP02")!;

        Assert.Equal(255, defaults.FogDensity);
        Assert.Equal(255, defaults.OutsideFogDensity);
        Assert.Equal(-16, defaults.HorizWallShade);
        Assert.Equal(16, defaults.VertWallShade);
        Assert.Equal(1.2, defaults.PixelRatio);
        Assert.True(defaults.HasFadeColor);
        Assert.Equal(-255, clamped.HorizWallShade);
        Assert.Equal(255, clamped.VertWallShade);
    }
}
