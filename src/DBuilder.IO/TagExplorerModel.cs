// ABOUTME: Builds UDB-style TagExplorer entries from map tags, actions, polyobjects, and comments.
// ABOUTME: Keeps plugin tree filtering and sorting testable without binding to the editor window.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public enum TagExplorerDisplayMode
{
    TagsAndActions,
    Tags,
    Actions,
    Polyobjects,
}

public enum TagExplorerSortMode
{
    ByIndex,
    ByTag,
    ByAction,
    ByPolyobjectNumber,
}

public enum TagExplorerEntryKind
{
    Thing,
    Sector,
    Linedef,
}

public sealed record TagExplorerOptions(
    TagExplorerDisplayMode DisplayMode = TagExplorerDisplayMode.TagsAndActions,
    TagExplorerSortMode SortMode = TagExplorerSortMode.ByIndex,
    string SearchText = "",
    bool CommentsOnly = false,
    bool IsUdmf = true);

public sealed record TagExplorerSpecialFilters(
    IReadOnlySet<int> Tags,
    IReadOnlySet<int> Actions,
    IReadOnlySet<int> Polyobjects)
{
    public bool HasAny => Tags.Count > 0 || Actions.Count > 0 || Polyobjects.Count > 0;
}

public sealed record TagExplorerEntry(
    TagExplorerEntryKind Kind,
    int Index,
    int Tag,
    int Action,
    int PolyobjectNumber,
    string Comment,
    string DefaultName)
{
    public bool HasComment => Comment.Length > 0;
}

public static class TagExplorerModel
{
    public const int NoPolyobjectNumber = int.MinValue;

    public static IReadOnlyList<TagExplorerEntry> BuildEntries(
        MapSet map,
        GameConfiguration? config,
        TagExplorerOptions options)
    {
        var filters = ParseSpecialFilters(options.SearchText);
        string commentSearch = NormalizeCommentSearch(options.SearchText, options.IsUdmf, filters);
        bool showTags = options.DisplayMode is TagExplorerDisplayMode.Tags or TagExplorerDisplayMode.TagsAndActions;
        bool showActions = options.DisplayMode is TagExplorerDisplayMode.Actions or TagExplorerDisplayMode.TagsAndActions;
        bool commentsOnly = options.IsUdmf && options.CommentsOnly;

        var entries = new List<TagExplorerEntry>();
        AddThings(entries, map, config, options.DisplayMode, showTags, showActions, filters, commentSearch, commentsOnly, options.IsUdmf);
        AddSectors(entries, map, options.DisplayMode, showTags, showActions, filters, commentSearch, commentsOnly, options.IsUdmf);
        AddLinedefs(entries, map, options.DisplayMode, showTags, showActions, filters, commentSearch, commentsOnly, options.IsUdmf);

        entries.Sort(Comparer(options.SortMode, options.DisplayMode));
        return entries;
    }

    public static TagExplorerSpecialFilters ParseSpecialFilters(string searchText)
    {
        var tags = new HashSet<int>();
        var actions = new HashSet<int>();
        var polyobjects = new HashSet<int>();

        AddSpecialValues(searchText, '#', tags);
        AddSpecialValues(searchText, '$', actions);
        AddSpecialValues(searchText, '^', polyobjects);

        return new TagExplorerSpecialFilters(tags, actions, polyobjects);
    }

