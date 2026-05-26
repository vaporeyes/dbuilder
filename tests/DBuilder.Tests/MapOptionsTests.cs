// ABOUTME: Tests minimal MapOptions persistence backed by Configuration.
// ABOUTME: Covers UDB-compatible selection group and tag-label write/read shape.

using System.Collections;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapOptionsTests
{
    [Fact]
    public void WriteSelectionGroupsStoresUdbCompatibleIndexLists()
    {
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[2].Groups = MapSet.GroupMask(0);
        map.Linedefs[0].Groups = MapSet.GroupMask(1);
        map.Sectors[0].Groups = MapSet.GroupMask(1);
        map.Things[0].Groups = MapSet.GroupMask(2);
        var options = new MapOptions();

        options.WriteSelectionGroups(map);

        var groups = options.MapConfiguration.ReadSetting(MapOptions.SelectionGroupsPath, (IDictionary?)null);
        Assert.NotNull(groups);
        var group0 = Assert.IsAssignableFrom<IDictionary>(groups![0]);
        var group1 = Assert.IsAssignableFrom<IDictionary>(groups[1]);
        var group2 = Assert.IsAssignableFrom<IDictionary>(groups[2]);
        Assert.Equal("0 2", group0["vertices"]);
        Assert.Equal("0", group1["linedefs"]);
        Assert.Equal("0", group1["sectors"]);
        Assert.Equal("0", group2["things"]);
    }

    [Fact]
    public void ReadSelectionGroupsRestoresMembershipByIndex()
    {
        var src = BuildMap();
        src.Vertices[1].Groups = MapSet.GroupMask(0);
        src.Linedefs[0].Groups = MapSet.GroupMask(3);
        src.Sectors[0].Groups = MapSet.GroupMask(3);
        src.Things[0].Groups = MapSet.GroupMask(4);
        var options = new MapOptions();
        options.WriteSelectionGroups(src);

        var dst = BuildMap();
        options.ReadSelectionGroups(dst);

        Assert.Equal(0, dst.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), dst.Vertices[1].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Linedefs[0].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Sectors[0].Groups);
        Assert.Equal(MapSet.GroupMask(4), dst.Things[0].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsReplacesPersistedGroupBits()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "1";
                }
            }
            """);
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[1].Groups = MapSet.GroupMask(2);

        options.ReadSelectionGroups(map);

        Assert.Equal(0, map.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), map.Vertices[1].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsIgnoresInvalidIndices()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "-1 0 99 bad";
                    things = "5";
                }
            }
            """);
        var map = BuildMap();

        options.ReadSelectionGroups(map);

        Assert.Equal(MapSet.GroupMask(0), map.Vertices[0].Groups);
        Assert.Equal(0, map.Things[0].Groups);
    }

    [Fact]
    public void WriteTagLabelsStoresUdbCompatibleEntries()
    {
        var options = new MapOptions();
        options.TagLabels[12] = "Door controls";
        options.TagLabels[3] = "Secret lift";

        options.WriteTagLabels();

        var labels = options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null);
        Assert.NotNull(labels);
        var first = Assert.IsAssignableFrom<IDictionary>(labels!["taglabel1"]);
        var second = Assert.IsAssignableFrom<IDictionary>(labels["taglabel2"]);
        Assert.Equal(12, first["tag"]);
        Assert.Equal("Door controls", first["label"]);
        Assert.Equal(3, second["tag"]);
        Assert.Equal("Secret lift", second["label"]);
    }

    [Fact]
    public void ReadTagLabelsRestoresValidEntries()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 12;
                    label = "Door controls";
                }
                taglabel2
                {
                    tag = 3;
                    label = "Secret lift";
                }
            }
            """);

        options.ReadTagLabels();

        Assert.Equal("Door controls", options.TagLabels[12]);
        Assert.Equal("Secret lift", options.TagLabels[3]);
    }

    [Fact]
    public void ReadTagLabelsReplacesExistingLabelsAndIgnoresInvalidEntries()
    {
        var options = new MapOptions();
        options.TagLabels[99] = "Stale";
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 0;
                    label = "Ignored zero";
                }
                taglabel2
                {
                    tag = 8;
                    label = "";
                }
                taglabel3
                {
                    tag = 5;
                    label = "Valid";
                }
                broken = 12;
            }
            """);

        options.ReadTagLabels();

        Assert.Single(options.TagLabels);
        Assert.Equal("Valid", options.TagLabels[5]);
    }

    [Fact]
    public void WriteTagLabelsRemovesStaleConfigurationWhenEmpty()
    {
        var options = new MapOptions();
        options.TagLabels[1] = "Old";
        options.WriteTagLabels();
        options.TagLabels.Clear();

        options.WriteTagLabels();

        Assert.Null(options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null));
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(64, 64));
        var line = map.AddLinedef(v0, v1);
        map.AddLinedef(v1, v2);
        map.AddSidedef(line, true, sector);
        map.AddThing(new Vector2D(32, 16), 3001);
        map.BuildIndexes();
        return map;
    }
}
