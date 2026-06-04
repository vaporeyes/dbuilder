// ABOUTME: Verifies UDB-style TagExplorer filtering and sorting over map tags, actions, and comments.
// ABOUTME: Covers special search tokens, comments-only filtering, zero-sort rules, and polyobject entries.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class TagExplorerModelTests
{
    [Fact]
    public void ParseSpecialFiltersReadsTagActionAndPolyobjectTokens()
    {
        var filters = TagExplorerModel.ParseSpecialFilters("alpha #12 text $80 ^7 #bad #15 #-2 $+3 ^-4");

        Assert.Equal(new[] { 12, 15 }, filters.Tags.Order());
        Assert.Equal(new[] { 80 }, filters.Actions);
        Assert.Equal(new[] { 7 }, filters.Polyobjects);
    }

    [Fact]
    public void UiMetadataMatchesUdbTagExplorerPanel()
    {
        Assert.Equal("tagexplorerdockerpanel", TagExplorerModel.DockerId);
        Assert.Equal("Tag Explorer", TagExplorerModel.DockerTitle);
        Assert.Equal("Show:", TagExplorerModel.ShowLabel);
        Assert.Equal("Sort:", TagExplorerModel.SortLabel);
        Assert.Equal("Filter:", TagExplorerModel.FilterLabel);
        Assert.Equal("Hide elements without comments", TagExplorerModel.CommentsOnlyText);
        Assert.Equal("Export to file...", TagExplorerModel.ExportToFileText);
        Assert.Equal("Text files|*.txt", TagExplorerModel.ExportFileFilter);
        Assert.Equal(
            new[] { "Tags and Action Specials", "Tags", "Action Specials", "Polyobjects" },
            TagExplorerModel.DisplayModeOptions.Select(option => option.Title));
        Assert.Equal(
            new[] { "By Index", "By Tag", "By Action Special" },
            TagExplorerModel.SortModeOptions.Select(option => option.Title));
        Assert.Contains("Example: #667", TagExplorerModel.SearchHint, StringComparison.Ordinal);
        Assert.Contains("Example: $80", TagExplorerModel.SearchHint, StringComparison.Ordinal);
        Assert.Contains("Example: ^22", TagExplorerModel.SearchHint, StringComparison.Ordinal);
        Assert.Contains("edit item's comment", TagExplorerModel.UdmfNodeTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("edit item's comment", TagExplorerModel.NonUdmfNodeTooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSettingsUsesUdbPluginKeysAndClampsIndices()
    {
        var settings = new Dictionary<string, object?>
        {
            [TagExplorerModel.DisplayModeSettingKey] = 99,
            [TagExplorerModel.SortModeSettingKey] = "-4",
            [TagExplorerModel.CommentsOnlySettingKey] = "true",
            [TagExplorerModel.CenterOnSelectedSettingKey] = true,
            [TagExplorerModel.SelectOnClickSettingKey] = "true",
        };

        TagExplorerPersistedSettings result = TagExplorerModel.ReadSettings(settings);

        Assert.Equal(TagExplorerDisplayMode.Polyobjects, result.DisplayMode);
        Assert.Equal(TagExplorerSortMode.ByIndex, result.SortMode);
        Assert.True(result.CommentsOnly);
        Assert.True(result.CenterOnSelected);
        Assert.True(result.SelectOnClick);
    }

    [Fact]
    public void WriteSettingsUsesUdbPluginKeysAndModeIndices()
    {
        var result = TagExplorerModel.WriteSettings(new TagExplorerPersistedSettings(
            TagExplorerDisplayMode.Actions,
            TagExplorerSortMode.ByTag,
            CommentsOnly: true,
            CenterOnSelected: false,
            SelectOnClick: true));

        Assert.Equal(2, result[TagExplorerModel.DisplayModeSettingKey]);
        Assert.Equal(1, result[TagExplorerModel.SortModeSettingKey]);
        Assert.True((bool)result[TagExplorerModel.CommentsOnlySettingKey]);
        Assert.False((bool)result[TagExplorerModel.CenterOnSelectedSettingKey]);
        Assert.True((bool)result[TagExplorerModel.SelectOnClickSettingKey]);
    }

    [Fact]
    public void BuildEntriesIncludesTaggedAndActionObjects()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tag = 5;
        line.Action = 80;
        var sector = map.AddSector();
        sector.Tag = 9;
        sector.Special = 10;
        var thing = map.AddThing(new Vector2D(8, 8), 1);
        thing.Tag = 3;
        thing.Action = 11;

        var entries = TagExplorerModel.BuildEntries(map, null, new TagExplorerOptions());

        Assert.Equal(
            new[] { TagExplorerEntryKind.Thing, TagExplorerEntryKind.Sector, TagExplorerEntryKind.Linedef },
            entries.Select(e => e.Kind));
        Assert.Equal(new[] { 3, 9, 5 }, entries.Select(e => e.Tag));
        Assert.Equal(new[] { 11, 10, 80 }, entries.Select(e => e.Action));
    }

    [Fact]
    public void BuildEntriesFiltersSpecialTokens()
    {
        var map = new MapSet();
        var first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        first.Tag = 5;
        first.Action = 80;
        var second = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));
        second.Tag = 6;
        second.Action = 81;

        var entries = TagExplorerModel.BuildEntries(
            map,
            null,
            new TagExplorerOptions(SearchText: "#5 $80"));

        var entry = Assert.Single(entries);
        Assert.Equal(0, entry.Index);
        Assert.Equal(5, entry.Tag);
        Assert.Equal(80, entry.Action);
    }

    [Fact]
    public void BuildEntriesFiltersCommentsOnlyAndCommentSearchForUdmf()
    {
        var map = new MapSet();
        var first = map.AddThing(new Vector2D(0, 0), 1);
        first.Tag = 1;
        first.Fields["comment"] = "Secret switch";
        var second = map.AddThing(new Vector2D(8, 8), 1);
        second.Tag = 2;

        var entries = TagExplorerModel.BuildEntries(
            map,
            null,
            new TagExplorerOptions(SearchText: "switch", CommentsOnly: true));

        var entry = Assert.Single(entries);
        Assert.Equal(0, entry.Index);
        Assert.Equal("Secret switch", entry.Comment);
    }

    [Fact]
    public void BuildEntriesSortsZeroTagsAndActionsLast()
    {
        var map = new MapSet();
        var noAction = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        noAction.Tag = 1;
        var action = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));
        action.Tag = 2;
        action.Action = 10;
        var noTag = map.AddLinedef(map.AddVertex(new Vector2D(0, 128)), map.AddVertex(new Vector2D(64, 128)));
        noTag.Action = 11;

        var byAction = TagExplorerModel.BuildEntries(
            map,
            null,
            new TagExplorerOptions(SortMode: TagExplorerSortMode.ByAction));

        Assert.Equal(new[] { 1, 2, 0 }, byAction.Select(e => e.Index));

        var byTag = TagExplorerModel.BuildEntries(
            map,
            null,
            new TagExplorerOptions(SortMode: TagExplorerSortMode.ByTag));

        Assert.Equal(new[] { 0, 1, 2 }, byTag.Select(e => e.Index));
    }

    [Fact]
    public void BuildEntriesIncludesPolyobjectThingsAndLinedefs()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(0, 0), 9300);
        thing.Angle = 22;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Action = 1;
        line.Args[0] = 7;

        var entries = TagExplorerModel.BuildEntries(
            map,
            null,
            new TagExplorerOptions(DisplayMode: TagExplorerDisplayMode.Polyobjects));

        Assert.Equal(
            new[] { TagExplorerEntryKind.Linedef, TagExplorerEntryKind.Thing },
            entries.Select(e => e.Kind));
        Assert.Equal(new[] { 7, 22 }, entries.Select(e => e.PolyobjectNumber));
    }

    [Fact]
    public void BuildTreeGroupsEntriesByKindAndTagWithLabels()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(0, 0), 1);
        thing.Tag = 7;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tag = 7;
        line.Action = 80;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByTag);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, null, options);
        IReadOnlyList<TagExplorerTreeNode> tree = TagExplorerModel.BuildTree(
            entries,
            options,
            new Dictionary<int, string> { [7] = "Door group" });

        Assert.Equal(new[] { "Things:", "Linedefs:" }, tree.Select(node => node.Title));
        Assert.Equal("Tag 7: Door group", tree[0].Children[0].Title);
        Assert.Equal("Thing, Index 0", tree[0].Children[0].Children[0].Title);
        Assert.Equal("Action 80: Linedef, Index 0", tree[1].Children[0].Children[0].Title);
    }

    [Fact]
    public void BuildTreeGroupsThingsByCategoryInIndexModeLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            thingtypes
            {
                monsters
                {
                    title = "Monsters";
                    3001 { title = "Imp"; }
                }
                decorations
                {
                    title = "Decorations";
                    2001 { title = "Column"; }
                }
            }
            """);
        var map = new MapSet();
        var imp = map.AddThing(new Vector2D(0, 0), 3001);
        imp.Tag = 7;
        var unknown = map.AddThing(new Vector2D(8, 8), 9999);
        unknown.Tag = 9;
        var column = map.AddThing(new Vector2D(16, 16), 2001);
        column.Tag = 11;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByIndex);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, config, options);
        TagExplorerTreeNode things = Assert.Single(TagExplorerModel.BuildTree(entries, options));

        Assert.Equal("Things:", things.Title);
        Assert.Equal(new[] { "Monsters", "UNKNOWN", "Decorations" }, things.Children.Select(node => node.Title));
        Assert.Equal("0: Thing, Tag 7", things.Children[0].Children[0].Title);
        Assert.Equal("1: Thing, Tag 9", things.Children[1].Children[0].Title);
        Assert.Equal("2: Thing, Tag 11", things.Children[2].Children[0].Title);
    }

    [Fact]
    public void BuildTreeGroupsEntriesByActionAndLeavesNoActionLast()
    {
        var map = new MapSet();
        var first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        first.Tag = 1;
        var second = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));
        second.Tag = 2;
        second.Action = 80;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByAction);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, null, options);
        TagExplorerTreeNode linedefs = Assert.Single(TagExplorerModel.BuildTree(entries, options));

        Assert.Equal("Linedefs:", linedefs.Title);
        Assert.Equal(new[] { "Action 80", "No Action" }, linedefs.Children.Select(node => node.Title));
        Assert.Equal("Tag 2: Linedef, Index 1", linedefs.Children[0].Children[0].Title);
        Assert.Equal("Tag 1: Linedef, Index 0", linedefs.Children[1].Children[0].Title);
    }

    [Fact]
    public void BuildTreeLabelsActionAndEffectGroupsFromGameConfigLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            linedeftypes
            {
                doors
                {
                    80 { title = "Door Open"; }
                }
            }
            sectortypes
            {
                9 = "Secret";
            }
            """);
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tag = 1;
        line.Action = 80;
        var sector = map.AddSector();
        sector.Tag = 2;
        sector.Special = 9;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByAction);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, config, options);
        IReadOnlyList<TagExplorerTreeNode> tree = TagExplorerModel.BuildTree(entries, options);

        Assert.Equal(new[] { "Sectors:", "Linedefs:" }, tree.Select(node => node.Title));
        Assert.Equal("9 - Secret", tree[0].Children[0].Title);
        Assert.Equal("80 - Door Open", tree[1].Children[0].Title);
    }

    [Fact]
    public void BuildTreeExpandsGeneralizedSectorEffectsByActionLikeUdb()
    {
        var config = GameConfiguration.FromText("""
            gen_sectortypes
            {
                damage
                {
                    0 = "None";
                    32 = "5 per second";
                }
            }
            sectortypes
            {
                1 = "Secret";
            }
            """);
        var map = new MapSet();
        var sector = map.AddSector();
        sector.Tag = 7;
        sector.Special = 33;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByAction);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, config, options);
        TagExplorerTreeNode sectors = Assert.Single(TagExplorerModel.BuildTree(entries, options));

        Assert.Equal("Sectors:", sectors.Title);
        Assert.Equal(new[] { "1 - Secret", "32 - Damage", "33 - Secret + Damage: 5 per second" }, sectors.Children.Select(node => node.Title));
        Assert.Equal("Tag 7: Secret, Index 0", sectors.Children[0].Children[0].Title);
        Assert.Equal("Tag 7: Damage: 5 per second, Index 0", sectors.Children[1].Children[0].Title);
        Assert.Equal("Tag 7: Secret + Damage: 5 per second: 33, Index 0", sectors.Children[2].Children[0].Title);
    }

    [Fact]
    public void ExportTreeTextMatchesUdbIndentedShape()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.Tag = 7;
        sector.Special = 9;
        sector.Fields["comment"] = "Tagged lift";

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByTag);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, null, options);
        IReadOnlyList<TagExplorerTreeNode> tree = TagExplorerModel.BuildTree(entries, options);

        string text = TagExplorerModel.ExportTreeText(tree, options.SortMode);

        Assert.Equal(
            "Sectors (by tag):" + Environment.NewLine +
            "  Tag 7:" + Environment.NewLine +
            "    Action 9: Tagged lift, Index 0",
            text);
    }

    [Fact]
    public void FlattenTreePreservesTreeDepthAndEntryTargets()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.Tag = 7;
        sector.Special = 9;

        var options = new TagExplorerOptions(SortMode: TagExplorerSortMode.ByTag);
        IReadOnlyList<TagExplorerEntry> entries = TagExplorerModel.BuildEntries(map, null, options);
        IReadOnlyList<TagExplorerTreeNode> tree = TagExplorerModel.BuildTree(entries, options);

        IReadOnlyList<TagExplorerTreeRow> rows = TagExplorerModel.FlattenTree(tree);

        Assert.Equal(new[] { "Sectors:", "Tag 7", "Action 9: Sector, Index 0" }, rows.Select(row => row.Title));
        Assert.Equal(new[] { 0, 1, 2 }, rows.Select(row => row.Depth));
        Assert.False(rows[0].IsEntry);
        Assert.False(rows[1].IsEntry);
        Assert.True(rows[2].IsEntry);
        Assert.Same(entries[0], rows[2].Entry);
    }
}
