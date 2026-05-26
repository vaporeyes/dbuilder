// ABOUTME: Interface for map elements that own custom UDMF fields.
// ABOUTME: Enables typed field access helpers across vertices, linedefs, sidedefs, sectors and things.

namespace DBuilder.Map;

public interface IFielded
{
    Dictionary<string, object> Fields { get; }
}
