// ABOUTME: Verifies UDB-style visual vertex handle height fallback and bounds.
// ABOUTME: Keeps visual-mode vertex handle behavior testable without renderer state.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualVertexHandleTests
{
    [Fact]
    public void PairCreatesDistinctFloorAndCeilingHandles()
    {
        var (map, vertex, _, _) = TwoSectorSharedVertexMap();

        VisualVertexHandlePair pair = VisualVertexHandles.CreatePair(map, vertex);

        Assert.Equal(VisualVertexHandleKind.Floor, pair.FloorVertex.Kind);
        Assert.Equal(VisualVertexHandleKind.Ceiling, pair.CeilingVertex.Kind);
        Assert.False(pair.FloorVertex.CeilingVertex);
        Assert.True(pair.CeilingVertex.CeilingVertex);
        Assert.Equal(new[] { pair.FloorVertex, pair.CeilingVertex }, pair.Vertices);
    }

    [Fact]
    public void VisiblePairsRequireUdmfVertexHeightSupportAndToggleLikeUdb()
    {
        var (map, _, _, _) = TwoSectorSharedVertexMap();

        Assert.Empty(VisualVertexHandles.CreateVisiblePairs(map, isUdmf: false, vertexHeightSupport: true, showVisualVertices: true));
        Assert.Empty(VisualVertexHandles.CreateVisiblePairs(map, isUdmf: true, vertexHeightSupport: false, showVisualVertices: true));
        Assert.Empty(VisualVertexHandles.CreateVisiblePairs(map, isUdmf: true, vertexHeightSupport: true, showVisualVertices: false));

        IReadOnlyList<VisualVertexHandlePair> pairs = VisualVertexHandles.CreateVisiblePairs(
            map,
            isUdmf: true,
            vertexHeightSupport: true,
            showVisualVertices: true);

        Assert.Equal(map.Vertices.Count, pairs.Count);
        Assert.All(pairs, pair =>
        {
            Assert.Equal(VisualVertexHandleKind.Floor, pair.FloorVertex.Kind);
            Assert.Equal(VisualVertexHandleKind.Ceiling, pair.CeilingVertex.Kind);
        });
    }

    [Fact]
    public void FloorFallbackUsesHighestAdjacentSectorFloor()
    {
        var (map, vertex, low, high) = TwoSectorSharedVertexMap();
        low.FloorHeight = -16;
        high.FloorHeight = 24;

        VisualVertexHandle handle = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Floor);

        Assert.False(handle.HaveHeightOffset);
        Assert.Equal(24, handle.Position.z);
    }

    [Fact]
    public void CeilingFallbackUsesLowestAdjacentSectorCeiling()
    {
        var (map, vertex, low, high) = TwoSectorSharedVertexMap();
        low.CeilHeight = 96;
        high.CeilHeight = 160;

        VisualVertexHandle handle = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Ceiling);

        Assert.False(handle.HaveHeightOffset);
        Assert.Equal(96, handle.Position.z);
    }

    [Fact]
    public void ExplicitVertexHeightsOverrideSectorFallback()
    {
        var (map, vertex, _, _) = TwoSectorSharedVertexMap();
        vertex.ZFloor = 12.5;
        vertex.ZCeiling = 88.25;

        VisualVertexHandle floor = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Floor);
        VisualVertexHandle ceiling = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Ceiling);

        Assert.True(floor.HaveHeightOffset);
        Assert.True(ceiling.HaveHeightOffset);
        Assert.Equal(12.5, floor.Position.z);
        Assert.Equal(88.25, ceiling.Position.z);
    }

    [Fact]
    public void BoundsMatchUdbFloorAndCeilingHandleShape()
    {
        var map = new MapSet();
        Vertex vertex = map.AddVertex(new Vector2D(10, 20));
        vertex.ZFloor = 30;
        vertex.ZCeiling = 90;

        VisualVertexHandle floor = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Floor, scale: 2.0);
        VisualVertexHandle ceiling = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Ceiling, scale: 2.0);

        Assert.Equal(new Vector3D(-2, 8, 30), floor.BoundsMin);
        Assert.Equal(new Vector3D(22, 32, 42), floor.BoundsMax);
        Assert.Equal(new Vector3D(-2, 8, 78), ceiling.BoundsMin);
        Assert.Equal(new Vector3D(22, 32, 90), ceiling.BoundsMax);
    }

    [Fact]
    public void IsolatedVertexFallsBackToZeroHeight()
    {
        var map = new MapSet();
        Vertex vertex = map.AddVertex(new Vector2D(0, 0));

        VisualVertexHandle floor = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Floor);
        VisualVertexHandle ceiling = VisualVertexHandles.Create(map, vertex, VisualVertexHandleKind.Ceiling);

        Assert.Equal(0, floor.Position.z);
        Assert.Equal(0, ceiling.Position.z);
    }

    private static (MapSet Map, Vertex Shared, Sector Low, Sector High) TwoSectorSharedVertexMap()
    {
        var map = new MapSet();
        Sector low = map.AddSector();
        Sector high = map.AddSector();

        Vertex shared = map.AddVertex(new Vector2D(0, 0));
        Vertex a = map.AddVertex(new Vector2D(64, 0));
        Vertex b = map.AddVertex(new Vector2D(0, 64));
        Vertex c = map.AddVertex(new Vector2D(-64, 0));
        Vertex d = map.AddVertex(new Vector2D(0, -64));

        Linedef ab = map.AddLinedef(shared, a);
        Linedef ba = map.AddLinedef(a, b);
        Linedef bs = map.AddLinedef(b, shared);
        map.AddSidedef(ab, true, low);
        map.AddSidedef(ba, true, low);
        map.AddSidedef(bs, true, low);

        Linedef cd = map.AddLinedef(shared, c);
        Linedef dc = map.AddLinedef(c, d);
        Linedef ds = map.AddLinedef(d, shared);
        map.AddSidedef(cd, true, high);
        map.AddSidedef(dc, true, high);
        map.AddSidedef(ds, true, high);
        map.BuildIndexes();

        return (map, shared, low, high);
    }
}
