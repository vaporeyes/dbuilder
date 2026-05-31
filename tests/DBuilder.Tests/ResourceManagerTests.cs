// ABOUTME: Tests for ResourceManager - palette/flat resolution, caching, and newest-resource-wins override across WADs.
// ABOUTME: Uses a synthetic WAD with a grayscale PLAYPAL so decoded flat pixels are predictable; an opportunistic test hits a real WAD.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourceManagerTests
{
    // A grayscale palette: index i -> (i, i, i), so a flat filled with index v decodes to RGBA (v, v, v, 255).
    private static byte[] GrayscalePlaypal()
    {
        var p = new byte[768];
        for (int i = 0; i < 256; i++) { p[i * 3] = (byte)i; p[i * 3 + 1] = (byte)i; p[i * 3 + 2] = (byte)i; }
        return p;
    }

    private static byte[] SolidPlaypal(byte red, byte green, byte blue)
    {
        var p = new byte[768];
        for (int i = 0; i < 256; i++) { p[i * 3] = red; p[i * 3 + 1] = green; p[i * 3 + 2] = blue; }
        return p;
    }

    private static byte[] SolidFlat(byte index)
    {
        var f = new byte[DoomFlatReader.RawSize];
        for (int i = 0; i < f.Length; i++) f[i] = index;
        return f;
    }

    private static byte[] ColormapBytes(byte offset)
    {
        var bytes = new byte[DoomColormap.LevelSize * DoomColormap.StandardLevelCount];
        for (int level = 0; level < DoomColormap.StandardLevelCount; level++)
            for (int index = 0; index < DoomColormap.LevelSize; index++)
                bytes[level * DoomColormap.LevelSize + index] = (byte)((index + offset) & 0xFF);
        return bytes;
    }

    private static WAD BuildWad(params (string name, byte[] data)[] lumps)
    {
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            int pos = 0;
            foreach (var (name, data) in lumps)
            {
                var lump = wad.Insert(name, pos++, data.Length)!;
                lump.Stream.Write(data, 0, data.Length);
            }
            wad.WriteHeaders();
        }
        ms.Position = 0;
        return new WAD(ms, openreadonly: true);
    }

    private static byte[] FixedAscii(string s, int len)
    {
        var b = new byte[len];
        Encoding.ASCII.GetBytes(s, 0, Math.Min(s.Length, len), b, 0);
        return b;
    }

    private static byte[] PNames(params string[] names)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((uint)names.Length);
        foreach (string name in names) w.Write(FixedAscii(name, 8));
        return ms.ToArray();
    }

    private static byte[] Texture1(params (string TextureName, int Width, int Height, ushort PatchIndex)[] textures)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((uint)textures.Length);
        int offset = 4 + textures.Length * 4;
        for (int i = 0; i < textures.Length; i++)
        {
            w.Write((uint)offset);
            offset += 32;
        }

        foreach (var texture in textures)
        {
            w.Write(FixedAscii(texture.TextureName, 8));
            w.Write((ushort)0);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)texture.Width);
            w.Write((short)texture.Height);
            w.Write((short)0);
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)0);
            w.Write((short)0);
            w.Write(texture.PatchIndex);
            w.Write((short)0);
            w.Write((short)0);
        }

        return ms.ToArray();
    }

    private static byte[] DoomPatch(byte index)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((short)1);
        w.Write((short)1);
        w.Write((short)0);
        w.Write((short)0);
        w.Write((int)12);
        w.Write((byte)0);
        w.Write((byte)1);
        w.Write((byte)0);
        w.Write(index);
        w.Write((byte)0);
        w.Write((byte)0xFF);
        return ms.ToArray();
    }

    [Fact]
    public void ResolvesPaletteAndFlat()
    {
        using var wad = BuildWad(("PLAYPAL", GrayscalePlaypal()), ("FLAT5", SolidFlat(5)));
        var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.NotNull(rm.Palette);
        var flat = rm.GetFlat("FLAT5");
        Assert.NotNull(flat);
        Assert.Equal(64, flat!.Width);
        Assert.Equal(64, flat.Height);
        Assert.Equal(64 * 64 * 4, flat.Rgba.Length);
        // Grayscale palette: index 5 -> R=G=B=5, opaque.
        Assert.Equal(5, flat.Rgba[0]);
        Assert.Equal(5, flat.Rgba[1]);
        Assert.Equal(5, flat.Rgba[2]);
        Assert.Equal(255, flat.Rgba[3]);
    }

    [Fact]
    public void WadStrictPatchesRestrictsClassicTexturePatchLookup()
    {
        string wadPath = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("PNAMES", PNames("PATCH")),
            ("TEXTURE1", Texture1(("UNUSED", 1, 1, 0), ("WALL", 1, 1, 0))),
            ("F_START", Array.Empty<byte>()),
            ("PATCH", DoomPatch(70)),
            ("F_END", Array.Empty<byte>()));
        try
        {
            using (var rm = new ResourceManager())
            {
                rm.AddResource(wadPath);
                Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetWallTexture("WALL")!.Rgba[0..4]);
            }

            using (var rm = new ResourceManager())
            {
                rm.AddResource(new DataLocation(DataLocationType.Wad, wadPath, option1: true));
                Assert.Null(rm.GetWallTexture("WALL"));
            }
        }
        finally { File.Delete(wadPath); }
    }

    [Fact]
    public void WadTexture1FirstEntryIsSkippedLikeUdb()
    {
        string wadPath = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("PNAMES", PNames("PATCH")),
            ("TEXTURE1", Texture1(("UNUSED", 1, 1, 0), ("WALL", 1, 1, 0))),
            ("PATCH", DoomPatch(70)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(wadPath);

            Assert.Null(rm.GetWallTexture("UNUSED"));
            Assert.DoesNotContain("UNUSED", rm.GetTextureNames());
            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetWallTexture("WALL")!.Rgba[0..4]);
            Assert.Contains("WALL", rm.GetTextureNames());
        }
        finally { File.Delete(wadPath); }
    }

    [Fact]
    public void WadConfiguredTextureRangesProvideWallTextures()
    {
        var config = GameConfiguration.FromText("""
            textures
            {
                walls { start = "TX_START"; end = "TX_END"; }
            }
            """);
        using var wad = BuildWad(
            ("PLAYPAL", GrayscalePlaypal()),
            ("TX_START", Array.Empty<byte>()),
            ("TXPIC", DoomPatch(70)),
            ("TX_END", Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.Null(rm.GetWallTexture("TXPIC"));

        rm.Configuration = config;

        Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetWallTexture("TXPIC")!.Rgba[0..4]);
        Assert.Contains("TXPIC", rm.GetTextureNames());
    }

    [Fact]
    public void WadConfiguredFlatRangesProvideAndPrioritizeFlats()
    {
        var config = GameConfiguration.FromText("""
            flats
            {
                floors { start = "FF_START"; end = "FF_END"; }
            }
            """);
        using var wad = BuildWad(
            ("PLAYPAL", GrayscalePlaypal()),
            ("RANGEFL", SolidFlat(9)),
            ("FF_START", Array.Empty<byte>()),
            ("RANGEFL", SolidFlat(70)),
            ("FF_END", Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.Equal(9, rm.GetFlat("RANGEFL")!.Rgba[0]);

        rm.Configuration = config;

        Assert.Equal(70, rm.GetFlat("RANGEFL")!.Rgba[0]);
        Assert.Contains("RANGEFL", rm.GetFlatNames());
    }

    [Fact]
    public void WadConfiguredColormapRangesProvideAndPrioritizeNamedColormaps()
    {
        var config = GameConfiguration.FromText("""
            colormaps
            {
                fog { start = "C_START"; end = "C_END"; }
            }
            """);
        using var wad = BuildWad(
            ("FOGMAP", ColormapBytes(1)),
            ("C_START", Array.Empty<byte>()),
            ("FOGMAP", ColormapBytes(7)),
            ("C_END", Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.Equal(11, rm.GetColormap("FOGMAP")!.Lookup(0, 10));

        rm.Configuration = config;

        Assert.Equal(17, rm.GetColormap("FOGMAP")!.Lookup(0, 10));
        Assert.Contains("FOGMAP", rm.GetTextureNames());
    }

    [Fact]
    public void WadConfiguredSpriteRangesProvideAndPrioritizeSprites()
    {
        var config = GameConfiguration.FromText("""
            sprites
            {
                actors { start = "SP_START"; end = "SP_END"; }
            }
            """);
        using var wad = BuildWad(
            ("PLAYPAL", GrayscalePlaypal()),
            ("POSSA0", DoomPatch(1)),
            ("SP_START", Array.Empty<byte>()),
            ("POSSA0", DoomPatch(70)),
            ("SP_END", Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.Equal(1, rm.GetSprite("POSSA0")!.Rgba[0]);

        rm.Configuration = config;

        Assert.Equal(70, rm.GetSprite("POSSA0")!.Rgba[0]);
        Assert.Contains("POSSA0", rm.GetSpriteNames());
    }

    [Fact]
    public void WadConfiguredPatchRangesPrioritizeClassicTexturePatches()
    {
        var config = GameConfiguration.FromText("""
            patches
            {
                art { start = "PT_START"; end = "PT_END"; }
            }
            """);
        string wadPath = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("PNAMES", PNames("PATCH")),
            ("TEXTURE1", Texture1(("UNUSED", 1, 1, 0), ("WALL", 1, 1, 0))),
            ("PATCH", DoomPatch(1)),
            ("PT_START", Array.Empty<byte>()),
            ("PATCH", DoomPatch(70)),
            ("PT_END", Array.Empty<byte>()));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(wadPath);

            Assert.Equal(1, rm.GetWallTexture("WALL")!.Rgba[0]);

            rm.Configuration = config;

            Assert.Equal(70, rm.GetWallTexture("WALL")!.Rgba[0]);
        }
        finally { File.Delete(wadPath); }
    }

    [Fact]
    public void WadPatchFallbackPrefersLumpsOutsideConfiguredFlatRanges()
    {
        var config = GameConfiguration.FromText("""
            flats
            {
                map { start = "F_START"; end = "F_END"; }
            }
            """);
        string wadPath = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("PNAMES", PNames("PATCH")),
            ("TEXTURE1", Texture1(("UNUSED", 1, 1, 0), ("WALL", 1, 1, 0))),
            ("F_START", Array.Empty<byte>()),
            ("PATCH", SolidFlat(1)),
            ("F_END", Array.Empty<byte>()),
            ("PATCH", DoomPatch(70)));
        try
        {
            using var rm = new ResourceManager(config);
            rm.AddResource(wadPath);

            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetWallTexture("WALL")!.Rgba[0..4]);
        }
        finally { File.Delete(wadPath); }
    }

    [Fact]
    public void ResolvesMainColormapNewestResourceFirst()
    {
        using var lower = BuildWad(("COLORMAP", ColormapBytes(1)));
        using var higher = BuildWad(("COLORMAP", ColormapBytes(7)));
        using var rm = new ResourceManager();
        rm.AddResource(lower);
        rm.AddResource(higher);

        var colormap = rm.Colormap;

        Assert.NotNull(colormap);
        Assert.Equal(DoomColormap.StandardLevelCount, colormap!.LevelCount);
        Assert.Equal(17, colormap.Lookup(0, 10));
    }

    [Fact]
    public void Pk3NestedWadPaletteAndColormapOverrideRootLumps()
    {
        string nestedWad = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", SolidPlaypal(7, 8, 9)),
            ("COLORMAP", ColormapBytes(7)));
        string pk3 = TestArtifacts.BuildPk3(
            ("PLAYPAL", SolidPlaypal(1, 2, 3)),
            ("COLORMAP", ColormapBytes(1)),
            ("nested.wad", File.ReadAllBytes(nestedWad)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.NotNull(rm.Palette);
            Assert.Equal(0xFF070809u, rm.Palette!.Colors[0]);
            Assert.NotNull(rm.Colormap);
            Assert.Equal(17, rm.Colormap!.Lookup(0, 10));
        }
        finally
        {
            File.Delete(pk3);
            File.Delete(nestedWad);
        }
    }

    [Fact]
    public void Pk3NestedWadFlatOverridesFolderFlat()
    {
        string nestedWad = TestArtifacts.BuildPwadFile(
            ("PLAYPAL", GrayscalePlaypal()),
            ("F_START", Array.Empty<byte>()),
            ("SAMEFLAT", SolidFlat(77)),
            ("F_END", Array.Empty<byte>()));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/SAMEFLAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("nested.wad", File.ReadAllBytes(nestedWad)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var flat = rm.GetFlat("SAMEFLAT");

            Assert.NotNull(flat);
            Assert.Equal(new byte[] { 77, 77, 77, 255 }, flat!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(pk3);
            File.Delete(nestedWad);
        }
    }

    [Fact]
    public void Pk3NestedWadNamedColormapOverridesFolderColormap()
    {
        string nestedWad = TestArtifacts.BuildPwadFile(("FOGMAP", ColormapBytes(7)));
        string pk3 = TestArtifacts.BuildPk3(
            ("colormaps/FOGMAP.lmp", ColormapBytes(1)),
            ("nested.wad", File.ReadAllBytes(nestedWad)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            var colormap = rm.GetColormap("FOGMAP");

            Assert.NotNull(colormap);
            Assert.Equal(17, colormap!.Lookup(0, 10));
        }
        finally
        {
            File.Delete(pk3);
            File.Delete(nestedWad);
        }
    }

    [Fact]
    public void AddingResourceInvalidatesMainColormap()
    {
        using var empty = BuildWad(("PLAYPAL", GrayscalePlaypal()));
        using var withColormap = BuildWad(("COLORMAP", ColormapBytes(3)));
        using var rm = new ResourceManager();
        rm.AddResource(empty);

        Assert.Null(rm.Colormap);

        rm.AddResource(withColormap);

        Assert.NotNull(rm.Colormap);
        Assert.Equal(8, rm.Colormap!.Lookup(0, 5));
    }

    [Fact]
    public void ResolvesNamedColormapFromWadLump()
    {
        using var wad = BuildWad(("FOGMAP", ColormapBytes(9)));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        var colormap = rm.GetColormap("FOGMAP");

        Assert.NotNull(colormap);
        Assert.Equal(11, colormap!.Lookup(0, 2));
    }

    [Fact]
    public void ResolvesNamedColormapFromPk3ColormapsFolder()
    {
        string lower = TestArtifacts.BuildPk3(("colormaps/FOGMAP.lmp", ColormapBytes(1)));
        string higher = TestArtifacts.BuildPk3(("colormaps/FOGMAP.lmp", ColormapBytes(4)));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            var colormap = rm.GetColormap("FOGMAP");

            Assert.NotNull(colormap);
            Assert.Equal(10, colormap!.Lookup(0, 6));
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }

    [Fact]
    public void MissingFlatReturnsNullAndCaches()
    {
        using var wad = BuildWad(("PLAYPAL", GrayscalePlaypal()));
        var rm = new ResourceManager();
        rm.AddResource(wad);
        Assert.Null(rm.GetFlat("NOPE"));
        Assert.Null(rm.GetFlat("NOPE")); // second call hits the cache, still null
    }

    [Fact]
    public void AddingResourceInvalidatesCachedMisses()
    {
        using var empty = BuildWad(("PLAYPAL", GrayscalePlaypal()));
        using var withFlat = BuildWad(("FLAT7", SolidFlat(7)));
        using var rm = new ResourceManager();
        rm.AddResource(empty);

        Assert.Null(rm.GetFlat("FLAT7"));

        rm.AddResource(withFlat);

        var flat = rm.GetFlat("FLAT7");
        Assert.NotNull(flat);
        Assert.Equal(7, flat!.Rgba[0]);
    }

    [Fact]
    public void DisposeDoesNotDisposeCallerOwnedWads()
    {
        using var wad = BuildWad(("PLAYPAL", GrayscalePlaypal()));
        var rm = new ResourceManager();
        rm.AddResource(wad);

        rm.Dispose();

        Assert.False(wad.IsDisposed);
    }

    [Fact]
    public void ClearResourcesDropsCachedImagesNamesPaletteAndColormap()
    {
        using var wad = BuildWad(
            ("PLAYPAL", GrayscalePlaypal()),
            ("COLORMAP", ColormapBytes(0)),
            ("F_START", System.Array.Empty<byte>()),
            ("FLAT1", SolidFlat(1)),
            ("F_END", System.Array.Empty<byte>()));
        using var rm = new ResourceManager();
        rm.AddResource(wad);

        Assert.NotNull(rm.Palette);
        Assert.NotNull(rm.Colormap);
        Assert.NotNull(rm.GetFlat("FLAT1"));
        Assert.Contains("FLAT1", rm.GetFlatNames());

        rm.ClearResources();

        Assert.Null(rm.Palette);
        Assert.Null(rm.Colormap);
        Assert.Null(rm.GetFlat("FLAT1"));
        Assert.DoesNotContain("FLAT1", rm.GetFlatNames());
        Assert.False(wad.IsDisposed);
    }

    [Fact]
    public void SameInstanceReturnedFromCache()
    {
        using var wad = BuildWad(("PLAYPAL", GrayscalePlaypal()), ("FLAT9", SolidFlat(9)));
        var rm = new ResourceManager();
        rm.AddResource(wad);
        var a = rm.GetFlat("FLAT9");
        var b = rm.GetFlat("FLAT9");
        Assert.Same(a, b);
    }

    [Fact]
    public void LaterResourceOverridesEarlier()
    {
        // Both WADs define FLAT1 with different fills; the last-added WAD must win (PWAD over IWAD).
        using var iwad = BuildWad(("PLAYPAL", GrayscalePlaypal()), ("FLAT1", SolidFlat(10)));
        using var pwad = BuildWad(("FLAT1", SolidFlat(200)));
        var rm = new ResourceManager();
        rm.AddResource(iwad); // lower priority
        rm.AddResource(pwad); // higher priority (added last)

        var flat = rm.GetFlat("FLAT1");
        Assert.NotNull(flat);
        Assert.Equal(200, flat!.Rgba[0]); // from the pwad
    }

    [Fact]
    public void NullAndDashFlatNamesReturnNull()
    {
        using var wad = BuildWad(("PLAYPAL", GrayscalePlaypal()));
        var rm = new ResourceManager();
        rm.AddResource(wad);
        Assert.Null(rm.GetFlat("-"));
        Assert.Null(rm.GetFlat(""));
    }

    [Fact]
    public void NoPaletteYieldsNoImages()
    {
        using var wad = BuildWad(("FLAT5", SolidFlat(5))); // no PLAYPAL
        var rm = new ResourceManager();
        rm.AddResource(wad);
        Assert.Null(rm.Palette);
        Assert.Null(rm.GetFlat("FLAT5"));
    }

    [Fact]
    public void ResolvesWallTextureFromRealWadWhenAvailable()
    {
        // Opportunistic: only runs if a real Doom IWAD is present at a common path.
        string[] candidates =
        {
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "media/DoomStuff/doom.wad"),
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "media/DoomStuff/doom2.wad"),
        };
        string? wadPath = System.Array.Find(candidates, System.IO.File.Exists);
        if (wadPath == null) return;

        using var rm = new ResourceManager();
        rm.AddResource(wadPath);
        Assert.NotNull(rm.Palette);
        // STARTAN3 (Doom) / a common wall texture should compose. If absent, at least the call must not throw.
        var tex = rm.GetWallTexture("STARTAN3");
        if (tex != null)
        {
            Assert.True(tex.Width > 0 && tex.Height > 0);
            Assert.Equal(tex.Width * tex.Height * 4, tex.Rgba.Length);
        }
    }
}
