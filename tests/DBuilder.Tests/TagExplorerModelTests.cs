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
        var filters = TagExplorerModel.ParseSpecialFilters("alpha #12 text $80 ^7 #bad #15");

        Assert.Equal(new[] { 12, 15 }, filters.Tags.Order());
        Assert.Equal(new[] { 80 }, filters.Actions);
        Assert.Equal(new[] { 7 }, filters.Polyobjects);
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
}
