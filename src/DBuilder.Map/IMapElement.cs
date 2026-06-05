// ABOUTME: Interface for map elements that expose lifecycle state after removal from a MapSet.
// ABOUTME: Lets editor code detect stale element handles without coupling to a concrete element type.

namespace DBuilder.Map;

public interface IMapElement
{
    int Index { get; set; }
    bool IsDisposed { get; set; }
    HashSet<MapIssueKind> IgnoredErrorChecks { get; }
}
