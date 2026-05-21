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

    public static string ReadFixedString(BinaryReader r, int length)
    {
        byte[] buf = r.ReadBytes(length);
        int end = 0;
        while (end < buf.Length && buf[end] != 0) end++;
        return Encoding.ASCII.GetString(buf, 0, end).ToUpperInvariant();
    }
}
