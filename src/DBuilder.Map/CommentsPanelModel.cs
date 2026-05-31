// ABOUTME: Builds UDB-style comment groups from UDMF comment fields on map elements.
// ABOUTME: Keeps CommentsPanel data behavior separate from editor docking and selection UI.

namespace DBuilder.Map;

using System.Drawing;
using DBuilder.Geometry;

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

public sealed record CommentSelectionTarget(CommentsPanelMode Mode, IReadOnlyList<IFielded> Elements);

public sealed record CommentEditTarget(CommentsPanelMode Mode, IReadOnlyList<IFielded> Elements);

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

    public static CommentsPanelMode SelectionMode(CommentGroup group)
    {
        if (group.Elements.Count == 0) return CommentsPanelMode.All;

        return group.Elements[0].Kind switch
        {
            CommentedElementKind.Vertex => CommentsPanelMode.Vertices,
            CommentedElementKind.Linedef or CommentedElementKind.Sidedef => CommentsPanelMode.Linedefs,
            CommentedElementKind.Sector => CommentsPanelMode.Sectors,
            CommentedElementKind.Thing => CommentsPanelMode.Things,
            _ => CommentsPanelMode.All,
        };
    }

    public static CommentSelectionTarget CreateSelectionTarget(CommentGroup group)
    {
        var elements = new List<IFielded>(group.Elements.Count);
        foreach (var item in group.Elements)
            elements.Add(item.Element);

        return new CommentSelectionTarget(SelectionMode(group), elements);
    }

    public static CommentEditTarget CreateEditTarget(CommentGroup group)
    {
        var elements = new List<IFielded>(group.Elements.Count);
        foreach (var item in group.Elements)
            elements.Add(item.Element is Sidedef side ? side.Line : item.Element);

        return new CommentEditTarget(SelectionMode(group), elements);
    }

    public static RectangleF CreateViewArea(CommentGroup group)
    {
        var points = new List<Vector2D>();
        foreach (var item in group.Elements)
            AddViewPoints(item.Element, points);

        RectangleF area = MapSet.CreateEmptyArea();
        foreach (var point in points)
            area = MapSet.IncreaseArea(area, point);

        if (area.Width > area.Height)
        {
            float delta = area.Width - area.Height;
            area.Y -= delta * 0.5f;
            area.Height += delta;
        }
        else
        {
            float delta = area.Height - area.Width;
            area.X -= delta * 0.5f;
            area.Width += delta;
        }

        area.Inflate(100f, 100f);
        return area;
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

    private static void AddViewPoints(IFielded element, List<Vector2D> points)
    {
        switch (element)
        {
            case Vertex vertex:
                points.Add(vertex.Position);
                break;

            case Linedef line:
                points.Add(line.Start.Position);
                points.Add(line.End.Position);
                break;

            case Sidedef side:
                points.Add(side.Line.Start.Position);
                points.Add(side.Line.End.Position);
                break;

            case Sector sector:
                foreach (var side in sector.Sidedefs)
                {
                    points.Add(side.Line.Start.Position);
                    points.Add(side.Line.End.Position);
                }
                break;

            case Thing thing:
                var p = thing.Position;
                double size = thing.Size * 2.0;
                points.Add(p);
                points.Add(p + new Vector2D(size, size));
                points.Add(p + new Vector2D(size, -size));
                points.Add(p + new Vector2D(-size, size));
                points.Add(p + new Vector2D(-size, -size));
                break;
        }
    }
}
