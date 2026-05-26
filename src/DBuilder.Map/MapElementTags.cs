// ABOUTME: Tag helpers for map elements that support primary tags and UDMF moreids.
// ABOUTME: Centralizes tag search and replacement semantics for linedefs, sectors and things.

namespace DBuilder.Map;

public static class MapElementTags
{
    public static bool HasTag(Linedef line, int tag) => HasTag(line.Tags, tag);
    public static bool HasTag(Sector sector, int tag) => HasTag(sector.Tags, tag);
    public static bool HasTag(Thing thing, int tag) => thing.Tag == tag;

    public static bool ReplaceTag(Linedef line, int from, int to) => ReplaceTag(line.Tags, from, to);
    public static bool ReplaceTag(Sector sector, int from, int to) => ReplaceTag(sector.Tags, from, to);

    public static bool ReplaceTag(Thing thing, int from, int to)
    {
        if (thing.Tag != from) return false;
        thing.Tag = to;
        return true;
    }

    public static IEnumerable<int> PositiveTags(Linedef line) => PositiveTags(line.Tags);
    public static IEnumerable<int> PositiveTags(Sector sector) => PositiveTags(sector.Tags);

    public static IEnumerable<int> PositiveTags(Thing thing)
    {
        if (thing.Tag > 0) yield return thing.Tag;
    }

    private static bool HasTag(List<int> tags, int tag)
    {
        if (tags.Count == 0) return tag == 0;
        return tags.Contains(tag);
    }

    private static bool ReplaceTag(List<int> tags, int from, int to)
    {
        if (tags.Count == 0)
        {
            if (from != 0) return false;
            tags.Add(to);
            return true;
        }

        bool changed = false;
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] != from) continue;
            tags[i] = to;
            changed = true;
        }
        return changed;
    }

    private static IEnumerable<int> PositiveTags(List<int> tags)
    {
        foreach (int tag in tags)
            if (tag > 0) yield return tag;
    }
}
