// ABOUTME: Reports how many selected map elements were considered and moved by snap-to-grid operations.
// ABOUTME: Lets editor code show UDB-style status messages without duplicating map selection logic.

namespace DBuilder.Map;

public readonly record struct SnapSelectionResult(
    int VertexTargets,
    int ThingTargets,
    int SnappedVertices,
    int SnappedThings)
{
    public int TargetCount => VertexTargets + ThingTargets;
    public int SnappedCount => SnappedVertices + SnappedThings;
}
