// ABOUTME: Merge-geometry mode selection ported from UDB's Map.MergeGeometryMode.
// ABOUTME: Controls how stitched geometry interacts with existing sectors.

namespace DBuilder.Map;

public enum MergeGeometryMode
{
    Classic,
    Merge,
    Replace,
}
