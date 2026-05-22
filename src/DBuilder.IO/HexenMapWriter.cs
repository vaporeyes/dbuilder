// ABOUTME: Hexen-binary map writer - inverse of HexenMapLoader. 16-byte LINEDEFS with args[5], 20-byte THINGS with tid/z/args.
// ABOUTME: VERTEXES/SIDEDEFS/SECTORS lumps reuse Doom format via DoomMapWriter; a BEHAVIOR lump is emitted to mark the map as Hexen.

/*
 * Hexen/ZDoom binary format:
 *   VERTEXES  4-byte:  int16 x, int16 y                        (Doom)
 *   SECTORS  26-byte:  same as Doom                            (Doom)
 *   SIDEDEFS 30-byte:  same as Doom                            (Doom)
 *   LINEDEFS 16-byte:  uint16 v1, v2, flags; byte action, args[5]; uint16 s1, s2
 *   THINGS   20-byte:  uint16 tid; int16 x, y, z, angle, type; uint16 flags;
 *                      byte action, args[5]
 *
 * Round-trip is byte-exact when the MapSet hasn't been modified after loading.
 */

using System.Collections.Generic;
using System.IO;
using DBuilder.Map;

namespace DBuilder.IO;

public static class HexenMapWriter
{
    public const int LinedefRecordSize = 16;
    public const int ThingRecordSize = 20;

    /// <summary>
    /// Writes the map's six lumps (THINGS, LINEDEFS, SIDEDEFS, VERTEXES, SECTORS, BEHAVIOR) into <paramref name="wad"/>
    /// preceded by a zero-length <paramref name="markerName"/> marker at <paramref name="insertPos"/>.
    /// The BEHAVIOR lump is emitted with <paramref name="behaviorBytes"/> (compiled ACS) or an empty payload when null.
    /// </summary>
    public static void WriteMap(MapSet map, WAD wad, string markerName, int insertPos, byte[]? behaviorBytes = null)
    {
        if (wad.IsReadOnly) throw new System.IO.IOException("WAD is read-only");

        byte[] thingsBytes = WriteThings(map);
        byte[] linedefsBytes = WriteLinedefs(map);
        byte[] sidedefsBytes = DoomMapWriter.WriteSidedefs(map);
        byte[] vertexesBytes = DoomMapWriter.WriteVertexes(map);
        byte[] sectorsBytes  = DoomMapWriter.WriteSectors(map);

        int pos = insertPos;
        wad.Insert(markerName, pos++, 0);
        InsertLump(wad, "THINGS",   thingsBytes,   pos++);
        InsertLump(wad, "LINEDEFS", linedefsBytes, pos++);
        InsertLump(wad, "SIDEDEFS", sidedefsBytes, pos++);
        InsertLump(wad, "VERTEXES", vertexesBytes, pos++);
        InsertLump(wad, "SECTORS",  sectorsBytes,  pos++);
        InsertLump(wad, "BEHAVIOR", behaviorBytes ?? System.Array.Empty<byte>(), pos++);
        wad.WriteHeaders();
    }

    public static byte[] WriteLinedefs(MapSet map)
    {
        var vertexIndex = BuildIndex(map.Vertices);
        var sidedefIndex = BuildIndex(map.Sidedefs);

        using var ms = new MemoryStream(map.Linedefs.Count * LinedefRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var l in map.Linedefs)
        {
            ushort v1 = vertexIndex.TryGetValue(l.Start, out int v1i) ? (ushort)v1i : (ushort)0;
            ushort v2 = vertexIndex.TryGetValue(l.End,   out int v2i) ? (ushort)v2i : (ushort)0;
            ushort sideFront = l.Front != null && sidedefIndex.TryGetValue(l.Front, out int srI) ? (ushort)srI : (ushort)0xFFFF;
            ushort sideBack  = l.Back  != null && sidedefIndex.TryGetValue(l.Back,  out int slI) ? (ushort)slI : (ushort)0xFFFF;

            w.Write(v1);
            w.Write(v2);
            w.Write((ushort)l.Flags);
            w.Write((byte)l.Action);
            w.Write((byte)l.Args[0]);
            w.Write((byte)l.Args[1]);
            w.Write((byte)l.Args[2]);
            w.Write((byte)l.Args[3]);
            w.Write((byte)l.Args[4]);
            w.Write(sideFront);
            w.Write(sideBack);
        }
        return ms.ToArray();
    }

    public static byte[] WriteThings(MapSet map)
    {
        using var ms = new MemoryStream(map.Things.Count * ThingRecordSize);
        using var w = new BinaryWriter(ms);
        foreach (var t in map.Things)
        {
            w.Write((ushort)t.Tag);                  // tid
            w.Write((short)t.Position.x);
            w.Write((short)t.Position.y);
            w.Write((short)t.Height);                // z
            w.Write((short)t.Angle);
            w.Write((short)t.Type);
            w.Write((ushort)t.Flags);
            w.Write((byte)t.Action);
            w.Write((byte)t.Args[0]);
            w.Write((byte)t.Args[1]);
            w.Write((byte)t.Args[2]);
            w.Write((byte)t.Args[3]);
            w.Write((byte)t.Args[4]);
        }
        return ms.ToArray();
    }

    private static Dictionary<T, int> BuildIndex<T>(IReadOnlyList<T> list) where T : class
    {
        var dict = new Dictionary<T, int>(list.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < list.Count; i++) dict[list[i]] = i;
        return dict;
    }

    private static void InsertLump(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        if (data.Length > 0) lump.Stream.Write(data, 0, data.Length);
    }
}
