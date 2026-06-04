// ABOUTME: Verifies UDB-style TagRange calculations for absolute and relative tag assignment.
// ABOUTME: Covers duplicate detection, skip-used behavior, out-of-range reporting, and selected map updates.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class TagRangeModelTests
{
    [Fact]
    public void MetadataMatchesUdbActionButtonAndDialogText()
    {
        Assert.Equal("rangetagselection", TagRangeModel.ActionName);
        Assert.Equal("Tag Range", TagRangeModel.ToolWindowTitle);
        Assert.Equal("Tag Range", TagRangeModel.ToolButtonText);
        Assert.Equal("Tag Selected Range", TagRangeModel.FormDesignerTitle);
        Assert.Equal("Start Tag:", TagRangeModel.StartTagLabel);
        Assert.Equal("Increment:", TagRangeModel.IncrementLabel);
        Assert.Equal("End Tag:", TagRangeModel.EndTagLabel);
        Assert.Equal("Relative to existing tags", TagRangeModel.RelativeModeText);
        Assert.Equal("Skip over already used tags", TagRangeModel.SkipUsedTagsText);
        Assert.Equal("Warning: The tag range contains already used tags.", TagRangeModel.DuplicateWarningText);
        Assert.Equal(
            "The range exceeds the maximum or minimum allowed tags and cannot be created.",
            TagRangeModel.OutOfTagsWarningText);
        Assert.Equal(
            "The range exceeds the maximum allowed tags and cannot be created.",
            TagRangeModel.OutOfTagsMessage);
        Assert.Equal("OK", TagRangeModel.OkText);
        Assert.Equal("Cancel", TagRangeModel.CancelText);
    }

    [Theory]
    [InlineData("SectorsMode", false, false, true)]
    [InlineData("LinedefsMode", true, false, true)]
    [InlineData("LinedefsMode", false, true, false)]
    [InlineData("ThingsMode", false, true, true)]
    [InlineData("ThingsMode", true, false, false)]
    [InlineData("VerticesMode", true, true, false)]
    [InlineData(null, true, true, false)]
    public void ToolbarButtonVisibilityMatchesUdbModeAndFormatRules(
        string? modeName,
        bool hasLinedefTag,
        bool hasThingTag,
        bool expected)
    {
        var capabilities = new TagRangeFormatCapabilities(hasLinedefTag, hasThingTag);

        Assert.Equal(expected, TagRangeModel.ShouldShowToolbarButton(modeName, capabilities));
    }

    [Fact]
    public void HasSelectionMatchesUdbActionWarningCondition()
    {
        Assert.False(TagRangeModel.HasSelection(0));
        Assert.True(TagRangeModel.HasSelection(1));
        Assert.Equal("This action requires a selection!", TagRangeModel.NoSelectionWarning);
    }

    [Theory]
    [InlineData(TagRangeTargetKind.Sectors, 2, "Set 2 sector tags")]
    [InlineData(TagRangeTargetKind.Linedefs, 1, "Set 1 linedef tags")]
    [InlineData(TagRangeTargetKind.Things, 3, "Set 3 thing tags")]
    public void UndoDescriptionMatchesUdbText(TagRangeTargetKind target, int selectionCount, string expected)
    {
        Assert.Equal(expected, TagRangeModel.UndoDescription(target, selectionCount));
    }

    [Fact]
    public void StatusTextMatchesEditorApplyOutOfTagsAndEmptySelectionMessages()
    {
        Assert.Equal("No selected linedefs to tag.", TagRangeModel.EmptySelectionStatus(TagRangeTargetKind.Linedefs));
        Assert.Equal("Tag range ran out of tags after 1 assignment.", TagRangeModel.OutOfTagsStatus(1));
        Assert.Equal("Tag range ran out of tags after 2 assignments.", TagRangeModel.OutOfTagsStatus(2));
        Assert.Equal("Tag range assigned 1 tag.", TagRangeModel.AppliedStatus(1, tagsUsed: false));
        Assert.Equal("Tag range assigned 3 tags.", TagRangeModel.AppliedStatus(3, tagsUsed: false));
        Assert.Equal(
            "Tag range assigned 3 tags; one or more tags were already in use.",
            TagRangeModel.AppliedStatus(3, tagsUsed: true));
    }

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
    public void CreatePreviewStateMatchesUdbWarningsAndEndTag()
    {
        TagRangePreviewState preview = TagRangeModel.CreatePreviewState(
            TagRangeTargetKind.Sectors,
            selectionCount: 3,
            initialTags: new[] { 0, 0, 0 },
            usedTags: new HashSet<int> { 12 },
            options: new TagRangeOptions(StartTag: 10, Step: 2, Relative: false, SkipUsedTags: false));

        Assert.Equal("Create tag range for 3 sectors", preview.Title);
        Assert.Equal(14, preview.EndTag);
        Assert.False(preview.OutOfTagsWarningVisible);
        Assert.True(preview.OkEnabled);
        Assert.True(preview.DoubleTagWarningVisible);
        Assert.True(preview.SkipUsedTagsVisible);
    }

    [Fact]
    public void CreatePreviewStateHidesDuplicateWarningWhenOutOfTags()
    {
        TagRangePreviewState preview = TagRangeModel.CreatePreviewState(
            TagRangeTargetKind.Linedefs,
            selectionCount: 1,
            initialTags: new[] { 0, 0, 0 },
            usedTags: new HashSet<int> { 10 },
            options: new TagRangeOptions(StartTag: 8, Step: 2, Relative: false, SkipUsedTags: false, MinTag: 1, MaxTag: 10));

        Assert.Equal("Create tag range for 1 linedef", preview.Title);
        Assert.Equal(10, preview.EndTag);
        Assert.True(preview.OutOfTagsWarningVisible);
        Assert.False(preview.OkEnabled);
        Assert.False(preview.DoubleTagWarningVisible);
        Assert.False(preview.SkipUsedTagsVisible);
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
    public void CreateSelectionContextCapturesSelectedTagsAndUsedTags()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.Tag = 2;
        sector.Selected = true;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tag = 4;
        line.Selected = true;
        var thing = map.AddThing(new Vector2D(16, 16), 1);
        thing.Tag = 6;
        thing.Selected = true;
        var skippedThing = map.AddThing(new Vector2D(32, 32), 1);
        skippedThing.Tag = 8;

        TagRangeSelectionContext context = TagRangeModel.CreateSelectionContext(map);

        Assert.Equal(new[] { 2 }, context.SectorTags);
        Assert.Equal(new[] { 4 }, context.LinedefTags);
        Assert.Equal(new[] { 6 }, context.ThingTags);
        Assert.Equal(new[] { 2, 4, 6, 8 }, context.UsedTags.Order());
        Assert.Equal(context.LinedefTags, context.InitialTags(TagRangeTargetKind.Linedefs));
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

    [Fact]
    public void CollectUsedTagsIncludesMoreIdsWhenPrimaryTagIsZeroLikeUdbForAllTags()
    {
        var map = new MapSet();
        map.AddSector().Tags.AddRange(new[] { 0, 2 });
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        line.Tags.AddRange(new[] { 0, 3 });
        map.AddThing(new Vector2D(8, 8), 1).Tag = 4;

        Assert.Equal(new[] { 2, 3, 4 }, TagRangeModel.CollectUsedTags(map).Order());
    }
}
