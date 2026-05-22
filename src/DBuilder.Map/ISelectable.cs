// ABOUTME: Interface for map elements that carry a transient editor selection flag.
// ABOUTME: Lets MapSet query/clear selection generically across vertices, linedefs, sidedefs, sectors and things.

namespace DBuilder.Map;

public interface ISelectable
{
    bool Selected { get; set; }
}
