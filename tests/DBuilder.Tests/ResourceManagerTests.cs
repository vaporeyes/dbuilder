// ABOUTME: Tests for ResourceManager - palette/flat resolution, caching, and newest-resource-wins override across WADs.
// ABOUTME: Uses a synthetic WAD with a grayscale PLAYPAL so decoded flat pixels are predictable; an opportunistic test hits a real WAD.

using System.IO;
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

    private static byte[] SolidFlat(byte index)
    {
        var f = new byte[DoomFlatReader.RawSize];
        for (int i = 0; i < f.Length; i++) f[i] = index;
        return f;
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
