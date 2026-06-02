// ABOUTME: Verifies UDB-style thing model render gating and transform planning.
// ABOUTME: Pins MODELDEF offsets, scales, thing scale, voxel transforms, and rotation center behavior.

using System.Numerics;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ThingModelRenderPlannerTests
{
    [Fact]
    public void ShouldRenderMatchesUdbModelRenderModes()
    {
        Assert.False(ThingModelRenderPlanner.ShouldRender(ThingModelRenderMode.None, selected: true));
        Assert.False(ThingModelRenderPlanner.ShouldRender(ThingModelRenderMode.Selection, selected: false));
        Assert.True(ThingModelRenderPlanner.ShouldRender(ThingModelRenderMode.Selection, selected: true));
        Assert.False(ThingModelRenderPlanner.ShouldRender(
            ThingModelRenderMode.ActiveThingsFilter,
            selected: false,
            activeFilterAlpha: 0.5));
        Assert.True(ThingModelRenderPlanner.ShouldRender(
            ThingModelRenderMode.ActiveThingsFilter,
            selected: false,
            activeFilterAlpha: 1.0));
        Assert.True(ThingModelRenderPlanner.ShouldRender(ThingModelRenderMode.All, selected: false));
    }

    [Fact]
    public void ShouldRender3DMatchesUdbVisualModelModes()
    {
        Assert.False(ThingModelRenderPlanner.ShouldRender3D(ThingModelRenderMode.None, selected: true));
        Assert.False(ThingModelRenderPlanner.ShouldRender3D(ThingModelRenderMode.Selection, selected: false));
        Assert.True(ThingModelRenderPlanner.ShouldRender3D(ThingModelRenderMode.Selection, selected: true));
        Assert.True(ThingModelRenderPlanner.ShouldRender3D(ThingModelRenderMode.ActiveThingsFilter, selected: false));
        Assert.True(ThingModelRenderPlanner.ShouldRender3D(ThingModelRenderMode.All, selected: false));
    }

    [Fact]
    public void CyclesModelRenderModesLikeUdb()
    {
        Assert.Equal(ThingModelRenderMode.Selection, ThingModelRenderPlanner.NextMode(ThingModelRenderMode.None));
        Assert.Equal(ThingModelRenderMode.ActiveThingsFilter, ThingModelRenderPlanner.NextMode(ThingModelRenderMode.Selection));
        Assert.Equal(ThingModelRenderMode.All, ThingModelRenderPlanner.NextMode(ThingModelRenderMode.ActiveThingsFilter));
        Assert.Equal(ThingModelRenderMode.None, ThingModelRenderPlanner.NextMode(ThingModelRenderMode.All));
        Assert.Equal("ACTIVE THINGS FILTER ONLY", ThingModelRenderPlanner.StatusLabel(ThingModelRenderMode.ActiveThingsFilter));
    }

    [Fact]
    public void CyclesDynamicLightRenderModesLikeUdbVisualMode()
    {
        Assert.Equal(ThingLightRenderMode.All, ThingLightRenderPlanner.NextMode(ThingLightRenderMode.None));
        Assert.Equal(ThingLightRenderMode.Animated, ThingLightRenderPlanner.NextMode(ThingLightRenderMode.All));
        Assert.Equal(ThingLightRenderMode.None, ThingLightRenderPlanner.NextMode(ThingLightRenderMode.Animated));
        Assert.False(ThingLightRenderPlanner.ShouldRender(ThingLightRenderMode.None));
        Assert.True(ThingLightRenderPlanner.ShouldRender(ThingLightRenderMode.All));
        Assert.True(ThingLightRenderPlanner.ShouldRender(ThingLightRenderMode.Animated));
        Assert.Equal("ANIMATED", ThingLightRenderPlanner.StatusLabel(ThingLightRenderMode.Animated));
    }

    [Fact]
    public void BuildsModeldefTransformWithUdbOffsetAndScaleAxes()
    {
        ThingModelDisplay display = Display(
            scale: new ModeldefVector(2.0f, 3.0f, 4.0f),
            offset: new ModeldefVector(5.0f, 7.0f, 11.0f));

        ThingModelRenderPlan plan = ThingModelRenderPlanner.Plan3D(display, new ThingModelRenderInput());

        Vector3 origin = Vector3.Transform(Vector3.Zero, plan.ModeldefTransform);
        Vector3 xAxis = Vector3.Transform(Vector3.UnitX, plan.ModeldefTransform) - origin;
        Vector3 yAxis = Vector3.Transform(Vector3.UnitY, plan.ModeldefTransform) - origin;
        Vector3 zAxis = Vector3.Transform(Vector3.UnitZ, plan.ModeldefTransform) - origin;

        AssertVector(new Vector3(7.0f, -5.0f, 11.0f), origin);
        AssertVector(new Vector3(2.0f, 0.0f, 0.0f), xAxis);
        AssertVector(new Vector3(0.0f, 3.0f, 0.0f), yAxis);
        AssertVector(new Vector3(0.0f, 0.0f, 4.0f), zAxis);
    }

    [Fact]
    public void Plan3DAppliesThingScaleAndPositionLikeUdb()
    {
        ThingModelRenderInput input = new(
            PositionX: 10.0,
            PositionY: 20.0,
            PositionZ: 30.0,
            ScaleX: 2.0,
            ScaleY: 3.0,
            ActorScaleWidth: 4.0,
            ActorScaleHeight: 5.0);

        ThingModelRenderPlan plan = ThingModelRenderPlanner.Plan3D(Display(), input);

        Vector3 origin = Vector3.Transform(Vector3.Zero, plan.World3D);
        Vector3 xAxis = Vector3.Transform(Vector3.UnitX, plan.World3D) - origin;
        Vector3 yAxis = Vector3.Transform(Vector3.UnitY, plan.World3D) - origin;
        Vector3 zAxis = Vector3.Transform(Vector3.UnitZ, plan.World3D) - origin;

        AssertVector(new Vector3(10.0f, 20.0f, 30.0f), origin);
        AssertVector(new Vector3(8.0f, 0.0f, 0.0f), xAxis);
        AssertVector(new Vector3(0.0f, 8.0f, 0.0f), yAxis);
        AssertVector(new Vector3(0.0f, 0.0f, 15.0f), zAxis);
    }

    [Fact]
    public void Plan3DRotatesAroundRotationCenterWhenEnabled()
    {
        ThingModelDisplay display = Display(
            rotationCenter: new ModeldefVector(1.0f, 0.0f, 0.0f),
            useRotationCenter: true);
        ThingModelRenderInput input = new(AngleRadians: MathF.PI / 2.0f);

        ThingModelRenderPlan plan = ThingModelRenderPlanner.Plan3D(display, input);

        Vector3 origin = Vector3.Transform(Vector3.Zero, plan.World3D);

        Assert.True(plan.UsesRotationCenter);
        AssertVector(new Vector3(1.0f, -1.0f, 0.0f), origin);
    }

    [Fact]
    public void PlanVoxel3DAppliesThingScaleRotationAndPosition()
    {
        ThingModelRenderInput input = new(
            PositionX: 10.0,
            PositionY: 20.0,
            PositionZ: 30.0,
            ScaleX: 2.0,
            ScaleY: 3.0,
            ActorScaleWidth: 4.0,
            ActorScaleHeight: 5.0,
            AngleRadians: MathF.PI / 2.0f);

        Matrix4x4 transform = ThingModelRenderPlanner.PlanVoxel3D(input);

        Vector3 transformed = Vector3.Transform(new Vector3(1.0f, 0.0f, 1.0f), transform);
        AssertVector(new Vector3(10.0f, 28.0f, 45.0f), transformed);
    }

    private static ThingModelDisplay Display(
        ModeldefVector? scale = null,
        ModeldefVector? offset = null,
        ModeldefVector? rotationCenter = null,
        bool useRotationCenter = false)
        => new(
            new Modeldef(),
            "",
            scale ?? new ModeldefVector(1.0f, 1.0f, 1.0f),
            offset ?? new ModeldefVector(0.0f, 0.0f, 0.0f),
            rotationCenter ?? new ModeldefVector(0.0f, 0.0f, 0.0f),
            AngleOffset: 0.0f,
            PitchOffset: 0.0f,
            RollOffset: 0.0f,
            InheritActorPitch: false,
            UseActorPitch: false,
            UseActorRoll: false,
            UseRotationCenter: useRotationCenter,
            Array.Empty<ThingModelDisplayPart>());

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 5);
        Assert.Equal(expected.Y, actual.Y, precision: 5);
        Assert.Equal(expected.Z, actual.Z, precision: 5);
    }
}
