// ABOUTME: Builds UDB-style comment groups from UDMF comment fields on map elements.
// ABOUTME: Keeps CommentsPanel data behavior separate from editor docking and selection UI.

namespace DBuilder.Map;

public enum CommentsPanelMode
{
    All,
    Vertices,
    Linedefs,
    Sectors,
    Things,
}

public enum CommentedElementKind
{
    Vertex,
    Linedef,
    Sidedef,
    Sector,
    Thing,
}

public sealed record CommentedElement(CommentedElementKind Kind, IFielded Element);

public sealed record CommentGroup(CommentsPanelMode Group, string Comment, IReadOnlyList<CommentedElement> Elements);

public static class CommentsPanelModel
{
    public const string CommentField = "comment";

    public static IReadOnlyList<CommentGroup> BuildGroups(MapSet map, CommentsPanelMode filterMode = CommentsPanelMode.All)
    {
        var groups = new List<CommentGroup>();

        if (filterMode is CommentsPanelMode.All or CommentsPanelMode.Vertices)
            AddGroups(groups, CommentsPanelMode.Vertices, map.Vertices, CommentedElementKind.Vertex);

        if (filterMode is CommentsPanelMode.All or CommentsPanelMode.Linedefs)
        {
            var byComment = new Dictionary<string, List<CommentedElement>>(StringComparer.Ordinal);
            AddElements(byComment, map.Linedefs, CommentedElementKind.Linedef);
            AddElements(byComment, map.Sidedefs, CommentedElementKind.Sidedef);
            AddGroups(groups, CommentsPanelMode.Linedefs, byComment);
        }

        if (filterMode is CommentsPanelMode.All or CommentsPanelMode.Sectors)
            AddGroups(groups, CommentsPanelMode.Sectors, map.Sectors, CommentedElementKind.Sector);

        if (filterMode is CommentsPanelMode.All or CommentsPanelMode.Things)
            AddGroups(groups, CommentsPanelMode.Things, map.Things, CommentedElementKind.Thing);

        return groups
            .OrderBy(g => g.Comment, StringComparer.Ordinal)
            .ThenBy(g => g.Group)
            .ToList();
    }

    public static bool CanSetSelectionComment(CommentsPanelMode mode, int selectedCount)
        => mode is CommentsPanelMode.Vertices or CommentsPanelMode.Linedefs or CommentsPanelMode.Sectors or CommentsPanelMode.Things
            && selectedCount > 0;

    public static void SetComment(IEnumerable<IFielded> elements, string comment)
    {
        foreach (var element in elements)
            element.Fields[CommentField] = comment;
    }

    public static void RemoveComment(CommentGroup group)
    {
        foreach (var item in group.Elements)
            item.Element.Fields.Remove(CommentField);
    }

    private static void AddGroups<T>(
        List<CommentGroup> groups,
        CommentsPanelMode group,
        IEnumerable<T> elements,
        CommentedElementKind kind)
        where T : IFielded
    {
        var byComment = new Dictionary<string, List<CommentedElement>>(StringComparer.Ordinal);
        AddElements(byComment, elements, kind);

        AddGroups(groups, group, byComment);
    }

    private static void AddElements<T>(
        Dictionary<string, List<CommentedElement>> byComment,
        IEnumerable<T> elements,
        CommentedElementKind kind)
        where T : IFielded
    {
        foreach (var element in elements)
        {
            if (!element.Fields.TryGetValue(CommentField, out var raw)) continue;

            string comment = raw?.ToString() ?? "";
            if (!byComment.TryGetValue(comment, out var list))
            {
                list = new List<CommentedElement>();
                byComment.Add(comment, list);
            }

            list.Add(new CommentedElement(kind, element));
        }
    }

    private static void AddGroups(
        List<CommentGroup> groups,
        CommentsPanelMode group,
        Dictionary<string, List<CommentedElement>> byComment)
    {
        foreach (var item in byComment)
            groups.Add(new CommentGroup(group, item.Key, item.Value));
    }
}