    private static void AddThings(
        List<TagExplorerEntry> entries,
        MapSet map,
        GameConfiguration? config,
        TagExplorerDisplayMode displayMode,
        bool showTags,
        bool showActions,
        TagExplorerSpecialFilters filters,
        string commentSearch,
        bool commentsOnly,
        bool isUdmf)
    {
        if (!((config?.HasThingAction ?? true) || (config?.HasThingTag ?? true))) return;

        for (int i = 0; i < map.Things.Count; i++)
        {
            var thing = map.Things[i];
            int polyobjectNumber = ThingPolyobjectNumber(thing);
            bool includeTagged = (showTags && thing.Tag != 0) || (showActions && thing.Action > 0);

            if (includeTagged)
            {
                if (filters.Tags.Count > 0 && !filters.Tags.Contains(thing.Tag)) continue;
                if (filters.Actions.Count > 0 && !filters.Actions.Contains(thing.Action)) continue;
                if (filters.Polyobjects.Count > 0 && !filters.Polyobjects.Contains(polyobjectNumber)) continue;

                AddEntry(entries, new TagExplorerEntry(
                    TagExplorerEntryKind.Thing,
                    i,
                    thing.Tag,
                    thing.Action,
                    polyobjectNumber,
                    Comment(thing, isUdmf),
                    "Thing"), commentSearch, commentsOnly, displayMode);
            }
            else if (displayMode == TagExplorerDisplayMode.Polyobjects &&
                polyobjectNumber != NoPolyobjectNumber &&
                (filters.Polyobjects.Count == 0 || filters.Polyobjects.Contains(polyobjectNumber)))
            {
                AddEntry(entries, new TagExplorerEntry(
                    TagExplorerEntryKind.Thing,
                    i,
                    thing.Tag,
                    thing.Action,
                    polyobjectNumber,
                    "",
                    "Thing"), commentSearch, commentsOnly, displayMode);
            }
        }
    }

    private static void AddSectors(
        List<TagExplorerEntry> entries,
        MapSet map,
        TagExplorerDisplayMode displayMode,
        bool showTags,
        bool showActions,
        TagExplorerSpecialFilters filters,
        string commentSearch,
        bool commentsOnly,
        bool isUdmf)
    {
        if (displayMode == TagExplorerDisplayMode.Polyobjects) return;

        for (int i = 0; i < map.Sectors.Count; i++)
        {
            var sector = map.Sectors[i];
            if (!((showTags && sector.Tag != 0) || (showActions && sector.Special > 0))) continue;
            if (filters.Actions.Count > 0 && !filters.Actions.Contains(sector.Special)) continue;

            foreach (int tag in TagsOrZero(sector.Tags))
            {
                if (filters.Tags.Count > 0 && !filters.Tags.Contains(tag)) continue;
                AddEntry(entries, new TagExplorerEntry(
                    TagExplorerEntryKind.Sector,
                    i,
                    tag,
                    sector.Special,
                    NoPolyobjectNumber,
                    Comment(sector, isUdmf),
                    "Sector"), commentSearch, commentsOnly, displayMode);
            }
        }
    }

