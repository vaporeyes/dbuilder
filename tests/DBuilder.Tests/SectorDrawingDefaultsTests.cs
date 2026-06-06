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
            longtexturenames = true;
            defaultwalltexture = "WALLCFG";
            defaultfloortexture = "FLOORCONFIG";
            defaultceilingtexture = "CEILCONFIG";
            """);

        SectorDrawingDefaults.Apply(map, sector, null, config);

        Assert.Equal("FLOORCONFIG", sector.FloorTexture);
        Assert.Equal("CEILCONFIG", sector.CeilTexture);
        Assert.Equal(Lump.MakeLongName("FLOORCONFIG", useLongNames: true), sector.LongFloorTexture);
        Assert.Equal(Lump.MakeLongName("CEILCONFIG", useLongNames: true), sector.LongCeilTexture);
        Assert.All(sector.Sidedefs, side => Assert.Equal("WALLCFG", side.MidTexture));
        Assert.All(sector.Sidedefs, side => Assert.Equal(Lump.MakeLongName("WALLCFG", useLongNames: true), side.LongMiddleTexture));
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
            DefaultFloorTexture = "FLOOROPTION",
            OverrideCeilingTexture = true,
            DefaultCeilingTexture = "CEILOPTION",
            OverrideMiddleTexture = true,
            DefaultWallTexture = "MIDOPTION",
            OverrideFloorHeight = true,
            CustomFloorHeight = -24,
            OverrideCeilingHeight = true,
            CustomCeilingHeight = 192,
            OverrideBrightness = true,
            CustomBrightness = 208,
            UseLongTextureNames = true,
        };

        SectorDrawingDefaults.Apply(map, sector, options, config);

        Assert.Equal(-24, sector.FloorHeight);
        Assert.Equal(192, sector.CeilHeight);
        Assert.Equal(208, sector.Brightness);
        Assert.Equal("FLOOROPTION", sector.FloorTexture);
        Assert.Equal("CEILOPTION", sector.CeilTexture);
        Assert.Equal(Lump.MakeLongName("FLOOROPTION", useLongNames: true), sector.LongFloorTexture);
        Assert.Equal(Lump.MakeLongName("CEILOPTION", useLongNames: true), sector.LongCeilTexture);
        Assert.All(sector.Sidedefs, side => Assert.Equal("MIDOPTION", side.MidTexture));
        Assert.All(sector.Sidedefs, side => Assert.Equal(Lump.MakeLongName("MIDOPTION", useLongNames: true), side.LongMiddleTexture));
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
            DefaultTopTexture = "UPPEROPTION",
            OverrideBottomTexture = true,
            DefaultBottomTexture = "LOWEROPTION",
            OverrideMiddleTexture = true,
            DefaultWallTexture = "MIDOPT",
            UseLongTextureNames = true,
        };

        SectorDrawingDefaults.Apply(map, target, options, null);

        Assert.True(front.HighRequired());
        Assert.True(front.LowRequired());
        Assert.False(front.MiddleRequired());
        Assert.Equal("UPPEROPTION", front.HighTexture);
        Assert.Equal("LOWEROPTION", front.LowTexture);
        Assert.Equal(Lump.MakeLongName("UPPEROPTION", useLongNames: true), front.LongHighTexture);
        Assert.Equal(Lump.MakeLongName("LOWEROPTION", useLongNames: true), front.LongLowTexture);
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
