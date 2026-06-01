// ABOUTME: Builds UDB-style visual-mode culling plans from projected frustum blockmap queries.
// ABOUTME: Keeps visible linedef and thing deduplication testable outside the editor renderer.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record VisualCullingPlan(
    IReadOnlyList<BlockMapCell> Blocks,
    IReadOnlyList<Linedef> Linedefs,
    IReadOnlyList<Thing> Things);

public static class VisualCulling
{
    public static VisualCullingPlan BuildPlan(
        BlockMap blockMap,
        ProjectedFrustum2D frustum,
        bool includeGeometry = true,
        bool includeThings = true,
        Func<Thing, bool>? thingFilter = null)
    {
        ArgumentNullException.ThrowIfNull(blockMap);
        ArgumentNullException.ThrowIfNull(frustum);

        var blocks = blockMap.GetFrustumBlocks(frustum);
        var linedefs = new List<Linedef>();
        var things = new List<Thing>();
        var seenLines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        var seenThings = new HashSet<Thing>(ReferenceEqualityComparer.Instance);

        foreach (BlockMapCell block in blocks)
        {
            if (includeGeometry)
            {
                foreach (Linedef line in block.Lines)
                {
                    if (seenLines.Add(line))
                        linedefs.Add(line);
                }
            }

            if (includeThings)
            {
                foreach (Thing thing in block.Things)
                {
                    if (!seenThings.Add(thing)) continue;
                    if (thingFilter != null && !thingFilter(thing)) continue;
                    things.Add(thing);
                }
            }
        }

        return new VisualCullingPlan(blocks, linedefs, things);
    }
}
