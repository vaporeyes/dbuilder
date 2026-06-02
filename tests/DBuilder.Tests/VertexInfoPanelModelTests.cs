// ABOUTME: Verifies structured vertex info rows used by the editor info panel.
// ABOUTME: Covers topology counts, selection groups, vertex heights, and custom field counts.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VertexInfoPanelModelTests
{
    [Fact]
    public void BuildsVertexInfoRows()
    {
        var map = new MapSet();
        Vertex vertex = map.AddVertex(new Vector2D(12.125, -8.5));
        Vertex end = map.AddVertex(new Vector2D(64, -8.5));
        map.AddLinedef(vertex, end);
        vertex.Groups = MapSet.GroupMask(0) | MapSet.GroupMask(2);
        vertex.ZFloor = 16.25;
        vertex.ZCeiling = 96.5;
        vertex.Fields["comment"] = "slope pivot";
        map.BuildIndexes();

        VertexInfoPanelState state = VertexInfoPanelModel.Build(map, vertex);

        Assert.Equal("Vertex 0", state.Header);
        Assert.Equal("(12.125, -8.5)", Field(state, "Position"));
        Assert.Equal("1", Field(state, "Linedefs"));
        Assert.Equal("1, 3", Field(state, "Groups"));
        Assert.Equal("16.25", Field(state, "Z floor"));
        Assert.Equal("96.5", Field(state, "Z ceiling"));
        Assert.Equal("1", Field(state, "Custom fields"));
    }

    [Fact]
    public void BuildsDetachedVertexInfoRowsWithFallbacks()
    {
        var map = new MapSet();
        var vertex = new Vertex(new Vector2D(1, 2));

        VertexInfoPanelState state = VertexInfoPanelModel.Build(map, vertex);

        Assert.Equal("Vertex", state.Header);
        Assert.Equal("(1, 2)", Field(state, "Position"));
        Assert.Equal("0", Field(state, "Linedefs"));
        Assert.Equal("-", Field(state, "Groups"));
        Assert.Equal("-", Field(state, "Z floor"));
        Assert.Equal("-", Field(state, "Z ceiling"));
        Assert.Equal("0", Field(state, "Custom fields"));
    }

    private static string Field(VertexInfoPanelState state, string label)
        => Assert.Single(state.Fields, field => field.Label == label).Value;
}
