// ABOUTME: In-memory map container - simplified subset of UDB's MapSet.
// ABOUTME: Holds the five core map element lists; bbox helper for fitting to a viewport.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class MapSet
{
    public List<Vertex> Vertices { get; } = new();
    public List<Linedef> Linedefs { get; } = new();
    public List<Sidedef> Sidedefs { get; } = new();
    public List<Sector> Sectors { get; } = new();
    public List<Thing> Things { get; } = new();

    // UDMF namespace (e.g. "ZDoomTranslated", "Doom").
    public string Namespace { get; set; } = "";

    /// <summary>
    /// Populates the topology back-references used by Triangulation: Vertex.Linedefs, Sidedef.Other,
    /// Sector.Sidedefs. Call this once after loading; idempotent (clears existing entries first).
    /// </summary>
    public void BuildIndexes()
    {
        foreach (var v in Vertices) v.Linedefs.Clear();
        foreach (var s in Sectors)  s.Sidedefs.Clear();
        foreach (var sd in Sidedefs) sd.Other = null;

        foreach (var line in Linedefs)
        {
            if (line.Start != null) line.Start.Linedefs.Add(line);
            // Avoid double-adding if Start == End (degenerate but observed in the wild).
            if (line.End != null && !object.ReferenceEquals(line.End, line.Start)) line.End.Linedefs.Add(line);

            if (line.Front != null)
            {
                if (line.Front.Sector != null) line.Front.Sector.Sidedefs.Add(line.Front);
                line.Front.Other = line.Back;
            }
            if (line.Back != null)
            {
                if (line.Back.Sector != null) line.Back.Sector.Sidedefs.Add(line.Back);
                line.Back.Other = line.Front;
            }
        }
    }

    /// <summary>Axis-aligned bounding box of the vertex set; (0,0,0,0) when empty.</summary>
    public (double minX, double minY, double maxX, double maxY) Bounds()
    {
        if (Vertices.Count == 0) return (0, 0, 0, 0);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var v in Vertices)
        {
            if (v.Position.x < minX) minX = v.Position.x;
            if (v.Position.y < minY) minY = v.Position.y;
            if (v.Position.x > maxX) maxX = v.Position.x;
            if (v.Position.y > maxY) maxY = v.Position.y;
        }
        return (minX, minY, maxX, maxY);
    }
}
