// ABOUTME: In-memory map container - simplified subset of UDB's MapSet.
// ABOUTME: Holds the five core map element lists; bbox helper for fitting to a viewport.

namespace DBuilder.Map;

using System.Drawing;
using DBuilder.Geometry;

/// <summary>Counts the contents of a one-based selection group for UI labels and summaries.</summary>
public readonly record struct SelectionGroupInfo(int Index, int SectorCount, int LinedefCount, int VertexCount, int ThingCount)
{
    public bool Empty => SectorCount == 0 && LinedefCount == 0 && VertexCount == 0 && ThingCount == 0;

    public int TotalCount => SectorCount + LinedefCount + VertexCount + ThingCount;

    public string AddedStatusText(int selectedCount)
        => $"Added {CountLabel(selectedCount, "selected element")} to group {Index}.";

    public string SelectedStatusText()
        => $"Selected {CountLabel(TotalCount, "element")} from group {Index}.";

    public string ClearedStatusText()
        => $"Cleared {CountLabel(TotalCount, "element")} from group {Index}.";

    public static string ClearedStatusText(int groupIndex, int elementCount)
        => $"Cleared {CountLabel(elementCount, "element")} from group {groupIndex + 1}.";

    public override string ToString()
    {
        if (Empty) return $"{Index}: Empty";

        var parts = new List<string>();
        if (SectorCount > 0) parts.Add($"{SectorCount} {(SectorCount == 1 ? "sector" : "sectors")}");
        if (LinedefCount > 0) parts.Add($"{LinedefCount} {(LinedefCount == 1 ? "line" : "lines")}");
        if (VertexCount > 0) parts.Add($"{VertexCount} {(VertexCount == 1 ? "vertex" : "vertices")}");
        if (ThingCount > 0) parts.Add($"{ThingCount} {(ThingCount == 1 ? "thing" : "things")}");

        return $"{Index}: {string.Join(", ", parts)}";
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public readonly record struct GeometryStitchResult(
    int JoinedVertices,
    int VertexLineSplits,
    int LineLineSplits,
    int RemovedLoopedLinedefs,
    int RemovedInteriorLinedefs,
    int RemovedBridgeVertices,
    int JoinedOverlappingLinedefs,
    int CorrectedOuterSidedefs,
    int FlippedBackwardLinedefs)
{
    public int TotalChanges => JoinedVertices + VertexLineSplits + LineLineSplits + RemovedLoopedLinedefs +
        RemovedInteriorLinedefs + RemovedBridgeVertices + JoinedOverlappingLinedefs + CorrectedOuterSidedefs +
        FlippedBackwardLinedefs;
}

public readonly record struct GeometryCleanupResult(int Repaired, int Sectors, int Vertices, int SidedefTextures)
{
    public int Total => Repaired + Sectors + Vertices + SidedefTextures;

    public string StatusText
        => $"Geometry cleanup: {CountLabel(Repaired, "reference repair")}, {CountLabel(Sectors, "sector")}, {CountLabel(Vertices, "unused vertex removal")}, {CountLabel(SidedefTextures, "sidedef texture cleanup")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public enum MapTagKind
{
    Linedef,
    Sector,
    Thing,
}

public class MapSet : IDisposable
{
    public const float STITCH_DISTANCE = 0.005f;
    public const long EmptyLongName = 3655919633L;
    public const string VirtualSectorField = "!virtual_sector";
    public const string VIRTUAL_SECTOR_FIELD = VirtualSectorField;
    public const int VirtualSectorValue = 0;

    public List<Vertex> Vertices { get; } = new();
    public List<Linedef> Linedefs { get; } = new();
    public List<Sidedef> Sidedefs { get; } = new();
    public List<Sector> Sectors { get; } = new();
    public List<Thing> Things { get; } = new();

    public bool IsDisposed { get; private set; }
    public bool IsSafeToAccess { get; set; } = true;
    public SelectionType SelectionType { get; set; }

    private int addRemoveDepth;

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
        => Update();

    public void Update()
        => Update(dolines: true, dosectors: true);

    public void Update(bool dolines, bool dosectors)
    {
        ReindexAllElements();
        if (dolines)
        {
            foreach (var vertex in Vertices) vertex.Linedefs.Clear();
            foreach (var side in Sidedefs) side.Other = null;
        }

        if (dosectors)
        {
            foreach (var sector in Sectors) sector.Sidedefs.Clear();
        }

        foreach (var line in Linedefs)
        {
            if (dolines)
            {
                if (line.Start != null && line.End != null)
                    line.Angle = Linedef.ComputeAngle(line.Start, line.End);
                if (line.Start != null) line.Start.Linedefs.Add(line);
                // Avoid double-adding if Start == End (degenerate but observed in the wild).
                if (line.End != null && !object.ReferenceEquals(line.End, line.Start)) line.End.Linedefs.Add(line);
            }

            if (line.Front != null)
            {
                if (dosectors && line.Front.Sector != null) line.Front.Sector.Sidedefs.Add(line.Front);
                if (dolines) line.Front.Other = line.Back;
            }
            if (line.Back != null)
            {
                if (dosectors && line.Back.Sector != null) line.Back.Sector.Sidedefs.Add(line.Back);
                if (dolines) line.Back.Other = line.Front;
            }
        }

        if (dosectors)
            foreach (var sector in Sectors) sector.UpdateBBox();
    }

    public void UpdateConfiguration()
    {
    }

    // ============================================================
    // Mutation operations.
    // ============================================================
    // These maintain the primary lists and direct references (Linedef.Front/Back/Start/End, Sidedef.Sector).
    // Derived state (Vertex.Linedefs, Sector.Sidedefs, Sidedef.Other) is NOT updated incrementally - call
    // BuildIndexes() once after a batch of edits before triangulating or rendering. Removals scan the primary
    // lists rather than the derived back-references so they stay correct even when indexes are stale.

    public void BeginAddRemove()
        => addRemoveDepth++;

    public void SetCapacity(int vertices, int linedefs, int sidedefs, int sectors, int things)
    {
        if (addRemoveDepth == 0)
            throw new InvalidOperationException("You must call BeginAddRemove before setting the reserved capacity.");

        ReserveCapacity(Vertices, vertices);
        ReserveCapacity(Linedefs, linedefs);
        ReserveCapacity(Sidedefs, sidedefs);
        ReserveCapacity(Sectors, sectors);
        ReserveCapacity(Things, things);
    }

    public void EndAddRemove()
    {
        if (addRemoveDepth <= 0) return;

        addRemoveDepth--;
        if (addRemoveDepth > 0) return;

        Vertices.TrimExcess();
        Linedefs.TrimExcess();
        Sidedefs.TrimExcess();
        Sectors.TrimExcess();
        Things.TrimExcess();
    }

    private static void ReserveCapacity<T>(List<T> elements, int capacity)
    {
        if (capacity > elements.Capacity)
            elements.Capacity = capacity;
    }

    public Vertex AddVertex(Vector2D position)
    {
        var v = new Vertex(position) { Index = Vertices.Count };
        Vertices.Add(v);
        return v;
    }

    public Vertex CreateVertex(Vector2D position)
        => AddVertex(position);

    public Vertex CreateVertex(int index, Vector2D position)
    {
        Vertex vertex = AddVertex(position);
        MoveCreatedElementToIndex(Vertices, vertex, index);
        return vertex;
    }

    public Linedef AddLinedef(Vertex start, Vertex end)
    {
        var l = new Linedef(start, end) { Index = Linedefs.Count };
        Linedefs.Add(l);
        return l;
    }

    public Linedef CreateLinedef(Vertex start, Vertex end)
        => AddLinedef(start, end);

    public Linedef CreateLinedef(int index, Vertex start, Vertex end)
    {
        Linedef linedef = AddLinedef(start, end);
        MoveCreatedElementToIndex(Linedefs, linedef, index);
        return linedef;
    }

    public Sidedef AddSidedef(Linedef line, bool isFront, Sector? sector)
    {
        var sd = new Sidedef(line, isFront) { Index = Sidedefs.Count, Sector = sector };
        if (isFront) line.Front = sd; else line.Back = sd;
        Sidedefs.Add(sd);
        return sd;
    }

    public Sidedef CreateSidedef(Linedef line, bool front, Sector? sector)
        => AddSidedef(line, front, sector);

    public Sidedef CreateSidedef(int index, Linedef line, bool front, Sector? sector)
    {
        Sidedef sidedef = AddSidedef(line, front, sector);
        MoveCreatedElementToIndex(Sidedefs, sidedef, index);
        return sidedef;
    }

    public Sector AddSector()
    {
        var s = new Sector { Index = Sectors.Count };
        Sectors.Add(s);
        return s;
    }

    public Sector CreateSector()
        => AddSector();

    public Sector CreateSector(int index)
    {
        Sector sector = AddSector();
        MoveCreatedElementToIndex(Sectors, sector, index);
        return sector;
    }

    public Thing AddThing(Vector2D position, int type)
    {
        var t = new Thing(position, type) { Index = Things.Count };
        Things.Add(t);
        return t;
    }

    public Thing CreateThing()
        => AddThing(new Vector2D(), type: 0);

    internal Thing CreateTempThing()
        => CreateThing();

    public Thing CreateThing(int index)
    {
        Thing thing = CreateThing();
        MoveCreatedElementToIndex(Things, thing, index);
        return thing;
    }

    private static void MoveCreatedElementToIndex<T>(List<T> elements, T element, int index)
        where T : class, IMapElement
    {
        int lastIndex = elements.Count - 1;
        if (index < 0 || index > lastIndex) throw new ArgumentOutOfRangeException(nameof(index));
        if (index == lastIndex) return;

        T displaced = elements[index];
        elements[index] = element;
        elements[lastIndex] = displaced;
        element.Index = index;
        displaced.Index = lastIndex;
    }

    public Thing PlaceUniqueThing(int type, Vector2D position)
    {
        Thing? kept = null;
        foreach (Thing thing in Things.ToList())
        {
            if (thing.Type != type) continue;
            if (kept == null)
            {
                kept = thing;
                continue;
            }

            RemoveThing(thing);
        }

        if (kept == null)
            kept = AddThing(position, type);
        else
            kept.Move(position);

        kept.Selected = true;
        return kept;
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
            CopyIgnoredChecks(vertex, copy);
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
                LongFloorTexture = sector.LongFloorTexture,
                LongCeilTexture = sector.LongCeilTexture,
                Brightness = sector.Brightness,
                Special = sector.Special,
                FloorSlope = sector.FloorSlope,
                FloorSlopeOffset = sector.FloorSlopeOffset,
                CeilSlope = sector.CeilSlope,
                CeilSlopeOffset = sector.CeilSlopeOffset,
            };
            copy.Tags.AddRange(sector.Tags);
            foreach (var flag in sector.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(sector, copy);
            CopyIgnoredChecks(sector, copy);
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
                Activate = line.Activate,
            };
            copy.Tags.AddRange(line.Tags);
            CopyArgs(line, copy);
            foreach (var flag in line.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(line, copy);
            CopyIgnoredChecks(line, copy);
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
                LongHighTexture = side.LongHighTexture,
                LongMiddleTexture = side.LongMiddleTexture,
                LongLowTexture = side.LongLowTexture,
            };
            foreach (var flag in side.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(side, copy);
            CopyIgnoredChecks(side, copy);
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
                Size = thing.Size,
                FixedSize = thing.FixedSize,
                Flags = thing.Flags,
                Tag = thing.Tag,
                Action = thing.Action,
                Sector = thing.Sector == null ? null : sectorMap[thing.Sector],
            };
            CopyArgs(thing, copy);
            foreach (var flag in thing.UdmfFlags) copy.UdmfFlags.Add(flag);
            CopyFields(thing, copy);
            CopyIgnoredChecks(thing, copy);
            clone.Things.Add(copy);
        }

        clone.BuildIndexes();
        return clone;
    }

    /// <summary>Creates a deep copy of the marked geometry, using a virtual sector for unmarked adjacent sectors.</summary>
    public MapSet CloneMarked()
    {
        var clone = new MapSet { Namespace = Namespace };
        var vertexMap = new Dictionary<Vertex, Vertex>(ReferenceEqualityComparer.Instance);
        var sectorMap = new Dictionary<Sector, Sector>(ReferenceEqualityComparer.Instance);
        Sector? virtualSector = null;

        foreach (var vertex in Vertices)
        {
            if (!vertex.Marked) continue;
            var copy = clone.AddVertex(vertex.Position);
            vertex.CopyPropertiesTo(copy);
            vertexMap[vertex] = copy;
        }

        foreach (var sector in Sectors)
        {
            if (!sector.Marked) continue;
            var copy = clone.AddSector();
            sector.CopyPropertiesTo(copy);
            sectorMap[sector] = copy;
        }

        foreach (var line in Linedefs)
        {
            if (!line.Marked) continue;
            if (!vertexMap.TryGetValue(line.Start, out var start) || !vertexMap.TryGetValue(line.End, out var end)) continue;

            var copy = clone.AddLinedef(start, end);
            line.CopyPropertiesTo(copy);
            if (line.Front != null) CopyMarkedSidedef(line.Front, copy, true);
            if (line.Back != null) CopyMarkedSidedef(line.Back, copy, false);
        }

        foreach (var thing in Things)
        {
            if (!thing.Marked) continue;
            var copy = clone.AddThing(thing.Position, thing.Type);
            thing.CopyPropertiesTo(copy);
            copy.Sector = thing.Sector != null && sectorMap.TryGetValue(thing.Sector, out var sector) ? sector : null;
        }

        clone.BuildIndexes();
        return clone;

        void CopyMarkedSidedef(Sidedef side, Linedef copyLine, bool isFront)
        {
            var targetSector = CloneSidedefSector(side);
            var copySide = clone.AddSidedef(copyLine, isFront, targetSector);
            side.CopyPropertiesTo(copySide);
        }

        Sector? CloneSidedefSector(Sidedef side)
        {
            if (side.Sector == null) return null;
            if (sectorMap.TryGetValue(side.Sector, out var markedSector)) return markedSector;

            if (virtualSector == null)
            {
                virtualSector = clone.AddSector();
                side.Sector.CopyPropertiesTo(virtualSector);
                virtualSector.Fields[VirtualSectorField] = VirtualSectorValue;
            }

            return virtualSector;
        }
    }

    /// <summary>Removes a sidedef and detaches it from its owning linedef.</summary>
    public void RemoveSidedef(Sidedef sd)
    {
        if (sd.Line != null)
        {
            if (ReferenceEquals(sd.Line.Front, sd)) sd.Line.Front = null;
            if (ReferenceEquals(sd.Line.Back, sd)) sd.Line.Back = null;
        }
        if (Sidedefs.Remove(sd))
        {
            DisposeElement(sd);
            ReindexElements(Sidedefs);
        }
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
        if (Linedefs.Remove(l))
        {
            DisposeElement(l);
            ReindexElements(Linedefs);
            ReindexElements(Sidedefs);
        }
    }

    /// <summary>Removes a vertex and every linedef touching it (which in turn removes those linedefs' sidedefs).</summary>
    public void RemoveVertex(Vertex v)
    {
        for (int i = Linedefs.Count - 1; i >= 0; i--)
        {
            var l = Linedefs[i];
            if (ReferenceEquals(l.Start, v) || ReferenceEquals(l.End, v)) RemoveLinedef(l);
        }
        if (Vertices.Remove(v))
        {
            DisposeElement(v);
            ReindexElements(Vertices);
        }
    }

    /// <summary>Removes a sector; sidedefs that referenced it are left in place with a null sector reference.</summary>
    public void RemoveSector(Sector s)
    {
        foreach (var sd in Sidedefs)
            if (ReferenceEquals(sd.Sector, s)) sd.Sector = null;
        if (Sectors.Remove(s))
        {
            DisposeElement(s);
            ReindexSectors();
        }
    }

    /// <summary>Removes UDB virtual sectors from pasted or cloned geometry and clears their sidedef references.</summary>
    public int RemoveVirtualSectors()
    {
        int removed = 0;
        for (int i = Sectors.Count - 1; i >= 0; i--)
        {
            if (!Sectors[i].Fields.ContainsKey(VirtualSectorField)) continue;
            RemoveSector(Sectors[i]);
            removed++;
        }

        ReindexSectors();
        return removed;
    }

    public void RemoveThing(Thing t)
    {
        if (Things.Remove(t))
        {
            DisposeElement(t);
            ReindexElements(Things);
        }
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

        if (repairs > 0) ReindexAllElements();
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
        => ReindexElements(Sectors);

    private void ReindexAllElements()
    {
        ReindexElements(Vertices);
        ReindexElements(Linedefs);
        ReindexElements(Sidedefs);
        ReindexElements(Sectors);
        ReindexElements(Things);
    }

    private static void ReindexElements<T>(List<T> elements) where T : IMapElement
    {
        for (int i = 0; i < elements.Count; i++) elements[i].Index = i;
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

    /// <summary>Splits each linedef at its midpoint. Returns the number split. Call BuildIndexes() afterward.</summary>
    public int SplitLinedefsAtMidpoints(IEnumerable<Linedef> linedefs)
    {
        var targets = new List<Linedef>();
        var seen = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        foreach (Linedef linedef in linedefs)
        {
            if (linedef.IsDisposed || !seen.Add(linedef)) continue;
            targets.Add(linedef);
        }

        foreach (Linedef linedef in targets)
            SplitLinedef(linedef, linedef.GetCenterPoint());

        return targets.Count;
    }

    /// <summary>Splits a linedef at an existing vertex; returns the new (second-half) linedef.</summary>
    public Linedef SplitLinedefAt(Linedef l, Vertex v)
    {
        double firstHalfLen = (v.Position - l.Start.Position).GetLength();
        var oldEnd = l.End;

        var newLine = new Linedef(v, oldEnd) { Flags = l.Flags, Action = l.Action, Activate = l.Activate };
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
        if (Vertices.Remove(remove))
        {
            DisposeElement(remove);
            ReindexElements(Vertices);
        }

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
    /// Joins nearby vertices from two collections. Only vertices that appear in different collections are joined.
    /// Vertices from the second collection are moved to the first collection vertex before joining.
    /// </summary>
    public int JoinVertices(ICollection<Vertex> set1, ICollection<Vertex> set2, bool keepSecond, double joinDistance)
    {
        double joinDistanceSquared = joinDistance * joinDistance;
        int joins = 0;
        bool joined;
        do
        {
            joined = false;
            foreach (var first in set1.ToArray())
            {
                foreach (var second in set2.ToArray())
                {
                    if (ReferenceEquals(first, second)) continue;
                    if (Vector2D.DistanceSq(first.Position, second.Position) > joinDistanceSquared) continue;

                    second.Position = first.Position;
                    if (keepSecond)
                    {
                        JoinVertices(second, first);
                        set1.Remove(first);
                        set2.Remove(first);
                    }
                    else
                    {
                        JoinVertices(first, second);
                        set1.Remove(second);
                        set2.Remove(second);
                    }
                    joins++;
                    joined = true;
                    break;
                }

                if (joined) break;
            }
        } while (joined);

        return joins;
    }

    /// <summary>
    /// Joins nearby vertices in one collection into the first matching vertex.
    /// </summary>
    public int JoinVertices(List<Vertex> vertices, double joinDistance)
    {
        double joinDistanceSquared = joinDistance * joinDistance;
        int joins = 0;
        bool joined;
        do
        {
            joined = false;
            for (int i = 0; i < vertices.Count - 1 && !joined; i++)
            {
                for (int c = i + 1; c < vertices.Count; c++)
                {
                    Vertex first = vertices[i];
                    Vertex second = vertices[c];
                    if (ReferenceEquals(first, second)) continue;
                    if (!ContainsVertex(first) || !ContainsVertex(second)) continue;
                    if (Vector2D.DistanceSq(first.Position, second.Position) > joinDistanceSquared) continue;

                    second.Position = first.Position;
                    JoinVertices(first, second);
                    vertices.Remove(second);
                    joins++;
                    joined = true;
                    break;
                }
            }
        } while (joined);

        return joins;
    }

    /// <summary>
    /// Removes linedefs whose endpoints are the same vertex or occupy the same position.
    /// Returns the number of unique linedefs removed. Call BuildIndexes() afterward.
    /// </summary>
    public int RemoveLoopedLinedefs(ICollection<Linedef> linedefs)
    {
        int removed = 0;
        bool removedLine;
        do
        {
            removedLine = false;
            foreach (var line in linedefs.ToArray())
            {
                if (!ReferenceEquals(line.Start, line.End) && line.Start.Position != line.End.Position) continue;

                if (Linedefs.Contains(line)) RemoveLinedef(line);
                else DisposeElement(line);
                while (linedefs.Remove(line))
                {
                }
                removed++;
                removedLine = true;
                break;
            }
        } while (removedLine);

        return removed;
    }

    /// <summary>
    /// Splits any linedef that passes through a vertex (a T-junction) at that vertex, welding drawn or pasted
    /// geometry onto existing walls. A vertex within <paramref name="distance"/> of a line's interior (not at an
    /// endpoint) triggers a split. Returns the number of splits performed. Call BuildIndexes() afterward.
    /// </summary>
    public int SplitLinedefsAtVertices(double distance = 1.0)
    {
        return SplitLinesByVertices(Linedefs, Vertices, distance);
    }

    /// <summary>
    /// Splits the supplied linedefs at supplied vertices when a vertex lies on a linedef interior.
    /// New linedefs are added to <paramref name="lines"/> and both halves are added to <paramref name="changedLines"/>.
    /// </summary>
    public int SplitLinesByVertices(
        ICollection<Linedef> lines,
        ICollection<Vertex> vertices,
        double distance,
        ICollection<Linedef>? changedLines = null,
        ICollection<Vertex>? splitVertices = null)
    {
        double epsSq = distance * distance;
        int total = 0, pass, guard = 0;
        do
        {
            pass = 0;
            foreach (var v in vertices.ToArray())
            {
                var p = v.Position;
                // Lines grows as we split; a newly created half has v as an endpoint, so it is skipped here.
                var scan = lines as IList<Linedef> ?? lines.ToList();
                for (int i = 0; i < scan.Count; i++)
                {
                    var l = scan[i];
                    if (ReferenceEquals(l.Start, v) || ReferenceEquals(l.End, v)) continue;
                    var a = l.Start.Position;
                    var b = l.End.Position;
                    if ((b - a).GetLengthSq() < 1e-9) continue; // degenerate line
                    double u = Line2D.GetNearestOnLine(a, b, p);
                    if (u <= 1e-6 || u >= 1 - 1e-6) continue; // at/near an endpoint - not an interior split
                    if (Line2D.GetDistanceToLineSq(a, b, p, bounded: true) > epsSq) continue;
                    var newLine = SplitLinedefAt(l, v);
                    if (!ReferenceEquals(lines, Linedefs) && !lines.Contains(newLine)) lines.Add(newLine);
                    splitVertices?.Add(v);
                    if (changedLines != null)
                    {
                        if (!changedLines.Contains(l)) changedLines.Add(l);
                        if (!changedLines.Contains(newLine)) changedLines.Add(newLine);
                    }
                    pass++;
                }
            }
            total += pass;
        } while (pass > 0 && ++guard < 64);
        return total;
    }

    /// <summary>
    /// UDB-compatible split-by-vertices overload.
    /// </summary>
    public bool SplitLinesByVertices(
        ICollection<Linedef> lines,
        ICollection<Vertex> vertices,
        double distance,
        ICollection<Linedef> changedLines,
        MergeGeometryMode mergeMode)
    {
        SplitLinesByVertices(lines, vertices, distance, changedLines);
        return true;
    }

    /// <summary>
    /// Splits intersecting linedefs from <paramref name="lines"/> and <paramref name="changedLines"/>.
    /// New halves are added to <paramref name="changedLines"/>. Returns the number of intersections split.
    /// </summary>
    public int SplitLinesByLines(
        ICollection<Linedef> lines,
        ICollection<Linedef> changedLines,
        ICollection<Vertex>? splitVertices = null)
    {
        if (lines.Count == 0 || changedLines.Count == 0) return 0;

        int splits = 0;
        bool split;
        do
        {
            split = false;
            var allLines = lines.Concat(changedLines).Distinct().ToArray();
            for (int i = 0; i < allLines.Length && !split; i++)
            {
                for (int j = i + 1; j < allLines.Length; j++)
                {
                    var first = allLines[i];
                    var second = allLines[j];
                    if (!changedLines.Contains(first) && !changedLines.Contains(second)) continue;
                    if (ReferenceEquals(first, second)) continue;
                    if (first.IsDisposed || second.IsDisposed) continue;
                    if (first.Start.Position == second.Start.Position) continue;
                    if (first.Start.Position == second.End.Position) continue;
                    if (first.End.Position == second.Start.Position) continue;
                    if (first.End.Position == second.End.Position) continue;

                    var intersection = Line2D.GetIntersectionPoint(
                        new Line2D(first.Start.Position, first.End.Position),
                        new Line2D(second.Start.Position, second.End.Position),
                        bounded: true);
                    if (double.IsNaN(intersection.x)) continue;
                    if (first.Start.Position == intersection || first.End.Position == intersection) continue;
                    if (second.Start.Position == intersection || second.End.Position == intersection) continue;

                    var splitVertex = Vertices.FirstOrDefault(v => v.Position == intersection);
                    if (splitVertex == null) splitVertex = AddVertex(intersection);
                    splitVertices?.Add(splitVertex);

                    var firstNewLine = SplitLinedefAt(first, splitVertex);
                    var secondNewLine = SplitLinedefAt(second, splitVertex);
                    if (!changedLines.Contains(firstNewLine)) changedLines.Add(firstNewLine);
                    if (!changedLines.Contains(secondNewLine)) changedLines.Add(secondNewLine);

                    splits++;
                    split = true;
                    break;
                }
            }
        } while (split);

        return splits;
    }

    /// <summary>
    /// UDB-compatible split-by-lines overload. Classic mode leaves the supplied collections unchanged.
    /// </summary>
    public bool SplitLinesByLines(
        ICollection<Linedef> lines,
        HashSet<Linedef> changedLines,
        MergeGeometryMode mergeMode)
    {
        if (mergeMode == MergeGeometryMode.Classic) return true;

        SplitLinesByLines(lines, changedLines);
        return true;
    }

    /// <summary>
    /// Stitches selected geometry against unselected geometry by joining nearby vertices, splitting crossed lines,
    /// removing looped changed lines, and correcting backward changed lines. Call BuildIndexes() afterward.
    /// </summary>
    public GeometryStitchResult StitchSelectedGeometry(double stitchDistance = 1.0)
        => StitchSelectedGeometry(MergeGeometryMode.Classic, stitchDistance);

    public bool StitchGeometry()
        => StitchGeometry(MergeGeometryMode.Classic);

    public bool StitchGeometry(MergeGeometryMode mergeMode)
    {
        List<(ISelectable Element, bool Selected)> selection = SaveSelection();
        try
        {
            ClearAllSelected();
            foreach (Vertex vertex in GetMarkedVertices(marked: true))
                vertex.Selected = true;

            StitchSelectedGeometry(mergeMode, STITCH_DISTANCE);
            BuildIndexes();
            return true;
        }
        finally
        {
            RestoreSelection(selection);
        }
    }

    /// <summary>
    /// Stitches selected geometry against unselected geometry using the requested UDB merge-geometry mode.
    /// Non-classic modes also try to restore missing outer sidedefs for changed lines inside existing sectors.
    /// </summary>
    public GeometryStitchResult StitchSelectedGeometry(MergeGeometryMode mergeMode, double stitchDistance = 1.0)
    {
        var movingVertices = SelectedGeometryVertices();
        if (movingVertices.Count == 0) return default;

        var fixedVertices = new HashSet<Vertex>(
            Vertices.Where(vertex => !movingVertices.Contains(vertex)),
            ReferenceEqualityComparer.Instance);
        int joinedVertices = JoinVertices(fixedVertices, movingVertices, keepSecond: true, joinDistance: stitchDistance);

        movingVertices = new HashSet<Vertex>(
            movingVertices.Where(vertex => !vertex.IsDisposed),
            ReferenceEqualityComparer.Instance);
        fixedVertices = new HashSet<Vertex>(
            Vertices.Where(vertex => !movingVertices.Contains(vertex)),
            ReferenceEqualityComparer.Instance);

        var movingLines = new HashSet<Linedef>(
            Linedefs.Where(line => movingVertices.Contains(line.Start) || movingVertices.Contains(line.End)),
            ReferenceEqualityComparer.Instance);
        var replacedSectors = mergeMode == MergeGeometryMode.Replace
            ? GetSectorsFromLinedefs(movingLines)
            : new HashSet<Sector>();
        var fixedLines = new HashSet<Linedef>(
            Linedefs.Where(line => !movingVertices.Contains(line.Start) && !movingVertices.Contains(line.End)),
            ReferenceEqualityComparer.Instance);
        var changedLines = new HashSet<Linedef>(movingLines, ReferenceEqualityComparer.Instance);
        var splitVertices = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);

        int vertexLineSplits = 0;
        vertexLineSplits += SplitLinesByVertices(movingLines, fixedVertices, stitchDistance, changedLines, splitVertices);
        fixedLines = new HashSet<Linedef>(
            fixedLines.Where(line => !line.IsDisposed),
            ReferenceEqualityComparer.Instance);
        vertexLineSplits += SplitLinesByVertices(fixedLines, movingVertices, stitchDistance, changedLines, splitVertices);

        int lineLineSplits = SplitLinesByLines(fixedLines, changedLines, splitVertices);
        int removedLooped = RemoveLoopedLinedefs(changedLines);
        int removedInterior = mergeMode == MergeGeometryMode.Replace
            ? RemoveLinedefsInsideSectors(
                new HashSet<Linedef>(fixedLines.Concat(changedLines), ReferenceEqualityComparer.Instance),
                replacedSectors,
                changedLines)
            : 0;
        int joinedOverlapping = JoinOverlappingLinedefs(changedLines);
        int removedBridgeVertices = mergeMode == MergeGeometryMode.Replace
            ? RemoveBridgeVertices(
                splitVertices.Where(vertex => !movingVertices.Contains(vertex)).ToArray(),
                new HashSet<Linedef>(fixedLines.Concat(changedLines), ReferenceEqualityComparer.Instance),
                changedLines)
            : 0;
        int correctedOuter = mergeMode == MergeGeometryMode.Classic ? 0 : CorrectOuterSidedefs(changedLines);
        int flippedBackward = FlipBackwardLinedefs(changedLines);

        return new GeometryStitchResult(
            joinedVertices,
            vertexLineSplits,
            lineLineSplits,
            removedLooped,
            removedInterior,
            removedBridgeVertices,
            joinedOverlapping,
            correctedOuter,
            flippedBackward);
    }

    /// <summary>
    /// Joins overlapping linedefs in the supplied collection when they share both endpoints.
    /// Missing sidedefs are transferred to the kept line. Returns the number of linedefs removed.
    /// </summary>
    public int JoinOverlappingLinedefs(ICollection<Linedef> lines)
    {
        int removed = 0;
        bool joined;
        do
        {
            joined = false;
            foreach (var keep in lines.ToArray())
            {
                foreach (var remove in lines.ToArray())
                {
                    if (ReferenceEquals(keep, remove)) continue;
                    if (keep.IsDisposed || remove.IsDisposed) continue;

                    bool sameDirection = ReferenceEquals(keep.Start, remove.Start) && ReferenceEquals(keep.End, remove.End);
                    bool oppositeDirection = ReferenceEquals(keep.Start, remove.End) && ReferenceEquals(keep.End, remove.Start);
                    if (!sameDirection && !oppositeDirection) continue;

                    MergeOverlappingLinedef(keep, remove, oppositeDirection);
                    while (lines.Remove(remove))
                    {
                    }
                    removed++;
                    joined = true;
                    break;
                }

                if (joined) break;
            }
        } while (joined);

        return removed;
    }

    /// <summary>
    /// UDB-compatible wrapper for joining overlapping linedefs.
    /// </summary>
    public bool JoinOverlappingLines(ICollection<Linedef> lines)
    {
        JoinOverlappingLinedefs(lines);
        return true;
    }

    /// <summary>
    /// Adds missing front/back sidedefs to changed lines that are fully inside an existing sector.
    /// New sides copy properties from the line's existing side. Returns the number of sides created.
    /// </summary>
    public int CorrectOuterSidedefs(ICollection<Linedef> changedLines)
    {
        int created = 0;
        foreach (var line in changedLines)
        {
            if (line.Front != null && line.Back != null) continue;

            var containingSector = GetSectorContaining(line);
            if (containingSector == null) continue;

            var source = line.Front ?? line.Back;
            if (source == null) continue;

            if (line.Front == null)
            {
                var side = AddSidedef(line, true, containingSector);
                CopySidedefProperties(source, side);
                created++;
            }

            if (line.Back == null)
            {
                var side = AddSidedef(line, false, containingSector);
                CopySidedefProperties(source, side);
                created++;
            }
        }

        return created;
    }

    /// <summary>
    /// Removes linedefs whose start, midpoint and end are fully inside changed sectors and whose sides do not
    /// already reference those sectors. This is used by UDB-style replace geometry stitching.
    /// </summary>
    public int RemoveLinedefsInsideSectors(
        ICollection<Linedef> lines,
        IEnumerable<Sector> sectors,
        ICollection<Linedef>? changedLines = null)
    {
        var sectorSet = new HashSet<Sector>(sectors, ReferenceEqualityComparer.Instance);
        if (lines.Count == 0 || sectorSet.Count == 0) return 0;

        int removed = 0;
        foreach (var line in lines.ToArray())
        {
            if (line.IsDisposed || line.Start == null || line.End == null) continue;
            if (line.Front?.Sector != null && sectorSet.Contains(line.Front.Sector)) continue;
            if (line.Back?.Sector != null && sectorSet.Contains(line.Back.Sector)) continue;

            foreach (var sector in sectorSet)
            {
                if (!SectorContainsLine(sector, line)) continue;

                while (!lines.IsReadOnly && lines.Remove(line))
                {
                }

                if (changedLines != null)
                {
                    while (!changedLines.IsReadOnly && changedLines.Remove(line))
                    {
                    }
                }

                RemoveLinedef(line);
                removed++;
                break;
            }
        }

        return removed;
    }

    /// <summary>
    /// Collapses split-created vertices that only bridge two linedefs by extending one linedef through the
    /// vertex, removing the other linedef, and removing the vertex. This matches UDB replace-mode cleanup.
    /// </summary>
    public int RemoveBridgeVertices(
        IEnumerable<Vertex> vertices,
        ICollection<Linedef>? lines = null,
        ICollection<Linedef>? changedLines = null)
    {
        int removed = 0;
        var candidates = new HashSet<Vertex>(vertices, ReferenceEqualityComparer.Instance);
        foreach (var vertex in candidates.ToArray())
        {
            if (vertex.IsDisposed || !Vertices.Contains(vertex)) continue;

            var incident = Linedefs
                .Where(line => !line.IsDisposed && (ReferenceEquals(line.Start, vertex) || ReferenceEquals(line.End, vertex)))
                .Take(3)
                .ToArray();
            if (incident.Length != 2) continue;

            var keep = incident[0];
            var remove = incident[1];
            var far = ReferenceEquals(remove.Start, vertex) ? remove.End : remove.Start;
            if (ReferenceEquals(far, vertex)) continue;

            if (ReferenceEquals(keep.Start, vertex)) keep.Start = far;
            else if (ReferenceEquals(keep.End, vertex)) keep.End = far;
            else continue;
            keep.Angle = Linedef.ComputeAngle(keep.Start, keep.End);

            if (lines != null)
            {
                while (!lines.IsReadOnly && lines.Remove(remove))
                {
                }
            }

            if (changedLines != null)
            {
                while (!changedLines.IsReadOnly && changedLines.Remove(remove))
                {
                }
            }

            RemoveLinedef(remove);
            if (Vertices.Remove(vertex))
            {
                DisposeElement(vertex);
                ReindexElements(Vertices);
            }
            removed++;
        }

        return removed;
    }

    private void MergeOverlappingLinedef(Linedef keep, Linedef remove, bool oppositeDirection)
    {
        var sourceFront = oppositeDirection ? remove.Back : remove.Front;
        var sourceBack = oppositeDirection ? remove.Front : remove.Back;

        TransferSidedef(keep, isFront: true, sourceFront);
        TransferSidedef(keep, isFront: false, sourceBack);

        remove.Front = null;
        remove.Back = null;
        if (Linedefs.Remove(remove))
        {
            DisposeElement(remove);
            ReindexElements(Linedefs);
        }
    }

    private void TransferSidedef(Linedef targetLine, bool isFront, Sidedef? source)
    {
        if (source == null) return;

        var target = isFront ? targetLine.Front : targetLine.Back;
        if (target != null)
        {
            RemoveSidedef(source);
            return;
        }

        if (source.Line.Front == source) source.Line.Front = null;
        if (source.Line.Back == source) source.Line.Back = null;
        source.Line = targetLine;
        source.IsFront = isFront;
        if (isFront) targetLine.Front = source;
        else targetLine.Back = source;
    }

    private bool SectorContainsLine(Sector sector, Linedef line)
    {
        return ReferenceEquals(sector, GetSectorAt(line.Start.Position, line)) &&
            ReferenceEquals(sector, GetSectorAt(line.GetCenterPoint(), line)) &&
            ReferenceEquals(sector, GetSectorAt(line.End.Position, line));
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
        if (removed > 0) ReindexElements(Vertices);
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
        if (removed > 0) ReindexSectors();
        return removed;
    }

    public int RemoveUnusedSectors(bool reportWarnings)
        => RemoveUnusedSectors();

    /// <summary>Removes unneeded textures from sidedefs. Returns the number of sidedefs changed.</summary>
    public int RemoveUnneededSidedefTextures(bool autoClearSidedefTextures)
    {
        BuildIndexes();
        int changed = 0;
        foreach (var side in Sidedefs)
        {
            if (side.RemoveUnneededTextures(side.Other != null, force: false, shiftMiddle: true, autoClearSidedefTextures))
                changed++;
        }

        return changed;
    }

    private static void CopySidedefProperties(Sidedef src, Sidedef dst)
    {
        dst.OffsetX = src.OffsetX;
        dst.OffsetY = src.OffsetY;
        dst.HighTexture = src.HighTexture;
        dst.MidTexture = src.MidTexture;
        dst.LowTexture = src.LowTexture;
        dst.LongHighTexture = src.LongHighTexture;
        dst.LongMiddleTexture = src.LongMiddleTexture;
        dst.LongLowTexture = src.LongLowTexture;
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

    private static void CopyIgnoredChecks(IMapElement src, IMapElement dst)
    {
        foreach (var check in src.IgnoredErrorChecks) dst.IgnoredErrorChecks.Add(check);
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

    public List<Sidedef> GetSidedefsFromSelectedLinedefs(bool selected)
    {
        var result = new List<Sidedef>();
        foreach (var line in Linedefs)
        {
            if (line.Selected != selected) continue;
            if (line.Front != null) result.Add(line.Front);
            if (line.Back != null) result.Add(line.Back);
        }
        return result;
    }

    public int SelectedVerticesCount => Count(Vertices);
    public int SelectedVerticessCount => SelectedVerticesCount;
    public int SelectedLinedefsCount => Count(Linedefs);
    public int SelectedSidedefsCount => Count(Sidedefs);
    public int SelectedSectorsCount => Count(Sectors);
    public int SelectedThingsCount => Count(Things);

    public void ClearSelectedVertices() { foreach (var v in Vertices) v.Selected = false; }
    public void ClearSelectedLinedefs() { foreach (var l in Linedefs) l.Selected = false; }
    public void ClearSelectedSidedefs() { foreach (var s in Sidedefs) s.Selected = false; }
    public void ClearSelectedSectors() { foreach (var s in Sectors) s.Selected = false; }
    public void ClearSelectedThings() { foreach (var t in Things) t.Selected = false; }

    public int KeepSelectedLinedefsBySidedness(bool doubleSided)
    {
        int kept = 0;
        foreach (var line in GetSelectedLinedefs())
        {
            bool lineIsDoubleSided = line.Front != null && line.Back != null;
            if (lineIsDoubleSided == doubleSided)
            {
                kept++;
                continue;
            }

            line.Selected = false;
        }

        return kept;
    }

    public int AlignSelectedLinedefs()
    {
        List<Linedef> selected = GetSelectedLinedefs();
        if (selected.Count == 0) return 0;

        var sectors = new Dictionary<Sector, int>(ReferenceEqualityComparer.Instance);
        foreach (var line in selected)
        {
            CountSector(line.Front?.Sector);
            CountSector(line.Back?.Sector);
        }

        List<Sector> orderedSectors = sectors
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToList();
        Tools.FlipSectorLinedefs(orderedSectors, selectedLinesOnly: true);

        if (selected.Count == 1)
            selected[0].Selected = false;

        return selected.Count;

        void CountSector(Sector? sector)
        {
            if (sector == null) return;
            sectors.TryGetValue(sector, out int count);
            sectors[sector] = count + 1;
        }
    }

    public int AlignLinedefsOfSectors(IReadOnlyList<Sector> sectors)
    {
        if (sectors.Count == 0) return 0;
        Tools.FlipSectorLinedefs(sectors.ToList(), selectedLinesOnly: false);
        return sectors.Count;
    }

    public int FlipLinedefsOfSectors(IReadOnlyList<Sector> sectors)
    {
        if (sectors.Count == 0) return 0;

        var lines = new HashSet<Linedef>();
        foreach (Sector sector in sectors)
        {
            foreach (Sidedef side in sector.Sidedefs)
            {
                Linedef line = side.Line;
                if (line.Back == null && line.Front != null) continue;
                lines.Add(line);
            }
        }

        foreach (Linedef line in lines)
        {
            line.FlipVertices();
            line.FlipSidedefs();
        }

        return lines.Count;
    }

    public void SelectAllVertices() => SetSelected(Vertices, true);
    public void SelectAllLinedefs() => SetSelected(Linedefs, true);
    public void SelectAllSidedefs() => SetSelected(Sidedefs, true);
    public void SelectAllSectors() => SetSelected(Sectors, true);
    public void SelectAllThings() => SetSelected(Things, true);

    public void SelectAll()
    {
        SelectAllVertices();
        SelectAllLinedefs();
        SelectAllSidedefs();
        SelectAllSectors();
        SelectAllThings();
    }

    public void InvertSelectedVertices() => InvertSelected(Vertices);
    public void InvertSelectedLinedefs() => InvertSelected(Linedefs);
    public void InvertSelectedSidedefs() => InvertSelected(Sidedefs);
    public void InvertSelectedSectors() => InvertSelected(Sectors);
    public void InvertSelectedThings() => InvertSelected(Things);

    public void InvertAllSelected()
    {
        InvertSelectedVertices();
        InvertSelectedLinedefs();
        InvertSelectedSidedefs();
        InvertSelectedSectors();
        InvertSelectedThings();
    }

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
        SelectMarkedSidedefs(mark, select);
        SelectMarkedSectors(mark, select);
        SelectMarkedThings(mark, select);
    }

    public void SelectMarkedVertices(bool mark, bool select) => SelectMarked(Vertices, mark, select);
    public void SelectMarkedLinedefs(bool mark, bool select) => SelectMarked(Linedefs, mark, select);
    public void SelectMarkedSidedefs(bool mark, bool select) => SelectMarked(Sidedefs, mark, select);
    public void SelectMarkedSectors(bool mark, bool select) => SelectMarked(Sectors, mark, select);
    public void SelectMarkedThings(bool mark, bool select) => SelectMarked(Things, mark, select);

    public void ConvertSelection(SelectionType target) => ConvertSelection(SelectionType.All, target);

    public void ConvertSelection(SelectionType source, SelectionType target)
    {
        ClearAllMarked(false);

        switch (target)
        {
            case SelectionType.Vertices:
                if (InSelectionType(source, SelectionType.Linedefs)) MarkSelectedLinedefs(selected: true, mark: true);
                if (InSelectionType(source, SelectionType.Sectors)) MarkSelectedSectors(selected: true, mark: true);
                foreach (var vertex in GetVerticesFromLinesMarks(mark: true)) vertex.Selected = true;
                foreach (var vertex in GetVerticesFromSectorsMarks(mark: true)) vertex.Selected = true;
                ClearSelectedSectors();
                ClearSelectedLinedefs();
                break;

            case SelectionType.Linedefs:
                if (InSelectionType(source, SelectionType.Vertices)) MarkSelectedVertices(selected: true, mark: true);
                if (!InSelectionType(source, SelectionType.Linedefs)) ClearSelectedLinedefs();
                foreach (var line in LinedefsFromMarkedVertices(includeUnmarked: false, includeStable: true, includeUnstable: false))
                    line.Selected = true;
                if (InSelectionType(source, SelectionType.Sectors))
                {
                    foreach (var sector in Sectors)
                    {
                        if (!sector.Selected) continue;
                        foreach (var side in sector.Sidedefs) side.Line.Selected = true;
                    }
                }
                ClearSelectedSectors();
                ClearSelectedVertices();
                break;

            case SelectionType.Sectors:
                if (InSelectionType(source, SelectionType.Vertices)) MarkSelectedVertices(selected: true, mark: true);
                if (!InSelectionType(source, SelectionType.Linedefs)) ClearSelectedLinedefs();
                foreach (var line in LinedefsFromMarkedVertices(includeUnmarked: false, includeStable: true, includeUnstable: false))
                    line.Selected = true;
                ClearMarkedSectors(true);
                foreach (var line in Linedefs)
                {
                    if (line.Selected) continue;
                    if (line.Front?.Sector != null) line.Front.Sector.Marked = false;
                    if (line.Back?.Sector != null) line.Back.Sector.Marked = false;
                }
                ClearSelectedLinedefs();
                ClearSelectedVertices();
                foreach (var sector in Sectors)
                {
                    if ((InSelectionType(source, SelectionType.Sectors) && sector.Selected) ||
                        (sector.Marked && sector.Sidedefs.Count > 0))
                    {
                        sector.Selected = true;
                        foreach (var side in sector.Sidedefs) side.Line.Selected = true;
                    }
                    else if (!InSelectionType(source, SelectionType.Sectors))
                    {
                        sector.Selected = false;
                    }
                }
                break;

            default:
                throw new ArgumentException("Unsupported selection target conversion", nameof(target));
        }
    }

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

    public SelectionGroupInfo GetGroupInfo(int groupIndex)
    {
        int mask = GroupMask(groupIndex);
        return new SelectionGroupInfo(
            groupIndex + 1,
            CountInGroup(Sectors, mask),
            CountInGroup(Linedefs, mask),
            CountInGroup(Vertices, mask),
            CountInGroup(Things, mask));
    }

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

    public void MarkSelectedVertices(bool selected, bool mark) => MarkSelected(Vertices, selected, mark);
    public void MarkSelectedLinedefs(bool selected, bool mark) => MarkSelected(Linedefs, selected, mark);
    public void MarkSelectedSidedefs(bool selected, bool mark) => MarkSelected(Sidedefs, selected, mark);
    public void MarkSelectedSectors(bool selected, bool mark) => MarkSelected(Sectors, selected, mark);
    public void MarkSelectedThings(bool selected, bool mark) => MarkSelected(Things, selected, mark);

    public int MarkedVerticesCount => CountMarked(Vertices);
    public int MarkedLinedefsCount => CountMarked(Linedefs);
    public int MarkedSidedefsCount => CountMarked(Sidedefs);
    public int MarkedSectorsCount => CountMarked(Sectors);
    public int MarkedThingsCount => CountMarked(Things);

    public void ClearMarkedVertices() => ClearMarkedVertices(false);
    public void ClearMarkedLinedefs() => ClearMarkedLinedefs(false);
    public void ClearMarkedSidedefs() => ClearMarkedSidedefs(false);
    public void ClearMarkedSectors() => ClearMarkedSectors(false);
    public void ClearMarkedThings() => ClearMarkedThings(false);

    public void ClearMarkedVertices(bool mark) { foreach (var v in Vertices) v.Marked = mark; }
    public void ClearMarkedLinedefs(bool mark) { foreach (var l in Linedefs) l.Marked = mark; }
    public void ClearMarkedSidedefs(bool mark) { foreach (var s in Sidedefs) s.Marked = mark; }
    public void ClearMarkedSectors(bool mark) { foreach (var s in Sectors) s.Marked = mark; }
    public void ClearMarkedThings(bool mark) { foreach (var t in Things) t.Marked = mark; }

    /// <summary>Clears the Marked flag on every element of every type.</summary>
    public void ClearAllMarked() => ClearAllMarked(false);

    public void ClearAllMarks() => ClearAllMarked();
    public void ClearAllMarks(bool mark) => ClearAllMarked(mark);

    /// <summary>Sets the Marked flag on every element of every type to <paramref name="mark"/>.</summary>
    public void ClearAllMarked(bool mark)
    {
        ClearMarkedVertices(mark);
        ClearMarkedLinedefs(mark);
        ClearMarkedSidedefs(mark);
        ClearMarkedSectors(mark);
        ClearMarkedThings(mark);
    }

    public void InvertMarkedVertices() { foreach (var v in Vertices) v.Marked = !v.Marked; }
    public void InvertMarkedLinedefs() { foreach (var l in Linedefs) l.Marked = !l.Marked; }
    public void InvertMarkedSidedefs() { foreach (var s in Sidedefs) s.Marked = !s.Marked; }
    public void InvertMarkedSectors() { foreach (var s in Sectors) s.Marked = !s.Marked; }
    public void InvertMarkedThings() { foreach (var t in Things) t.Marked = !t.Marked; }

    /// <summary>Inverts the Marked flag on every element of every type.</summary>
    public void InvertAllMarks() => InvertAllMarked();

    public void InvertAllMarked()
    {
        InvertMarkedVertices();
        InvertMarkedLinedefs();
        InvertMarkedSidedefs();
        InvertMarkedSectors();
        InvertMarkedThings();
    }

    public void MarkSidedefsFromLinedefs(bool matchMark, bool setMark)
    {
        foreach (var line in Linedefs)
        {
            if (line.Marked != matchMark) continue;
            if (line.Front != null) line.Front.Marked = setMark;
            if (line.Back != null) line.Back.Marked = setMark;
        }
    }

    public void MarkSidedefsFromSectors(bool matchMark, bool setMark)
    {
        foreach (var side in Sidedefs)
            if (side.Sector?.Marked == matchMark) side.Marked = setMark;
    }

    public List<Vertex> GetVerticesFromLinesMarks(bool mark)
    {
        var result = new List<Vertex>();
        foreach (var vertex in Vertices)
        {
            foreach (var line in vertex.Linedefs)
            {
                if (line.Marked != mark) continue;
                result.Add(vertex);
                break;
            }
        }
        return result;
    }

    public List<Vertex> GetVerticesFromAllLinesMarks(bool mark)
    {
        var result = new List<Vertex>();
        foreach (var vertex in Vertices)
        {
            bool qualified = true;
            foreach (var line in vertex.Linedefs)
            {
                if (line.Marked == mark) continue;
                qualified = false;
                break;
            }
            if (qualified) result.Add(vertex);
        }
        return result;
    }

    public List<Vertex> GetVerticesFromSectorsMarks(bool mark)
    {
        var result = new List<Vertex>();
        foreach (var vertex in Vertices)
        {
            foreach (var line in vertex.Linedefs)
            {
                if (line.Front?.Sector?.Marked == mark || line.Back?.Sector?.Marked == mark)
                {
                    result.Add(vertex);
                    break;
                }
            }
        }
        return result;
    }

    public List<Linedef> LinedefsFromMarkedVertices(bool includeUnmarked, bool includeStable, bool includeUnstable)
    {
        var result = new List<Linedef>();
        foreach (var line in Linedefs)
        {
            bool startMarked = line.Start.Marked;
            bool endMarked = line.End.Marked;
            if ((includeStable && startMarked && endMarked) ||
                (includeUnstable && startMarked != endMarked) ||
                (includeUnmarked && !startMarked && !endMarked))
            {
                result.Add(line);
            }
        }
        return result;
    }

    public static ICollection<Linedef> UnstableLinedefsFromVertices(ICollection<Vertex> vertices)
    {
        var lines = new Dictionary<Linedef, Linedef>();
        foreach (var vertex in vertices)
        {
            foreach (var line in vertex.Linedefs)
            {
                if (!lines.Remove(line)) lines.Add(line, line);
            }
        }
        return new List<Linedef>(lines.Values);
    }

    public HashSet<Sector> GetUnselectedSectorsFromLinedefs(IEnumerable<Linedef> linedefs)
    {
        return GetSectorsFromLinedefs(linedefs, excludeSelected: true);
    }

    public HashSet<Sector> GetSectorsFromLinedefs(IEnumerable<Linedef> linedefs)
    {
        return GetSectorsFromLinedefs(linedefs, excludeSelected: false);
    }

    public void MarkAllSelectedGeometry(
        bool mark,
        bool linedefsFromVertices,
        bool verticesFromLinedefs,
        bool sectorsFromLinedefs,
        bool sidedefsFromSectors)
    {
        ClearAllMarked(!mark);

        MarkSelectedVertices(selected: true, mark);
        MarkSelectedLinedefs(selected: true, mark);

        if (linedefsFromVertices)
        {
            foreach (var line in LinedefsFromMarkedVertices(includeUnmarked: !mark, includeStable: mark, includeUnstable: !mark))
                line.Marked = mark;
        }

        if (verticesFromLinedefs)
        {
            foreach (var vertex in GetVerticesFromLinesMarks(mark))
                vertex.Marked = mark;
        }

        if (sectorsFromLinedefs)
        {
            ClearMarkedSectors(mark);
            foreach (var line in Linedefs)
            {
                if (line.Selected) continue;
                if (line.Front?.Sector != null) line.Front.Sector.Marked = !mark;
                if (line.Back?.Sector != null) line.Back.Sector.Marked = !mark;
            }
        }

        MarkSelectedSectors(selected: true, mark);
        MarkSelectedThings(selected: true, mark);

        if (sidedefsFromSectors)
            MarkSidedefsFromSectors(matchMark: true, setMark: mark);
        else
            MarkSidedefsFromLinedefs(matchMark: true, setMark: mark);
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

    public bool ChangeVertexIndex(Vertex vertex, int newIndex)
        => ChangeIndex(Vertices, vertex, newIndex);

    public bool ChangeVertexIndex(int oldIndex, int newIndex)
        => ChangeIndex(Vertices, oldIndex, newIndex);

    public bool ChangeLinedefIndex(Linedef linedef, int newIndex)
        => ChangeIndex(Linedefs, linedef, newIndex);

    public bool ChangeLinedefIndex(int oldIndex, int newIndex)
        => ChangeIndex(Linedefs, oldIndex, newIndex);

    public bool ChangeLindefIndex(int oldIndex, int newIndex)
        => ChangeLinedefIndex(oldIndex, newIndex);

    public bool ChangeSidedefIndex(Sidedef sidedef, int newIndex)
        => ChangeIndex(Sidedefs, sidedef, newIndex);

    public bool ChangeSidedefIndex(int oldIndex, int newIndex)
        => ChangeIndex(Sidedefs, oldIndex, newIndex);

    public bool ChangeSectorIndex(Sector sector, int newIndex)
    {
        return ChangeIndex(Sectors, sector, newIndex);
    }

    public bool ChangeSectorIndex(int oldIndex, int newIndex)
        => ChangeIndex(Sectors, oldIndex, newIndex);

    public bool ChangeThingIndex(Thing thing, int newIndex)
        => ChangeIndex(Things, thing, newIndex);

    public bool ChangeThingIndex(int oldIndex, int newIndex)
        => ChangeIndex(Things, oldIndex, newIndex);

    private static bool ChangeIndex<T>(List<T> list, T item, int newIndex) where T : class, IMapElement
    {
        int oldIndex = list.IndexOf(item);
        return ChangeIndex(list, oldIndex, newIndex);
    }

    private static bool ChangeIndex<T>(List<T> list, int oldIndex, int newIndex) where T : class, IMapElement
    {
        if (oldIndex < 0 || oldIndex >= list.Count || newIndex < 0 || newIndex >= list.Count) return false;
        if (oldIndex == newIndex) return true;

        T item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, item);
        ReindexElements(list);
        return true;
    }

    /// <summary>Returns the first positive tag not used by any tagged map element, or 0 when none is available.</summary>
    public int GetNewTag(int maxTag = int.MaxValue)
        => GetNewTag(Array.Empty<int>(), maxTag);

    /// <summary>Returns the first positive tag not used by the map or the supplied extra used tags.</summary>
    public int GetNewTag(IEnumerable<int> moreUsedTags, int maxTag = int.MaxValue)
    {
        var used = CollectUsedTags(markedOnly: false);
        foreach (int tag in moreUsedTags)
            if (tag > 0) used.Add(tag);

        return FirstUnusedTag(used, maxTag);
    }

    /// <summary>Returns the first positive tag not used by marked geometry when <paramref name="markedOnly"/> is true.</summary>
    public int GetNewTag(bool markedOnly, int maxTag = int.MaxValue)
        => FirstUnusedTag(CollectUsedTags(markedOnly), maxTag);

    /// <summary>Returns the first positive tag not used by the requested tagged element owner type.</summary>
    public int GetNewTag(MapTagKind kind, int maxTag = int.MaxValue)
        => FirstUnusedTag(CollectUsedTags(kind), maxTag);

    /// <summary>Returns up to <paramref name="count"/> positive tags not used by any tagged map element.</summary>
    public List<int> GetMultipleNewTags(int count, int maxTag = int.MaxValue)
        => GetMultipleNewTags(count, markedOnly: false, maxTag);

    /// <summary>Returns up to <paramref name="count"/> positive tags not used by marked geometry when requested.</summary>
    public List<int> GetMultipleNewTags(int count, bool markedOnly, int maxTag = int.MaxValue)
    {
        var result = new List<int>(Math.Max(0, count));
        if (count <= 0) return result;

        var used = CollectUsedTags(markedOnly);
        for (int tag = 1; tag <= maxTag && result.Count < count; tag++)
        {
            if (used.Contains(tag)) continue;
            result.Add(tag);
            used.Add(tag);
        }
        return result;
    }

    private HashSet<int> CollectUsedTags(bool markedOnly)
    {
        var used = new HashSet<int>();
        foreach (var line in Linedefs)
            if (!markedOnly || line.Marked)
                foreach (int tag in MapElementTags.PositiveTags(line)) used.Add(tag);
        foreach (var sector in Sectors)
            if (!markedOnly || sector.Marked)
                foreach (int tag in MapElementTags.PositiveTags(sector)) used.Add(tag);
        foreach (var thing in Things)
            if (!markedOnly || thing.Marked)
                foreach (int tag in MapElementTags.PositiveTags(thing)) used.Add(tag);
        return used;
    }

    private HashSet<int> CollectUsedTags(MapTagKind kind)
    {
        var used = new HashSet<int>();
        switch (kind)
        {
            case MapTagKind.Linedef:
                foreach (var line in Linedefs)
                    foreach (int tag in MapElementTags.PositiveTags(line)) used.Add(tag);
                break;
            case MapTagKind.Sector:
                foreach (var sector in Sectors)
                    foreach (int tag in MapElementTags.PositiveTags(sector)) used.Add(tag);
                break;
            case MapTagKind.Thing:
                foreach (var thing in Things)
                    foreach (int tag in MapElementTags.PositiveTags(thing)) used.Add(tag);
                break;
        }
        return used;
    }

    private static int FirstUnusedTag(HashSet<int> used, int maxTag)
    {
        for (int tag = 1; tag <= maxTag; tag++)
            if (!used.Contains(tag)) return tag;
        return 0;
    }

    /// <summary>Snaps vertex and thing coordinates to the active map format accuracy.</summary>
    public void SnapAllToAccuracy()
        => SnapAllToAccuracy(usePrecisePosition: true);

    /// <summary>Snaps vertex and thing coordinates to UDB's default precise or integer accuracy.</summary>
    public void SnapAllToAccuracy(bool usePrecisePosition)
        => SnapAllToAccuracy(vertexDecimals: 3, usePrecisePosition);

    /// <summary>Snaps vertex and thing coordinates to the requested map format accuracy.</summary>
    public void SnapAllToAccuracy(int vertexDecimals, bool usePrecisePosition = true)
    {
        foreach (var vertex in Vertices) vertex.SnapToAccuracy(vertexDecimals, usePrecisePosition);
        foreach (var thing in Things) thing.SnapToAccuracy(vertexDecimals, usePrecisePosition);
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

    public int DissolveSelectedVertices()
    {
        var selected = Vertices.Where(vertex => vertex.Selected).ToList();
        int dissolved = 0;

        foreach (Vertex vertex in selected)
        {
            if (!Vertices.Contains(vertex)) continue;

            var lines = Linedefs
                .Where(line => ReferenceEquals(line.Start, vertex) || ReferenceEquals(line.End, vertex))
                .ToList();

            if (lines.Count == 2)
            {
                Vertex otherA = OtherVertex(lines[0], vertex);
                Vertex otherB = OtherVertex(lines[1], vertex);
                bool outerLineExists = Linedefs.Any(line =>
                    !ReferenceEquals(line, lines[0]) &&
                    !ReferenceEquals(line, lines[1]) &&
                    ((ReferenceEquals(line.Start, otherA) && ReferenceEquals(line.End, otherB)) ||
                     (ReferenceEquals(line.Start, otherB) && ReferenceEquals(line.End, otherA))));

                if (!outerLineExists)
                {
                    if (ReferenceEquals(lines[0].Start, vertex)) lines[0].SetStartVertex(otherB);
                    else lines[0].SetEndVertex(otherB);
                    RemoveLinedef(lines[1]);
                    if (Vertices.Remove(vertex))
                    {
                        DisposeElement(vertex);
                        ReindexElements(Vertices);
                    }
                    dissolved++;
                    continue;
                }
            }

            RemoveVertex(vertex);
            dissolved++;
        }

        return dissolved;
    }

    public int DissolveSelectedLinedefs()
    {
        var selected = Linedefs.Where(line => line.Selected).ToList();
        int dissolved = 0;

        foreach (Linedef line in selected)
        {
            if (!Linedefs.Contains(line)) continue;

            if (line.Front?.Sector != null && line.Back?.Sector != null && !ReferenceEquals(line.Front.Sector, line.Back.Sector))
                JoinSectors(new[] { line.Front.Sector, line.Back.Sector });

            RemoveLinedef(line);
            dissolved++;
        }

        RemoveUnusedVertices();
        return dissolved;
    }

    public int DissolveSelectedSectors()
        => DeleteSelection();

    private static Vertex OtherVertex(Linedef line, Vertex vertex)
        => ReferenceEquals(line.Start, vertex) ? line.End : line.Start;

    public SnapSelectionResult SnapSelectedMapElementsToGrid(Func<Vector2D, Vector2D> snap)
    {
        ArgumentNullException.ThrowIfNull(snap);

        var vertices = new HashSet<Vertex>();
        foreach (var vertex in GetSelectedVertices()) vertices.Add(vertex);
        foreach (var line in GetSelectedLinedefs())
        {
            vertices.Add(line.Start);
            vertices.Add(line.End);
        }

        int snappedVertices = 0;
        foreach (var vertex in vertices)
        {
            var snapped = snap(vertex.Position);
            if (snapped == vertex.Position) continue;
            vertex.Move(snapped);
            snappedVertices++;
        }

        int snappedThings = 0;
        foreach (var thing in GetSelectedThings())
        {
            var snapped = snap(thing.Position);
            if (snapped == thing.Position) continue;
            thing.Move(snapped);
            snappedThings++;
        }

        return new SnapSelectionResult(vertices.Count, GetSelectedThings().Count, snappedVertices, snappedThings);
    }

    /// <summary>Flips valid selected linedefs. Returns the number flipped. Call BuildIndexes() after.</summary>
    public int FlipSelectedLinedefs()
    {
        int n = 0;
        foreach (var l in Linedefs)
        {
            if (!l.Selected || (l.Back == null && l.Front != null)) continue;
            l.FlipVertices();
            l.FlipSidedefs();
            n++;
        }
        return n;
    }

    /// <summary>Swaps front/back sidedefs on selected two-sided linedefs. Returns the number swapped. Call BuildIndexes() after.</summary>
    public int FlipSelectedSidedefs()
    {
        int n = 0;
        foreach (var l in Linedefs)
        {
            if (!l.Selected || l.Front == null || l.Back == null) continue;
            l.FlipSidedefs();
            n++;
        }
        return n;
    }

    public static int FlipBackwardLinedefs(ICollection<Linedef> linedefs)
    {
        int flips = 0;
        foreach (var line in linedefs)
        {
            if (line.Back == null || line.Front != null) continue;
            line.FlipVertices();
            line.FlipSidedefs();
            flips++;
        }
        return flips;
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
            if (Things[i].Selected) { DisposeElement(Things[i]); Things.RemoveAt(i); removed++; }
        ReindexElements(Things);

        for (int i = Sectors.Count - 1; i >= 0; i--)
            if (Sectors[i].Selected) { RemoveSector(Sectors[i]); removed++; }

        for (int i = Linedefs.Count - 1; i >= 0; i--)
            if (Linedefs[i].Selected) { RemoveLinedef(Linedefs[i]); removed++; }

        for (int i = Vertices.Count - 1; i >= 0; i--)
            if (Vertices[i].Selected) { RemoveVertex(Vertices[i]); removed++; }

        return removed;
    }

    private List<(ISelectable Element, bool Selected)> SaveSelection()
    {
        var selection = new List<(ISelectable Element, bool Selected)>(
            Vertices.Count + Linedefs.Count + Sidedefs.Count + Sectors.Count + Things.Count);
        AddSelection(Vertices);
        AddSelection(Linedefs);
        AddSelection(Sidedefs);
        AddSelection(Sectors);
        AddSelection(Things);
        return selection;

        void AddSelection<T>(List<T> elements) where T : ISelectable
        {
            foreach (T element in elements)
                selection.Add((element, element.Selected));
        }
    }

    private static void RestoreSelection(List<(ISelectable Element, bool Selected)> selection)
    {
        foreach (var item in selection)
            item.Element.Selected = item.Selected;
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

    private static int CountInGroup<T>(List<T> items, int mask) where T : IGroupable
    {
        int n = 0;
        foreach (var it in items)
            if ((it.Groups & mask) != 0) n++;
        return n;
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

    private static bool InSelectionType(SelectionType value, SelectionType bits) => (value & bits) == bits;

    private HashSet<Sector> GetSectorsFromLinedefs(IEnumerable<Linedef> linedefs, bool excludeSelected)
    {
        var result = new HashSet<Sector>();
        var sectorsBySides = new Dictionary<Sector, HashSet<Sidedef>>();

        foreach (var line in linedefs)
        {
            AddSideSector(line.Front, sectorsBySides, excludeSelected);
            AddSideSector(line.Back, sectorsBySides, excludeSelected);
        }

        foreach (var group in sectorsBySides)
        {
            if (group.Key.Sidedefs.Count == group.Value.Count) result.Add(group.Key);
        }

        return result;
    }

    private static void AddSideSector(Sidedef? side, Dictionary<Sector, HashSet<Sidedef>> sectorsBySides, bool excludeSelected)
    {
        if (side?.Sector == null) return;
        if (excludeSelected && side.Sector.Selected) return;
        if (!sectorsBySides.TryGetValue(side.Sector, out var sides))
        {
            sides = new HashSet<Sidedef>();
            sectorsBySides.Add(side.Sector, sides);
        }
        sides.Add(side);
    }

    private static void MarkSelected<T>(List<T> items, bool selected, bool mark) where T : ISelectable, IMarkable
    {
        foreach (var it in items)
            if (it.Selected == selected) it.Marked = mark;
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
            double d = v.DistanceToSq(pos);
            if (d < bestSq) { bestSq = d; closest = v; }
        }
        return closest;
    }

    /// <summary>Nearest vertex to <paramref name="pos"/> from the supplied selection, or null when the selection is empty.</summary>
    public static Vertex? NearestVertex(ICollection<Vertex> selection, Vector2D pos)
    {
        Vertex? closest = null;
        double bestSq = double.MaxValue;
        foreach (var v in selection)
        {
            double d = v.DistanceToSq(pos);
            if (d < bestSq) { bestSq = d; closest = v; }
        }
        return closest;
    }

    /// <summary>Nearest vertex inside a square range, ranked by Manhattan distance like UDB.</summary>
    public static Vertex? NearestVertexSquareRange(ICollection<Vertex> selection, Vector2D pos, double maxRange)
    {
        var range = RectangleF.FromLTRB(
            (float)(pos.x - maxRange),
            (float)(pos.y - maxRange),
            (float)(pos.x + maxRange),
            (float)(pos.y + maxRange));
        Vertex? closest = null;
        double best = double.MaxValue;
        foreach (var v in selection)
        {
            double x = v.Position.x;
            double y = v.Position.y;
            if (x < range.Left || x > range.Right || y < range.Top || y > range.Bottom) continue;

            double d = Math.Abs(x - pos.x) + Math.Abs(y - pos.y);
            if (d < best) { best = d; closest = v; }
        }
        return closest;
    }

    /// <summary>Nearest map vertex inside a square range, ranked by Manhattan distance like UDB.</summary>
    public Vertex? NearestVertexSquareRange(Vector2D pos, double maxRange)
        => NearestVertexSquareRange(Vertices, pos, maxRange);

    /// <summary>Nearest thing to <paramref name="pos"/> within <paramref name="maxRange"/> units, or null if none.</summary>
    public Thing? NearestThing(Vector2D pos, double maxRange = double.MaxValue)
    {
        Thing? closest = null;
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        foreach (var t in Things)
        {
            double d = t.DistanceToSq(pos);
            if (d < bestSq) { bestSq = d; closest = t; }
        }
        return closest;
    }

    /// <summary>Nearest thing to <paramref name="pos"/> from the supplied selection, or null when the selection is empty.</summary>
    public static Thing? NearestThing(ICollection<Thing> selection, Vector2D pos)
    {
        Thing? closest = null;
        double bestSq = double.MaxValue;
        foreach (var t in selection)
        {
            double d = t.DistanceToSq(pos);
            if (d < bestSq) { bestSq = d; closest = t; }
        }
        return closest;
    }

    /// <summary>Nearest thing to <paramref name="thing"/> from the supplied selection, excluding <paramref name="thing"/> itself.</summary>
    public static Thing? NearestThing(ICollection<Thing> selection, Thing thing)
    {
        Thing? closest = null;
        double bestSq = double.MaxValue;
        foreach (var t in selection)
        {
            if (ReferenceEquals(t, thing)) continue;
            double d = t.DistanceToSq(thing.Position);
            if (d < bestSq) { bestSq = d; closest = t; }
        }
        return closest;
    }

    /// <summary>Nearest thing inside a square range, ranked by Manhattan distance and then smaller display size.</summary>
    public static Thing? NearestThingSquareRange(ICollection<Thing> selection, Vector2D pos, double maxRange)
    {
        var range = RectangleF.FromLTRB(
            (float)(pos.x - maxRange),
            (float)(pos.y - maxRange),
            (float)(pos.x + maxRange),
            (float)(pos.y + maxRange));
        Thing? closest = null;
        double best = double.MaxValue;
        double bestSize = double.MaxValue;
        foreach (var t in selection)
        {
            double x = t.Position.x;
            double y = t.Position.y;
            double size = t.Size;
            if (x < range.Left - size || x > range.Right + size || y < range.Top - size || y > range.Bottom + size) continue;

            double d = Math.Abs(x - pos.x) + Math.Abs(y - pos.y);
            if (d < best || (d == best && size < bestSize)) { best = d; bestSize = size; closest = t; }
        }
        return closest;
    }

    /// <summary>Nearest map thing inside a square range, ranked by Manhattan distance and then smaller display size.</summary>
    public Thing? NearestThingSquareRange(Vector2D pos, double maxRange)
        => NearestThingSquareRange(Things, pos, maxRange);

    /// <summary>Nearest linedef to <paramref name="pos"/> (bounded segment distance) within <paramref name="maxRange"/>, or null.</summary>
    public Linedef? NearestLinedef(Vector2D pos, double maxRange = double.MaxValue)
        => NearestLinedef(pos, maxRange, ignoredLine: null);

    /// <summary>Nearest linedef to <paramref name="pos"/> excluding the supplied linedefs, matching UDB MapSet.</summary>
    public Linedef? NearestLinedef(Vector2D pos, HashSet<Linedef> linesToExclude)
    {
        Linedef? closest = null;
        double bestSq = double.MaxValue;
        foreach (var line in Linedefs)
        {
            if (linesToExclude.Contains(line)) continue;
            double d = line.SafeDistanceToSq(pos, bounded: true);
            if (d < bestSq) { bestSq = d; closest = line; }
        }
        return closest;
    }

    /// <summary>Nearest linedef to <paramref name="pos"/> from the supplied selection, or null when the selection is empty.</summary>
    public static Linedef? NearestLinedef(ICollection<Linedef> selection, Vector2D pos)
    {
        Linedef? closest = null;
        double bestSq = double.MaxValue;
        foreach (var l in selection)
        {
            double d = l.SafeDistanceToSq(pos, bounded: true);
            if (d < bestSq) { bestSq = d; closest = l; }
        }
        return closest;
    }

    /// <summary>Nearest linedef to <paramref name="pos"/> from the supplied selection within <paramref name="maxRange"/>, or null.</summary>
    public static Linedef? NearestLinedefRange(ICollection<Linedef> selection, Vector2D pos, double maxRange)
    {
        Linedef? closest = null;
        double bestSq = double.MaxValue;
        double maxRangeSq = maxRange * maxRange;
        foreach (var l in selection)
        {
            double d = l.SafeDistanceToSq(pos, bounded: true);
            if (d < bestSq && d <= maxRangeSq) { bestSq = d; closest = l; }
        }
        return closest;
    }

    /// <summary>Nearest map linedef to <paramref name="pos"/> within <paramref name="maxRange"/>, or null.</summary>
    public Linedef? NearestLinedefRange(Vector2D pos, double maxRange)
        => NearestLinedefRange(Linedefs, pos, maxRange);

    /// <summary>Nearest linedef within range that is not connected to <paramref name="vertex"/>, matching UDB MapSet.</summary>
    public Linedef? NearestUnselectedUnreferencedLinedef(Vector2D pos, double maxRange, Vertex vertex, out double distance)
    {
        Linedef? closest = null;
        distance = double.MaxValue;
        double maxRangeSq = maxRange * maxRange;

        foreach (var line in Linedefs)
        {
            double d = line.SafeDistanceToSq(pos, bounded: true);
            if (d <= maxRangeSq && d < distance && line.Start != vertex && line.End != vertex)
            {
                closest = line;
                distance = d;
            }
        }

        return closest;
    }

    private Linedef? NearestLinedef(Vector2D pos, double maxRange, Linedef? ignoredLine)
    {
        Linedef? closest = null;
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        foreach (var l in Linedefs)
        {
            if (ReferenceEquals(l, ignoredLine)) continue;
            double d = l.SafeDistanceToSq(pos, bounded: true);
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

    /// <summary>Nearest sidedef from a collection, matching UDB's direct sidedef distance and tie behavior.</summary>
    public static Sidedef? NearestSidedef(ICollection<Sidedef> selection, Vector2D pos)
    {
        Sidedef? closest = null;
        double distance = double.MaxValue;

        foreach (var side in selection)
        {
            double d = side.Line.SafeDistanceToSq(pos, bounded: true);
            if (d == distance)
            {
                double lineSide = side.Line.SideOfLine(pos);
                if ((lineSide <= 0.0 && side.IsFront) || (lineSide > 0.0 && !side.IsFront))
                {
                    closest = side;
                    distance = d;
                }
            }
            else if (d < distance)
            {
                closest = side;
                distance = d;
            }
        }

        return closest;
    }

    /// <summary>
    /// Returns the sector containing <paramref name="pos"/>, determined via the nearest linedef's facing side.
    /// Assumes well-formed, closed sectors. Returns null when there are no linedefs or the facing side has no sector.
    /// </summary>
    public Sector? GetSectorAt(Vector2D pos)
    {
        return GetSectorAt(pos, ignoredLine: null);
    }

    /// <summary>Returns the first sector whose polygon contains <paramref name="pos"/>, matching UDB MapSet.</summary>
    public Sector? GetSectorByCoordinates(Vector2D pos)
    {
        foreach (var sector in Sectors)
        {
            if (sector.Intersect(pos)) return sector;
        }
        return null;
    }

    /// <summary>Returns the sector containing <paramref name="pos"/> using an accelerated blockmap lookup.</summary>
    public Sector? GetSectorByCoordinates(Vector2D pos, BlockMap blockMap)
        => blockMap.GetSectorAt(pos);

    private Sector? GetSectorAt(Vector2D pos, Linedef? ignoredLine)
    {
        var line = NearestLinedef(pos, double.MaxValue, ignoredLine);
        if (line == null) return null;
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) < 0;
        // The facing side determines the sector. A null facing side means the point is in the void
        // (outside a one-sided wall), so return null rather than the wall's own sector.
        var side = front ? line.Front : line.Back;
        return side?.Sector;
    }

    /// <summary>
    /// Returns the sector that contains a linedef's start, midpoint, and end, or null when it crosses a boundary.
    /// </summary>
    public Sector? GetSectorContaining(Linedef line)
    {
        Linedef? ignoredLine = Linedefs.Contains(line) ? line : null;
        var start = GetSectorAt(line.Start.Position, ignoredLine);
        if (start == null) return null;
        if (!ReferenceEquals(start, GetSectorAt(line.GetCenterPoint(), ignoredLine))) return null;
        if (!ReferenceEquals(start, GetSectorAt(line.End.Position, ignoredLine))) return null;
        return start;
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

    public static RectangleF CreateEmptyArea()
    {
        return new RectangleF(float.MaxValue / 2, float.MaxValue / 2, -float.MaxValue, -float.MaxValue);
    }

    public static RectangleF CreateArea(ICollection<Vertex> vertices)
    {
        RectangleF area = CreateEmptyArea();
        return IncreaseArea(area, vertices);
    }

    public static RectangleF CreateArea(ICollection<Linedef> linedefs)
    {
        RectangleF area = CreateEmptyArea();
        return IncreaseArea(area, linedefs);
    }

    public static RectangleF IncreaseArea(RectangleF area, ICollection<Vertex> vertices)
    {
        float left = area.Left;
        float top = area.Top;
        float right = area.Right;
        float bottom = area.Bottom;

        foreach (var vertex in vertices)
            AdjustArea(vertex.Position, ref left, ref top, ref right, ref bottom);

        return new RectangleF(left, top, right - left, bottom - top);
    }

    public static RectangleF IncreaseArea(RectangleF area, ICollection<Linedef> linedefs)
    {
        float left = area.Left;
        float top = area.Top;
        float right = area.Right;
        float bottom = area.Bottom;

        foreach (var line in linedefs)
        {
            AdjustArea(line.Start.Position, ref left, ref top, ref right, ref bottom);
            AdjustArea(line.End.Position, ref left, ref top, ref right, ref bottom);
        }

        return new RectangleF(left, top, right - left, bottom - top);
    }

    public static RectangleF IncreaseArea(RectangleF area, ICollection<Thing> things)
    {
        float left = area.Left;
        float top = area.Top;
        float right = area.Right;
        float bottom = area.Bottom;

        foreach (var thing in things)
            AdjustArea(thing.Position, ref left, ref top, ref right, ref bottom);

        return new RectangleF(left, top, right - left, bottom - top);
    }

    public static RectangleF IncreaseArea(RectangleF area, ICollection<Vector2D> vertices)
    {
        float left = area.Left;
        float top = area.Top;
        float right = area.Right;
        float bottom = area.Bottom;

        foreach (var vertex in vertices)
            AdjustArea(vertex, ref left, ref top, ref right, ref bottom);

        return new RectangleF(left, top, right - left, bottom - top);
    }

    public static RectangleF IncreaseArea(RectangleF area, Vector2D vertex)
    {
        float left = area.Left;
        float top = area.Top;
        float right = area.Right;
        float bottom = area.Bottom;
        AdjustArea(vertex, ref left, ref top, ref right, ref bottom);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    public static int GetCSFieldBits(Vector2D vertex, RectangleF area)
    {
        int bits = 0;
        if (vertex.y < area.Top) bits |= 0x01;
        if (vertex.y > area.Bottom) bits |= 0x02;
        if (vertex.x < area.Left) bits |= 0x04;
        if (vertex.x > area.Right) bits |= 0x08;
        return bits;
    }

    public static HashSet<Linedef> FilterByArea(ICollection<Linedef> linedefs, ref RectangleF area)
    {
        var result = new HashSet<Linedef>();
        foreach (var line in linedefs)
        {
            if ((GetCSFieldBits(line.Start.Position, area) & GetCSFieldBits(line.End.Position, area)) == 0)
                result.Add(line);
        }
        return result;
    }

    public static ICollection<Vertex> FilterByArea(ICollection<Vertex> vertices, ref RectangleF area)
    {
        var result = new List<Vertex>(vertices.Count);
        foreach (var vertex in vertices)
        {
            if (vertex.Position.x < area.Left || vertex.Position.x > area.Right ||
                vertex.Position.y < area.Top || vertex.Position.y > area.Bottom)
            {
                continue;
            }
            result.Add(vertex);
        }
        return result;
    }

    private static void AdjustArea(Vector2D vertex, ref float left, ref float top, ref float right, ref float bottom)
    {
        if (vertex.x < left) left = (float)vertex.x;
        if (vertex.x > right) right = (float)vertex.x;
        if (vertex.y < top) top = (float)vertex.y;
        if (vertex.y > bottom) bottom = (float)vertex.y;
    }
}
