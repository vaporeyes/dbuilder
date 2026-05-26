// ABOUTME: Interface for map elements that carry a transient editor mark flag.
// ABOUTME: Lets MapSet query and clear algorithm scratch marks generically across element types.

namespace DBuilder.Map;

public interface IMarkable
{
    bool Marked { get; set; }
}
