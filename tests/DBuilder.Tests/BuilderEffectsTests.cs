// ABOUTME: Tests data-level BuilderEffects jitter transforms ported from UDB.
// ABOUTME: Verifies vertex, sector, and thing transforms use cached factors and UDB formulas.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class BuilderEffectsTests
{
    [Fact]
    public void VertexTranslationUsesSinCosAndSafeDistance()
    {
        var vertex = new Vertex(new Vector2D(10, 20));
        var jitter = new VertexJitter(vertex, vertex.Position, Angle2D.DegToRad(90), SafeDistance: 8);

        BuilderEffects.ApplyVertexTranslation(new[] { jitter }, amount: 16);

        Assert.Equal(new Vector2D(18, 20), vertex.Position);
    }

    [Fact]
    public void SectorHeightJitterMatchesFloorAndCeilingDirections()
    {
        var sector = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var jitter = new SectorHeightJitter(sector, 0, 128, FloorFactor: 0.5, CeilingFactor: 0.25);

        BuilderEffects.ApplySectorFloorHeight(new[] { jitter }, amount: 10);
        BuilderEffects.ApplySectorCeilingHeight(new[] { jitter }, amount: 12);

        Assert.Equal(5, sector.FloorHeight);
        Assert.Equal(125, sector.CeilHeight);
    }

    [Fact]
    public void SectorHeightJitterSupportsRaiseOnlyAndLowerOnlyModes()
    {
        var floorSector = new Sector { FloorHeight = 32, CeilHeight = 128 };
        var ceilSector = new Sector { FloorHeight = 0, CeilHeight = 128 };

        BuilderEffects.ApplySectorFloorHeight(
            new[] { new SectorHeightJitter(floorSector, 32, 128, FloorFactor: -0.5, CeilingFactor: 0) },
            amount: 10,
            JitterOffsetMode.RaiseOnly);
        BuilderEffects.ApplySectorCeilingHeight(
            new[] { new SectorHeightJitter(ceilSector, 0, 128, FloorFactor: 0, CeilingFactor: 0.5) },
            amount: 10,
            JitterOffsetMode.LowerOnly);

        Assert.Equal(37, floorSector.FloorHeight);
        Assert.Equal(133, ceilSector.CeilHeight);
    }

    [Fact]
    public void SectorHeightJitterCanUseTriangularVertexHeights()
    {
        var sector = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var floorVertex = new Vertex(new Vector2D(0, 0)) { ZFloor = 4 };
        var ceilingVertex = new Vertex(new Vector2D(64, 0)) { ZCeiling = 120 };
        var jitter = new SectorHeightJitter(
            sector,
            0,
            128,
            FloorFactor: 0.25,
            CeilingFactor: 0.5,
            VertexHeights:
            [
                new SectorVertexHeightJitter(floorVertex, 4, 128, FloorFactor: 0.5, CeilingFactor: 0),
                new SectorVertexHeightJitter(ceilingVertex, 0, 120, FloorFactor: 0, CeilingFactor: 0.25),
            ]);

        BuilderEffects.ApplySectorFloorHeight(new[] { jitter }, amount: 10, useVertexHeights: true);
        BuilderEffects.ApplySectorCeilingHeight(new[] { jitter }, amount: 12, useVertexHeights: true);

        Assert.Equal(0, sector.FloorHeight);
        Assert.Equal(128, sector.CeilHeight);
        Assert.Equal(9, floorVertex.ZFloor);
        Assert.Equal(117, ceilingVertex.ZCeiling);
    }

    [Fact]
    public void SectorPeggingAppliesNamedAndNumericLineFlags()
    {
        var map = new MapSet();
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        var sector = map.AddSector();
        map.AddSidedef(line, isFront: true, sector);
        map.BuildIndexes();

        int changed = BuilderEffects.ApplySectorPegging(
            [sector],
            upperUnpeggedFlag: "dontpegtop",
            lowerUnpeggedFlag: "16",
            upperUnpegged: true,
            lowerUnpegged: true);

        Assert.Equal(2, changed);
        Assert.True(line.IsFlagSet("dontpegtop"));
        Assert.Equal(16, line.Flags & 16);
    }

    [Fact]
    public void ThingTranslationRotationPitchRollAndHeightUseCachedValues()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001, angle: 90)
        {
            Height = 4,
            Pitch = 10,
            Roll = 20
        };
        var jitter = new ThingJitter(
            thing,
            thing.Position,
            thing.Angle,
            thing.Pitch,
            thing.Roll,
            thing.Height,
            thing.ScaleX,
            thing.ScaleY,
            OffsetAngle: Angle2D.DegToRad(0),
            RotationFactor: 0.5,
            PitchFactor: 0.25,
            RollFactor: -0.5,
            HeightFactor: 0.5,
            ScaleXFactor: 0,
            ScaleYFactor: 0,
            SafeDistance: 64,
            SectorHeight: 32);

        BuilderEffects.ApplyThingTranslation(new[] { jitter }, amount: 10);
        BuilderEffects.ApplyThingRotation(new[] { jitter }, amount: 20, snapToDoomAngles: true);
        BuilderEffects.ApplyThingPitch(new[] { jitter }, amount: 20, relative: true);
        BuilderEffects.ApplyThingRoll(new[] { jitter }, amount: 20, relative: false);
        BuilderEffects.ApplyThingHeight(new[] { jitter }, amount: 8);

        Assert.Equal(new Vector2D(0, 10), thing.Position);
        Assert.Equal(90, thing.Angle);
        Assert.Equal(15, thing.Pitch);
        Assert.Equal(350, thing.Roll);
        Assert.Equal(6, thing.Height);
    }

    [Fact]
    public void ThingScaleSupportsRelativeUniformAndSwappedRanges()
    {
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            ScaleX = 1.5,
            ScaleY = 2.0
        };
        var jitter = new ThingJitter(
            thing,
            thing.Position,
            thing.Angle,
            thing.Pitch,
            thing.Roll,
            thing.Height,
            thing.ScaleX,
            thing.ScaleY,
            OffsetAngle: 0,
            RotationFactor: 0,
            PitchFactor: 0,
            RollFactor: 0,
            HeightFactor: 0,
            ScaleXFactor: 0.25,
            ScaleYFactor: 0.75);

        BuilderEffects.ApplyThingScale(new[] { jitter }, minX: 2, maxX: 1, minY: 4, maxY: 2, relative: true, uniform: false);

        Assert.Equal(2.75, thing.ScaleX);
        Assert.Equal(5.5, thing.ScaleY);
    }

    [Fact]
    public void DirectionalShadingAppliesRelativeSectorLightAndColorFromCachedValues()
    {
        var sector = new Sector { Brightness = 160 };
        sector.FloorSlope = new Vector3D(1, 0, 1).GetNormal();
        sector.Fields["lightcolor"] = 0x112233;
        var captured = BuilderEffects.CaptureDirectionalShadingSector(sector);

        int changed = BuilderEffects.ApplyDirectionalShading(
            new[] { captured },
            Array.Empty<DirectionalShadingSide>(),
            new DirectionalShadingOptions(SunAngleDegrees: 0, LightAmount: 64, LightColor: 0xFDEBD7, ShadeAmount: 16, ShadeColor: 0xABC8EB));

        Assert.Equal(1, changed);
        Assert.Equal(64, sector.GetIntegerField("lightfloor"));
        Assert.Equal(0xFDEBD7, sector.GetIntegerField("lightcolor"));
    }

    [Fact]
    public void DirectionalShadingUsesInitialAbsoluteSectorLightForRepeatedUpdates()
    {
        var sector = new Sector { Brightness = 96 };
        sector.FloorSlope = new Vector3D(1, 0, 1).GetNormal();
        sector.Fields["lightfloorabsolute"] = true;
        sector.Fields["lightfloor"] = 100;
        var captured = BuilderEffects.CaptureDirectionalShadingSector(sector);

        BuilderEffects.ApplyDirectionalShading(
            new[] { captured },
            Array.Empty<DirectionalShadingSide>(),
            new DirectionalShadingOptions(SunAngleDegrees: 0, LightAmount: 64, LightColor: BuilderEffects.WhiteNoAlpha, ShadeAmount: 16, ShadeColor: BuilderEffects.WhiteNoAlpha));
        BuilderEffects.ApplyDirectionalShading(
            new[] { captured },
            Array.Empty<DirectionalShadingSide>(),
            new DirectionalShadingOptions(SunAngleDegrees: 0, LightAmount: 16, LightColor: BuilderEffects.WhiteNoAlpha, ShadeAmount: 16, ShadeColor: BuilderEffects.WhiteNoAlpha));

        Assert.Equal(116, sector.GetIntegerField("lightfloor"));
        Assert.False(sector.Fields.ContainsKey("lightcolor"));
    }

    [Fact]
    public void DirectionalShadingAppliesSidedefLightUsingUdbSideNormal()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(0, -64)));
        line.Angle = 0;
        var sector = new Sector { Brightness = 100 };
        var side = new Sidedef(line, isFront: false) { Sector = sector };
        var captured = BuilderEffects.CaptureDirectionalShadingSide(side);

        BuilderEffects.ApplyDirectionalShading(
            Array.Empty<DirectionalShadingSector>(),
            new[] { captured },
            new DirectionalShadingOptions(SunAngleDegrees: 0, LightAmount: 64, LightColor: 0xFDEBD7, ShadeAmount: 16, ShadeColor: 0xABC8EB));

        Assert.Equal(27, side.GetIntegerField("light"));
    }
}
