// ABOUTME: Skeleton of UDB's Map.Sector with binary-record fields + Sidedefs back-ref used by triangulation.
// ABOUTME: Still omits the full UDB surface (marks, selection, slopes, 3D floors, BSP linkage).

namespace DBuilder.Map;

public class Sector
{
    public int Index { get; set; }

    public int FloorHeight { get; set; }
    public int CeilHeight { get; set; }
    public string FloorTexture { get; set; } = "-";
    public string CeilTexture { get; set; } = "-";
    public int Brightness { get; set; } = 160;
    public int Special { get; set; }
    public int Tag { get; set; }

    /// <summary>All sidedefs belonging to this sector. Populated by MapSet.BuildIndexes().</summary>
    public List<Sidedef> Sidedefs { get; } = new();
}
