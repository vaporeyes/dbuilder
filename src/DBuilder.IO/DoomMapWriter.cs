// ABOUTME: Doom-binary map writer - inverse of DoomMapLoader. Produces Doom map lumps from a MapSet.
// ABOUTME: Vertex/sidedef/sector indices are resolved against the MapSet's list ordering via a one-pass dictionary build so writes stay O(n) overall.

/*
 * Round-trip is byte-exact when the MapSet hasn't been modified after loading: writing the result of
 * DoomMapLoader.Load produces the same lump bodies the loader consumed.  Mutating fields between read
 * and write changes the written bytes (intentionally - the writer reflects current map state).
 *
 * Map-format coverage: standard 1993 Doom binary (10-byte THINGS + 14-byte LINEDEFS).  Hexen-format
 * extensions (16-byte LINEDEFS with args[5], 20-byte THINGS with tid/z/args) go in HexenMapWriter.
 */

using System.Collections.Generic;
using System.IO;
using System.Text;
using DBuilder.Map;

namespace DBuilder.IO;

public static class DoomMapWriter
{
    public const int VertexRecordSize = 4;
    public const int SectorRecordSize = 26;
    public const int SidedefRecordSize = 30;
    public const int LinedefRecordSize = 14;
    public const int ThingRecordSize = 10;

    /// <summary>
    /// Writes the map's core lumps into <paramref name="wad"/> as a Doom-format map block.  Inserts
    /// a zero-length <paramref name="markerName"/> lump at <paramref name="insertPos"/> followed by THINGS,
    /// LINEDEFS, SIDEDEFS, VERTEXES, SECTORS, REJECT, BLOCKMAP - the canonical Doom order.
    /// </summary>
    public static void WriteMap(MapSet map, WAD wad, string markerName, int insertPos)
    {
        if (wad.IsReadOnly) throw new System.IO.IOException("WAD is read-only");

        byte[] thingsBytes = WriteThings(map);
        byte[] linedefsBytes = WriteLinedefs(map);
        byte[] sidedefsBytes = WriteSidedefs(map);
        byte[] vertexesBytes = WriteVertexes(map);
        byte[] sectorsBytes  = WriteSectors(map);

        int pos = insertPos;
        wad.Insert(markerName, pos++, 0);
        InsertLump(wad, "THINGS",   thingsBytes,   pos++);
        InsertLump(wad, "LINEDEFS", linedefsBytes, pos++);
        InsertLump(wad, "SIDEDEFS", sidedefsBytes, pos++);
        InsertLump(wad, "VERTEXES", vertexesBytes, pos++);
        InsertLump(wad, "SECTORS",  sectorsBytes,  pos++);
        InsertLump(wad, "REJECT",   System.Array.Empty<byte>(), pos++);
        InsertLump(wad, "BLOCKMAP", System.Array.Empty<byte>(), pos++);
        wad.WriteHeaders();
    }

    public static byte[] WriteVertexes(MapSet map)
    {
        using var ms = new MemoryStream(map.Vertices.Count * VertexRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var v in map.Vertices)
        {
            w.Write((short)v.Position.x);
            w.Write((short)v.Position.y);
        }
        return ms.ToArray();
    }

    public static byte[] WriteSectors(MapSet map)
    {
        using var ms = new MemoryStream(map.Sectors.Count * SectorRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var s in map.Sectors)
        {
            w.Write((short)s.FloorHeight);
            w.Write((short)s.CeilHeight);
            w.Write(FixedString(s.FloorTexture, 8));
            w.Write(FixedString(s.CeilTexture, 8));
            w.Write((short)s.Brightness);
            w.Write((ushort)s.Special);
            w.Write((ushort)s.Tag);
        }
        return ms.ToArray();
    }

    public static byte[] WriteSidedefs(MapSet map)
    {
        var sectorIndex = BuildIndex(map.Sectors);

        using var ms = new MemoryStream(map.Sidedefs.Count * SidedefRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var sd in map.Sidedefs)
        {
            w.Write((short)sd.OffsetX);
            w.Write((short)sd.OffsetY);
            w.Write(FixedString(sd.HighTexture, 8));
            w.Write(FixedString(sd.LowTexture, 8));
            w.Write(FixedString(sd.MidTexture, 8));
            w.Write((ushort)RequireIndex(sectorIndex, sd.Sector, "sidedef sector"));
        }
        return ms.ToArray();
    }

    public static byte[] WriteLinedefs(MapSet map)
    {
        var vertexIndex = BuildIndex(map.Vertices);
        var sidedefIndex = BuildIndex(map.Sidedefs);

        using var ms = new MemoryStream(map.Linedefs.Count * LinedefRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var l in map.Linedefs)
        {
            ushort v1 = (ushort)RequireIndex(vertexIndex, l.Start, "linedef start vertex");
            ushort v2 = (ushort)RequireIndex(vertexIndex, l.End, "linedef end vertex");
            ushort sideRight = l.Front != null && sidedefIndex.TryGetValue(l.Front, out int srI) ? (ushort)srI : ushort.MaxValue;
            ushort sideLeft  = l.Back  != null && sidedefIndex.TryGetValue(l.Back,  out int slI) ? (ushort)slI : ushort.MaxValue;
            w.Write(v1);
            w.Write(v2);
            w.Write((ushort)l.Flags);
            w.Write((ushort)l.Action);
            w.Write((ushort)l.Tag);
            w.Write(sideRight);
            w.Write(sideLeft);
        }
        return ms.ToArray();
    }

    public static byte[] WriteThings(MapSet map)
    {
        using var ms = new MemoryStream(map.Things.Count * ThingRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var t in map.Things)
        {
            w.Write((short)t.Position.x);
            w.Write((short)t.Position.y);
            w.Write((short)t.Angle);
            w.Write((short)t.Type);
            w.Write((ushort)t.Flags);
        }
        return ms.ToArray();
    }

    private static Dictionary<T, int> BuildIndex<T>(IReadOnlyList<T> list) where T : class
    {
        var dict = new Dictionary<T, int>(list.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < list.Count; i++) dict[list[i]] = i;
        return dict;
    }

    private static int RequireIndex<T>(Dictionary<T, int> indexes, T? item, string description) where T : class
    {
        if (item != null && indexes.TryGetValue(item, out int index)) return index;
        throw new InvalidDataException("Cannot write Doom map with missing " + description + ".");
    }

    private static byte[] FixedString(string s, int length)
    {
        var bytes = new byte[length];
        if (string.IsNullOrEmpty(s)) return bytes;
        var src = Encoding.ASCII.GetBytes(s.ToUpperInvariant());
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }

    private static void InsertLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        lump.Stream.Write(data, 0, data.Length);
    }
}