    private static void AddLinedefs(
        List<TagExplorerEntry> entries,
        MapSet map,
        TagExplorerDisplayMode displayMode,
        bool showTags,
        bool showActions,
        TagExplorerSpecialFilters filters,
        string commentSearch,
        bool commentsOnly,
        bool isUdmf)
    {
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            var line = map.Linedefs[i];
            int polyobjectNumber = LinedefPolyobjectNumber(line);
            bool includeTagged = (showTags && line.Tag != 0) || (showActions && line.Action > 0);

            if (includeTagged)
            {
                if (filters.Actions.Count > 0 && !filters.Actions.Contains(line.Action)) continue;
                if (filters.Polyobjects.Count > 0 && !filters.Polyobjects.Contains(polyobjectNumber)) continue;

                foreach (int tag in TagsOrZero(line.Tags))
                {
                    if (filters.Tags.Count > 0 && !filters.Tags.Contains(tag)) continue;
                    AddEntry(entries, new TagExplorerEntry(
                        TagExplorerEntryKind.Linedef,
                        i,
                        tag,
                        line.Action,
                        polyobjectNumber,
                        polyobjectNumber == NoPolyobjectNumber ? Comment(line, isUdmf) : "",
                        "Linedef"), commentSearch, commentsOnly, displayMode);
                }
            }
            else if (displayMode == TagExplorerDisplayMode.Polyobjects &&
                polyobjectNumber != NoPolyobjectNumber &&
                (filters.Polyobjects.Count == 0 || filters.Polyobjects.Contains(polyobjectNumber)))
            {
                AddEntry(entries, new TagExplorerEntry(
                    TagExplorerEntryKind.Linedef,
                    i,
                    line.Tag,
                    line.Action,
                    polyobjectNumber,
                    "",
                    "Linedef"), commentSearch, commentsOnly, displayMode);
            }
        }
    }

    private static void AddEntry(
        List<TagExplorerEntry> entries,
        TagExplorerEntry entry,
        string commentSearch,
        bool commentsOnly,
        TagExplorerDisplayMode displayMode)
    {
        if (displayMode != TagExplorerDisplayMode.Polyobjects)
        {
            if (commentsOnly && !entry.HasComment) return;
            if (commentSearch.Length > 0 && !entry.Comment.Contains(commentSearch, StringComparison.OrdinalIgnoreCase)) return;
        }

        entries.Add(entry);
    }

    private static Comparison<TagExplorerEntry> Comparer(TagExplorerSortMode sortMode, TagExplorerDisplayMode displayMode)
        => sortMode switch
        {
            TagExplorerSortMode.ByAction => SortByAction,
            TagExplorerSortMode.ByTag => SortByTag,
            TagExplorerSortMode.ByIndex => displayMode == TagExplorerDisplayMode.Polyobjects ? SortByPolyobjectNumber : SortByIndex,
            TagExplorerSortMode.ByPolyobjectNumber => SortByPolyobjectNumber,
            _ => SortByIndex,
        };

    private static int SortByAction(TagExplorerEntry left, TagExplorerEntry right)
    {
        if (left.Action == right.Action) return SortByTag(left, right);
        if (left.Action == 0) return 1;
        if (right.Action == 0) return -1;
        return left.Action.CompareTo(right.Action);
    }

    private static int SortByTag(TagExplorerEntry left, TagExplorerEntry right)
    {
        if (left.Tag == right.Tag) return SortByIndex(left, right);
        if (left.Tag == 0) return 1;
        if (right.Tag == 0) return -1;
        return left.Tag.CompareTo(right.Tag);
    }

    private static int SortByIndex(TagExplorerEntry left, TagExplorerEntry right)
        => left.Index.CompareTo(right.Index);

    private static int SortByPolyobjectNumber(TagExplorerEntry left, TagExplorerEntry right)
    {
        int number = left.PolyobjectNumber.CompareTo(right.PolyobjectNumber);
        return number != 0 ? number : string.CompareOrdinal(left.DefaultName, right.DefaultName);
    }

    private static string NormalizeCommentSearch(string searchText, bool isUdmf, TagExplorerSpecialFilters filters)
        => !isUdmf || filters.HasAny ? "" : searchText;

    private static void AddSpecialValues(string searchText, char marker, HashSet<int> result)
    {
        foreach (var part in searchText.Split(marker))
        {
            int value = ReadNumber(part);
            if (value != NoPolyobjectNumber) result.Add(value);
        }
    }

    private static int ReadNumber(string text)
    {
        int pos = 0;
        while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '-' || text[pos] == '+')) pos++;
        if (pos == 0) return NoPolyobjectNumber;

        return int.TryParse(text[..pos], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : NoPolyobjectNumber;
    }

    private static string Comment(IFielded element, bool isUdmf)
        => isUdmf && element.Fields.TryGetValue(CommentsPanelModel.CommentField, out var raw)
            ? raw?.ToString() ?? ""
            : "";

    private static IEnumerable<int> TagsOrZero(IReadOnlyCollection<int> tags)
    {
        if (tags.Count == 0)
        {
            yield return 0;
            yield break;
        }

        foreach (int tag in tags)
            yield return tag;
    }

    private static int ThingPolyobjectNumber(Thing thing)
        => thing.Type > 9299 && thing.Type < 9304 ? thing.Angle : NoPolyobjectNumber;

    private static int LinedefPolyobjectNumber(Linedef line)
        => line.Action > 0 && line.Action < 9 ? line.Args[0] : NoPolyobjectNumber;
}
