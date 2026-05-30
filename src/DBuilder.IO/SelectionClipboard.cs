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

        foreach (var sd in map.Sidedefs)
        {
            if (!sd.Selected) continue;
            AddSidedef(sd, vertices, sidedefs, sectors, linedefs);
        }

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

        if (vertices.Count + sectors.Count + linedefs.Count + sidedefs.Count + things.Count == 0) return null;

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

    private static void AddSidedef(Sidedef sd, OrderedSet<Vertex> vertices, OrderedSet<Sidedef> sidedefs,
        OrderedSet<Sector> sectors, OrderedSet<Linedef> linedefs)
    {
        sidedefs.Add(sd);
        if (sd.Sector != null) sectors.Add(sd.Sector);
        if (sd.Line == null) return;

        linedefs.Add(sd.Line);
        vertices.Add(sd.Line.Start);
        vertices.Add(sd.Line.End);
    }

    /// <summary>
    /// Pastes a clipboard buffer into <paramref name="map"/>, translating the new geometry by <paramref name="offset"/>,
    /// then clears the prior selection and selects everything pasted. Returns the appended slice.
    /// </summary>
    public static PasteResult Paste(MapSet map, byte[] data, Vector2D offset)
        => Paste(map, data, offset, new PasteOptions());

    public static PasteResult Paste(MapSet map, byte[] data, Vector2D offset, PasteOptions options)
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
        for (int i = result.FirstSidedef; i < result.FirstSidedef + result.SidedefCount; i++) map.Sidedefs[i].Selected = true;
        for (int i = result.FirstSector; i < result.FirstSector + result.SectorCount; i++) map.Sectors[i].Selected = true;
        for (int i = result.FirstThing; i < result.FirstThing + result.ThingCount; i++) map.Things[i].Selected = true;

        ApplyOptions(map, result, options);
        return result;
    }

    /// <summary>
    /// Duplicates the current selection in-place by serializing the dependency closure and appending a pasted copy.
    /// Returns null when no selectable elements are selected.
    /// </summary>
    public static PasteResult? DuplicateSelection(MapSet map, Vector2D offset, Action? beforePaste = null)
        => DuplicateSelection(map, offset, new PasteOptions(), beforePaste);

    public static PasteResult? DuplicateSelection(MapSet map, Vector2D offset, PasteOptions options, Action? beforePaste = null)
    {
        var data = CopySelection(map);
        if (data is null) return null;
        beforePaste?.Invoke();
        return Paste(map, data, offset, options);
    }

    /// <summary>
    /// Pastes a buffer so the pasted geometry's lower-left corner lands at <paramref name="anchor"/> (origin
    /// independent - used for prefab insertion at the cursor). Returns the appended slice.
    /// </summary>
    public static PasteResult PasteAtAnchor(MapSet map, byte[] data, Vector2D anchor)
    {
        var res = Paste(map, data, new Vector2D(0, 0));
        MovePasteToAnchor(map, res, anchor);
        return res;
    }

    public static PasteResult PasteAtAnchor(MapSet map, byte[] data, Vector2D anchor, PasteOptions options)
    {
        var res = Paste(map, data, new Vector2D(0, 0), options);
        MovePasteToAnchor(map, res, anchor);
        return res;
    }

    private static void MovePasteToAnchor(MapSet map, PasteResult res, Vector2D anchor)
    {
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
        if (!any) return;

        var delta = new Vector2D(anchor.x - minX, anchor.y - minY);
        for (int i = res.FirstVertex; i < res.FirstVertex + res.VertexCount; i++) map.Vertices[i].Position += delta;
        for (int i = res.FirstThing; i < res.FirstThing + res.ThingCount; i++) map.Things[i].Position += delta;
    }

    private static void ApplyOptions(MapSet map, PasteResult result, PasteOptions options)
    {
        if (options.ChangeTags == PasteTagMode.Remove) RemovePastedTags(map, result);
        else if (options.ChangeTags == PasteTagMode.Renumber) RenumberPastedTags(map, result);

        if (options.RemoveActions) RemovePastedActions(map, result);
    }

    private static void RemovePastedTags(MapSet map, PasteResult result)
    {
        foreach (var line in PastedLinedefs(map, result)) line.Tags.Clear();
        foreach (var sector in PastedSectors(map, result)) sector.Tags.Clear();
        foreach (var thing in PastedThings(map, result)) thing.Tag = 0;
    }

    private static void RenumberPastedTags(MapSet map, PasteResult result)
    {
        var used = CollectTagsOutsidePaste(map, result);
        var remap = new Dictionary<int, int>();

        void MapTag(int tag)
        {
            if (tag <= 0 || remap.ContainsKey(tag)) return;
            int newTag = FirstUnusedTag(used);
            remap.Add(tag, newTag);
            used.Add(newTag);
        }

        foreach (var line in PastedLinedefs(map, result))
            foreach (int tag in MapElementTags.PositiveTags(line)) MapTag(tag);
        foreach (var sector in PastedSectors(map, result))
            foreach (int tag in MapElementTags.PositiveTags(sector)) MapTag(tag);
        foreach (var thing in PastedThings(map, result))
            foreach (int tag in MapElementTags.PositiveTags(thing)) MapTag(tag);

        foreach (var line in PastedLinedefs(map, result)) ReplaceTags(line.Tags, remap);
        foreach (var sector in PastedSectors(map, result)) ReplaceTags(sector.Tags, remap);
        foreach (var thing in PastedThings(map, result))
            if (remap.TryGetValue(thing.Tag, out int newTag)) thing.Tag = newTag;
    }

    private static void RemovePastedActions(MapSet map, PasteResult result)
    {
        foreach (var line in PastedLinedefs(map, result))
        {
            line.Action = 0;
            System.Array.Clear(line.Args);
        }

        foreach (var thing in PastedThings(map, result))
        {
            thing.Action = 0;
            System.Array.Clear(thing.Args);
        }
    }

    private static HashSet<int> CollectTagsOutsidePaste(MapSet map, PasteResult result)
    {
        var used = new HashSet<int>();
        for (int i = 0; i < map.Linedefs.Count; i++)
        {
            if (i >= result.FirstLinedef && i < result.FirstLinedef + result.LinedefCount) continue;
            foreach (int tag in MapElementTags.PositiveTags(map.Linedefs[i])) used.Add(tag);
        }

        for (int i = 0; i < map.Sectors.Count; i++)
        {
            if (i >= result.FirstSector && i < result.FirstSector + result.SectorCount) continue;
            foreach (int tag in MapElementTags.PositiveTags(map.Sectors[i])) used.Add(tag);
        }

        for (int i = 0; i < map.Things.Count; i++)
        {
            if (i >= result.FirstThing && i < result.FirstThing + result.ThingCount) continue;
            foreach (int tag in MapElementTags.PositiveTags(map.Things[i])) used.Add(tag);
        }

        return used;
    }

    private static int FirstUnusedTag(HashSet<int> used)
    {
        int tag = 1;
        while (used.Contains(tag)) tag++;
        return tag;
    }

    private static void ReplaceTags(List<int> tags, IReadOnlyDictionary<int, int> remap)
    {
        for (int i = 0; i < tags.Count; i++)
            if (remap.TryGetValue(tags[i], out int newTag)) tags[i] = newTag;
    }

    private static IEnumerable<Linedef> PastedLinedefs(MapSet map, PasteResult result)
    {
        for (int i = result.FirstLinedef; i < result.FirstLinedef + result.LinedefCount; i++) yield return map.Linedefs[i];
    }

    private static IEnumerable<Sector> PastedSectors(MapSet map, PasteResult result)
    {
        for (int i = result.FirstSector; i < result.FirstSector + result.SectorCount; i++) yield return map.Sectors[i];
    }

    private static IEnumerable<Thing> PastedThings(MapSet map, PasteResult result)
    {
        for (int i = result.FirstThing; i < result.FirstThing + result.ThingCount; i++) yield return map.Things[i];
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
