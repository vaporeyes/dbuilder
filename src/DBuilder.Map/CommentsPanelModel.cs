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

public sealed record CommentsPanelPersistedSettings(
    bool FilterMode,
    bool ClickSelects);

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

public enum CommentIconKind
{
    Regular,
    Info,
    Question,
    Problem,
    Smile,
}

public enum CommentIconColorRole
{
    White,
    Selection,
    Highlight,
}

public sealed record CommentRenderOptions(
    CommentsPanelMode Mode,
    bool IsUdmf = true,
    bool RenderComments = true,
    double Scale = 1.0,
    bool FixedThingsScale = false,
    IFielded? Highlighted = null,
    IReadOnlyDictionary<Sector, IReadOnlyList<LabelPositionInfo>>? SectorLabels = null);

public sealed record CommentRenderIcon(
    CommentedElementKind Kind,
    IFielded Element,
    RectangleF Rectangle,
    CommentIconKind Icon,
    CommentIconColorRole Color,
    string Comment,
    string TooltipText);

public static class CommentsPanelModel
{
    public const string DockerId = "commentsdockerpanel";
    public const string DockerTitle = "Comments";
    public const string FilterModeSettingKey = "filtermode";
    public const string ClickSelectsSettingKey = "clickselects";
    public const string CommentField = "comment";
    public static readonly string[] CommentTypePrefixes = ["", "[i]", "[?]", "[!]", "[:]"];

    public static CommentsPanelPersistedSettings ReadSettings(IReadOnlyDictionary<string, object?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CommentsPanelPersistedSettings(
            ReadBool(settings, FilterModeSettingKey, false),
            ReadBool(settings, ClickSelectsSettingKey, false));
    }

