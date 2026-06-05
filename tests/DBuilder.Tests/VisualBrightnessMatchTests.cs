// ABOUTME: Tests UDB-style visual Match Brightness behavior for UDMF surface light fields.
// ABOUTME: Covers target brightness resolution plus absolute and relative light writes.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualBrightnessMatchTests
{
    [Fact]
    public void ReadsRelativeFloorBrightnessFromSectorBase()
    {
        var sector = new Sector { Brightness = 160 };
        sector.SetIntegerField("lightfloor", -32);

        bool ok = VisualBrightnessMatch.TryReadTargetBrightness(FloorHit(sector), out int brightness, out string message);

        Assert.True(ok);
        Assert.Equal("", message);
        Assert.Equal(128, brightness);
    }

    [Fact]
    public void ReadsAbsoluteCeilingBrightness()
    {
        var sector = new Sector { Brightness = 160 };
        sector.SetField("lightceilingabsolute", true);
        sector.SetIntegerField("lightceiling", 96);

        bool ok = VisualBrightnessMatch.TryReadTargetBrightness(CeilingHit(sector), out int brightness, out _);

        Assert.True(ok);
        Assert.Equal(96, brightness);
    }

    [Fact]
    public void AppliesTargetBrightnessToRelativeAndAbsoluteSurfaces()
    {
        var target = new Sector { Brightness = 120 };
        target.SetIntegerField("lightfloor", 40);

        var relativeFloor = new Sector { Brightness = 96 };
        var absoluteCeiling = new Sector { Brightness = 32 };
        absoluteCeiling.SetField("lightceilingabsolute", true);
        var sideSector = new Sector { Brightness = 144 };
        Sidedef side = Sidedef(sideSector);

        Assert.True(VisualBrightnessMatch.TryReadTargetBrightness(FloorHit(target), out int brightness, out _));

        VisualBrightnessMatchResult result = VisualBrightnessMatch.Apply(
            brightness,
            [FloorHit(relativeFloor), CeilingHit(absoluteCeiling), WallHit(side)],
            FloorHit(target),
            config: null);

        Assert.Equal(3, result.ChangedSurfaces);
        Assert.Equal("Matched brightness for 3 surfaces.", result.Message);
        Assert.Equal(64, relativeFloor.GetIntegerField("lightfloor"));
        Assert.Equal(160, absoluteCeiling.GetIntegerField("lightceiling"));
        Assert.Equal(16, side.GetIntegerField("light"));
    }

    [Fact]
    public void WallMatchBrightnessUpdatesLightFogUsingMapInfo()
    {
        var target = new Sector { Brightness = 120 };
        target.SetIntegerField("lightfloor", 40);
        var side = Sidedef(new Sector { Brightness = 144 });
        var mapInfo = new MapInfoEntry { FadeColor = (1, 2, 3), FogDensity = 255 };

        Assert.True(VisualBrightnessMatch.TryReadTargetBrightness(FloorHit(target), out int brightness, out _));

        VisualBrightnessMatch.Apply(
            brightness,
            [WallHit(side)],
            FloorHit(target),
            config: null,
            mapInfo);

        Assert.Equal(16, side.GetIntegerField("light"));
        Assert.True(side.IsFlagSet("lightfog"));
    }

    [Fact]
    public void StatusUsesSelectedCountEvenWhenHighlightedSurfaceIsSkippedLikeUdb()
    {
        var target = new Sector { Brightness = 120 };
        var selected = new Sector { Brightness = 96 };
        Assert.True(VisualBrightnessMatch.TryReadTargetBrightness(FloorHit(target), out int brightness, out _));

        VisualBrightnessMatchResult result = VisualBrightnessMatch.Apply(
            brightness,
            [FloorHit(target), FloorHit(selected)],
            FloorHit(target),
            config: null);

        Assert.Equal(1, result.ChangedSurfaces);
        Assert.Equal("Matched brightness for 2 surfaces.", result.Message);
        Assert.Equal(24, selected.GetIntegerField("lightfloor"));
    }

    [Fact]
    public void RejectsThingTarget()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);

        bool ok = VisualBrightnessMatch.TryReadTargetBrightness(
            new VisualHit(VisualHitKind.Thing, 1, new Vector3D(0, 0, 0), null, null, true, 0, 0, Thing: thing),
            out _,
            out string message);

        Assert.False(ok);
        Assert.Equal(VisualBrightnessMatch.InvalidTargetMessage, message);
    }

    private static VisualHit FloorHit(Sector sector)
        => new(VisualHitKind.Floor, 1, new Vector3D(0, 0, sector.FloorHeight), sector, null, true, 0, 0);

    private static VisualHit CeilingHit(Sector sector)
        => new(VisualHitKind.Ceiling, 1, new Vector3D(0, 0, sector.CeilHeight), sector, null, true, 0, 0);

    private static VisualHit WallHit(Sidedef side)
        => new(VisualHitKind.Wall, 1, new Vector3D(0, 0, 0), side.Sector, side.Line, true, 0, 128, SidedefPart.Middle);

    private static Sidedef Sidedef(Sector sector)
    {
        var start = new Vertex(new Vector2D(0, 0));
        var end = new Vertex(new Vector2D(64, 0));
        var line = new Linedef(start, end);
        var side = new Sidedef(line, true) { Sector = sector };
        line.Front = side;
        return side;
    }
}
