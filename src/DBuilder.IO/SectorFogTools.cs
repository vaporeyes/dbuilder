// ABOUTME: Provides UDB-style sector fog color selection helpers.
// ABOUTME: Uses MAPINFO fog colors and configured sky flat names without depending on rendering assemblies.

using DBuilder.Map;

namespace DBuilder.IO;

public static class SectorFogTools
{
    public const int BlackArgb = unchecked((int)0xFF000000);
    public const string FadeColorField = "fadecolor";

    public static int GetSectorFadeColorArgb(Sector sector, MapInfoEntry? mapInfo, GameConfiguration? config)
        => GetSectorFadeColorArgb(sector, mapInfo, config?.SkyFlatName ?? "F_SKY1");

    public static int GetSectorFadeColorArgb(Sector sector, MapInfoEntry? mapInfo, string skyFlatName = "F_SKY1")
    {
        if (sector.Fields.ContainsKey(FadeColorField))
            return sector.GetIntegerField(FadeColorField);

        if ((mapInfo?.HasOutsideFogColor ?? false) && HasSkyCeiling(sector, skyFlatName) && mapInfo.OutsideFogColor is { } outsideFog)
            return ToOpaqueArgb(outsideFog);

        return (mapInfo?.HasFadeColor ?? false) && mapInfo.FadeColor is { } fade
            ? ToOpaqueArgb(fade)
            : BlackArgb;
    }

    private static int ToOpaqueArgb((byte R, byte G, byte B) color)
        => unchecked((int)(0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B));

    private static bool HasSkyCeiling(Sector sector, string skyFlatName)
        => string.Equals(sector.CeilTexture, skyFlatName, StringComparison.OrdinalIgnoreCase);
}
