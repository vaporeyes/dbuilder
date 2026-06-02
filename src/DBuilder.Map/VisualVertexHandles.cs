// ABOUTME: Models UDB visual-mode floor and ceiling vertex handles without renderer dependencies.
// ABOUTME: Resolves handle heights, offset state, and pick bounds from adjacent sectors.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisualVertexHandleKind
{
    Floor,
    Ceiling,
}

public sealed record VisualVertexHandle(
    Vertex Vertex,
    VisualVertexHandleKind Kind,
    Vector3D Position,
    Vector3D BoundsMin,
    Vector3D BoundsMax,
    bool HaveHeightOffset)
{
    public bool CeilingVertex => Kind == VisualVertexHandleKind.Ceiling;
}

public sealed record VisualVertexHandlePair(VisualVertexHandle FloorVertex, VisualVertexHandle CeilingVertex)
{
    public VisualVertexHandle[] Vertices => [FloorVertex, CeilingVertex];
}

public static class VisualVertexHandles
{
    public const double DefaultSize = 6.0;

    public static IReadOnlyList<VisualVertexHandlePair> CreateVisiblePairs(
        MapSet map,
        bool isUdmf,
        bool vertexHeightSupport,
        bool showVisualVertices,
        double scale = 1.0)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (!isUdmf || !vertexHeightSupport || !showVisualVertices) return Array.Empty<VisualVertexHandlePair>();

        return map.Vertices
            .Select(vertex => CreatePair(map, vertex, scale))
            .ToArray();
    }

    public static VisualVertexHandlePair CreatePair(MapSet map, Vertex vertex, double scale = 1.0)
        => new(
            Create(map, vertex, VisualVertexHandleKind.Floor, scale),
            Create(map, vertex, VisualVertexHandleKind.Ceiling, scale));

    public static VisualVertexHandle Create(MapSet map, Vertex vertex, VisualVertexHandleKind kind, double scale = 1.0)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (vertex == null) throw new ArgumentNullException(nameof(vertex));

        bool ceiling = kind == VisualVertexHandleKind.Ceiling;
        double z = ceiling ? vertex.ZCeiling : vertex.ZFloor;
        bool haveOffset = !double.IsNaN(z);
        if (!haveOffset) z = GetSectorHeight(map, vertex, kind);

        var position = new Vector3D(vertex.Position.x, vertex.Position.y, z);
        double radius = DefaultSize * Math.Max(scale, 0.0);
        var min = new Vector3D(position.x - radius, position.y - radius, ceiling ? position.z - radius : position.z);
        var max = new Vector3D(position.x + radius, position.y + radius, ceiling ? position.z : position.z + radius);
        return new VisualVertexHandle(vertex, kind, position, min, max, haveOffset);
    }

    public static double GetSectorHeight(MapSet map, Vertex vertex, VisualVertexHandleKind kind)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (vertex == null) throw new ArgumentNullException(nameof(vertex));

        var sectors = AdjacentSectors(map, vertex).ToList();
        if (sectors.Count == 0) return 0;

        return kind == VisualVertexHandleKind.Ceiling
            ? sectors.Min(sector => sector.CeilHeight)
            : sectors.Max(sector => sector.FloorHeight);
    }

    public static IEnumerable<Sector> AdjacentSectors(MapSet map, Vertex vertex)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (vertex == null) throw new ArgumentNullException(nameof(vertex));

        var sectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        IEnumerable<Linedef> lines = vertex.Linedefs.Count > 0
            ? vertex.Linedefs
            : map.Linedefs.Where(line => ReferenceEquals(line.Start, vertex) || ReferenceEquals(line.End, vertex));

        foreach (Linedef line in lines)
        {
            if (line.Front?.Sector != null) sectors.Add(line.Front.Sector);
            if (line.Back?.Sector != null) sectors.Add(line.Back.Sector);
        }

        return sectors;
    }
}
