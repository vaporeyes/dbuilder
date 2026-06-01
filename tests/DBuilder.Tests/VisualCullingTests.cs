// ABOUTME: Covers UDB-style visual-mode culling over blockmap frustum ranges.
// ABOUTME: Verifies visible linedef and thing deduplication without depending on renderer state.

using System.Drawing;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualCullingTests
{
    [Fact]
    public void BuildPlanDeduplicatesLinedefsAndThingsAcrossVisibleBlocks()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(-32, -96)), map.AddVertex(new Vector2D(32, -32)));
        var thing = map.AddThing(new Vector2D(0, -64), 3001);
        thing.Size = 80;
        map.BuildIndexes();
        var blockMap = new BlockMap(new RectangleF(-128, -128, 256, 256), 64);
        blockMap.AddLinedef(line);
        blockMap.AddThing(thing);
        var frustum = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 8, far: 160, fov: (float)(Math.PI / 2));

        VisualCullingPlan plan = VisualCulling.BuildPlan(blockMap, frustum);

        Assert.True(plan.Blocks.Count > 1);
        Assert.Equal(new[] { line }, plan.Linedefs);
        Assert.Equal(new[] { thing }, plan.Things);
    }

    [Fact]
    public void BuildPlanHonorsGeometryThingTogglesAndThingFilter()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(-16, -80)), map.AddVertex(new Vector2D(16, -80)));
        var visibleThing = map.AddThing(new Vector2D(-16, -64), 1);
        var hiddenThing = map.AddThing(new Vector2D(16, -64), 2);
        map.BuildIndexes();
        var blockMap = new BlockMap(new RectangleF(-128, -128, 256, 256), 64);
        blockMap.AddLinedef(line);
        blockMap.AddThings(new[] { visibleThing, hiddenThing });
        var frustum = new ProjectedFrustum2D(new Vector2D(0, 0), xyangle: 0, zangle: 0, near: 8, far: 160, fov: (float)(Math.PI / 2));

        VisualCullingPlan thingsOnly = VisualCulling.BuildPlan(
            blockMap,
            frustum,
            includeGeometry: false,
            includeThings: true,
            thingFilter: thing => thing.Type == 1);
        VisualCullingPlan geometryOnly = VisualCulling.BuildPlan(blockMap, frustum, includeGeometry: true, includeThings: false);

        Assert.Empty(thingsOnly.Linedefs);
        Assert.Equal(new[] { visibleThing }, thingsOnly.Things);
        Assert.Equal(new[] { line }, geometryOnly.Linedefs);
        Assert.Empty(geometryOnly.Things);
    }
}
