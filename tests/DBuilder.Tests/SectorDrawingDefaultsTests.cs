// ABOUTME: Tests UDB-style sector drawing default application for newly created sectors.
// ABOUTME: Covers game-config fallbacks and per-map drawing option overrides without editor UI.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorDrawingDefaultsTests
{
    [Fact]
    public void AppliesGameConfigTextureDefaultsToBlankNewSector()
    {
        var map = new MapSet();
        Sector sector = CreateSquareSector(map);
        var config = GameConfiguration.FromText("""
            defaultwalltexture = "WALLCFG";
            defaultfloortexture = "FLOORCFG";
            defaultceilingtexture = "CEILCFG";
            """);

        SectorDrawingDefaults.Apply(map, sector, null, config);

        Assert.Equal("FLOORCFG", sector.FloorTexture);
        Assert.Equal("CEILCFG", sector.CeilTexture);
        Assert.All(sector.Sidedefs, side => Assert.Equal("WALLCFG", side.MidTexture));
    }

    [Fact]
    public void AppliesEnabledMapOptionOverridesToNewSector()
    {
        var map = new MapSet();
        Sector sector = CreateSquareSector(map);
        var config = GameConfiguration.FromText("""
            defaultwalltexture = "WALLCFG";
            defaultfloortexture = "FLOORCFG";
            defaultceilingtexture = "CEILCFG";
            """);
        var options = new MapOptions
        {
            OverrideFloorTexture = true,
            DefaultFloorTexture = "FLOOROPT",
            OverrideCeilingTexture = true,
            DefaultCeilingTexture = "CEILOPT",
            OverrideMiddleTexture = true,
            DefaultWallTexture = "MIDOPT",
            OverrideFloorHeight = true,
            CustomFloorHeight = -24,
            OverrideCeilingHeight = true,
            CustomCeilingHeight = 192,
            OverrideBrightness = true,
            CustomBrightness = 208,
        };

        SectorDrawingDefaults.Apply(map, sector, options, config);

        Assert.Equal(-24, sector.FloorHeight);
        Assert.Equal(192, sector.CeilHeight);
        Assert.Equal(208, sector.Brightness);
        Assert.Equal("FLOOROPT", sector.FloorTexture);
        Assert.Equal("CEILOPT", sector.CeilTexture);
        Assert.All(sector.Sidedefs, side => Assert.Equal("MIDOPT", side.MidTexture));
    }

    [Fact]
    public void AppliesUpperAndLowerOverridesToRequiredTwoSidedParts()
    {
        var map = new MapSet();
        var target = map.AddSector();
        target.FloorHeight = -16;
        target.CeilHeight = 128;
        var neighbor = map.AddSector();
        neighbor.FloorHeight = 0;
        neighbor.CeilHeight = 64;
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(v0, v1);
        var front = map.AddSidedef(line, true, target);
        map.AddSidedef(line, false, neighbor);
        map.BuildIndexes();
        var options = new MapOptions
        {
            OverrideTopTexture = true,
            DefaultTopTexture = "UPPEROPT",
            OverrideBottomTexture = true,
            DefaultBottomTexture = "LOWEROPT",
            OverrideMiddleTexture = true,
            DefaultWallTexture = "MIDOPT",
        };

        SectorDrawingDefaults.Apply(map, target, options, null);

        Assert.True(front.HighRequired());
        Assert.True(front.LowRequired());
        Assert.False(front.MiddleRequired());
        Assert.Equal("UPPEROPT", front.HighTexture);
        Assert.Equal("LOWEROPT", front.LowTexture);
        Assert.Equal("-", front.MidTexture);
    }

    private static Sector CreateSquareSector(MapSet map)
    {
        var loop = new[]
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)),
            map.AddVertex(new Vector2D(64, 64)),
            map.AddVertex(new Vector2D(0, 64)),
        };
        Sector sector = SectorBuilder.CreateSector(map, loop)!;
        map.BuildIndexes();
        return sector;
    }
}
