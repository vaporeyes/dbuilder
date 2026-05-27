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
    3004 = Zombieman
    32000 = CustomActor, 1, 2
}
SpawnNums
{
    4 = DoomImp
    255 = BossBrain
}";

        var mi = MapInfo.Parse(text);

        Assert.Equal("Zombieman", mi.DoomEdNums[3004]);
        Assert.Equal("CustomActor", mi.DoomEdNums[32000]);
        Assert.Equal("DoomImp", mi.SpawnNums[4]);
        Assert.Equal("BossBrain", mi.SpawnNums[255]);
    }
}
