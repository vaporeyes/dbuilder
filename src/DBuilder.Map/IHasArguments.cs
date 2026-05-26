// ABOUTME: Interface for map elements that carry five Hexen or UDMF action arguments.
// ABOUTME: Lets callers use bounded argument helpers for linedefs and things.

namespace DBuilder.Map;

public interface IHasArguments
{
    int[] Args { get; }
}
