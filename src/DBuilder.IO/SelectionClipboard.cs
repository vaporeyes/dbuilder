// ABOUTME: Copy/paste of a map selection - gathers the dependency closure of selected elements and round-trips it.
// ABOUTME: Builds on ClipboardStreamWriter/Reader; paste appends, offsets the new geometry, and selects it.

using System.Collections.Generic;
using System.IO;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class SelectionClipboard
{
    /// <summary>
    /// Serializes the current selection (and the geometry it depends on) to a clipboard buffer, or null when
    /// nothing is selected. Selected linedefs pull in their vertices/sidedefs/sectors; selected sectors pull in
    /// their bounding lines, sidedefs and vertices; selected vertices and things are taken as-is.
    /// </summary>
    public static byte[]? CopySelection(MapSet map)
    {
        var vertices = new OrderedSet<Vertex>();
        var sidedefs = new OrderedSet<Sidedef>();
        var sectors = new OrderedSet<Sector>();
        var linedefs = new OrderedSet<Linedef>();
        var things = new OrderedSet<Thing>();

        foreach (var t in map.Things) if (t.Selected) things.Add(t);
        foreach (var v in map.Vertices) if (v.Selected) vertices.Add(v);

        foreach (var l in map.Linedefs)
        {
            if (!l.Selected) continue;
            AddLine(l, vertices, sidedefs, sectors, linedefs);
        }

        foreach (var s in map.Sectors)
        {
            if (!s.Selected) continue;
            sectors.Add(s);
            foreach (var sd in map.Sidedefs)
            {
                if (!ReferenceEquals(sd.Sector, s)) continue;
                sidedefs.Add(sd);
                if (sd.Line != null) AddLine(sd.Line, vertices, sidedefs, sectors, linedefs);
            }
        }

        if (vertices.Count + sectors.Count + linedefs.Count + things.Count == 0) return null;

        using var ms = new MemoryStream();
        ClipboardStreamWriter.Write(vertices.Items, linedefs.Items, sidedefs.Items, sectors.Items, things.Items, ms);
        return ms.ToArray();
    }

    private static void AddLine(Linedef l, OrderedSet<Vertex> vertices, OrderedSet<Sidedef> sidedefs,
        OrderedSet<Sector> sectors, OrderedSet<Linedef> linedefs)
    {
        linedefs.Add(l);
        vertices.Add(l.Start);
        vertices.Add(l.End);
        if (l.Front != null) { sidedefs.Add(l.Front); if (l.Front.Sector != null) sectors.Add(l.Front.Sector); }
        if (l.Back != null) { sidedefs.Add(l.Back); if (l.Back.Sector != null) sectors.Add(l.Back.Sector); }
    }

    /// <summary>
    /// Pastes a clipboard buffer into <paramref name="map"/>, translating the new geometry by <paramref name="offset"/>,
    /// then clears the prior selection and selects everything pasted. Returns the appended slice.
    /// </summary>
    public static PasteResult Paste(MapSet map, byte[] data, Vector2D offset)
    {
        using var ms = new MemoryStream(data);
        var result = ClipboardStreamReader.Read(map, ms);

        for (int i = result.FirstVertex; i < result.FirstVertex + result.VertexCount; i++)
            map.Vertices[i].Position += offset;
        for (int i = result.FirstThing; i < result.FirstThing + result.ThingCount; i++)
            map.Things[i].Position += offset;

        map.ClearAllSelected();
        for (int i = result.FirstVertex; i < result.FirstVertex + result.VertexCount; i++) map.Vertices[i].Selected = true;
        for (int i = result.FirstLinedef; i < result.FirstLinedef + result.LinedefCount; i++) map.Linedefs[i].Selected = true;
        for (int i = result.FirstSector; i < result.FirstSector + result.SectorCount; i++) map.Sectors[i].Selected = true;
        for (int i = result.FirstThing; i < result.FirstThing + result.ThingCount; i++) map.Things[i].Selected = true;

        return result;
    }

    /// <summary>
    /// Pastes a buffer so the pasted geometry's lower-left corner lands at <paramref name="anchor"/> (origin
    /// independent - used for prefab insertion at the cursor). Returns the appended slice.
    /// </summary>
    public static PasteResult PasteAtAnchor(MapSet map, byte[] data, Vector2D anchor)
    {
        var res = Paste(map, data, new Vector2D(0, 0));

        double minX = double.MaxValue, minY = double.MaxValue;
        bool any = false;
        for (int i = res.FirstVertex; i < res.FirstVertex + res.VertexCount; i++)
        {
            var p = map.Vertices[i].Position;
            if (p.x < minX) minX = p.x; if (p.y < minY) minY = p.y; any = true;
        }
        for (int i = res.FirstThing; i < res.FirstThing + res.ThingCount; i++)
        {
            var p = map.Things[i].Position;
            if (p.x < minX) minX = p.x; if (p.y < minY) minY = p.y; any = true;
        }
        if (!any) return res;

        var delta = new Vector2D(anchor.x - minX, anchor.y - minY);
        for (int i = res.FirstVertex; i < res.FirstVertex + res.VertexCount; i++) map.Vertices[i].Position += delta;
        for (int i = res.FirstThing; i < res.FirstThing + res.ThingCount; i++) map.Things[i].Position += delta;
        return res;
    }

    // Insertion-ordered set so the serialized subset's indices are stable and references resolve positionally.
    private sealed class OrderedSet<T> where T : class
    {
        private readonly HashSet<T> seen = new(ReferenceEqualityComparer.Instance);
        private readonly List<T> items = new();
        public IReadOnlyList<T> Items => items;
        public int Count => items.Count;
        public void Add(T item) { if (seen.Add(item)) items.Add(item); }
    }
}
