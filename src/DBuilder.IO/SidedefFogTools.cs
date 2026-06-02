// ABOUTME: Provides UDB-style sidedef fog flag maintenance helpers.
// ABOUTME: Applies MAPINFO fog state and configured sky flat names without using UDB globals.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record SidedefLightFogFlagResult(int AddedCount, int RemovedCount)
{
    public bool Changed => AddedCount > 0 || RemovedCount > 0;

    public string Message => "Added 'lightfog' flag to " + AddedCount + " sidedefs, removed it from " + RemovedCount + " sidedefs.";
}

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

    public static SidedefLightFogFlagResult ApplyLightFogFlags(IEnumerable<Linedef> linedefs, MapInfoEntry? mapInfo, GameConfiguration? config)
    {
        int addedCount = 0;
        int removedCount = 0;

        foreach (Linedef linedef in linedefs)
        {
            Count(UpdateSide(linedef.Front));
            Count(UpdateSide(linedef.Back));
        }

        return new SidedefLightFogFlagResult(addedCount, removedCount);

        int UpdateSide(Sidedef? side)
            => side == null ? 0 : UpdateLightFogFlag(side, mapInfo, config);

        void Count(int result)
        {
            if (result > 0) addedCount++;
            else if (result < 0) removedCount++;
        }
    }

    private static bool HasSkyCeiling(Sector sector, string skyFlatName)
        => string.Equals(sector.CeilTexture, skyFlatName, StringComparison.OrdinalIgnoreCase);
}
