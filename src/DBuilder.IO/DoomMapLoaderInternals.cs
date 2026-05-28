// ABOUTME: Shared lump readers for the Doom and Hexen binary map loaders.
// ABOUTME: VERTEXES/SECTORS/SIDEDEFS lumps are byte-for-byte identical in both formats; only LINEDEFS/THINGS differ.

using System.IO;
using System.Text;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

internal static class DoomMapLoaderInternals
{
    public static void ReadVertexes(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 4;
        for (int i = 0; i < n; i++)
        {
            short x = r.ReadInt16();
            short y = r.ReadInt16();
            map.Vertices.Add(new Vertex(new Vector2D(x, y)));
        }
    }

    public static void ReadSectors(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 26;
        for (int i = 0; i < n; i++)
        {
            short floorHeight = r.ReadInt16();
            short ceilHeight = r.ReadInt16();
            string floorTex = ReadFixedString(r, 8);
            string ceilTex  = ReadFixedString(r, 8);
            short light = r.ReadInt16();
            short special = r.ReadInt16();
            short tag = r.ReadInt16();

            map.Sectors.Add(new Sector
            {
                Index = i,
                FloorHeight = floorHeight,
                CeilHeight = ceilHeight,
                FloorTexture = floorTex,
                CeilTexture = ceilTex,
                Brightness = light,
                Special = special,
                Tag = tag,
            });
        }
    }

    public static void ReadSidedefs(Lump lump, MapSet map)
    {
        byte[] bytes = lump.Stream.ReadAllBytes();
        using var r = new BinaryReader(new MemoryStream(bytes));
        int n = bytes.Length / 30;
        for (int i = 0; i < n; i++)
        {
            short offsetX = r.ReadInt16();
            short offsetY = r.ReadInt16();
            string upper = ReadFixedString(r, 8);
            string lower = ReadFixedString(r, 8);
            string middle = ReadFixedString(r, 8);
            short sectorIdx = r.ReadInt16();

            map.Sidedefs.Add(new Sidedef
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                HighTexture = upper,
                LowTexture = lower,
                MidTexture = middle,
                Sector = (sectorIdx >= 0 && sectorIdx < map.Sectors.Count) ? map.Sectors[sectorIdx] : null,
            });
        }
    }

    /// <summary>
    /// Attaches sidedef <paramref name="idx"/> to <paramref name="line"/>'s front/back. Vanilla maps (e.g. Plutonia,
    /// TNT) sometimes share one sidedef index across multiple linedefs to save space; an editor needs each
    /// linedef-side to own its sidedef, so a sidedef already in use is cloned (unpacked) here.
    /// </summary>
    public static void AttachSidedef(MapSet map, int idx, Linedef line, bool front, HashSet<Sidedef> used)
    {
        if (idx < 0 || idx >= map.Sidedefs.Count) return;
        var sd = map.Sidedefs[idx];
        if (!used.Add(sd)) // already attached to another linedef -> clone it
        {
            var clone = new Sidedef(line, front)
            {
                OffsetX = sd.OffsetX,
                OffsetY = sd.OffsetY,
                HighTexture = sd.HighTexture,
                MidTexture = sd.MidTexture,
                LowTexture = sd.LowTexture,
                Sector = sd.Sector,
            };
            foreach (var kv in sd.Fields) clone.Fields[kv.Key] = kv.Value;
            map.Sidedefs.Add(clone);
            used.Add(clone);
            sd = clone;
        }
        sd.Line = line;
        sd.IsFront = front;
        if (front) line.Front = sd; else line.Back = sd;
    }

    public static Vertex SafeVertex(MapSet map, int idx)
    {
        if (map.Vertices.Count == 0)
        {
            // Degenerate input: synthesize a placeholder so the linedef can still be constructed.
            var v = new Vertex(new Vector2D(0, 0));
            map.Vertices.Add(v);
            return v;
        }
        if (idx < 0 || idx >= map.Vertices.Count) return map.Vertices[0];
        return map.Vertices[idx];
    }

    public static bool TryGetValidLinedefVertices(MapSet map, int startIndex, int endIndex, out Vertex start, out Vertex end)
    {
        start = null!;
        end = null!;
        if (startIndex < 0 || startIndex >= map.Vertices.Count || endIndex < 0 || endIndex >= map.Vertices.Count)
            return false;

        start = map.Vertices[startIndex];
        end = map.Vertices[endIndex];
        return Vector2D.ManhattanDistance(start.Position, end.Position) > 0.0001;
    }

    public static string ReadFixedString(BinaryReader r, int length)
    {
        byte[] buf = r.ReadBytes(length);
        int end = 0;
        while (end < buf.Length && buf[end] != 0) end++;
        return Encoding.ASCII.GetString(buf, 0, end).ToUpperInvariant();
    }
}
