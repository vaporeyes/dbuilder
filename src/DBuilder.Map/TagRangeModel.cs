// ABOUTME: Builds UDB-style TagRange assignments for selected sectors, linedefs, or things.
// ABOUTME: Keeps range calculation and validation separate from plugin toolbar and form UI.

namespace DBuilder.Map;

public enum TagRangeTargetKind
{
    Sectors,
    Linedefs,
    Things,
}

public sealed record TagRangeOptions(
    int StartTag,
    int Step,
    bool Relative,
    bool SkipUsedTags,
    int MinTag = 1,
    int MaxTag = int.MaxValue);

public sealed record TagRangeStoredOptions(int Step = 1, bool Relative = false);

public sealed record TagRangeResult(
    IReadOnlyList<int> Tags,
    bool TagsUsed,
    bool OutOfTags);

public sealed record TagRangePreviewState(
    string Title,
    int? EndTag,
    bool OutOfTagsWarningVisible,
    bool OkEnabled,
    bool DoubleTagWarningVisible,
    bool SkipUsedTagsVisible);

public sealed record TagRangeFormatCapabilities(
    bool HasLinedefTag,
    bool HasThingTag);

public static class TagRangeModel
{
    public const string ActionName = "rangetagselection";
    public const string ToolWindowTitle = "Tag Range";
    public const string ToolButtonText = "Tag Range";
    public const string FormDesignerTitle = "Tag Selected Range";
    public const string StartTagLabel = "Start Tag:";
    public const string IncrementLabel = "Increment:";
    public const string EndTagLabel = "End Tag:";
    public const string RelativeModeText = "Relative to existing tags";
    public const string SkipUsedTagsText = "Skip over already used tags";
    public const string DuplicateWarningText = "Warning: The tag range contains already used tags.";
    public const string OutOfTagsWarningText = "The range exceeds the maximum or minimum allowed tags and cannot be created.";
    public const string OutOfTagsMessage = "The range exceeds the maximum allowed tags and cannot be created.";
    public const string OkText = "OK";
    public const string CancelText = "Cancel";
    public const string NoSelectionWarning = "This action requires a selection!";

    public static bool ShouldShowToolbarButton(string? modeName, TagRangeFormatCapabilities capabilities)
        => modeName switch
        {
            "SectorsMode" => true,
            "LinedefsMode" => capabilities.HasLinedefTag,
            "ThingsMode" => capabilities.HasThingTag,
            _ => false,
        };

    public static bool HasSelection(int selectionCount) => selectionCount > 0;

    public static TagRangeStoredOptions StoredOptionsFrom(TagRangeOptions options)
    {
        int step = options.Step == 0 ? 1 : options.Step;
        return new TagRangeStoredOptions(step, options.Relative);
    }

    public static TagRangeStoredOptions NormalizeStoredOptions(TagRangeStoredOptions? options)
    {
        options ??= new TagRangeStoredOptions();
        int step = options.Step == 0 ? 1 : options.Step;
        return options with { Step = step };
    }

    public static TagRangeResult CreateRange(
        IReadOnlyList<int> initialTags,
        IReadOnlySet<int> usedTags,
        TagRangeOptions options)
    {
        var newTags = new List<int>(initialTags.Count);
        bool tagsUsed = false;

        if (options.Relative)
        {
            int addTag = 0;
            for (int i = 0; i < initialTags.Count; i++)
            {
                int newTag = initialTags[i] + options.StartTag + addTag;
                if (OutsideRange(newTag, options)) return new TagRangeResult(newTags, tagsUsed, true);

                if (options.SkipUsedTags)
                {
                    while (usedTags.Contains(newTag))
                    {
                        if (AtOrBeyondRange(newTag, options)) return new TagRangeResult(newTags, tagsUsed, true);

                        newTag += options.Step;
                        addTag += options.Step;
                    }
                }
                else
                {
                    tagsUsed |= usedTags.Contains(newTag);
                }

                newTags.Add(newTag);
                addTag += options.Step;
            }
        }
        else
        {
            int newTag = options.StartTag;
            for (int i = 0; i < initialTags.Count; i++)
            {
                if (OutsideRange(newTag, options)) return new TagRangeResult(newTags, tagsUsed, true);

                if (options.SkipUsedTags)
                {
                    while (usedTags.Contains(newTag))
                    {
                        if (AtOrBeyondRange(newTag, options)) return new TagRangeResult(newTags, tagsUsed, true);

                        newTag += options.Step;
                    }
                }
                else
                {
                    tagsUsed |= usedTags.Contains(newTag);
                }

                newTags.Add(newTag);
                newTag += options.Step;
            }
        }

        return new TagRangeResult(newTags, tagsUsed, false);
    }

