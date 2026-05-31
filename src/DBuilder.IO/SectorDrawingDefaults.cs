// ABOUTME: Applies UDB-style sector drawing override options to newly materialized map sectors.
// ABOUTME: Combines per-map drawing options with game-configuration defaults without editor UI dependencies.

using DBuilder.Map;

namespace DBuilder.IO;

public static class SectorDrawingDefaults
{
    public static void Apply(MapSet map, Sector? sector, MapOptions? options, GameConfiguration? config)
    {
        if (sector == null) return;

        if (options?.OverrideFloorHeight == true) sector.FloorHeight = options.CustomFloorHeight;
        if (options?.OverrideCeilingHeight == true) sector.CeilHeight = options.CustomCeilingHeight;
        if (options?.OverrideBrightness == true) sector.Brightness = options.CustomBrightness;

        string floor = options?.OverrideFloorTexture == true ? options.DefaultFloorTexture : config?.DefaultFloorTexture ?? "";
        string ceiling = options?.OverrideCeilingTexture == true ? options.DefaultCeilingTexture : config?.DefaultCeilingTexture ?? "";
        if (!string.IsNullOrWhiteSpace(floor) && (options?.OverrideFloorTexture == true || IsBlankTexture(sector.FloorTexture)))
            sector.FloorTexture = floor;
        if (!string.IsNullOrWhiteSpace(ceiling) && (options?.OverrideCeilingTexture == true || IsBlankTexture(sector.CeilTexture)))
            sector.CeilTexture = ceiling;

        string upper = options?.DefaultTopTexture ?? "";
        string middle = options?.OverrideMiddleTexture == true ? options.DefaultWallTexture : config?.DefaultWallTexture ?? "";
        string lower = options?.DefaultBottomTexture ?? "";

        foreach (Sidedef side in map.Sidedefs)
        {
            if (!ReferenceEquals(side.Sector, sector)) continue;

            if (options?.OverrideTopTexture == true && !string.IsNullOrWhiteSpace(upper) && side.HighRequired())
                side.SetTextureHigh(upper);
            if (!string.IsNullOrWhiteSpace(middle) && (options?.OverrideMiddleTexture == true || IsBlankTexture(side.MidTexture)) && side.MiddleRequired())
                side.SetTextureMid(middle);
            if (options?.OverrideBottomTexture == true && !string.IsNullOrWhiteSpace(lower) && side.LowRequired())
                side.SetTextureLow(lower);
        }
    }

    private static bool IsBlankTexture(string? name)
        => string.IsNullOrWhiteSpace(name) || name == "-";
}
