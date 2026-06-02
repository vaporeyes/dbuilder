// ABOUTME: Plans UDB-style thing model render eligibility and transform matrices.
// ABOUTME: Keeps MODELDEF and thing transform math testable before live mesh drawing is wired.

using System.Numerics;

namespace DBuilder.IO;

public enum ThingModelRenderMode
{
    None,
    Selection,
    ActiveThingsFilter,
    All,
}

public sealed record ThingModelRenderInput(
    double PositionX = 0.0,
    double PositionY = 0.0,
    double PositionZ = 0.0,
    double ScaleX = 1.0,
    double ScaleY = 1.0,
    double ActorScaleWidth = 1.0,
    double ActorScaleHeight = 1.0,
    double AngleRadians = 0.0,
    double PitchRadians = 0.0,
    double RollRadians = 0.0,
    bool Selected = false,
    double ActiveFilterAlpha = 1.0);

public sealed record ThingModelRenderPlan(
    Matrix4x4 World3D,
    Matrix4x4 ModeldefTransform,
    Matrix4x4 ThingScale,
    Matrix4x4 ThingRotation,
    bool UsesRotationCenter);

public static class ThingModelRenderPlanner
{
    public static ThingModelRenderMode NextMode(ThingModelRenderMode mode)
        => mode switch
        {
            ThingModelRenderMode.None => ThingModelRenderMode.Selection,
            ThingModelRenderMode.Selection => ThingModelRenderMode.ActiveThingsFilter,
            ThingModelRenderMode.ActiveThingsFilter => ThingModelRenderMode.All,
            ThingModelRenderMode.All => ThingModelRenderMode.None,
            _ => ThingModelRenderMode.All,
        };

    public static string StatusLabel(ThingModelRenderMode mode)
        => mode switch
        {
            ThingModelRenderMode.None => "NONE",
            ThingModelRenderMode.Selection => "SELECTION ONLY",
            ThingModelRenderMode.ActiveThingsFilter => "ACTIVE THINGS FILTER ONLY",
            ThingModelRenderMode.All => "ALL",
            _ => "ALL",
        };

    public static bool ShouldRender(ThingModelRenderMode mode, bool selected, double activeFilterAlpha = 1.0)
        => mode switch
        {
            ThingModelRenderMode.None => false,
            ThingModelRenderMode.Selection => selected,
            ThingModelRenderMode.ActiveThingsFilter => activeFilterAlpha >= 1.0,
            ThingModelRenderMode.All => true,
            _ => false,
        };

    public static bool ShouldRender3D(ThingModelRenderMode mode, bool selected)
        => mode switch
        {
            ThingModelRenderMode.None => false,
            ThingModelRenderMode.Selection => selected,
            ThingModelRenderMode.ActiveThingsFilter => true,
            ThingModelRenderMode.All => true,
            _ => false,
        };

    public static ThingModelRenderPlan Plan3D(
        ThingModelDisplay display,
        ThingModelRenderInput input,
        double invertedVerticalViewStretch = 1.0)
    {
        Matrix4x4 modeldefTransform = BuildModeldefTransform(display, invertedVerticalViewStretch);
        Matrix4x4 thingScale = BuildThingScale(input);
        Matrix4x4 thingRotation = BuildThingRotation(input);
        Matrix4x4 position = Matrix4x4.CreateTranslation(
            (float)input.PositionX,
            (float)input.PositionY,
            (float)input.PositionZ);

        Matrix4x4 world = display.UseRotationCenter
            ? modeldefTransform
                * thingScale
                * Matrix4x4.CreateTranslation(-ToVector3(display.RotationCenter))
                * thingRotation
                * Matrix4x4.CreateTranslation(ToVector3(display.RotationCenter))
                * position
            : modeldefTransform * thingScale * thingRotation * position;

        return new ThingModelRenderPlan(
            world,
            modeldefTransform,
            thingScale,
            thingRotation,
            display.UseRotationCenter);
    }

    private static Matrix4x4 BuildModeldefTransform(ThingModelDisplay display, double invertedVerticalViewStretch)
    {
        Matrix4x4 verticalStretch = Matrix4x4.CreateScale(1.0f, 1.0f, (float)invertedVerticalViewStretch);
        Matrix4x4 rotation =
            Matrix4x4.CreateRotationY(-DegreesToRadians(display.RollOffset))
            * Matrix4x4.CreateRotationX(-DegreesToRadians(display.PitchOffset))
            * Matrix4x4.CreateRotationZ(DegreesToRadians(display.AngleOffset));
        Matrix4x4 scale = Matrix4x4.CreateScale(display.Scale.X, display.Scale.Y, display.Scale.Z);
        Matrix4x4 offset = Matrix4x4.CreateTranslation(
            display.Offset.Y,
            -display.Offset.X,
            display.Offset.Z);

        return verticalStretch * rotation * scale * offset;
    }

    private static Matrix4x4 BuildThingScale(ThingModelRenderInput input)
    {
        float horizontalScale = (float)(input.ScaleX * input.ActorScaleWidth);
        float verticalScale = (float)(input.ScaleY * input.ActorScaleHeight);
        return Matrix4x4.CreateScale(horizontalScale, horizontalScale, verticalScale);
    }

    private static Matrix4x4 BuildThingRotation(ThingModelRenderInput input)
        => Matrix4x4.CreateRotationY((float)-input.RollRadians)
            * Matrix4x4.CreateRotationX((float)-input.PitchRadians)
            * Matrix4x4.CreateRotationZ((float)input.AngleRadians);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;

    private static Vector3 ToVector3(ModeldefVector vector) => new(vector.X, vector.Y, vector.Z);
}
