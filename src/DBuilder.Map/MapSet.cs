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

    /// <summary>
    /// Splits a linedef at <paramref name="pos"/>: shortens it to start..newVertex and adds a second linedef
    /// newVertex..oldEnd that copies the original's flags/action/tags/args/sidedefs (same sectors/textures).
    /// Returns the inserted vertex. Call BuildIndexes() afterward. Front-side X offset is advanced by the first
    /// half's length to keep front textures continuous (back-side and pegging adjustments are a later refinement).
    /// </summary>
    public Vertex SplitLinedef(Linedef l, Vector2D pos)
    {
        var v = AddVertex(pos);
        SplitLinedefAt(l, v);
        return v;
    }

    /// <summary>Splits a linedef at an existing vertex; returns the new (second-half) linedef.</summary>
    public Linedef SplitLinedefAt(Linedef l, Vertex v)
    {
        double firstHalfLen = (v.Position - l.Start.Position).GetLength();
        var oldEnd = l.End;

        var newLine = new Linedef(v, oldEnd) { Flags = l.Flags, Action = l.Action };
        newLine.Tags.Clear();
        newLine.Tags.AddRange(l.Tags);
        for (int i = 0; i < newLine.Args.Length; i++) newLine.Args[i] = l.Args[i];
        foreach (var f in l.UdmfFlags) newLine.UdmfFlags.Add(f);
        foreach (var kv in l.Fields) newLine.Fields[kv.Key] = kv.Value;
        Linedefs.Add(newLine);

        // Shorten the original to start..v and refresh both angle caches.
        l.End = v;
        l.Angle = Linedef.ComputeAngle(l.Start, v);
        newLine.Angle = Linedef.ComputeAngle(v, oldEnd);

        if (l.Front != null)
        {
            var nf = AddSidedef(newLine, true, l.Front.Sector);
            CopySidedefProperties(l.Front, nf);
            nf.OffsetX += (int)System.Math.Round(firstHalfLen);
        }
        if (l.Back != null)
        {
            var nb = AddSidedef(newLine, false, l.Back.Sector);
            CopySidedefProperties(l.Back, nb);
        }
        return newLine;
    }

    private static void CopySidedefProperties(Sidedef src, Sidedef dst)
    {
        dst.OffsetX = src.OffsetX;
        dst.OffsetY = src.OffsetY;
        dst.HighTexture = src.HighTexture;
        dst.MidTexture = src.MidTexture;
        dst.LowTexture = src.LowTexture;
        foreach (var kv in src.Fields) dst.Fields[kv.Key] = kv.Value;
    }

    // ============================================================
    // Selection.
    // ============================================================
    // Selection lives on each element's transient Selected flag (not serialized). These helpers query and
    // clear it. Selection is reset by undo/redo since snapshots restore fresh element instances.

    public List<Vertex> GetSelectedVertices() => Filter(Vertices);
    public List<Linedef> GetSelectedLinedefs() => Filter(Linedefs);
    public List<Sidedef> GetSelectedSidedefs() => Filter(Sidedefs);
    public List<Sector> GetSelectedSectors() => Filter(Sectors);
    public List<Thing> GetSelectedThings() => Filter(Things);

    public int SelectedVerticesCount => Count(Vertices);
    public int SelectedLinedefsCount => Count(Linedefs);
    public int SelectedSectorsCount => Count(Sectors);
    public int SelectedThingsCount => Count(Things);

    public void ClearSelectedVertices() { foreach (var v in Vertices) v.Selected = false; }
    public void ClearSelectedLinedefs() { foreach (var l in Linedefs) l.Selected = false; }
    public void ClearSelectedSidedefs() { foreach (var s in Sidedefs) s.Selected = false; }
    public void ClearSelectedSectors() { foreach (var s in Sectors) s.Selected = false; }
    public void ClearSelectedThings() { foreach (var t in Things) t.Selected = false; }

    /// <summary>Clears the Selected flag on every element of every type.</summary>
    public void ClearAllSelected()
    {
        ClearSelectedVertices();
        ClearSelectedLinedefs();
        ClearSelectedSidedefs();
        ClearSelectedSectors();
        ClearSelectedThings();
    }

    // ============================================================
    // Selection-driven edit operations.
    // ============================================================
    // These act on whatever is currently selected. Callers bracket them with UndoManager.CreateUndo and
    // call BuildIndexes() afterward (deletions change topology). Offsets move geometry in place.

    /// <summary>Moves every selected vertex by <paramref name="delta"/>. Returns the number moved.</summary>
    public int MoveSelectedVerticesBy(Vector2D delta)
    {
        int n = 0;
        foreach (var v in Vertices)
            if (v.Selected) { v.Position += delta; n++; }
        return n;
    }

    /// <summary>Moves every selected thing by <paramref name="delta"/>. Returns the number moved.</summary>
    public int MoveSelectedThingsBy(Vector2D delta)
    {
        int n = 0;
        foreach (var t in Things)
            if (t.Selected) { t.Position += delta; n++; }
        return n;
    }

    /// <summary>
    /// Deletes all currently selected elements in dependency-safe order: things, then sectors (orphaning
    /// their sidedefs), then linedefs (with their sidedefs), then vertices (cascading any remaining lines).
    /// Returns the total number of primary elements removed. Call BuildIndexes() afterward.
    /// </summary>
    public int DeleteSelection()
    {
        int removed = 0;

        for (int i = Things.Count - 1; i >= 0; i--)
            if (Things[i].Selected) { Things.RemoveAt(i); removed++; }

        for (int i = Sectors.Count - 1; i >= 0; i--)
            if (Sectors[i].Selected) { RemoveSector(Sectors[i]); removed++; }

        for (int i = Linedefs.Count - 1; i >= 0; i--)
            if (Linedefs[i].Selected) { RemoveLinedef(Linedefs[i]); removed++; }

        for (int i = Vertices.Count - 1; i >= 0; i--)
            if (Vertices[i].Selected) { RemoveVertex(Vertices[i]); removed++; }

        return removed;
    }

    private static List<T> Filter<T>(List<T> items) where T : ISelectable
    {
        var result = new List<T>();
        foreach (var it in items)
            if (it.Selected) result.Add(it);
        return result;
    }

    private static int Count<T>(List<T> items) where T : ISelectable
    {
        int n = 0;
        foreach (var it in items)
            if (it.Selected) n++;
        return n;
    }

    // ============================================================
    // Spatial queries / hit-testing.
    // ============================================================
    // Foundations for cursor picking and snapping. All distances are in map units. These scan the primary
    // lists linearly (O(n)); a blockmap acceleration structure can replace the scans later if needed.

    /// <summary>Nearest vertex to <paramref name="pos"/> within <paramref name="maxRange"/> units, or null if none.</summary>
    public Vertex? NearestVertex(Vector2D pos, double maxRange = double.MaxValue)
    {
        Vertex? closest = null;
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        foreach (var v in Vertices)
        {
            double dx = v.Position.x - pos.x;
            double dy = v.Position.y - pos.y;
            double d = dx * dx + dy * dy;
            if (d < bestSq) { bestSq = d; closest = v; }
        }
        return closest;
    }

    /// <summary>Nearest thing to <paramref name="pos"/> within <paramref name="maxRange"/> units, or null if none.</summary>
    public Thing? NearestThing(Vector2D pos, double maxRange = double.MaxValue)
    {
        Thing? closest = null;
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        foreach (var t in Things)
        {
            double dx = t.Position.x - pos.x;
            double dy = t.Position.y - pos.y;
            double d = dx * dx + dy * dy;
            if (d < bestSq) { bestSq = d; closest = t; }
        }
        return closest;
    }

    /// <summary>Nearest linedef to <paramref name="pos"/> (bounded segment distance) within <paramref name="maxRange"/>, or null.</summary>
    public Linedef? NearestLinedef(Vector2D pos, double maxRange = double.MaxValue)
    {
        Linedef? closest = null;
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        foreach (var l in Linedefs)
        {
            double d = LinedefDistanceSq(l, pos);
            if (d < bestSq) { bestSq = d; closest = l; }
        }
        return closest;
    }

    /// <summary>
    /// Nearest sidedef to <paramref name="pos"/>: finds the nearest linedef, then returns the sidedef on the
    /// side of that line where the point lies (front when the point is on the right of start->end).
    /// </summary>
    public Sidedef? NearestSidedef(Vector2D pos, double maxRange = double.MaxValue)
    {
        var line = NearestLinedef(pos, maxRange);
        if (line == null) return null;
        // GetSideOfLine < 0 means the point is on the front (right) side for start->end winding.
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) < 0;
        return front ? (line.Front ?? line.Back) : (line.Back ?? line.Front);
    }

    /// <summary>
    /// Returns the sector containing <paramref name="pos"/>, determined via the nearest linedef's facing side.
    /// Assumes well-formed, closed sectors. Returns null when there are no linedefs or the facing side has no sector.
    /// </summary>
    public Sector? GetSectorAt(Vector2D pos)
    {
        var line = NearestLinedef(pos);
        if (line == null) return null;
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) < 0;
        // The facing side determines the sector. A null facing side means the point is in the void
        // (outside a one-sided wall), so return null rather than the wall's own sector.
        var side = front ? line.Front : line.Back;
        return side?.Sector;
    }

    // Bounded point-to-segment squared distance, guarding against zero-length (degenerate) linedefs.
    private static double LinedefDistanceSq(Linedef l, Vector2D pos)
    {
        var a = l.Start.Position;
        var b = l.End.Position;
        double lenSq = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (lenSq < 1e-12)
        {
            double dx0 = pos.x - a.x, dy0 = pos.y - a.y;
            return dx0 * dx0 + dy0 * dy0;
        }
        return Line2D.GetDistanceToLineSq(a, b, pos, bounded: true);
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