    public static TagRangePreviewState CreatePreviewState(
        TagRangeTargetKind target,
        int selectionCount,
        IReadOnlyList<int> initialTags,
        IReadOnlySet<int> usedTags,
        TagRangeOptions options)
    {
        TagRangeResult result = CreateRange(initialTags, usedTags, options);
        bool showUsedTagWarning = result.TagsUsed && !result.OutOfTags;

        return new TagRangePreviewState(
            TitleFor(target, selectionCount),
            result.Tags.Count > 0 ? result.Tags[^1] : null,
            result.OutOfTags,
            !result.OutOfTags,
            showUsedTagWarning,
            showUsedTagWarning);
    }

    public static IReadOnlyList<int> SelectedInitialTags(MapSet map, TagRangeTargetKind target)
        => target switch
        {
            TagRangeTargetKind.Sectors => map.Sectors.Where(s => s.Selected).Select(s => s.Tag).ToList(),
            TagRangeTargetKind.Linedefs => map.Linedefs.Where(l => l.Selected).Select(l => l.Tag).ToList(),
            TagRangeTargetKind.Things => map.Things.Where(t => t.Selected).Select(t => t.Tag).ToList(),
            _ => Array.Empty<int>(),
        };

    public static HashSet<int> CollectUsedTags(MapSet map)
    {
        var used = new HashSet<int>();
        foreach (var sector in map.Sectors)
            foreach (int tag in MapElementTags.PositiveTags(sector))
                used.Add(tag);

        foreach (var line in map.Linedefs)
            foreach (int tag in MapElementTags.PositiveTags(line))
                used.Add(tag);

        foreach (var thing in map.Things)
            foreach (int tag in MapElementTags.PositiveTags(thing))
                used.Add(tag);

        return used;
    }

    public static int ApplyRange(MapSet map, TagRangeTargetKind target, IReadOnlyList<int> tags)
    {
        int index = 0;
        switch (target)
        {
            case TagRangeTargetKind.Sectors:
                foreach (var sector in map.Sectors)
                {
                    if (!sector.Selected) continue;
                    if (index >= tags.Count) return index;
                    sector.Tag = tags[index++];
                }
                break;

            case TagRangeTargetKind.Linedefs:
                foreach (var line in map.Linedefs)
                {
                    if (!line.Selected) continue;
                    if (index >= tags.Count) return index;
                    line.Tag = tags[index++];
                }
                break;

            case TagRangeTargetKind.Things:
                foreach (var thing in map.Things)
                {
                    if (!thing.Selected) continue;
                    if (index >= tags.Count) return index;
                    thing.Tag = tags[index++];
                }
                break;
        }

        return index;
    }

    public static string UndoDescription(TagRangeTargetKind target, int selectionCount)
    {
        string name = target switch
        {
            TagRangeTargetKind.Sectors => "sector",
            TagRangeTargetKind.Linedefs => "linedef",
            TagRangeTargetKind.Things => "thing",
            _ => "element",
        };

        return "Set " + selectionCount + " " + name + " tags";
    }

    private static bool OutsideRange(int tag, TagRangeOptions options)
        => tag > options.MaxTag || tag < options.MinTag;

    private static bool AtOrBeyondRange(int tag, TagRangeOptions options)
        => tag >= options.MaxTag || tag <= options.MinTag;

    private static string TitleFor(TagRangeTargetKind target, int selectionCount)
    {
        string name = target switch
        {
            TagRangeTargetKind.Sectors => selectionCount == 1 ? "sector" : "sectors",
            TagRangeTargetKind.Linedefs => selectionCount == 1 ? "linedef" : "linedefs",
            TagRangeTargetKind.Things => selectionCount == 1 ? "thing" : "things",
            _ => "elements",
        };

        return "Create tag range for " + selectionCount + " " + name;
    }
}
