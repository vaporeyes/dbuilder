// ABOUTME: Tests for TerrainParser behavior over TERRAIN terrain and splash declarations.
// ABOUTME: Covers flat properties, splash metadata, comments, and unknown block skipping.

using DBuilder.IO;

namespace DBuilder.Tests;

public class TerrainParserTests
{
    [Fact]
    public void ParsesTerrainAndSplashDefinitions()
    {
        const string text = @"
// water movement metadata
splash Water
{
    smallclass WaterSplash
    baseclass WaterBase
    chunkclass WaterChunk
    sound world/water
}
terrain FWATER1
{
    splash Water
    footclip 10
    friction 0.8125
    liquid
    damageamount 5
    damagetype Slime
}";

        var data = TerrainParser.Parse(text);

        Assert.True(data.Splashes.ContainsKey("Water"));
        Assert.Equal("WaterSplash", data.Splashes["Water"].SmallClass);
        Assert.Equal("world/water", data.Splashes["Water"].Sound);

        var terrain = data.Terrains["FWATER1"];
        Assert.Equal("Water", terrain.Splash);
        Assert.Equal(10, terrain.FootClip);
        Assert.Equal(0.8125f, terrain.Friction);
        Assert.True(terrain.Liquid);
        Assert.Equal(5, terrain.DamageAmount);
        Assert.Equal("Slime", terrain.DamageType);
    }

    [Fact]
    public void SkipsUnknownNestedBlocks()
    {
        const string text = @"
terrain LAVA1
{
    unknown { nested { value } }
    splash Lava
}
splash Lava { sound world/lava }";

        var data = TerrainParser.Parse(text);

        Assert.Equal("Lava", data.Terrains["LAVA1"].Splash);
        Assert.Equal("world/lava", data.Splashes["Lava"].Sound);
    }

    [Fact]
    public void AppliesBaseGameConditionals()
    {
        const string text = @"
ifdoom
terrain DOOMWATR { splash Doom }
endif
ifheretic
terrain HTICWATR { splash Heretic }
endif
ifhexen
terrain HEXWATR { splash Hexen }
endif
ifstrife
terrain STRWATR { splash Strife }
endif";

        var doom = TerrainParser.Parse(text, TerrainBaseGame.Doom);
        var heretic = TerrainParser.Parse(text, TerrainBaseGame.Heretic);
        var all = TerrainParser.Parse(text);

        Assert.True(doom.Terrains.ContainsKey("DOOMWATR"));
        Assert.False(doom.Terrains.ContainsKey("HTICWATR"));
        Assert.True(heretic.Terrains.ContainsKey("HTICWATR"));
        Assert.False(heretic.Terrains.ContainsKey("HEXWATR"));
        Assert.Equal(4, all.Terrains.Count);
    }
}
