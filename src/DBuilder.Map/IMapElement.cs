// ABOUTME: Interface for map elements that expose lifecycle state after removal from a MapSet.
// ABOUTME: Lets editor code detect stale element handles without coupling to a concrete element type.

namespace DBuilder.Map;

public interface IMapElement
{
    bool IsDisposed { get; set; }
}
