// ABOUTME: Verifies UDB-style TagRange calculations for absolute and relative tag assignment.
// ABOUTME: Covers duplicate detection, skip-used behavior, out-of-range reporting, and selected map updates.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class TagRangeModelTests
{
    [Fact]
    public void StoredOptionsKeepOnlyStepAndRelativeMode()
    {
        var options = new TagRangeOptions(
            StartTag: 32,
            Step: -4,
            Relative: true,
            SkipUsedTags: true,
            MaxTag: 128);

        var stored = TagRangeModel.StoredOptionsFrom(options);

        Assert.Equal(-4, stored.Step);
        Assert.True(stored.Relative);
    }

    [Fact]
    public void StoredOptionsNormalizeZeroStepLikeDialogInput()
    {
        var options = new TagRangeOptions(
            StartTag: 32,
            Step: 0,
            Relative: false,
            SkipUsedTags: false);

        var stored = TagRangeModel.StoredOptionsFrom(options);

        Assert.Equal(1, stored.Step);
        Assert.False(stored.Relative);
    }

    [Fact]
    public void CreateRangeBuildsAbsoluteRange()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 0, 0, 0 },
            new HashSet<int>(),
            new TagRangeOptions(StartTag: 10, Step: 2, Relative: false, SkipUsedTags: false));

        Assert.Equal(new[] { 10, 12, 14 }, result.Tags);
        Assert.False(result.TagsUsed);
        Assert.False(result.OutOfTags);
    }

    [Fact]
    public void CreateRangeReportsUsedTagsWithoutSkipping()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 0, 0, 0 },
            new HashSet<int> { 12 },
            new TagRangeOptions(StartTag: 10, Step: 2, Relative: false, SkipUsedTags: false));

        Assert.Equal(new[] { 10, 12, 14 }, result.Tags);
        Assert.True(result.TagsUsed);
        Assert.False(result.OutOfTags);
    }

    [Fact]
    public void CreateRangeSkipsUsedTags()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 0, 0, 0 },
            new HashSet<int> { 10, 12, 16 },
            new TagRangeOptions(StartTag: 10, Step: 2, Relative: false, SkipUsedTags: true));

        Assert.Equal(new[] { 14, 18, 20 }, result.Tags);
        Assert.False(result.TagsUsed);
        Assert.False(result.OutOfTags);
    }

    [Fact]
    public void CreateRangeBuildsRelativeRangeFromInitialTags()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 100, 200, 300 },
            new HashSet<int>(),
            new TagRangeOptions(StartTag: -10, Step: 5, Relative: true, SkipUsedTags: false, MinTag: 1, MaxTag: 1000));

        Assert.Equal(new[] { 90, 195, 300 }, result.Tags);
        Assert.False(result.OutOfTags);
    }

    [Fact]
    public void CreateRangeRelativeSkipUsedCarriesOffsetLikeUdb()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 10, 10, 10 },
            new HashSet<int> { 10, 11 },
            new TagRangeOptions(StartTag: 0, Step: 1, Relative: true, SkipUsedTags: true));

        Assert.Equal(new[] { 12, 13, 14 }, result.Tags);
        Assert.False(result.OutOfTags);
    }

    [Fact]
    public void CreateRangeReportsOutOfTags()
    {
        var result = TagRangeModel.CreateRange(
            new[] { 0, 0, 0 },
            new HashSet<int>(),
            new TagRangeOptions(StartTag: 8, Step: 2, Relative: false, SkipUsedTags: false, MinTag: 1, MaxTag: 10));

        Assert.Equal(new[] { 8, 10 }, result.Tags);
        Assert.True(result.OutOfTags);
    }

    [Fact]
    public void SelectedInitialTagsAndApplyRangeUseMapOrder()
    {
        var map = new MapSet();
        var first = map.AddThing(new Vector2D(0, 0), 1);
        first.Tag = 2;
        first.Selected = true;
        var skipped = map.AddThing(new Vector2D(8, 8), 1);
        skipped.Tag = 3;
        var second = map.AddThing(new Vector2D(16, 16), 1);
        second.Tag = 4;
        second.Selected = true;

        Assert.Equal(new[] { 2, 4 }, TagRangeModel.SelectedInitialTags(map, TagRangeTargetKind.Things));

        int applied = TagRangeModel.ApplyRange(map, TagRangeTargetKind.Things, new[] { 20, 21 });

        Assert.Equal(2, applied);
        Assert.Equal(20, first.Tag);
        Assert.Equal(3, skipped.Tag);
        Assert.Equal(21, second.Tag);
    }

    [Fact]
    public void CollectUsedTagsIncludesMultiTags()
    {
        var map = new MapSet();
        map.AddSector().Tags.AddRange(new[] { 2, 3 });
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tags.AddRange(new[] { 4, 5 });
        map.AddThing(new Vector2D(8, 8), 1).Tag = 6;

        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, TagRangeModel.CollectUsedTags(map).Order());
    }
}
