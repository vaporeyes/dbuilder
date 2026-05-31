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
    string DefaultName,
    string ThingCategory = "",
    string ActionTitle = "")
{
    public bool HasComment => Comment.Length > 0;
}

public sealed record TagExplorerTreeNode(
    string Title,
    TagExplorerEntry? Entry,
    IReadOnlyList<TagExplorerTreeNode> Children)
{
    public bool IsEntry => Entry != null;
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
        AddSectors(entries, map, config, options.DisplayMode, showTags, showActions, filters, commentSearch, commentsOnly, options.IsUdmf);
        AddLinedefs(entries, map, config, options.DisplayMode, showTags, showActions, filters, commentSearch, commentsOnly, options.IsUdmf);

        entries.Sort(Comparer(options.SortMode, options.DisplayMode));
        return entries;
    }

    public static IReadOnlyList<TagExplorerTreeNode> BuildTree(
        IReadOnlyList<TagExplorerEntry> entries,
        TagExplorerOptions options,
        IReadOnlyDictionary<int, string>? tagLabels = null)
    {
        var roots = new List<TagExplorerTreeNode>();
        AddKindRoot(roots, entries, TagExplorerEntryKind.Thing, "Things:", options, tagLabels);
        AddKindRoot(roots, entries, TagExplorerEntryKind.Sector, "Sectors:", options, tagLabels);
        AddKindRoot(roots, entries, TagExplorerEntryKind.Linedef, "Linedefs:", options, tagLabels);
        return roots;
    }

    public static string ExportTreeText(IReadOnlyList<TagExplorerTreeNode> roots, TagExplorerSortMode sortMode)
    {
        var blocks = new List<string>();
        string sortName = SortModeTitle(sortMode).ToLowerInvariant();

        foreach (TagExplorerTreeNode root in roots)
        {
            if (root.Children.Count == 0) continue;

            var lines = new List<string> { root.Title.Replace(":", " (" + sortName + "):", StringComparison.Ordinal) };
            foreach (TagExplorerTreeNode child in root.Children)
            {
                if (child.Children.Count > 0)
                {
                    lines.Add("  " + child.Title + ":");
                    foreach (TagExplorerTreeNode grandchild in child.Children)
                        lines.Add("    " + grandchild.Title);
                }
                else
                {
                    lines.Add("  " + child.Title);
                }
            }

            blocks.Add(string.Join(Environment.NewLine, lines));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
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

    private static void AddKindRoot(
        List<TagExplorerTreeNode> roots,
        IReadOnlyList<TagExplorerEntry> entries,
        TagExplorerEntryKind kind,
        string title,
        TagExplorerOptions options,
        IReadOnlyDictionary<int, string>? tagLabels)
    {
        var kindEntries = entries.Where(entry => entry.Kind == kind).ToList();
        if (kindEntries.Count == 0) return;

        kindEntries.Sort(Comparer(options.SortMode, options.DisplayMode));
        IReadOnlyList<TagExplorerTreeNode> children = options.SortMode switch
        {
            TagExplorerSortMode.ByTag => GroupByTag(kindEntries, options.SortMode, tagLabels),
            TagExplorerSortMode.ByAction => GroupByAction(kindEntries, kind, options.SortMode),
            TagExplorerSortMode.ByIndex when kind == TagExplorerEntryKind.Thing && options.DisplayMode != TagExplorerDisplayMode.Polyobjects => GroupThingsByCategory(kindEntries, options.SortMode),
            _ => kindEntries.Select(entry => EntryNode(entry, options.SortMode)).ToList(),
        };

        roots.Add(new TagExplorerTreeNode(title, null, children));
    }

    private static IReadOnlyList<TagExplorerTreeNode> GroupThingsByCategory(
        IReadOnlyList<TagExplorerEntry> entries,
        TagExplorerSortMode sortMode)
    {
        var categories = new List<TagExplorerTreeNode>();
        foreach (IGrouping<string, TagExplorerEntry> group in entries.GroupBy(entry => entry.ThingCategory))
        {
            string title = group.Key.Length > 0 ? group.Key : "UNKNOWN";
            categories.Add(new TagExplorerTreeNode(title, null, group.Select(entry => EntryNode(entry, sortMode)).ToList()));
        }

        return categories;
    }

    private static IReadOnlyList<TagExplorerTreeNode> GroupByTag(
        IReadOnlyList<TagExplorerEntry> entries,
        TagExplorerSortMode sortMode,
        IReadOnlyDictionary<int, string>? tagLabels)
    {
        var nodes = new List<TagExplorerTreeNode>();
        foreach (var group in entries.Where(entry => entry.Tag != 0).GroupBy(entry => entry.Tag).OrderBy(group => group.Key))
        {
            string title = "Tag " + group.Key;
            if (tagLabels != null && tagLabels.TryGetValue(group.Key, out string? label))
                title += ": " + label;

            nodes.Add(new TagExplorerTreeNode(title, null, group.Select(entry => EntryNode(entry, sortMode)).ToList()));
        }

        var noTag = entries.Where(entry => entry.Tag == 0).Select(entry => EntryNode(entry, sortMode)).ToList();
        if (noTag.Count > 0) nodes.Add(new TagExplorerTreeNode("No Tag", null, noTag));
        return nodes;
    }

    private static IReadOnlyList<TagExplorerTreeNode> GroupByAction(
        IReadOnlyList<TagExplorerEntry> entries,
        TagExplorerEntryKind kind,
        TagExplorerSortMode sortMode)
    {
        var nodes = new List<TagExplorerTreeNode>();
        foreach (var group in entries.Where(entry => entry.Action != 0).GroupBy(entry => entry.Action).OrderBy(group => group.Key))
            nodes.Add(new TagExplorerTreeNode(ActionGroupTitle(kind, group.Key, group.First().ActionTitle), null, group.Select(entry => EntryNode(entry, sortMode)).ToList()));

        var noAction = entries.Where(entry => entry.Action == 0).Select(entry => EntryNode(entry, sortMode)).ToList();
        if (noAction.Count > 0) nodes.Add(new TagExplorerTreeNode(kind == TagExplorerEntryKind.Sector ? "No Effect" : "No Action", null, noAction));
        return nodes;
    }

    private static string ActionGroupTitle(TagExplorerEntryKind kind, int action, string title)
    {
        if (title.Length > 0) return action + " - " + title;
        return kind == TagExplorerEntryKind.Sector ? "Effect " + action : "Action " + action;
    }

    private static TagExplorerTreeNode EntryNode(TagExplorerEntry entry, TagExplorerSortMode sortMode)
        => new(EntryTitle(entry, sortMode), entry, Array.Empty<TagExplorerTreeNode>());

    private static string EntryTitle(TagExplorerEntry entry, TagExplorerSortMode sortMode)
    {
        string name = entry.Comment.Length > 0 ? entry.Comment : entry.DefaultName;
        return sortMode switch
        {
            TagExplorerSortMode.ByAction => (entry.Tag > 0 ? "Tag " + entry.Tag + ": " : "") + name + ", Index " + entry.Index,
            TagExplorerSortMode.ByTag => (entry.Action > 0 ? "Action " + entry.Action + ": " : "") + name + ", Index " + entry.Index,
            TagExplorerSortMode.ByPolyobjectNumber => "PO " + entry.PolyobjectNumber + ": " + entry.DefaultName + ", Index " + entry.Index,
            _ => entry.Index + ": " + name + (entry.Tag > 0 ? ", Tag " + entry.Tag : "") + (entry.Action > 0 ? ", Action " + entry.Action : ""),
        };
    }

    private static string SortModeTitle(TagExplorerSortMode sortMode)
        => sortMode switch
        {
            TagExplorerSortMode.ByAction => "By Action Special",
            TagExplorerSortMode.ByTag => "By Tag",
            TagExplorerSortMode.ByPolyobjectNumber => "By Polyobject Number",
            _ => "By Index",
        };

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
                    "Thing",
                    ThingCategory(thing, config),
                    LinedefActionTitle(thing.Action, config)), commentSearch, commentsOnly, displayMode);
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
        GameConfiguration? config,
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
                    "Sector",
                    ActionTitle: SectorEffectTitle(sector.Special, config)), commentSearch, commentsOnly, displayMode);
            }
        }
    }

    private static void AddLinedefs(
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
                        "Linedef",
                        ActionTitle: LinedefActionTitle(line.Action, config)), commentSearch, commentsOnly, displayMode);
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

    private static string ThingCategory(Thing thing, GameConfiguration? config)
    {
        ThingTypeInfo? info = config?.GetThing(thing.Type);
        if (info == null) return "UNKNOWN";
        if (config != null && config.ThingCategories.TryGetValue(info.Category, out ThingCategoryInfo? category))
            return category.Title;

        return info.Category.Length > 0 ? info.Category : "UNKNOWN";
    }

    private static string LinedefActionTitle(int action, GameConfiguration? config)
        => action != 0 && config?.GetLinedefAction(action) is { } info ? info.Title : "";

    private static string SectorEffectTitle(int effect, GameConfiguration? config)
        => effect != 0 && config?.GetSectorEffect(effect) is { } info ? info.Title : "";

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
