// ABOUTME: Tests for PNAMES + TEXTURE1/TEXTURE2 parsing and patch composition.
// ABOUTME: Builds synthetic lumps byte-by-byte for both Doom-classic and Strife formats and verifies the compositor's pixel placement and transparency handling.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DoomTextureCompositionTests
{
    private static DoomPalette GrayPalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++) { bytes[i * 3] = (byte)i; bytes[i * 3 + 1] = (byte)i; bytes[i * 3 + 2] = (byte)i; }
        return DoomPalette.FromBytes(bytes);
    }

    // ============================================================
    // PNAMES tests
    // ============================================================

    [Fact]
    public void PNamesParsesCountAndNames()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)3);
            w.Write(FixedAscii("WALLA", 8));
            w.Write(FixedAscii("WALLB", 8));
            w.Write(FixedAscii("LONG_PAT", 8));
        }
        var pn = DoomPatchNames.FromBytes(ms.ToArray());
        Assert.Equal(3, pn.Length);
        Assert.Equal("WALLA",    pn[0]);
        Assert.Equal("WALLB",    pn[1]);
        Assert.Equal("LONG_PAT", pn[2]);
    }

    [Fact]
    public void PNamesUppercases()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);
            w.Write(FixedAscii("lowercase", 8));
        }
        var pn = DoomPatchNames.FromBytes(ms.ToArray());
        Assert.Equal("LOWERCAS", pn[0]); // also truncated to 8 bytes
    }

    [Fact]
    public void PNamesEmptyWhenLumpMissing()
    {
        // Build an empty WAD
        var ms = new MemoryStream();
        using (var wad = new WAD(ms)) { wad.WriteHeaders(); }
        ms.Position = 0;
        using var wad2 = new WAD(ms, openreadonly: true);
        Assert.Null(DoomPatchNames.FromWad(wad2));
        Assert.Empty(DoomPatchNames.Empty.Names);
    }

    // ============================================================
    // TEXTURE1/2 parsing - Doom-classic format
    // ============================================================

    [Fact]
    public void DoomClassicTextureListParsesOneTextureWithTwoPatches()
    {
        // Two patches in PNAMES: PATCHA (4x2 indexed bytes), PATCHB (2x2 indexed bytes).
        // Texture "WALL" is 6x4, places PATCHA at (0,0) and PATCHB at (4,1).

        // Doom-classic def header: 8(name) + 2(flags) + 1+1(scale) + 2+2(w,h) + 2(patches=0) + 2(skip) + 2(real patches)
        var lump = new MemoryStream();
        using (var w = new BinaryWriter(lump, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);             // numTextures
            // Offset table: one offset, pointing past itself
            const uint defOff = 4 + 4;
            w.Write((uint)defOff);

            // Def starts here
            w.Write(FixedAscii("WALL", 8));
            w.Write((ushort)0);           // flags
            w.Write((byte)0);             // scaleX
            w.Write((byte)0);             // scaleY
            w.Write((short)6);            // width
            w.Write((short)4);            // height
            w.Write((short)0);            // "patchCount" = 0 -> Doom-classic format
            w.Write((short)0);            // skipped (columndirectory hi half)
            w.Write((short)2);            // real patchCount

            // Patch records (10 bytes each in Doom-classic format)
            // Patch 0 at (0, 0), PNAMES index 0
            w.Write((short)0); w.Write((short)0); w.Write((ushort)0);
            w.Write((short)0); w.Write((short)0); // stepdir + colormap
            // Patch 1 at (4, 1), PNAMES index 1
            w.Write((short)4); w.Write((short)1); w.Write((ushort)1);
            w.Write((short)0); w.Write((short)0);
        }

        var defs = DoomTextureListReader.Parse(lump.ToArray());
        Assert.Single(defs);
        var def = defs[0];
        Assert.Equal("WALL", def.Name);
        Assert.Equal(6, def.Width);
        Assert.Equal(4, def.Height);
        Assert.Equal(DoomTextureFormat.DoomClassic, def.SourceFormat);
        Assert.Equal(2, def.Patches.Count);
        Assert.Equal(new DoomTexturePatch(0, 0, 0), def.Patches[0]);
        Assert.Equal(new DoomTexturePatch(4, 1, 1), def.Patches[1]);
    }

    [Fact]
    public void StrifeFormatTextureListParsesShortPatchRecords()
    {
        var lump = new MemoryStream();
        using (var w = new BinaryWriter(lump, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);
            w.Write((uint)8);                // offset to def

            w.Write(FixedAscii("STRIFE", 8));
            w.Write((ushort)0);
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)4); w.Write((short)4);
            w.Write((short)1);               // patchCount = 1, nonzero => Strife
            // Strife patch record: 6 bytes (originX, originY, patchIndex) - no stepdir/colormap
            w.Write((short)2); w.Write((short)1); w.Write((ushort)0);
        }

        var defs = DoomTextureListReader.Parse(lump.ToArray());
        Assert.Single(defs);
        Assert.Equal(DoomTextureFormat.Strife, defs[0].SourceFormat);
        Assert.Equal(new DoomTexturePatch(2, 1, 0), defs[0].Patches[0]);
    }

    [Fact]
    public void EmptyLumpReturnsEmptyList()
    {
        Assert.Empty(DoomTextureListReader.Parse(new byte[0]));
        Assert.Empty(DoomTextureListReader.Parse(new byte[] { 0, 0, 0, 0 })); // numTextures = 0
    }

    // ============================================================
    // Compositor tests
    // ============================================================

    [Fact]
    public void ComposeBlitsPatchAtOrigin()
    {
        // Build a WAD with PLAYPAL + a single 2x2 patch + TEXTURE1 referencing it.
        // Patch pixels: top-left=10, top-right=20, bot-left=30, bot-right=40
        // Texture: 4x4 with patch at origin (1, 1).
        var (wadBytes, _) = BuildSyntheticTextureWad();
        wadBytes.Position = 0;
        using var wad = new WAD(wadBytes, openreadonly: true);

        var palette = DoomPalette.FromWad(wad)!;
        var pnames = DoomPatchNames.FromWad(wad)!;
        var defs = DoomTextureListReader.FromWad(wad, "TEXTURE1")!;
        Assert.Single(defs);
        var def = defs[0];
        Assert.Equal("WALL", def.Name);

        byte[] rgba = DoomWallTextureCompositor.Compose(def, pnames, wad, palette)!;
        Assert.Equal(4 * 4 * 4, rgba.Length);

        // Patch is placed at (1, 1) so canvas pixel (1,1)=patch(0,0)=palette[10]=gray 10
        Assert.Equal(10, rgba[(1 * 4 + 1) * 4 + 0]); // R
        Assert.Equal(20, rgba[(1 * 4 + 2) * 4 + 0]);
        Assert.Equal(30, rgba[(2 * 4 + 1) * 4 + 0]);
        Assert.Equal(40, rgba[(2 * 4 + 2) * 4 + 0]);
        // Uncovered pixels remain transparent
        Assert.Equal(0, rgba[(0 * 4 + 0) * 4 + 3]);
        Assert.Equal(0, rgba[(3 * 4 + 3) * 4 + 3]);
    }

    [Fact]
    public void ComposeClipsPatchesGoingOffEdge()
    {
        // Same patch, but place its origin so it extends past the right edge.
        // Patch is 2x2; placing it at (3, 0) on a 4x4 canvas should clip column 1 of the patch.
        var palette = GrayPalette();
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);
            w.Write((uint)8);
            w.Write(FixedAscii("CLIP", 8));
            w.Write((ushort)0);
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)4); w.Write((short)4);
            w.Write((short)1);
            // Strife-style patch (6 bytes) at (3, 0) referencing patchIndex 0
            w.Write((short)3); w.Write((short)0); w.Write((ushort)0);
        }
        var def = DoomTextureListReader.Parse(ms.ToArray())[0];
        var pnames = BuildSyntheticPnames(new[] { "PATCH" });
        var wad = BuildSinglePatchWad("PATCH");

        byte[] rgba = DoomWallTextureCompositor.Compose(def, pnames, wad, palette)!;

        // Canvas pixel (0,3) should have patch(0,0)=10
        Assert.Equal(10, rgba[(0 * 4 + 3) * 4 + 0]);
        Assert.Equal(0xFF, rgba[(0 * 4 + 3) * 4 + 3]);
        // Patch's (0,1) column (would land at x=4) is clipped out; canvas (0,4)... doesn't exist.
        // Canvas (1, 3) should have patch(1, 0)=30
        Assert.Equal(30, rgba[(1 * 4 + 3) * 4 + 0]);
        // Canvas (0, 2) is NOT covered (patch starts at x=3); should remain transparent
        Assert.Equal(0, rgba[(0 * 4 + 2) * 4 + 3]);
    }

    [Fact]
    public void ComposeReturnsNullWhenAllPatchesMissing()
    {
        // Build a WAD with PLAYPAL but no patches, then reference a non-existent patch.
        var palette = GrayPalette();
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);
            w.Write((uint)8);
            w.Write(FixedAscii("EMPTY", 8));
            w.Write((ushort)0); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)4); w.Write((short)4); w.Write((short)1);
            w.Write((short)0); w.Write((short)0); w.Write((ushort)0);
        }
        var def = DoomTextureListReader.Parse(ms.ToArray())[0];

        var pnames = BuildSyntheticPnames(new[] { "NONEXIST" });

        // Empty WAD (no PATCH lump to find)
        var emptyWadBytes = new MemoryStream();
        using (var w = new WAD(emptyWadBytes)) { w.WriteHeaders(); }
        emptyWadBytes.Position = 0;
        using var wad = new WAD(emptyWadBytes, openreadonly: true);

        var result = DoomWallTextureCompositor.Compose(def, pnames, wad, palette);
        Assert.Null(result);
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>Builds a 2x2 Doom-picture-format patch with pixels (10, 20, 30, 40).</summary>
    private static byte[] BuildTwoByTwoPatch()
    {
        // 2 columns: col 0 has 2 pixels (10, 30), col 1 has 2 pixels (20, 40)
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((short)2); // width
        w.Write((short)2); // height
        w.Write((short)0); // offsetX
        w.Write((short)0); // offsetY
        // Column offset table: col 0 at byte 16, col 1 at byte 23.
        // Each column is 7 bytes: 1 startY + 1 count + 1 pad + 2 pixels + 1 pad + 1 terminator.
        w.Write((int)16);
        w.Write((int)23);
        // Column 0
        w.Write((byte)0); w.Write((byte)2); w.Write((byte)0);  // startY=0, count=2, pad
        w.Write((byte)10); w.Write((byte)30);                  // pixels
        w.Write((byte)0); w.Write((byte)0xFF);                 // pad + terminator
        // Column 1
        w.Write((byte)0); w.Write((byte)2); w.Write((byte)0);
        w.Write((byte)20); w.Write((byte)40);
        w.Write((byte)0); w.Write((byte)0xFF);
        return ms.ToArray();
    }

    private static (MemoryStream wadBytes, byte[] patchBytes) BuildSyntheticTextureWad()
    {
        // Build a 768-byte gray palette
        var palBytes = new byte[768];
        for (int i = 0; i < 256; i++) { palBytes[i * 3] = (byte)i; palBytes[i * 3 + 1] = (byte)i; palBytes[i * 3 + 2] = (byte)i; }

        byte[] patchBytes = BuildTwoByTwoPatch();

        // PNAMES: 1 patch named "PATCH"
        var pnamesMs = new MemoryStream();
        using (var w = new BinaryWriter(pnamesMs, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);
            w.Write(FixedAscii("PATCH", 8));
        }

        // TEXTURE1: one Doom-classic def "WALL" referencing patch 0 at (1, 1)
        var tex1Ms = new MemoryStream();
        using (var w = new BinaryWriter(tex1Ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write((uint)1);          // numTextures
            w.Write((uint)8);          // offset to def

            w.Write(FixedAscii("WALL", 8));
            w.Write((ushort)0);        // flags
            w.Write((byte)0); w.Write((byte)0); // scale
            w.Write((short)4); w.Write((short)4); // 4x4
            w.Write((short)0);         // patchCount = 0 -> Doom-classic
            w.Write((short)0);         // skipped
            w.Write((short)1);         // real patchCount
            // Doom-classic patch record (10 bytes)
            w.Write((short)1); w.Write((short)1); w.Write((ushort)0);
            w.Write((short)0); w.Write((short)0);
        }

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            Insert(wad, "PLAYPAL", palBytes, 0);
            Insert(wad, "PNAMES",  pnamesMs.ToArray(), 1);
            Insert(wad, "TEXTURE1", tex1Ms.ToArray(), 2);
            Insert(wad, "PATCH",   patchBytes, 3);
            wad.WriteHeaders();
        }
        wadBytes.Position = 0;
        return (wadBytes, patchBytes);
    }

    private static DoomPatchNames BuildSyntheticPnames(string[] names)
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((uint)names.Length);
        foreach (var n in names) w.Write(FixedAscii(n, 8));
        return DoomPatchNames.FromBytes(ms.ToArray());
    }

    private static WAD BuildSinglePatchWad(string patchName)
    {
        var palBytes = new byte[768];
        for (int i = 0; i < 256; i++) { palBytes[i * 3] = (byte)i; palBytes[i * 3 + 1] = (byte)i; palBytes[i * 3 + 2] = (byte)i; }
        byte[] patchBytes = BuildTwoByTwoPatch();

        var wadBytes = new MemoryStream();
        using (var wad = new WAD(wadBytes))
        {
            Insert(wad, "PLAYPAL", palBytes, 0);
            Insert(wad, patchName, patchBytes, 1);
            wad.WriteHeaders();
        }
        wadBytes.Position = 0;
        return new WAD(wadBytes, openreadonly: true);
    }

    private static void Insert(WAD wad, string name, byte[] data, int position)
    {
        var lump = wad.Insert(name, position, data.Length)!;
        lump.Stream.Write(data, 0, data.Length);
    }

    private static byte[] FixedAscii(string s, int length)
    {
        var bytes = new byte[length];
        var src = Encoding.ASCII.GetBytes(s.ToUpperInvariant());
        System.Array.Copy(src, 0, bytes, 0, System.Math.Min(src.Length, length));
        return bytes;
    }
}
