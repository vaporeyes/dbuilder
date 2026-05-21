// ABOUTME: Skeleton of UDB's Map.Sidedef expanded with binary-record fields needed for map I/O.
// ABOUTME: Still omits the full UDB surface (marks, selection, sector ownership logic).

namespace DBuilder.Map;

public class Sidedef
{
    public Linedef Line { get; set; } = null!;
    public Sector? Sector { get; set; }
    public bool IsFront { get; set; }

    // Binary record fields.
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string HighTexture { get; set; } = "-";
    public string MidTexture { get; set; } = "-";
    public string LowTexture { get; set; } = "-";

    public Sidedef() { }
    public Sidedef(Linedef line, bool isFront)
    {
        Line = line;
        IsFront = isFront;
    }
}
