// ABOUTME: Defines the UDB-style selection category flags used by MapSet conversion helpers.
// ABOUTME: Supports converting between selected vertices, linedefs, sectors, things, and all geometry.

namespace DBuilder.Map;

[Flags]
public enum SelectionType
{
    None = 0,
    Vertices = 1,
    Linedefs = 2,
    Sectors = 4,
    Things = 8,
    All = 0x7FFFFFFF,
}
