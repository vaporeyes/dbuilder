// ABOUTME: Tests config-aware tag search over direct tags and tag-typed action arguments.
// ABOUTME: Covers the UDB behavior where action metadata determines whether args participate in tag tools.

using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ConfiguredTagSearchTests
{
    private const string Cfg = """
        formatinterface = "UniversalMapSetIO";
        linedeftypes
        {
            tags
            {
                80
                {
                    title = "Tag args";
                    arg0 { type = 13; }
                    arg1 { type = 14; }
                    arg2 { type = 15; }
                    arg3 { type = 11; }
                }
            }
        }
        """;

    [Fact]
    public void FindSelectsTagTypedActionArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();

        var result = ConfiguredTagSearch.Find(map, "17", config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Things[0].Selected);
        Assert.False(map.Linedefs[1].Selected);
        Assert.False(map.Things[1].Selected);
    }

    [Fact]
    public void UsedTagsIncludesTagTypedActionArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();

        var stats = ConfiguredTagSearch.UsedTagStatistics(map, config);

        var tag17 = stats.Single(t => t.Tag == 17);
        Assert.Equal(0, tag17.Sectors);
        Assert.Equal(1, tag17.Linedefs);
        Assert.Equal(1, tag17.Things);
        Assert.DoesNotContain(stats, t => t.Tag == 99);
    }

    [Fact]
    public void UsedTagStatisticsSkipsDirectMoreidsWhenPrimaryTagIsZero()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Tags.Clear();
        map.Sectors[0].Tags.AddRange([0, 7]);
        map.Linedefs[0].Tags.Clear();
        map.Linedefs[0].Tags.AddRange([0, 7]);

        var stats = ConfiguredTagSearch.UsedTagStatistics(map, config);

        Assert.DoesNotContain(stats, stat => stat.Tag == 7);
    }

    [Fact]
    public void ReplaceUpdatesTagTypedActionArgsOncePerElement()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Tag = 17;

        int changed = ConfiguredTagSearch.Replace(map, "17", "23", config);

        Assert.Equal(2, changed);
        Assert.Equal(23, map.Linedefs[0].Tag);
        Assert.Equal(23, map.Linedefs[0].Args[0]);
        Assert.Equal(23, map.Things[0].Args[1]);
        Assert.Equal(99, map.Linedefs[1].Args[3]);
    }

    [Fact]
    public void ReplaceHonorsUdbTagReplacementRangesPerOwner()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Tag = 17;
        map.Linedefs[0].Tag = 17;
        map.Things[0].Tag = 17;

        int changed = ConfiguredTagSearch.Replace(map, "17", "-1", config);

        Assert.Equal(2, changed);
        Assert.Equal(-1, map.Sectors[0].Tag);
        Assert.Equal(-1, map.Linedefs[0].Tag);
        Assert.Equal(17, map.Linedefs[0].Args[0]);
        Assert.Equal(17, map.Things[0].Tag);
        Assert.Equal(17, map.Things[0].Args[1]);
    }

    [Fact]
    public void FindWithinSelectionLimitsConfiguredTagOwners()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[1].Args[0] = 17;
        map.Things[1].Args[1] = 17;
        map.Linedefs[1].Selected = true;
        map.Things[1].Selected = true;

        SearchResult result = ConfiguredTagSearch.Find(map, "17", config, withinSelection: true);

        Assert.Equal(2, result.Count);
        Assert.False(map.Linedefs[0].Selected);
        Assert.True(map.Linedefs[1].Selected);
        Assert.False(map.Things[0].Selected);
        Assert.True(map.Things[1].Selected);
    }

    [Fact]
    public void ReplaceWithinSelectionLimitsConfiguredTagOwners()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Things[1].Args[1] = 17;
        map.Linedefs[0].Selected = true;
        map.Things[1].Selected = true;

        int changed = ConfiguredTagSearch.Replace(map, "17", "23", config, withinSelection: true);

        Assert.Equal(2, changed);
        Assert.Equal(23, map.Linedefs[0].Args[0]);
        Assert.Equal(17, map.Things[0].Args[1]);
        Assert.Equal(23, map.Things[1].Args[1]);
    }

    [Fact]
    public void ReferenceSearchWithinSelectionLimitsConfiguredArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Things[1].Args[1] = 17;
        map.Linedefs[0].Selected = true;
        map.Things[1].Selected = true;

        SearchResult lineResult = ConfiguredTagSearch.FindReference(
            map,
            FindCategory.LinedefSectorReference,
            "17",
            config,
            withinSelection: true);
        Assert.Equal(1, lineResult.Count);
        Assert.True(map.Linedefs[0].Selected);

        map.Things[1].Selected = true;
        SearchResult thingResult = ConfiguredTagSearch.FindReference(
            map,
            FindCategory.ThingThingReference,
            "17",
            config,
            withinSelection: true);
        Assert.Equal(1, thingResult.Count);
        Assert.False(map.Things[0].Selected);
        Assert.True(map.Things[1].Selected);
    }

    [Fact]
    public void RemoveMarkedTagsClearsDirectTagsAndTagTypedActionArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Tags.AddRange(new[] { 5, 7 });
        map.Sectors[0].Marked = true;
        map.Linedefs[0].Tag = 11;
        map.Linedefs[0].Marked = true;
        map.Things[0].Tag = 12;
        map.Things[0].Marked = true;

        int changed = ConfiguredTagSearch.RemoveMarkedTags(map, config);

        Assert.Equal(3, changed);
        Assert.Equal(new[] { 0 }, map.Sectors[0].Tags);
        Assert.Equal(new[] { 0 }, map.Linedefs[0].Tags);
        Assert.Equal(0, map.Linedefs[0].Args[0]);
        Assert.Equal(0, map.Linedefs[0].Args[1]);
        Assert.Equal(0, map.Things[0].Tag);
        Assert.Equal(0, map.Things[0].Args[1]);
        Assert.Equal(99, map.Linedefs[1].Args[3]);
        Assert.Equal(17, map.Things[1].Args[3]);
    }

    [Fact]
    public void RemoveMarkedTagsHonorsConfiguredTagOwnerCapabilities()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "HexenMapSetIO";
            linedeftypes { tags { 80 { title = "Tag args"; arg0 { type = 13; } } } }
            """);
        var map = BuildMap();
        map.Linedefs[0].Tag = 17;
        map.Linedefs[0].Args[0] = 18;
        map.Linedefs[0].Marked = true;
        map.Things[0].Tag = 19;
        map.Things[0].Args[1] = 20;
        map.Things[0].Marked = true;

        int changed = ConfiguredTagSearch.RemoveMarkedTags(map, config);

        Assert.Equal(2, changed);
        Assert.Equal(17, map.Linedefs[0].Tag);
        Assert.Equal(0, map.Linedefs[0].Args[0]);
        Assert.Equal(0, map.Things[0].Tag);
        Assert.Equal(20, map.Things[0].Args[1]);
    }

    [Fact]
    public void RenumberMarkedTagsAllocatesTagsOutsideUnmarkedGeometry()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Tags.AddRange(new[] { 5, 7 });
        map.Sectors[0].Marked = true;
        map.Linedefs[0].Tag = 7;
        map.Linedefs[0].Args[0] = 5;
        map.Linedefs[0].Args[1] = 0;
        map.Linedefs[0].Marked = true;
        map.Things[0].Tag = 5;
        map.Things[0].Args[1] = 5;
        map.Things[0].Marked = true;

        map.Linedefs[1].Tag = 1;
        map.Things[1].Tag = 2;

        int changed = ConfiguredTagSearch.RenumberMarkedTags(map, config, maxTag: 10);

        Assert.Equal(3, changed);
        Assert.Equal(new[] { 3, 4 }, map.Sectors[0].Tags);
        Assert.Equal(new[] { 4 }, map.Linedefs[0].Tags);
        Assert.Equal(3, map.Things[0].Tag);
        Assert.Equal(3, map.Linedefs[0].Args[0]);
        Assert.Equal(3, map.Things[0].Args[1]);
        Assert.Equal(1, map.Linedefs[1].Tag);
        Assert.Equal(2, map.Things[1].Tag);
    }

    [Fact]
    public void RenumberMarkedTagsHonorsConfiguredTagOwnerCapabilities()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "HexenMapSetIO";
            linedeftypes { tags { 80 { title = "Tag args"; arg0 { type = 13; } } } }
            """);
        var map = BuildMap();
        map.Linedefs[0].Tag = 6;
        map.Linedefs[0].Args[0] = 7;
        map.Linedefs[0].Marked = true;
        map.Things[0].Tag = 8;
        map.Things[0].Args[1] = 9;
        map.Things[0].Marked = true;

        int changed = ConfiguredTagSearch.RenumberMarkedTags(map, config, maxTag: 10);

        Assert.Equal(2, changed);
        Assert.Equal(6, map.Linedefs[0].Tag);
        Assert.Equal(2, map.Linedefs[0].Args[0]);
        Assert.Equal(1, map.Things[0].Tag);
        Assert.Equal(9, map.Things[0].Args[1]);
    }

    [Fact]
    public void FindsAndReplacesConfiguredLinedefReferenceArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[0].Args[1] = 41;

        var sectorRefs = ConfiguredTagSearch.FindReference(map, FindCategory.LinedefSectorReference, "17", config);
        Assert.Equal(1, sectorRefs.Count);
        Assert.True(map.Linedefs[0].Selected);

        var thingRefs = ConfiguredTagSearch.FindReference(map, FindCategory.LinedefThingReference, "41", config);
        Assert.Equal(1, thingRefs.Count);
        Assert.True(map.Linedefs[0].Selected);

        Assert.Equal(1, ConfiguredTagSearch.ReplaceReference(map, FindCategory.LinedefSectorReference, "17", "23", config));
        Assert.Equal(23, map.Linedefs[0].Args[0]);
        Assert.Equal(41, map.Linedefs[0].Args[1]);
    }

    [Fact]
    public void FindsAndReplacesConfiguredThingReferenceArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Things[0].Args[0] = 17;
        map.Things[0].Args[1] = 41;

        var sectorRefs = ConfiguredTagSearch.FindReference(map, FindCategory.ThingSectorReference, "17", config);
        Assert.Equal(1, sectorRefs.Count);
        Assert.True(map.Things[0].Selected);

        var thingRefs = ConfiguredTagSearch.FindReference(map, FindCategory.ThingThingReference, "41", config);
        Assert.Equal(1, thingRefs.Count);
        Assert.True(map.Things[0].Selected);

        Assert.Equal(1, ConfiguredTagSearch.ReplaceReference(map, FindCategory.ThingThingReference, "41", "24", config));
        Assert.Equal(17, map.Things[0].Args[0]);
        Assert.Equal(24, map.Things[0].Args[1]);
    }

    [Fact]
    public void ThingThingReferenceUsesThingTagCapabilityGateLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.IO/ConfiguredTagSearch.cs"));

        Assert.Contains(
            "FindCategory.ThingThingReference => (config?.HasThingAction ?? true) && (config?.HasThingTag ?? true)",
            body,
            StringComparison.Ordinal);
        Assert.Contains(
            "FindCategory.ThingSectorReference => (config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true)",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceReplacementRejectsOutOfByteRangeValues()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();

        int changed = ConfiguredTagSearch.ReplaceReference(map, FindCategory.LinedefSectorReference, "17", "300", config);

        Assert.Equal(0, changed);
        Assert.Equal(17, map.Linedefs[0].Args[0]);
    }

    [Fact]
    public void NextFreeTagSkipsTagTypedActionArgs()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Tag = 1;
        map.Linedefs[0].Args[0] = 2;
        map.Things[0].Args[1] = 3;
        map.Linedefs[1].Args[3] = 4;

        Assert.Equal(4, ConfiguredTagSearch.NextFreeTag(map, config));
    }

    [Fact]
    public void ConfigCapabilitiesSuppressUnsupportedDirectTagOwners()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "HexenMapSetIO";
            linedeftypes { tags { 80 { title = "Tag args"; arg0 { type = 13; } } } }
            """);
        var map = BuildMap();
        map.Linedefs[0].Tag = 17;
        map.Linedefs[0].Args[0] = 0;
        map.Things[0].Tag = 17;
        map.Things[0].Args[1] = 0;

        var result = ConfiguredTagSearch.Find(map, "17", config);

        Assert.Equal(1, result.Count);
        Assert.False(map.Linedefs[0].Selected);
        Assert.True(map.Things[0].Selected);
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v1 = map.AddVertex(new Vector2D(0, 0));
        var v2 = map.AddVertex(new Vector2D(64, 0));
        var v3 = map.AddVertex(new Vector2D(64, 64));
        var line = map.AddLinedef(v1, v2);
        line.Action = 80;
        line.Args[0] = 17;
        line.Args[1] = 41;
        map.AddSidedef(line, true, sector);

        var nonTagLine = map.AddLinedef(v2, v3);
        nonTagLine.Action = 80;
        nonTagLine.Args[3] = 99;

        var thing = map.AddThing(new Vector2D(8, 8), 1);
        thing.Action = 80;
        thing.Args[1] = 17;
        var otherThing = map.AddThing(new Vector2D(16, 16), 2);
        otherThing.Action = 80;
        otherThing.Args[3] = 17;
        map.BuildIndexes();
        return map;
    }
}
