// ABOUTME: Skeleton of UDB's Map.Sidedef sufficient for the geometry port.
// ABOUTME: Real Sidedef (offsets, textures, flags, sector ownership) follows in the Map port.

namespace DBuilder.Map;

public class Sidedef
{
    public Linedef Line { get; set; } = null!;
    public Sector? Sector { get; set; }
    public bool IsFront { get; set; }

    public Sidedef() { }
    public Sidedef(Linedef line, bool isFront)
    {
        Line = line;
        IsFront = isFront;
    }
}
