// ABOUTME: Tests UDB BuilderModes draw-mode setting keys, defaults, and clamping.
// ABOUTME: Covers line, rectangle, ellipse, curve, and grid option persistence without UI.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class DrawModeSettingsTests
{
    [Fact]
    public void DrawLineSettingsUseUdbKeysAndDefaults()
    {
        DrawLineModeSettings settings = DrawLineModeSettings.FromDictionary(new Dictionary<string, object?>());

        Assert.False(settings.ContinuousDrawing);
        Assert.False(settings.AutoCloseDrawing);
        Assert.False(settings.ShowGuidelines);

        var target = new Dictionary<string, object?>();
        new DrawLineModeSettings(true, true, true).WriteTo(target);

        Assert.Equal(true, target[DrawLineModeSettings.ContinuousDrawingKey]);
        Assert.Equal(true, target[DrawLineModeSettings.AutoCloseDrawingKey]);
        Assert.Equal(true, target[DrawLineModeSettings.ShowGuidelinesKey]);
    }

    [Fact]
    public void DrawRectangleSettingsClampSubdivisionsAndWriteUdbKeys()
    {
        var source = new Dictionary<string, object?>
        {
            [DrawRectangleModeSettings.SubdivisionsKey] = 99,
            [DrawRectangleModeSettings.BevelWidthKey] = -16,
            [DrawRectangleModeSettings.ContinuousDrawingKey] = true,
            [DrawRectangleModeSettings.ShowGuidelinesKey] = true,
            [DrawRectangleModeSettings.RadialDrawingKey] = true,
            [DrawRectangleModeSettings.PlaceThingsAtVerticesKey] = true,
        };

        DrawRectangleModeSettings settings = DrawRectangleModeSettings.FromDictionary(source);

        Assert.Equal(DrawRectangleModeSettings.MaxSubdivisions, settings.Subdivisions);
        Assert.Equal(-16, settings.BevelWidth);
        Assert.True(settings.ContinuousDrawing);
        Assert.True(settings.ShowGuidelines);
        Assert.True(settings.RadialDrawing);
        Assert.True(settings.PlaceThingsAtVertices);

        var target = new Dictionary<string, object?>();
        settings.WriteTo(target);

        Assert.Equal(16, target[DrawRectangleModeSettings.SubdivisionsKey]);
        Assert.Equal(-16, target[DrawRectangleModeSettings.BevelWidthKey]);
        Assert.Equal(true, target[DrawRectangleModeSettings.ContinuousDrawingKey]);
        Assert.Equal(true, target[DrawRectangleModeSettings.ShowGuidelinesKey]);
        Assert.Equal(true, target[DrawRectangleModeSettings.RadialDrawingKey]);
        Assert.Equal(true, target[DrawRectangleModeSettings.PlaceThingsAtVerticesKey]);
    }

    [Fact]
    public void DrawRectangleSubdivisionsStepByOneLikeUdbActions()
    {
        var settings = new DrawRectangleModeSettings(Subdivisions: 15);

        Assert.Equal(16, settings.IncreaseSubdivisions().Subdivisions);
        Assert.Equal(16, settings.IncreaseSubdivisions().IncreaseSubdivisions().Subdivisions);
        Assert.Equal(14, settings.DecreaseSubdivisions().Subdivisions);
        Assert.Equal(0, new DrawRectangleModeSettings(Subdivisions: 0).DecreaseSubdivisions().Subdivisions);
    }

    [Fact]
    public void DrawEllipseSettingsClampSubdivisionsAndKeepAngle()
    {
        var lowSource = new Dictionary<string, object?>
        {
            [DrawEllipseModeSettings.SubdivisionsKey] = -5,
            [DrawEllipseModeSettings.AngleKey] = 45,
        };
        var highSource = new Dictionary<string, object?>
        {
            [DrawEllipseModeSettings.SubdivisionsKey] = 999,
            [DrawEllipseModeSettings.BevelWidthKey] = 12,
            [DrawEllipseModeSettings.ContinuousDrawingKey] = true,
            [DrawEllipseModeSettings.ShowGuidelinesKey] = true,
            [DrawEllipseModeSettings.RadialDrawingKey] = true,
            [DrawEllipseModeSettings.PlaceThingsAtVerticesKey] = true,
        };

        DrawEllipseModeSettings low = DrawEllipseModeSettings.FromDictionary(lowSource);
        DrawEllipseModeSettings high = DrawEllipseModeSettings.FromDictionary(highSource);

        Assert.Equal(DrawEllipseModeSettings.MinSubdivisions, low.Subdivisions);
        Assert.Equal(45, low.Angle);
        Assert.Equal(DrawEllipseModeSettings.MaxSubdivisions, high.Subdivisions);
        Assert.Equal(12, high.BevelWidth);
        Assert.True(high.ContinuousDrawing);
        Assert.True(high.ShowGuidelines);
        Assert.True(high.RadialDrawing);
        Assert.True(high.PlaceThingsAtVertices);
    }

    [Fact]
    public void DrawEllipseSubdivisionsUseUdbOddEvenStepRules()
    {
        Assert.Equal(6, new DrawEllipseModeSettings(Subdivisions: 5).IncreaseSubdivisions().Subdivisions);
        Assert.Equal(8, new DrawEllipseModeSettings(Subdivisions: 6).IncreaseSubdivisions().Subdivisions);
        Assert.Equal(511, new DrawEllipseModeSettings(Subdivisions: 511).IncreaseSubdivisions().Subdivisions);
        Assert.Equal(512, new DrawEllipseModeSettings(Subdivisions: 512).IncreaseSubdivisions().Subdivisions);

        Assert.Equal(6, new DrawEllipseModeSettings(Subdivisions: 7).DecreaseSubdivisions().Subdivisions);
        Assert.Equal(6, new DrawEllipseModeSettings(Subdivisions: 8).DecreaseSubdivisions().Subdivisions);
        Assert.Equal(4, new DrawEllipseModeSettings(Subdivisions: 4).DecreaseSubdivisions().Subdivisions);
        Assert.Equal(3, new DrawEllipseModeSettings(Subdivisions: 3).DecreaseSubdivisions().Subdivisions);
    }

    [Fact]
    public void DrawCurveSettingsClampSegmentLengthAndWriteUdbKeys()
    {
        DrawCurveModeSettings low = DrawCurveModeSettings.FromDictionary(new Dictionary<string, object?>
        {
            [DrawCurveModeSettings.SegmentLengthKey] = 1,
        });
        DrawCurveModeSettings high = DrawCurveModeSettings.FromDictionary(new Dictionary<string, object?>
        {
            [DrawCurveModeSettings.SegmentLengthKey] = 99999,
            [DrawCurveModeSettings.ContinuousDrawingKey] = true,
            [DrawCurveModeSettings.AutoCloseDrawingKey] = true,
            [DrawCurveModeSettings.PlaceThingsAtVerticesKey] = true,
        });

        Assert.Equal(DrawCurveModeSettings.MinSegmentLength, low.SegmentLength);
        Assert.Equal(DrawCurveModeSettings.MaxSegmentLength, high.SegmentLength);
        Assert.True(high.ContinuousDrawing);
        Assert.True(high.AutoCloseDrawing);
        Assert.True(high.PlaceThingsAtVertices);

        var target = new Dictionary<string, object?>();
        high.WriteTo(target);

        Assert.Equal(4096, target[DrawCurveModeSettings.SegmentLengthKey]);
        Assert.Equal(true, target[DrawCurveModeSettings.ContinuousDrawingKey]);
        Assert.Equal(true, target[DrawCurveModeSettings.AutoCloseDrawingKey]);
        Assert.Equal(true, target[DrawCurveModeSettings.PlaceThingsAtVerticesKey]);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(31, 16)]
    [InlineData(32, 16)]
    [InlineData(64, 32)]
    [InlineData(96, 48)]
    public void DrawCurveSegmentLengthIncrementMatchesUdbIntegerMath(int segmentLength, int expectedIncrement)
        => Assert.Equal(expectedIncrement, DrawCurveModeSettings.SegmentLengthIncrement(segmentLength));

    [Fact]
    public void DrawCurveSegmentLengthActionsUseUdbVariableStep()
    {
        Assert.Equal(48, new DrawCurveModeSettings(SegmentLength: 32).IncreaseSegmentLength().SegmentLength);
        Assert.Equal(96, new DrawCurveModeSettings(SegmentLength: 64).IncreaseSegmentLength().SegmentLength);
        Assert.Equal(4096, new DrawCurveModeSettings(SegmentLength: 4090).IncreaseSegmentLength().SegmentLength);
        Assert.Equal(4096, new DrawCurveModeSettings(SegmentLength: 4096).IncreaseSegmentLength().SegmentLength);

        Assert.Equal(32, new DrawCurveModeSettings(SegmentLength: 64).DecreaseSegmentLength().SegmentLength);
        Assert.Equal(16, new DrawCurveModeSettings(SegmentLength: 17).DecreaseSegmentLength().SegmentLength);
        Assert.Equal(16, new DrawCurveModeSettings(SegmentLength: 16).DecreaseSegmentLength().SegmentLength);
    }

    [Fact]
    public void DrawGridSettingsUseUdbKeysAndCreatePlannerOptions()
    {
        var source = new Dictionary<string, object?>
        {
            [DrawGridModeSettings.TriangulateKey] = true,
            [DrawGridModeSettings.GridLockModeKey] = (int)DrawGridLockMode.Both,
            [DrawGridModeSettings.HorizontalSlicesKey] = 0,
            [DrawGridModeSettings.VerticalSlicesKey] = -4,
            [DrawGridModeSettings.RelativeInterpolationKey] = false,
            [DrawGridModeSettings.HorizontalInterpolationKey] = (int)InterpolationTools.Mode.EASE_IN_SINE,
            [DrawGridModeSettings.VerticalInterpolationKey] = (int)InterpolationTools.Mode.EASE_OUT_SINE,
            [DrawGridModeSettings.ContinuousDrawingKey] = true,
            [DrawGridModeSettings.ShowGuidelinesKey] = true,
        };

        DrawGridModeSettings settings = DrawGridModeSettings.FromDictionary(source);
        DrawGridPlanOptions options = settings.ToPlanOptions();

        Assert.True(settings.Triangulate);
        Assert.Equal(DrawGridLockMode.Both, settings.GridLockMode);
        Assert.Equal(1, settings.HorizontalSlices);
        Assert.Equal(1, settings.VerticalSlices);
        Assert.False(settings.RelativeInterpolation);
        Assert.Equal(InterpolationTools.Mode.EASE_IN_SINE, settings.HorizontalInterpolation);
        Assert.Equal(InterpolationTools.Mode.EASE_OUT_SINE, settings.VerticalInterpolation);
        Assert.True(settings.ContinuousDrawing);
        Assert.True(settings.ShowGuidelines);
        Assert.Equal(settings.HorizontalSlices, options.HorizontalSlices);
        Assert.Equal(settings.VerticalSlices, options.VerticalSlices);
        Assert.True(options.Triangulate);
        Assert.False(options.RelativeInterpolation);

        var target = new Dictionary<string, object?>();
        settings.WriteTo(target);

        Assert.Equal(true, target[DrawGridModeSettings.TriangulateKey]);
        Assert.Equal((int)DrawGridLockMode.Both, target[DrawGridModeSettings.GridLockModeKey]);
        Assert.Equal(1, target[DrawGridModeSettings.HorizontalSlicesKey]);
        Assert.Equal(1, target[DrawGridModeSettings.VerticalSlicesKey]);
        Assert.Equal(false, target[DrawGridModeSettings.RelativeInterpolationKey]);
        Assert.Equal((int)InterpolationTools.Mode.EASE_IN_SINE, target[DrawGridModeSettings.HorizontalInterpolationKey]);
        Assert.Equal((int)InterpolationTools.Mode.EASE_OUT_SINE, target[DrawGridModeSettings.VerticalInterpolationKey]);
        Assert.Equal(true, target[DrawGridModeSettings.ContinuousDrawingKey]);
        Assert.Equal(true, target[DrawGridModeSettings.ShowGuidelinesKey]);
    }
}
