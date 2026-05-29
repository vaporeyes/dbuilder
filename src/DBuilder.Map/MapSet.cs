// ABOUTME: In-memory map container - simplified subset of UDB's MapSet.
// ABOUTME: Holds the five core map element lists; bbox helper for fitting to a viewport.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class MapSet : IDisposable
{
    public List<Vertex> Vertices { get; } = new();
    public List<Linedef> Linedefs { get; } = new();
    public List<Sidedef> Sidedefs { get; } = new();
    public List<Sector> Sectors { get; } = new();
    public List<Thing> Things { get; } = new();

    public bool IsDisposed { get; private set; }

    // UDMF namespace (e.g. "ZDoomTranslated", "Doom").
    public string Namespace { get; set; } = "";

    /// <summary>Top-level custom UDMF map metadata fields preserved across load/write and editor snapshots.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    /// <summary>Top-level UDMF blocks that are not part of the core map element set.</summary>
    public List<UnknownUdmfEntry> UnknownUdmfData { get; } = new();

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

    public void Dispose()
    {
        if (IsDisposed) return;

        foreach (var thing in Things) DisposeElement(thing);
        foreach (var sector in Sectors) DisposeElement(sector);
        foreach (var side in Sidedefs) DisposeElement(side);
        foreach (var line in Linedefs) DisposeElement(line);
        foreach (var vertex in Vertices) DisposeElement(vertex);

        Things.Clear();
        Sectors.Clear();
        Sidedefs.Clear();
        Linedefs.Clear();
        Vertices.Clear();
        Fields.Clear();
        UnknownUdmfData.Clear();
        Namespace = "";
        IsDisposed = true;
    }

    /// <summary>Creates a deep copy of this map, preserving element data, transient flags and references.</summary>
    public MapSet Clone()
    {
        var clone = new MapSet { Namespace = Namespace };
        foreach (var kv in Fields) clone.Fields[kv.Key] = kv.Value;
        foreach (var entry in UnknownUdmfData) clone.UnknownUdmfData.Add(entry.Clone());
        var vertexMap = new Dictionary<Vertex, Vertex>(ReferenceEqualityComparer.Instance);
        var sectorMap = new Dictionary<Sector, Sector>(ReferenceEqualityComparer.Instance);
        var linedefMap = new Dictionary<Linedef, Linedef>(ReferenceEqualityComparer.Instance);
        var sidedefMap = new Dictionary<Sidedef, Sidedef>(ReferenceEqualityComparer.Instance);

        foreach (var vertex in Vertices)
        {
            var copy = new Vertex(vertex.Position)
            {
                Selected = vertex.Selected,
                Marked = vertex.Marked,
                Groups = vertex.Groups,
                ZCeiling = vertex.ZCeiling,
                ZFloor = vertex.ZFloor,
            };
            CopyFields(vertex, copy);
            clone.Vertices.Add(copy);
            vertexMap[vertex] = copy;
        }

        foreach (var sector in Sectors)
        {
            var copy = new Sector
            {
                Index = sector.Index,
                Selected = sector.Selected,
                Marked = sector.Marked,
                Groups = sector.Groups,
                FloorHeight = sector.FloorHeight,
                CeilHeight = sector.CeilHeight,
                FloorTexture = sector.FloorTexture,
                CeilTexture = sector.CeilTexture,
                Brightness = sector.Brightness,
                Special = sector.Special,
                FloorSlope = sector.FloorSlope,
                FloorSlopeOffset = sector.FloorSlopeOffset,
                CeilSlope = sector.CeilSlope,
                CeilSlopeOffset = sector.CeilSlopeOffset,
            };
            copy.Tags.AddRange(sector.Tags);
            CopyFields(sector, copy);
            clone.Sectors.Add(copy);
            sectorMap[sector] = copy;
        }

        foreach (var line in Linedefs)
        {
            var copy = new Linedef(vertexMap[line.Start], vertexMap[line.End])
            {
                Selected = line.Selected,
                Marked = line.Marked,
                Groups = line.Groups,
                Flags = line.Flags,
                Action = line.Action,
            };
            copy.Tags.AddRange(line.Tags);
            CopyArgs(line, copy);
            foreach (var flag in line.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(line, copy);
            clone.Linedefs.Add(copy);
            linedefMap[line] = copy;
        }

        foreach (var side in Sidedefs)
        {
            var copy = new Sidedef(linedefMap[side.Line], side.IsFront)
            {
                Sector = side.Sector == null ? null : sectorMap[side.Sector],
                Selected = side.Selected,
                Marked = side.Marked,
                OffsetX = side.OffsetX,
                OffsetY = side.OffsetY,
                HighTexture = side.HighTexture,
                MidTexture = side.MidTexture,
                LowTexture = side.LowTexture,
            };
            foreach (var flag in side.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(side, copy);
            clone.Sidedefs.Add(copy);
            sidedefMap[side] = copy;
        }

        foreach (var line in Linedefs)
        {
            var copy = linedefMap[line];
            copy.Front = line.Front == null ? null : sidedefMap[line.Front];
            copy.Back = line.Back == null ? null : sidedefMap[line.Back];
        }

        foreach (var thing in Things)
        {
            var copy = new Thing(thing.Position, thing.Type, thing.Angle)
            {
                Selected = thing.Selected,
                Marked = thing.Marked,
                Groups = thing.Groups,
                Height = thing.Height,
                Pitch = thing.Pitch,
                Roll = thing.Roll,
                ScaleX = thing.ScaleX,
                ScaleY = thing.ScaleY,
                Flags = thing.Flags,
                Tag = thing.Tag,
                Action = thing.Action,
            };
            CopyArgs(thing, copy);
            foreach (var flag in thing.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(thing, copy);
            clone.Things.Add(copy);
        }

        clone.BuildIndexes();
        return clone;
    }

    /// <summary>Removes a sidedef and detaches it from its owning linedef.</summary>
    public void RemoveSidedef(Sidedef sd)
    {
        if (sd.Line != null)
        {
            if (ReferenceEquals(sd.Line.Front, sd)) sd.Line.Front = null;
            if (ReferenceEquals(sd.Line.Back, sd)) sd.Line.Back = null;
        }
        if (Sidedefs.Remove(sd)) DisposeElement(sd);
    }

    /// <summary>Removes a linedef along with its front and back sidedefs.</summary>
    public void RemoveLinedef(Linedef l)
    {
        if (l.Front != null)
        {
            if (Sidedefs.Remove(l.Front)) DisposeElement(l.Front);
        }
        if (l.Back != null)
        {
            if (Sidedefs.Remove(l.Back)) DisposeElement(l.Back);
        }
        l.Front = null;
        l.Back = null;
        if (Linedefs.Remove(l)) DisposeElement(l);
    }

    /// <summary>Removes a vertex and every linedef touching it (which in turn removes those linedefs' sidedefs).</summary>
    public void RemoveVertex(Vertex v)
    {
        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var l = Linedefs[i];
            if (ReferenceEquals(l.Start, v) || ReferenceEquals(l.End, v)) RemoveLinedef(l);
        }
        if (Vertices.Remove(v)) DisposeElement(v);
    }

    /// <summary>Removes a sector; sidedefs that referenced it are left in place with a null sector reference.</summary>
    public void RemoveSector(Sector s)
    {
        foreach (var sd in Sidedefs)
            if (ReferenceEquals(sd.Sector, s)) sd.Sector = null;
        if (Sectors.Remove(s)) DisposeElement(s);
    }

    public void RemoveThing(Thing t)
    {
        if (Things.Remove(t)) DisposeElement(t);
    }

    /// <summary>
    /// Repairs invalid cross-list references after low-level edits: removes linedefs whose vertices are no longer
    /// in the map, drops sidedefs not owned by their linedef, clears sidedef sector references to removed sectors,
    /// and removes front/back references to sidedefs not in the map. Returns the number of repairs made.
    /// </summary>
    public int RepairReferences()
    {
        int repairs = 0;
        var vertices = new HashSet<Vertex>(Vertices, ReferenceEqualityComparer.Instance);
        var sectors = new HashSet<Sector>(Sectors, ReferenceEqualityComparer.Instance);

        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var line = Linedefs[i];
            if (!vertices.Contains(line.Start) || !vertices.Contains(line.End))
            {
                RemoveLinedef(line);
                repairs++;
            }
        }

        var lines = new HashSet<Linedef>(Linedefs, ReferenceEqualityComparer.Instance);
        var sides = new HashSet<Sidedef>(Sidedefs, ReferenceEqualityComparer.Instance);

        foreach (var line in Linedefs)
        {
            if (line.Front != null && (!sides.Contains(line.Front) || !ReferenceEquals(line.Front.Line, line)))
            {
                line.Front = null;
                repairs++;
            }
            if (line.Back != null && (!sides.Contains(line.Back) || !ReferenceEquals(line.Back.Line, line)))
            {
                line.Back = null;
                repairs++;
            }
        }

        for (int i = Sidedefs.Count - 1; i >= 0; i--)
        {
            var side = Sidedefs[i];
            var line = side.Line;
            if (line == null || !lines.Contains(line) || (!ReferenceEquals(line.Front, side) && !ReferenceEquals(line.Back, side)))
            {
                Sidedefs.RemoveAt(i);
                DisposeElement(side);
                repairs++;
                continue;
            }
            if (side.Sector != null && !sectors.Contains(side.Sector))
            {
                side.Sector = null;
                repairs++;
            }
        }

        return repairs;
    }

    /// <summary>
    /// Reassigns every sidedef of the given sectors to the first one and removes the rest. Geometry (linedefs)
    /// is unchanged, so the lines between the joined sectors remain. Returns the kept sector (null if fewer than
    /// two were given). Call BuildIndexes() afterward.
    /// </summary>
    public Sector? JoinSectors(IReadOnlyList<Sector> sectors)
    {
        if (sectors == null || sectors.Count < 2) return null;
        var keep = sectors[0];
        var remove = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        for (int i = 1; i < sectors.Count; i++) if (!ReferenceEquals(sectors[i], keep)) remove.Add(sectors[i]);
        if (remove.Count == 0) return keep;

        foreach (var sd in Sidedefs)
            if (sd.Sector != null && remove.Contains(sd.Sector)) sd.Sector = keep;
        for (int i = Sectors.Count - 1; i >= 0; i--)
        {
            if (!remove.Contains(Sectors[i])) continue;
            DisposeElement(Sectors[i]);
            Sectors.RemoveAt(i);
        }
        ReindexSectors();
        return keep;
    }

    /// <summary>
    /// Joins the sectors and then removes the now-internal linedefs (both sides in the kept sector) and any
    /// vertices left unused, fusing them into one sector. Returns the kept sector. Call BuildIndexes() afterward.
    /// </summary>
    public Sector? MergeSectors(IReadOnlyList<Sector> sectors)
    {
        var keep = JoinSectors(sectors);
        if (keep == null) return null;

        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var l = Linedefs[i];
            if (l.Front?.Sector != null && l.Back?.Sector != null && ReferenceEquals(l.Front.Sector, l.Back.Sector))
                RemoveLinedef(l); // an internal wall between two merged sectors
        }
        RemoveUnusedVertices();
        return keep;
    }

    private void ReindexSectors()
    {
        for (int i = 0; i < Sectors.Count; i++) Sectors[i].Index = i;
    }

    /// <summary>
    /// The set of vertices implied by the current selection: directly-selected vertices, the endpoints of
    /// selected linedefs, and the vertices of selected sectors' sidedefs. Used by transforms and dragging.
    /// </summary>
    public HashSet<Vertex> SelectedGeometryVertices()
    {
        var set = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (var v in Vertices) if (v.Selected) set.Add(v);
        foreach (var l in Linedefs) if (l.Selected) { set.Add(l.Start); set.Add(l.End); }
        foreach (var sd in Sidedefs)
            if (sd.Line != null && sd.Sector is { Selected: true }) { set.Add(sd.Line.Start); set.Add(sd.Line.End); }
        return set;
    }

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

    // ============================================================
    // Geometry cleanup / merging.
    // ============================================================

    /// <summary>
    /// Merges <paramref name="remove"/> into <paramref name="keep"/>: every linedef referencing
    /// <paramref name="remove"/> is repointed to <paramref name="keep"/>, then any linedef that became
    /// degenerate (both ends the same vertex) is removed. Call BuildIndexes() afterward.
    /// </summary>
    public void JoinVertices(Vertex keep, Vertex remove)
    {
        if (ReferenceEquals(keep, remove)) return;
        foreach (var l in Linedefs)
        {
            if (ReferenceEquals(l.Start, remove)) l.Start = keep;
            if (ReferenceEquals(l.End, remove)) l.End = keep;
        }
        if (Vertices.Remove(remove)) DisposeElement(remove);

        // Drop linedefs that collapsed to a point, and refresh angles on the rest.
        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var l = Linedefs[i];
            if (ReferenceEquals(l.Start, l.End)) RemoveLinedef(l);
            else l.Angle = Linedef.ComputeAngle(l.Start, l.End);
        }
    }

    /// <summary>
    /// Merges every pair of vertices closer than <paramref name="distance"/> map units into one
    /// (useful after dragging vertices together). Returns the number of merges. Call BuildIndexes() after.
    /// </summary>
    public int MergeOverlappingVertices(double distance)
    {
        double d2 = distance * distance;
        int merges = 0;
        bool joined;
        do
        {
            joined = false;
            for (int i = 0; i < Vertices.Count - 1 && !joined; i++)
            {
                for (int c = i + 1; c < Vertices.Count; c++)
                {
                    double dx = Vertices[i].Position.x - Vertices[c].Position.x;
                    double dy = Vertices[i].Position.y - Vertices[c].Position.y;
                    if (dx * dx + dy * dy <= d2)
                    {
                        JoinVertices(Vertices[i], Vertices[c]);
                        merges++;
                        joined = true; // list mutated; restart the scan
                        break;
                    }
                }
            }
        } while (joined);
        return merges;
    }

    /// <summary>
    /// Splits any linedef that passes through a vertex (a T-junction) at that vertex, welding drawn or pasted
    /// geometry onto existing walls. A vertex within <paramref name="distance"/> of a line's interior (not at an
    /// endpoint) triggers a split. Returns the number of splits performed. Call BuildIndexes() afterward.
    /// </summary>
    public int SplitLinedefsAtVertices(double distance = 1.0)
    {
        double epsSq = distance * distance;
        int total = 0, pass, guard = 0;
        do
        {
            pass = 0;
            foreach (var v in Vertices)
            {
                var p = v.Position;
                // Linedefs grows as we split; a newly created half has v as an endpoint, so it is skipped here.
                for (int i = 0; i < Linedefs.Count; i++)
                {
                    var l = Linedefs[i];
                    if (ReferenceEquals(l.Start, v) || ReferenceEquals(l.End, v)) continue;
                    var a = l.Start.Position;
                    var b = l.End.Position;
                    if ((b - a).GetLengthSq() < 1e-9) continue; // degenerate line
                    double u = Line2D.GetNearestOnLine(a, b, p);
                    if (u <= 1e-6 || u >= 1 - 1e-6) continue; // at/near an endpoint - not an interior split
                    if (Line2D.GetDistanceToLineSq(a, b, p, bounded: true) > epsSq) continue;
                    SplitLinedefAt(l, v);
                    pass++;
                }
            }
            total += pass;
        } while (pass > 0 && ++guard < 64);
        return total;
    }

    /// <summary>Removes vertices not referenced by any linedef. Returns the number removed.</summary>
    public int RemoveUnusedVertices()
    {
        var used = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (var l in Linedefs) { used.Add(l.Start); used.Add(l.End); }
        int removed = 0;
        for (int i = Vertices.Count - 1; i >= 0; i--)
        {
            if (used.Contains(Vertices[i])) continue;
            DisposeElement(Vertices[i]);
            Vertices.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    /// <summary>Removes sectors not referenced by any sidedef. Returns the number removed.</summary>
    public int RemoveUnusedSectors()
    {
        var used = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        foreach (var sd in Sidedefs) if (sd.Sector != null) used.Add(sd.Sector);
        int removed = 0;
        for (int i = Sectors.Count - 1; i >= 0; i--)
        {
            if (used.Contains(Sectors[i])) continue;
            DisposeElement(Sectors[i]);
            Sectors.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    private static void CopySidedefProperties(Sidedef src, Sidedef dst)
    {
        dst.OffsetX = src.OffsetX;
        dst.OffsetY = src.OffsetY;
        dst.HighTexture = src.HighTexture;
        dst.MidTexture = src.MidTexture;
        dst.LowTexture = src.LowTexture;
        CopyFields(src, dst);
    }

    private static void CopyFields(IFielded src, IFielded dst)
    {
        foreach (var kv in src.Fields) dst.Fields[kv.Key] = kv.Value;
    }

    private static void CopyArgs(IHasArguments src, IHasArguments dst)
    {
        for (int i = 0; i < dst.Args.Length; i++) dst.Args[i] = src.Args[i];
    }

    private static void DisposeElement(IMapElement element) => element.IsDisposed = true;

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

    public List<Vertex> GetSelectedVertices(bool selected) => FilterSelected(Vertices, selected);
    public List<Linedef> GetSelectedLinedefs(bool selected) => FilterSelected(Linedefs, selected);
    public List<Sidedef> GetSelectedSidedefs(bool selected) => FilterSelected(Sidedefs, selected);
    public List<Sector> GetSelectedSectors(bool selected) => FilterSelected(Sectors, selected);
    public List<Thing> GetSelectedThings(bool selected) => FilterSelected(Things, selected);

    public int SelectedVerticesCount => Count(Vertices);
    public int SelectedLinedefsCount => Count(Linedefs);
    public int SelectedSidedefsCount => Count(Sidedefs);
    public int SelectedSectorsCount => Count(Sectors);
    public int SelectedThingsCount => Count(Things);

    public void ClearSelectedVertices() { foreach (var v in Vertices) v.Selected = false; }
    public void ClearSelectedLinedefs() { foreach (var l in Linedefs) l.Selected = false; }
    public void ClearSelectedSidedefs() { foreach (var s in Sidedefs) s.Selected = false; }
    public void ClearSelectedSectors() { foreach (var s in Sectors) s.Selected = false; }
    public void ClearSelectedThings() { foreach (var t in Things) t.Selected = false; }

    public void SelectAllVertices() => SetSelected(Vertices, true);
    public void SelectAllLinedefs() => SetSelected(Linedefs, true);
    public void SelectAllSidedefs() => SetSelected(Sidedefs, true);
    public void SelectAllSectors() => SetSelected(Sectors, true);
    public void SelectAllThings() => SetSelected(Things, true);

    public void InvertSelectedVertices() => InvertSelected(Vertices);
    public void InvertSelectedLinedefs() => InvertSelected(Linedefs);
    public void InvertSelectedSidedefs() => InvertSelected(Sidedefs);
    public void InvertSelectedSectors() => InvertSelected(Sectors);
    public void InvertSelectedThings() => InvertSelected(Things);

    /// <summary>Clears the Selected flag on every element of every type.</summary>
    public void ClearAllSelected()
    {
        ClearSelectedVertices();
        ClearSelectedLinedefs();
        ClearSelectedSidedefs();
        ClearSelectedSectors();
        ClearSelectedThings();
    }

    public void SelectMarkedGeometry(bool mark, bool select)
    {
        SelectMarkedVertices(mark, select);
        SelectMarkedLinedefs(mark, select);
        SelectMarkedSectors(mark, select);
        SelectMarkedThings(mark, select);
    }

    public void SelectMarkedVertices(bool mark, bool select) => SelectMarked(Vertices, mark, select);
    public void SelectMarkedLinedefs(bool mark, bool select) => SelectMarked(Linedefs, mark, select);
    public void SelectMarkedSectors(bool mark, bool select) => SelectMarked(Sectors, mark, select);
    public void SelectMarkedThings(bool mark, bool select) => SelectMarked(Things, mark, select);

    // ============================================================
    // Selection groups.
    // ============================================================
    // UDB selection groups are a transient bitmask on vertices, linedefs, sectors and things. Sidedefs are
    // intentionally excluded because UDB does not group them independently.

    /// <summary>Returns the bitmask for a zero-based selection group index.</summary>
    public static int GroupMask(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex > 30) throw new ArgumentOutOfRangeException(nameof(groupIndex));
        return 1 << groupIndex;
    }

    /// <summary>Adds every currently selected vertex, linedef, sector and thing to the group.</summary>
    public void AddSelectionToGroup(int groupIndex)
    {
        int mask = GroupMask(groupIndex);
        AddSelectedToGroup(Vertices, mask);
        AddSelectedToGroup(Linedefs, mask);
        AddSelectedToGroup(Sectors, mask);
        AddSelectedToGroup(Things, mask);
    }

    /// <summary>Removes the group mask from every vertex, linedef, sector and thing.</summary>
    public void ClearGroup(int groupMask)
    {
        ClearGroupMask(Vertices, groupMask);
        ClearGroupMask(Linedefs, groupMask);
        ClearGroupMask(Sectors, groupMask);
        ClearGroupMask(Things, groupMask);
    }

    public void SelectVerticesByGroup(int groupMask) => SelectByGroup(Vertices, groupMask);
    public void SelectLinedefsByGroup(int groupMask) => SelectByGroup(Linedefs, groupMask);
    public void SelectSectorsByGroup(int groupMask) => SelectByGroup(Sectors, groupMask);
    public void SelectThingsByGroup(int groupMask) => SelectByGroup(Things, groupMask);

    // ============================================================
    // Marking.
    // ============================================================
    // Marks are transient scratch flags used by editing and analysis algorithms. They intentionally mirror
    // selection helpers but are not user-visible selection state and are not serialized.

    public List<Vertex> GetMarkedVertices() => FilterMarked(Vertices);
    public List<Linedef> GetMarkedLinedefs() => FilterMarked(Linedefs);
    public List<Sidedef> GetMarkedSidedefs() => FilterMarked(Sidedefs);
    public List<Sector> GetMarkedSectors() => FilterMarked(Sectors);
    public List<Thing> GetMarkedThings() => FilterMarked(Things);

    public List<Vertex> GetMarkedVertices(bool marked) => FilterMarked(Vertices, marked);
    public List<Linedef> GetMarkedLinedefs(bool marked) => FilterMarked(Linedefs, marked);
    public List<Sidedef> GetMarkedSidedefs(bool marked) => FilterMarked(Sidedefs, marked);
    public List<Sector> GetMarkedSectors(bool marked) => FilterMarked(Sectors, marked);
    public List<Thing> GetMarkedThings(bool marked) => FilterMarked(Things, marked);

    public int MarkedVerticesCount => CountMarked(Vertices);
    public int MarkedLinedefsCount => CountMarked(Linedefs);
    public int MarkedSidedefsCount => CountMarked(Sidedefs);
    public int MarkedSectorsCount => CountMarked(Sectors);
    public int MarkedThingsCount => CountMarked(Things);

    public void ClearMarkedVertices() { foreach (var v in Vertices) v.Marked = false; }
    public void ClearMarkedLinedefs() { foreach (var l in Linedefs) l.Marked = false; }
    public void ClearMarkedSidedefs() { foreach (var s in Sidedefs) s.Marked = false; }
    public void ClearMarkedSectors() { foreach (var s in Sectors) s.Marked = false; }
    public void ClearMarkedThings() { foreach (var t in Things) t.Marked = false; }

    /// <summary>Clears the Marked flag on every element of every type.</summary>
    public void ClearAllMarked()
    {
        ClearMarkedVertices();
        ClearMarkedLinedefs();
        ClearMarkedSidedefs();
        ClearMarkedSectors();
        ClearMarkedThings();
    }

    // ============================================================
    // Element lookup.
    // ============================================================
    // These helpers make ownership/index checks explicit at call sites and keep reference semantics visible.

    public int IndexOfVertex(Vertex vertex) => Vertices.IndexOf(vertex);
    public int IndexOfLinedef(Linedef linedef) => Linedefs.IndexOf(linedef);
    public int IndexOfSidedef(Sidedef sidedef) => Sidedefs.IndexOf(sidedef);
    public int IndexOfSector(Sector sector) => Sectors.IndexOf(sector);
    public int IndexOfThing(Thing thing) => Things.IndexOf(thing);

    public Vertex? GetVertexByIndex(int index) => (uint)index < (uint)Vertices.Count ? Vertices[index] : null;
    public Linedef? GetLinedefByIndex(int index) => (uint)index < (uint)Linedefs.Count ? Linedefs[index] : null;
    public Sidedef? GetSidedefByIndex(int index) => (uint)index < (uint)Sidedefs.Count ? Sidedefs[index] : null;
    public Sector? GetSectorByIndex(int index) => (uint)index < (uint)Sectors.Count ? Sectors[index] : null;
    public Thing? GetThingByIndex(int index) => (uint)index < (uint)Things.Count ? Things[index] : null;

    public bool ContainsVertex(Vertex vertex) => IndexOfVertex(vertex) >= 0;
    public bool ContainsLinedef(Linedef linedef) => IndexOfLinedef(linedef) >= 0;
    public bool ContainsSidedef(Sidedef sidedef) => IndexOfSidedef(sidedef) >= 0;
    public bool ContainsSector(Sector sector) => IndexOfSector(sector) >= 0;
    public bool ContainsThing(Thing thing) => IndexOfThing(thing) >= 0;

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

    /// <summary>Reverses the direction of every selected linedef. Returns the number flipped. Call BuildIndexes() after.</summary>
    public int FlipSelectedLinedefs()
    {
        int n = 0;
        foreach (var l in Linedefs)
            if (l.Selected) { l.FlipVertices(); n++; }
        return n;
    }

    /// <summary>Swaps front/back sidedefs on every selected linedef. Returns the number swapped. Call BuildIndexes() after.</summary>
    public int FlipSelectedSidedefs()
    {
        int n = 0;
        foreach (var l in Linedefs)
            if (l.Selected) { l.FlipSidedefs(); n++; }
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

    private static List<T> FilterSelected<T>(List<T> items, bool selected) where T : ISelectable
    {
        var result = new List<T>();
        foreach (var it in items)
            if (it.Selected == selected) result.Add(it);
        return result;
    }

    private static int Count<T>(List<T> items) where T : ISelectable
    {
        int n = 0;
        foreach (var it in items)
            if (it.Selected) n++;
        return n;
    }

    private static void SetSelected<T>(List<T> items, bool selected) where T : ISelectable
    {
        foreach (var it in items) it.Selected = selected;
    }

    private static void InvertSelected<T>(List<T> items) where T : ISelectable
    {
        foreach (var it in items) it.Selected = !it.Selected;
    }

    private static void AddSelectedToGroup<T>(List<T> items, int mask) where T : IGroupable
    {
        foreach (var it in items)
            if (it.Selected) it.Groups |= mask;
    }

    private static void ClearGroupMask<T>(List<T> items, int mask) where T : IGroupable
    {
        foreach (var it in items) it.Groups &= ~mask;
    }

    private static void SelectByGroup<T>(List<T> items, int mask) where T : IGroupable
    {
        foreach (var it in items) it.Selected = (it.Groups & mask) != 0;
    }

    private static List<T> FilterMarked<T>(List<T> items) where T : IMarkable
    {
        var result = new List<T>();
        foreach (var it in items)
            if (it.Marked) result.Add(it);
        return result;
    }

    private static List<T> FilterMarked<T>(List<T> items, bool marked) where T : IMarkable
    {
        var result = new List<T>();
        foreach (var it in items)
            if (it.Marked == marked) result.Add(it);
        return result;
    }

    private static int CountMarked<T>(List<T> items) where T : IMarkable
    {
        int n = 0;
        foreach (var it in items)
            if (it.Marked) n++;
        return n;
    }

    private static void SelectMarked<T>(List<T> items, bool mark, bool select) where T : ISelectable, IMarkable
    {
        foreach (var it in items)
            if (it.Marked == mark) it.Selected = select;
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

    /// <summary>Vertices whose position falls within the axis-aligned box (inclusive).</summary>
    public List<Vertex> GetVerticesInBox(double minX, double minY, double maxX, double maxY)
    {
        var result = new List<Vertex>();
        foreach (var v in Vertices)
            if (InBox(v.Position, minX, minY, maxX, maxY)) result.Add(v);
        return result;
    }

    /// <summary>Things whose position falls within the axis-aligned box (inclusive).</summary>
    public List<Thing> GetThingsInBox(double minX, double minY, double maxX, double maxY)
    {
        var result = new List<Thing>();
        foreach (var t in Things)
            if (InBox(t.Position, minX, minY, maxX, maxY)) result.Add(t);
        return result;
    }

    /// <summary>Linedefs with BOTH endpoints inside the box (the classic rubber-band rule).</summary>
    public List<Linedef> GetLinedefsInBox(double minX, double minY, double maxX, double maxY)
    {
        var result = new List<Linedef>();
        foreach (var l in Linedefs)
            if (InBox(l.Start.Position, minX, minY, maxX, maxY) && InBox(l.End.Position, minX, minY, maxX, maxY))
                result.Add(l);
        return result;
    }

    /// <summary>Sectors all of whose boundary vertices are inside the box (a sector fully enclosed by the box).</summary>
    public List<Sector> GetSectorsInBox(double minX, double minY, double maxX, double maxY)
    {
        var result = new List<Sector>();
        foreach (var s in Sectors)
        {
            if (s.Sidedefs.Count == 0) continue;
            bool all = true;
            foreach (var sd in s.Sidedefs)
            {
                var line = sd.Line;
                if (line == null) continue;
                if (!InBox(line.Start.Position, minX, minY, maxX, maxY) || !InBox(line.End.Position, minX, minY, maxX, maxY))
                { all = false; break; }
            }
            if (all) result.Add(s);
        }
        return result;
    }

    private static bool InBox(Vector2D p, double minX, double minY, double maxX, double maxY)
        => p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;

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
