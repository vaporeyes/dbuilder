// ABOUTME: Tests game-configuration-aware map find/replace behavior outside direct tag searches.
// ABOUTME: Covers UDB generalized linedef action matching while keeping core MapSearch config-free.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ConfiguredMapSearchTests
{
    private const string Cfg = """
        generalizedlinedefs = true;
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

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
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