    public static IReadOnlyDictionary<string, object> WriteSettings(CommentsPanelPersistedSettings settings)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [FilterModeSettingKey] = settings.FilterMode,
            [ClickSelectsSettingKey] = settings.ClickSelects,
        };

    public static CommentsPanelMode EffectiveFilterMode(
        CommentsPanelPersistedSettings settings,
        CommentsPanelMode currentMode,
        CommentsPanelMode selectedMode)
        => settings.FilterMode ? currentMode : selectedMode;

    public static IReadOnlyList<CommentGroup> BuildGroups(
        MapSet map,
        CommentsPanelMode filterMode = CommentsPanelMode.All,
        string? searchText = null)
    {
        string search = searchText?.Trim() ?? "";
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
            .Where(group => MatchesSearch(group, search))
            .OrderBy(g => g.Comment, StringComparer.Ordinal)
            .ThenBy(g => g.Group)
            .ToList();
    }

    private static bool MatchesSearch(CommentGroup group, string search)
        => search.Length == 0
        || group.Comment.Contains(search, StringComparison.OrdinalIgnoreCase)
        || group.Group.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);

    public static bool CanSetSelectionComment(CommentsPanelMode mode, int selectedCount)
        => mode is CommentsPanelMode.Vertices or CommentsPanelMode.Linedefs or CommentsPanelMode.Sectors or CommentsPanelMode.Things
            && selectedCount > 0;

    public static string HeaderText(int groupCount)
        => groupCount == 0
            ? "No comments found."
            : $"{groupCount} {Label(groupCount, "comment group")}. Click a row to select and reveal it.";

    public static string RemoveStatusText(int elementCount)
        => $"Removed comment from {elementCount} {Label(elementCount, "element")}.";

    public static string SetStatusText(int elementCount)
        => $"Set comment on {elementCount} {Label(elementCount, "element")}.";

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
        {
            elements.Add(item.Element);
            if (item.Element is Sector sector)
            {
                foreach (Sidedef side in sector.Sidedefs)
                    if (side.Line != null)
                        elements.Add(side.Line);
            }
        }

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

    public static IReadOnlyList<CommentRenderIcon> BuildRenderIcons(MapSet map, CommentRenderOptions options)
    {
        if (!options.IsUdmf || !options.RenderComments) return Array.Empty<CommentRenderIcon>();

        double scale = Math.Max(options.Scale, 0.000001);
        var icons = new List<CommentRenderIcon>();

        if (options.Mode == CommentsPanelMode.Linedefs)
        {
            foreach (Linedef line in map.Linedefs)
                if (!line.IsDisposed && TryGetComment(line, out string comment, out CommentIconKind icon, out string tooltip))
                    icons.Add(RenderLine(line, scale, options.Highlighted, comment, icon, tooltip));
        }
        else if (options.Mode == CommentsPanelMode.Sectors)
        {
            foreach (Sector sector in map.Sectors)
                AddSectorIcons(icons, sector, options, scale);
        }
        else if (options.Mode == CommentsPanelMode.Things)
        {
            foreach (Thing thing in map.Things)
                AddThingIcon(icons, thing, options, scale);
        }

        return icons;
    }

    public static bool TryGetComment(
        IFielded element,
        out string comment,
        out CommentIconKind icon,
        out string tooltipText)
    {
        icon = CommentIconKind.Regular;
        tooltipText = "";

        if (!element.Fields.TryGetValue(CommentField, out object? raw))
        {
            comment = "";
            return false;
        }

        comment = raw?.ToString() ?? "";
        tooltipText = comment;

        if (comment.Length > 2)
        {
            string type = comment.Substring(0, 3);
            int index = Array.IndexOf(CommentTypePrefixes, type);
            if (index > 0)
            {
                icon = (CommentIconKind)index;
                tooltipText = comment.TrimStart(type.ToCharArray());
            }
        }

        return true;
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

    private static CommentRenderIcon RenderLine(
        Linedef line,
        double scale,
        IFielded? highlighted,
        string comment,
        CommentIconKind icon,
        string tooltip)
    {
        Vector2D center = line.GetCenterPoint();
        RectangleF rect = new(
            (float)(center.x - 8.0 / scale),
            (float)(center.y + 18.0 / scale),
            (float)(16.0 / scale),
            (float)(-16.0 / scale));

        return new CommentRenderIcon(
            CommentedElementKind.Linedef,
            line,
            rect,
            icon,
            ColorFor(line, highlighted),
            comment,
            tooltip);
    }

    private static void AddSectorIcons(List<CommentRenderIcon> icons, Sector sector, CommentRenderOptions options, double scale)
    {
        if (sector.IsDisposed || sector.Selected) return;
        if (!TryGetComment(sector, out string comment, out CommentIconKind icon, out string tooltip)) return;

        if (options.SectorLabels != null &&
            options.SectorLabels.TryGetValue(sector, out IReadOnlyList<LabelPositionInfo>? labels) &&
            labels.Count > 0)
        {
            foreach (LabelPositionInfo label in labels)
                icons.Add(RenderSector(sector, label.position, scale, options.Highlighted, comment, icon, tooltip));
        }
        else if (TryGetSectorCenter(sector, out Vector2D center))
        {
            icons.Add(RenderSector(sector, center, scale, options.Highlighted, comment, icon, tooltip));
        }
    }

    private static CommentRenderIcon RenderSector(
        Sector sector,
        Vector2D center,
        double scale,
        IFielded? highlighted,
        string comment,
        CommentIconKind icon,
        string tooltip)
    {
        RectangleF rect = new(
            (float)(center.x - 8.0 / scale),
            (float)(center.y + 8.0 / scale),
            (float)(16.0 / scale),
            (float)(-16.0 / scale));

        return new CommentRenderIcon(
            CommentedElementKind.Sector,
            sector,
            rect,
            icon,
            ReferenceEquals(sector, highlighted) ? CommentIconColorRole.Highlight : CommentIconColorRole.White,
            comment,
            tooltip);
    }

    private static void AddThingIcon(List<CommentRenderIcon> icons, Thing thing, CommentRenderOptions options, double scale)
    {
        if (thing.IsDisposed) return;
        if (!TryGetComment(thing, out string comment, out CommentIconKind icon, out string tooltip)) return;

        double size = ((thing.FixedSize || options.FixedThingsScale) && scale > 1.0)
            ? thing.Size / scale
            : thing.Size;
        if (size * scale < 1.5) return;

        RectangleF rect = new(
            (float)(thing.Position.x + size - 10.0 / scale),
            (float)(thing.Position.y + size + 18.0 / scale),
            (float)(16.0 / scale),
            (float)(-16.0 / scale));

        icons.Add(new CommentRenderIcon(
            CommentedElementKind.Thing,
            thing,
            rect,
            icon,
            ColorFor(thing, options.Highlighted),
            comment,
            tooltip));
    }

    private static bool TryGetSectorCenter(Sector sector, out Vector2D center)
    {
        RectangleF area = MapSet.CreateEmptyArea();
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line == null) continue;
            area = MapSet.IncreaseArea(area, side.Line.Start.Position);
            area = MapSet.IncreaseArea(area, side.Line.End.Position);
        }

        if (area.Width < 0 || area.Height < 0)
        {
            center = default;
            return false;
        }

        center = new Vector2D(area.Left + area.Width / 2.0, area.Top + area.Height / 2.0);
        return true;
    }

    private static CommentIconColorRole ColorFor(ISelectable element, IFielded? highlighted)
    {
        if (ReferenceEquals(element, highlighted)) return CommentIconColorRole.Highlight;
        return element.Selected ? CommentIconColorRole.Selection : CommentIconColorRole.White;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
    {
        if (!settings.TryGetValue(key, out object? value) || value == null) return fallback;

        return value switch
        {
            bool typed => typed,
            string text when bool.TryParse(text, out bool parsed) => parsed,
            _ => fallback,
        };
    }

    private static string Label(int count, string singular)
        => count == 1 ? singular : singular + "s";
}
