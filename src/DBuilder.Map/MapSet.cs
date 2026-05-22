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

    // ============================================================
    // Mutation operations.
    // ============================================================
    // These maintain the primary lists and direct references (Linedef.Front/Back/Start/End, Sidedef.Sector).
    // Derived state (Vertex.Linedefs, Sector.Sidedefs, Sidedef.Other) is NOT updated incrementally - call
    // BuildIndexes() once after a batch of edits before triangulating or rendering. Removals scan the primary
    // lists rather than the derived back-references so they stay correct even when indexes are stale.

    public Vertex AddVertex(Vector2D position)
    {
        var v = new Vertex(position);
        Vertices.Add(v);
        return v;
    }

    public Linedef AddLinedef(Vertex start, Vertex end)
    {
        var l = new Linedef(start, end);
        Linedefs.Add(l);
        return l;
    }

    public Sidedef AddSidedef(Linedef line, bool isFront, Sector? sector)
    {
        var sd = new Sidedef(line, isFront) { Sector = sector };
        if (isFront) line.Front = sd; else line.Back = sd;
        Sidedefs.Add(sd);
        return sd;
    }

    public Sector AddSector()
    {
        var s = new Sector { Index = Sectors.Count };
        Sectors.Add(s);
        return s;
    }

    public Thing AddThing(Vector2D position, int type)
    {
        var t = new Thing(position, type);
        Things.Add(t);
        return t;
    }

    /// <summary>Removes a sidedef and detaches it from its owning linedef.</summary>
    public void RemoveSidedef(Sidedef sd)
    {
        if (sd.Line != null)
        {
            if (ReferenceEquals(sd.Line.Front, sd)) sd.Line.Front = null;
            if (ReferenceEquals(sd.Line.Back, sd)) sd.Line.Back = null;
        }
        Sidedefs.Remove(sd);
    }

    /// <summary>Removes a linedef along with its front and back sidedefs.</summary>
    public void RemoveLinedef(Linedef l)
    {
        if (l.Front != null) Sidedefs.Remove(l.Front);
        if (l.Back != null) Sidedefs.Remove(l.Back);
        l.Front = null;
        l.Back = null;
        Linedefs.Remove(l);
    }

    /// <summary>Removes a vertex and every linedef touching it (which in turn removes those linedefs' sidedefs).</summary>
    public void RemoveVertex(Vertex v)
    {
        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var l = Linedefs[i];
            if (ReferenceEquals(l.Start, v) || ReferenceEquals(l.End, v)) RemoveLinedef(l);
        }
        Vertices.Remove(v);
    }

    /// <summary>Removes a sector; sidedefs that referenced it are left in place with a null sector reference.</summary>
    public void RemoveSector(Sector s)
    {
        foreach (var sd in Sidedefs)
            if (ReferenceEquals(sd.Sector, s)) sd.Sector = null;
        Sectors.Remove(s);
    }

    public void RemoveThing(Thing t) => Things.Remove(t);

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
