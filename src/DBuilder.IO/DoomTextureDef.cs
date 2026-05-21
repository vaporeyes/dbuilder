// ABOUTME: One entry from a TEXTURE1/TEXTURE2 lump - a named wall surface composed from one or more PNAMES-referenced patches.
// ABOUTME: Plain data record; composition (actually fetching patches and blitting) lives in DoomWallTextureCompositor.

namespace DBuilder.IO;

public sealed class DoomTextureDef
{
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<DoomTexturePatch> Patches { get; }

    /// <summary>UDB exposes worldpanning as a flag; preserved for symmetry but not used by the compositor.</summary>
    public bool WorldPanning { get; }

    /// <summary>Source format the def was parsed from (Doom-classic vs Strife). Mostly informational.</summary>
    public DoomTextureFormat SourceFormat { get; }

    public DoomTextureDef(string name, int width, int height, IReadOnlyList<DoomTexturePatch> patches, bool worldPanning, DoomTextureFormat format)
    {
        Name = name;
        Width = width;
        Height = height;
        Patches = patches;
        WorldPanning = worldPanning;
        SourceFormat = format;
    }
}

public sealed record DoomTexturePatch(int OriginX, int OriginY, int PatchIndex);

public enum DoomTextureFormat
{
    /// <summary>Classic Doom: 22-byte def header + 10-byte patch records (extra stepdir/colormap fields).</summary>
    DoomClassic,
    /// <summary>Strife: 18-byte def header + 6-byte patch records (no stepdir/colormap).</summary>
    Strife,
}
