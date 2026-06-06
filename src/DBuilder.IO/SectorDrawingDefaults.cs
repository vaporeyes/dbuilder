// ABOUTME: Applies UDB-style sector drawing override options to newly materialized map sectors.
// ABOUTME: Combines per-map drawing options with game-configuration defaults without editor UI dependencies.

using DBuilder.Map;

namespace DBuilder.IO;

public static class SectorDrawingDefaults
{
    public static void Apply(MapSet map, Sector? sector, MapOptions? options, GameConfiguration? config)
    {
        if (sector == null) return;
        bool useLongTextureNames = options?.UseLongTextureNames ?? config?.UseLongTextureNames ?? false;

        if (options?.OverrideFloorHeight == true) sector.FloorHeight = options.CustomFloorHeight;
        if (options?.OverrideCeilingHeight == true) sector.CeilHeight = options.CustomCeilingHeight;
        if (options?.OverrideBrightness == true) sector.Brightness = options.CustomBrightness;

        string floor = options?.OverrideFloorTexture == true ? options.DefaultFloorTexture : config?.DefaultFloorTexture ?? "";
        string ceiling = options?.OverrideCeilingTexture == true ? options.DefaultCeilingTexture : config?.DefaultCeilingTexture ?? "";
        if (!string.IsNullOrWhiteSpace(floor) && (options?.OverrideFloorTexture == true || IsBlankTexture(sector.FloorTexture)))
            SetFloorTexture(sector, floor, useLongTextureNames);
        if (!string.IsNullOrWhiteSpace(ceiling) && (options?.OverrideCeilingTexture == true || IsBlankTexture(sector.CeilTexture)))
            SetCeilTexture(sector, ceiling, useLongTextureNames);

        string upper = options?.DefaultTopTexture ?? "";
        string middle = options?.OverrideMiddleTexture == true ? options.DefaultWallTexture : config?.DefaultWallTexture ?? "";
        string lower = options?.DefaultBottomTexture ?? "";

        foreach (Sidedef side in map.Sidedefs)
        {
            if (!ReferenceEquals(side.Sector, sector)) continue;

            if (options?.OverrideTopTexture == true && !string.IsNullOrWhiteSpace(upper) && side.HighRequired())
                SetTextureHigh(side, upper, useLongTextureNames);
            if (!string.IsNullOrWhiteSpace(middle) && (options?.OverrideMiddleTexture == true || IsBlankTexture(side.MidTexture)) && side.MiddleRequired())
                SetTextureMid(side, middle, useLongTextureNames);
            if (options?.OverrideBottomTexture == true && !string.IsNullOrWhiteSpace(lower) && side.LowRequired())
                SetTextureLow(side, lower, useLongTextureNames);
        }
    }

    private static bool IsBlankTexture(string? name)
        => string.IsNullOrWhiteSpace(name) || name == "-";

    private static void SetFloorTexture(Sector sector, string texture, bool useLongTextureNames)
    {
        sector.SetFloorTexture(texture);
        sector.LongFloorTexture = Lump.MakeLongName(sector.FloorTexture, useLongTextureNames);
    }

    private static void SetCeilTexture(Sector sector, string texture, bool useLongTextureNames)
    {
        sector.SetCeilTexture(texture);
        sector.LongCeilTexture = Lump.MakeLongName(sector.CeilTexture, useLongTextureNames);
    }

    private static void SetTextureHigh(Sidedef side, string texture, bool useLongTextureNames)
    {
        side.SetTextureHigh(texture);
        side.LongHighTexture = Lump.MakeLongName(side.HighTexture, useLongTextureNames);
    }

    private static void SetTextureMid(Sidedef side, string texture, bool useLongTextureNames)
    {
        side.SetTextureMid(texture);
        side.LongMiddleTexture = Lump.MakeLongName(side.MidTexture, useLongTextureNames);
    }

    private static void SetTextureLow(Sidedef side, string texture, bool useLongTextureNames)
    {
        side.SetTextureLow(texture);
        side.LongLowTexture = Lump.MakeLongName(side.LowTexture, useLongTextureNames);
    }
}
