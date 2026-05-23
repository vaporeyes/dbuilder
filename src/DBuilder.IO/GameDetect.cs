// ABOUTME: Best-effort game detection from a WAD's signature lumps so the editor can auto-load a matching config.
// ABOUTME: Distinguishes Hexen/Heretic/Doom2/Doom by lumps unique to each IWAD (WINNOWR, MUMSIT, MAP01, E1M1).

namespace DBuilder.IO;

/// <summary>A detected base game, used to pick a default game configuration.</summary>
public enum DetectedGame { Unknown, Doom, Doom2, Heretic, Hexen }

public static class GameDetect
{
    /// <summary>
    /// Identifies the base game from characteristic lumps. Order matters: Hexen and Heretic share some lumps
    /// (TINTTAB, ADVISOR), so each is keyed on a lump unique to it. Falls back to Doom2 (MAP01) / Doom (E1M1)
    /// by map naming, then Unknown. Reliable for IWADs; PWADs without their IWAD may report by map naming only.
    /// </summary>
    public static DetectedGame FromWad(WAD wad)
    {
        if (wad.FindLump("WINNOWR") != null) return DetectedGame.Hexen;   // Hexen-only sound
        if (wad.FindLump("MUMSIT") != null) return DetectedGame.Heretic;  // Heretic Golem sound
        if (wad.FindLump("MAP01") != null) return DetectedGame.Doom2;
        if (wad.FindLump("E1M1") != null) return DetectedGame.Doom;
        return DetectedGame.Unknown;
    }
}
