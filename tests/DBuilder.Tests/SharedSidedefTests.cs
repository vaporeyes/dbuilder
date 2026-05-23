// ABOUTME: Regression test for vanilla shared-sidedef unpacking (Plutonia/TNT) - two linedefs referencing the
// ABOUTME: same SIDEDEFS index must get distinct sidedef objects so BuildIndexes/triangulation don't crash.

using System.IO;
using System.Text;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SharedSidedefTests
{
    private static void WriteLump(WAD wad, string name, byte[] data, int pos)
    {
        var lump = wad.Insert(name, pos, data.Length)!;
        if (data.Length > 0) lump.Stream.Write(data, 0, data.Length);
    }

    private static byte[] Fixed(string s, int len)
    {
        var b = new byte[len];
        var src = Encoding.ASCII.GetBytes(s);
        System.Array.Copy(src, 0, b, 0, System.Math.Min(src.Length, len));
        return b;
    }

    // A Doom map where two linedefs both reference SIDEDEFS index 0 (shared sidedef).
    private static WAD BuildSharedSidedefWad()
    {
        var verts = new MemoryStream();
        using (var w = new BinaryWriter(verts, Encoding.ASCII, true))
        {
            w.Write((short)0); w.Write((short)0);
            w.Write((short)64); w.Write((short)0);
            w.Write((short)64); w.Write((short)64);
        }

        var lines = new MemoryStream();
        using (var w = new BinaryWriter(lines, Encoding.ASCII, true))
        {
            void Line(short v1, short v2)
            {
                w.Write(v1); w.Write(v2); w.Write((short)0); w.Write((short)0); w.Write((short)0);
                w.Write((short)0);   // right sidedef = 0 (shared!)
                w.Write((short)-1);  // left sidedef = none
            }
            Line(0, 1);
            Line(1, 2);
        }

        var sides = new MemoryStream();
        using (var w = new BinaryWriter(sides, Encoding.ASCII, true))
        {
            w.Write((short)0); w.Write((short)0);
            sides.Write(Fixed("-", 8), 0, 8); sides.Write(Fixed("-", 8), 0, 8); sides.Write(Fixed("WALL", 8), 0, 8);
            w.Write((short)0); // sector 0
        }

        var sectors = new MemoryStream();
        using (var w = new BinaryWriter(sectors, Encoding.ASCII, true))
        {
            w.Write((short)0); w.Write((short)128);
            sectors.Write(Fixed("FLOOR", 8), 0, 8); sectors.Write(Fixed("CEIL", 8), 0, 8);
            w.Write((short)160); w.Write((short)0); w.Write((short)0);
        }

        var ms = new MemoryStream();
        var wad = new WAD(ms);
        wad.Insert("MAP01", 0, 0);
        WriteLump(wad, "THINGS", System.Array.Empty<byte>(), 1);
        WriteLump(wad, "LINEDEFS", lines.ToArray(), 2);
        WriteLump(wad, "SIDEDEFS", sides.ToArray(), 3);
        WriteLump(wad, "VERTEXES", verts.ToArray(), 4);
        WriteLump(wad, "SECTORS", sectors.ToArray(), 5);
        wad.WriteHeaders();
        ms.Position = 0;
        return new WAD(ms, openreadonly: true);
    }

    [Fact]
    public void SharedSidedefIsUnpackedIntoDistinctObjects()
    {
        using var wad = BuildSharedSidedefWad();
        var map = DoomMapLoader.Load(wad, "MAP01")!;

        Assert.Equal(2, map.Linedefs.Count);
        Assert.NotNull(map.Linedefs[0].Front);
        Assert.NotNull(map.Linedefs[1].Front);
        // Each linedef must own its own sidedef (the shared one was cloned).
        Assert.False(ReferenceEquals(map.Linedefs[0].Front, map.Linedefs[1].Front));
        Assert.Equal(2, map.Sidedefs.Count);                 // 1 original + 1 unpacked clone
        Assert.Same(map.Linedefs[0].Front!.Line, map.Linedefs[0]); // back-ref points at the right line
        Assert.Same(map.Linedefs[1].Front!.Line, map.Linedefs[1]);
        Assert.Equal("WALL", map.Linedefs[1].Front!.MidTexture); // clone copied the texture
    }

    [Fact]
    public void BuildIndexesDoesNotDuplicateSharedSidedef()
    {
        using var wad = BuildSharedSidedefWad();
        var map = DoomMapLoader.Load(wad, "MAP01")!; // Load calls BuildIndexes
        // The sector lists each sidedef once (a duplicate previously crashed triangulation).
        Assert.Equal(2, map.Sectors[0].Sidedefs.Count);
        var ex = Record.Exception(() => Triangulation.Create(map.Sectors[0]));
        Assert.Null(ex);
    }
}
