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

public static class TagRangeModel
{
    public static TagRangeStoredOptions StoredOptionsFrom(TagRangeOptions options)
    {
        int step = options.Step == 0 ? 1 : options.Step;
        return new TagRangeStoredOptions(step, options.Relative);
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

    private static bool OutsideRange(int tag, TagRangeOptions options)
        => tag > options.MaxTag || tag < options.MinTag;

    private static bool AtOrBeyondRange(int tag, TagRangeOptions options)
        => tag >= options.MaxTag || tag <= options.MinTag;
}
