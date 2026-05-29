// ABOUTME: Tests UDB-style primary and multi-tag helpers for map elements.
// ABOUTME: Covers tagged interfaces, replacement normalization and ordered tag assignment.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapElementTagsTests
{
    [Fact]
    public void TaggedInterfacesExposePrimaryAndMultiTags()
    {
        ITaggedMapElement thing = new Thing(new Vector2D(0, 0), 3001) { Tag = 7 };
        IMultiTaggedMapElement sector = new Sector { Tag = 3 };
        sector.Tags.Add(9);

        Assert.True(MapElementTags.HasTag(thing, 7));
        Assert.True(MapElementTags.HasTag(sector, 3));
        Assert.True(MapElementTags.HasTag(sector, 9));
        Assert.Equal(new[] { 7 }, MapElementTags.PositiveTags(thing));
        Assert.Equal(new[] { 3, 9 }, MapElementTags.PositiveTags(sector));
    }

    [Fact]
    public void SetTagsPreservesFirstOccurrenceOrder()
    {
        IMultiTaggedMapElement line = new Linedef();

        MapElementTags.SetTags(line, new[] { 5, 7, 5, 0, 7, 9 });

        Assert.Equal(new[] { 5, 7, 0, 9 }, line.Tags);
        Assert.Equal(5, line.Tag);
    }

    [Fact]
    public void ReplaceTagNormalizesDuplicateMultiTags()
    {
        var sector = new Sector();
        sector.Tags.AddRange(new[] { 5, 7, 9 });

        Assert.True(MapElementTags.ReplaceTag(sector, 9, 7));

        Assert.Equal(new[] { 5, 7 }, sector.Tags);
    }

    [Fact]
    public void NormalizeTagsKeepsEmptyAndSingleTagCollectionsStable()
    {
        IMultiTaggedMapElement sector = new Sector();

        MapElementTags.NormalizeTags(sector);

        Assert.Empty(sector.Tags);

        sector.Tag = 0;
        MapElementTags.NormalizeTags(sector);

        Assert.Equal(new[] { 0 }, sector.Tags);
    }

    [Fact]
    public void MapSetGetNewTagReturnsFirstUnusedPositiveTag()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(0, 0), 3001).Tag = 1;
        var sector = map.AddSector();
        sector.Tags.AddRange(new[] { 2, 4 });
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tags.Add(5);

        Assert.Equal(3, map.GetNewTag(maxTag: 8));
        Assert.Equal(6, map.GetNewTag(new[] { 3 }, maxTag: 8));
    }

    [Fact]
    public void MapSetGetNewTagCanScopeToMarkedGeometry()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(0, 0), 3001).Tag = 1;
        var marked = map.AddSector();
        marked.Tag = 1;
        marked.Marked = true;
        var unmarked = map.AddSector();
        unmarked.Tag = 2;

        Assert.Equal(3, map.GetNewTag(maxTag: 4));
        Assert.Equal(2, map.GetNewTag(markedOnly: true, maxTag: 4));
    }

    [Fact]
    public void MapSetGetMultipleNewTagsSkipsUsedAndStopsAtLimit()
    {
        var map = new MapSet();
        map.AddThing(new Vector2D(0, 0), 3001).Tag = 1;
        map.AddSector().Tag = 3;

        Assert.Equal(new[] { 2, 4, 5 }, map.GetMultipleNewTags(3, maxTag: 5));
        Assert.Equal(new[] { 2 }, map.GetMultipleNewTags(3, maxTag: 2));
        Assert.Empty(map.GetMultipleNewTags(0, maxTag: 5));
    }
}
