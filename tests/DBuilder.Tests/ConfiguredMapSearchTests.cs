// ABOUTME: Tests game-configuration-aware map find/replace behavior outside direct tag searches.
// ABOUTME: Covers UDB generalized action/effect matching while keeping core MapSearch config-free.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ConfiguredMapSearchTests
{
    private const string Cfg = """
        generalizedlinedefs = true;
        generalizedsectors = true;
        gen_linedeftypes
        {
            floors
            {
                title = "Floor";
                offset = 24576;
                length = 8192;
                trigger
                {
                    0 = "Walk Over Once";
                    2 = "Switch Once";
                    3 = "Switch Repeatable";
                }
                speed
                {
                    0 = "Slow";
                    8 = "Normal";
                    16 = "Fast";
                    24 = "Turbo";
                }
                direction
                {
                    0 = "Down";
                    64 = "Up";
                }
            }
        }
        sectortypes
        {
            9 = "Secret";
            11 = "End damage";
        }
        gen_sectortypes
        {
            damage
            {
                0 = "None";
                32 = "5 per second";
                64 = "10 per second";
                96 = "20 per second";
            }
            friction
            {
                0 = "Disabled";
                256 = "Friction";
            }
        }
        """;

    [Fact]
    public void FindLinedefActionMatchesGeneralizedActionsWithSharedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8 + 64;
        map.Linedefs[1].Action = 24576 + 3 + 16;
        map.Linedefs[2].Action = 24576 + 8;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.LinedefAction, (24576 + 2).ToString(), config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Linedefs[1].Selected);
        Assert.False(map.Linedefs[2].Selected);
    }

    [Fact]
    public void ReplaceLinedefActionArgumentsMatchesGeneralizedActionsWithSharedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8 + 64;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[1].Action = 24576 + 3 + 16;
        map.Linedefs[1].Args[0] = 17;
        map.Linedefs[2].Action = 24576 + 8;
        map.Linedefs[2].Args[0] = 17;

        int changed = ConfiguredMapSearch.Replace(
            map,
            FindCategory.LinedefActionArguments,
            (24576 + 2) + " 17",
            "80 23",
            config);

        Assert.Equal(2, changed);
        Assert.Equal(80, map.Linedefs[0].Action);
        Assert.Equal(23, map.Linedefs[0].Args[0]);
        Assert.Equal(80, map.Linedefs[1].Action);
        Assert.Equal(23, map.Linedefs[1].Args[0]);
        Assert.Equal(24576 + 8, map.Linedefs[2].Action);
        Assert.Equal(17, map.Linedefs[2].Args[0]);
    }

    [Fact]
    public void GeneralizedMatchingDoesNotApplyWithoutConfiguredGeneralizedActions()
    {
        var config = GameConfiguration.FromText(Cfg.Replace("generalizedlinedefs = true;", ""));
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8;
        map.Linedefs[1].Action = 24576 + 3 + 16;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.LinedefAction, (24576 + 2).ToString(), config);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void FindSectorEffectMatchesGeneralizedEffectSubsets()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "32", config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);
        Assert.False(map.Sectors[2].Selected);
    }

    [Fact]
    public void FindSectorEffectMatchesNormalBaseWithAdditionalGeneralizedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "9", config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.False(map.Sectors[1].Selected);
        Assert.True(map.Sectors[2].Selected);
    }

    [Fact]
    public void ReplaceSectorEffectMatchesGeneralizedEffectSubsets()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.SectorEffect, "32", "11", config);

        Assert.Equal(2, changed);
        Assert.Equal(11, map.Sectors[0].Special);
        Assert.Equal(11, map.Sectors[1].Special);
        Assert.Equal(9 + 64, map.Sectors[2].Special);
    }

    [Fact]
    public void GeneralizedSectorEffectMatchingDoesNotApplyWithoutConfiguredGeneralizedEffects()
    {
        var config = GameConfiguration.FromText(Cfg.Replace("generalizedsectors = true;", ""));
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "32", config);

        Assert.Equal(0, result.Count);
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        map.AddSector();
        map.AddSector();
        var v1 = map.AddVertex(new Vector2D(0, 0));
        var v2 = map.AddVertex(new Vector2D(64, 0));
        var v3 = map.AddVertex(new Vector2D(64, 64));
        var v4 = map.AddVertex(new Vector2D(0, 64));
        map.AddSidedef(map.AddLinedef(v1, v2), true, sector);
        map.AddSidedef(map.AddLinedef(v2, v3), true, sector);
        map.AddSidedef(map.AddLinedef(v3, v4), true, sector);
        map.BuildIndexes();
        return map;
    }
}
