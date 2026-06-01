// ABOUTME: Provides UDB-style sidedef fog flag maintenance helpers.
// ABOUTME: Applies MAPINFO fog state and configured sky flat names without using UDB globals.

using DBuilder.Map;

namespace DBuilder.IO;

public static class SidedefFogTools
{
    public static int UpdateLightFogFlag(Sidedef side, MapInfoEntry? mapInfo, GameConfiguration? config)
        => UpdateLightFogFlag(
            side,
            mapInfo?.HasFadeColor ?? false,
            mapInfo?.HasOutsideFogColor ?? false,
            config?.SkyFlatName ?? "F_SKY1");

    public static int UpdateLightFogFlag(Sidedef side, bool mapHasFadeColor, bool mapHasOutsideFogColor, string skyFlatName = "F_SKY1")
    {
        if (side.Sector == null) return 0;

        if (!side.Fields.ContainsKey("light"))
        {
            if (side.IsFlagSet("lightfog"))
            {
                side.SetFlag("lightfog", false);
                return -1;
            }

            return 0;
        }

        bool needsLightFog = mapHasFadeColor
            || (mapHasOutsideFogColor && HasSkyCeiling(side.Sector, skyFlatName))
            || side.Sector.Fields.ContainsKey("fadecolor");

        if (needsLightFog)
        {
            if (!side.IsFlagSet("lightfog"))
            {
                side.SetFlag("lightfog", true);
                return 1;
            }
        }
        else if (side.IsFlagSet("lightfog"))
        {
            side.SetFlag("lightfog", false);
            return -1;
        }

        return 0;
    }

    private static bool HasSkyCeiling(Sector sector, string skyFlatName)
        => string.Equals(sector.CeilTexture, skyFlatName, StringComparison.OrdinalIgnoreCase);
}
