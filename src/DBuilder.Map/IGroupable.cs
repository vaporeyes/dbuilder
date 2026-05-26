// ABOUTME: Interface for map elements that carry UDB-style transient selection group membership.
// ABOUTME: Groups are stored as a bitmask so editor code can select or clear multiple groups efficiently.

namespace DBuilder.Map;

public interface IGroupable : ISelectable
{
    int Groups { get; set; }
}
