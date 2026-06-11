// ABOUTME: Models UDB BuilderModes draw-mode option persistence without editor UI dependencies.
// ABOUTME: Preserves plugin setting keys, defaults, and clamp behavior for shape drawing modes.

using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record DrawLineModeSettings(
    bool ContinuousDrawing = false,
    bool AutoCloseDrawing = false,
    bool ShowGuidelines = false)
{
    public const string ContinuousDrawingKey = "drawlinesmode.continuousdrawing";
    public const string AutoCloseDrawingKey = "drawlinesmode.autoclosedrawing";
    public const string ShowGuidelinesKey = "drawlinesmode.showguidelines";

    public static DrawLineModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            ReadBool(settings, ContinuousDrawingKey, false),
            ReadBool(settings, AutoCloseDrawingKey, false),
            ReadBool(settings, ShowGuidelinesKey, false));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[ContinuousDrawingKey] = ContinuousDrawing;
        settings[AutoCloseDrawingKey] = AutoCloseDrawing;
        settings[ShowGuidelinesKey] = ShowGuidelines;
    }

    public DrawLineModeSettings Normalized() => this;

    internal static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
        => settings.TryGetValue(key, out object? value) && value is bool result ? result : fallback;

    internal static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
        => settings.TryGetValue(key, out object? value) && value is int result ? result : fallback;
}

public sealed record DrawRectangleModeSettings(
    int Subdivisions = 0,
    int BevelWidth = 0,
    bool ContinuousDrawing = false,
    bool ShowGuidelines = false,
    bool RadialDrawing = false,
    bool PlaceThingsAtVertices = false)
{
    public const int MinSubdivisions = 0;
    public const int MaxSubdivisions = 16;
    public const string SubdivisionsKey = "drawrectanglemode.subdivisions";
    public const string BevelWidthKey = "drawrectanglemode.bevelwidth";
    public const string ContinuousDrawingKey = "drawrectanglemode.continuousdrawing";
    public const string ShowGuidelinesKey = "drawrectanglemode.showguidelines";
    public const string RadialDrawingKey = "drawrectanglemode.radialdrawing";
    public const string PlaceThingsAtVerticesKey = "drawrectanglemode.placethingsatvertices";

    public static DrawRectangleModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            Math.Clamp(DrawLineModeSettings.ReadInt(settings, SubdivisionsKey, 0), MinSubdivisions, MaxSubdivisions),
            DrawLineModeSettings.ReadInt(settings, BevelWidthKey, 0),
            DrawLineModeSettings.ReadBool(settings, ContinuousDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, ShowGuidelinesKey, false),
            DrawLineModeSettings.ReadBool(settings, RadialDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, PlaceThingsAtVerticesKey, false));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[SubdivisionsKey] = Subdivisions;
        settings[BevelWidthKey] = BevelWidth;
        settings[ContinuousDrawingKey] = ContinuousDrawing;
        settings[ShowGuidelinesKey] = ShowGuidelines;
        settings[RadialDrawingKey] = RadialDrawing;
        settings[PlaceThingsAtVerticesKey] = PlaceThingsAtVertices;
    }

    public DrawRectangleModeSettings IncreaseSubdivisions()
        => this with { Subdivisions = Math.Min(Subdivisions + 1, MaxSubdivisions) };

    public DrawRectangleModeSettings DecreaseSubdivisions()
        => this with { Subdivisions = Math.Max(Subdivisions - 1, MinSubdivisions) };

    public DrawRectangleModeSettings IncreaseBevel(double gridSize)
        => this with { BevelWidth = BevelWidth + ValidGridStep(gridSize) };

    public DrawRectangleModeSettings DecreaseBevel(double gridSize)
        => this with { BevelWidth = BevelWidth - ValidGridStep(gridSize) };

    public DrawRectangleModeSettings Normalized()
        => this with { Subdivisions = Math.Clamp(Subdivisions, MinSubdivisions, MaxSubdivisions) };

    private static int ValidGridStep(double gridSize)
        => Math.Max(1, (int)Math.Round(double.IsFinite(gridSize) ? Math.Abs(gridSize) : 1.0));
}

public sealed record DrawEllipseModeSettings(
    int Subdivisions = 8,
    int BevelWidth = 0,
    int Angle = 0,
    bool ContinuousDrawing = false,
    bool ShowGuidelines = false,
    bool RadialDrawing = false,
    bool PlaceThingsAtVertices = false)
{
    public const int MinSubdivisions = 3;
    public const int MaxSubdivisions = 512;
    public const string SubdivisionsKey = "drawellipsemode.subdivisions";
    public const string BevelWidthKey = "drawellipsemode.bevelwidth";
    public const string AngleKey = "drawellipsemode.angle";
    public const string ContinuousDrawingKey = "drawellipsemode.continuousdrawing";
    public const string ShowGuidelinesKey = "drawellipsemode.showguidelines";
    public const string RadialDrawingKey = "drawellipsemode.radialdrawing";
    public const string PlaceThingsAtVerticesKey = "drawellipsemode.placethingsatvertices";

    public static DrawEllipseModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            Math.Clamp(DrawLineModeSettings.ReadInt(settings, SubdivisionsKey, 8), MinSubdivisions, MaxSubdivisions),
            DrawLineModeSettings.ReadInt(settings, BevelWidthKey, 0),
            DrawLineModeSettings.ReadInt(settings, AngleKey, 0),
            DrawLineModeSettings.ReadBool(settings, ContinuousDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, ShowGuidelinesKey, false),
            DrawLineModeSettings.ReadBool(settings, RadialDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, PlaceThingsAtVerticesKey, false));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[SubdivisionsKey] = Subdivisions;
        settings[BevelWidthKey] = BevelWidth;
        settings[AngleKey] = Angle;
        settings[ContinuousDrawingKey] = ContinuousDrawing;
        settings[ShowGuidelinesKey] = ShowGuidelines;
        settings[RadialDrawingKey] = RadialDrawing;
        settings[PlaceThingsAtVerticesKey] = PlaceThingsAtVertices;
    }

    public DrawEllipseModeSettings IncreaseSubdivisions()
    {
        if (MaxSubdivisions - Subdivisions <= 1) return this;
        int increment = Subdivisions % 2 != 0 ? 1 : 2;
        return this with { Subdivisions = Math.Min(Subdivisions + increment, MaxSubdivisions) };
    }

    public DrawEllipseModeSettings DecreaseSubdivisions()
    {
        if (Subdivisions - MinSubdivisions <= 1) return this;
        int decrement = Subdivisions % 2 != 0 ? 1 : 2;
        return this with { Subdivisions = Math.Max(Subdivisions - decrement, MinSubdivisions) };
    }

    public DrawEllipseModeSettings IncreaseBevel(double gridSize)
        => this with { BevelWidth = BevelWidth + Math.Max(1, (int)Math.Round(double.IsFinite(gridSize) ? Math.Abs(gridSize) : 1.0)) };

    public DrawEllipseModeSettings DecreaseBevel(double gridSize)
        => this with { BevelWidth = BevelWidth - Math.Max(1, (int)Math.Round(double.IsFinite(gridSize) ? Math.Abs(gridSize) : 1.0)) };

    public DrawEllipseModeSettings Normalized()
        => this with { Subdivisions = Math.Clamp(Subdivisions, MinSubdivisions, MaxSubdivisions) };
}

public sealed record DrawCurveModeSettings(
    int SegmentLength = 32,
    bool ContinuousDrawing = false,
    bool AutoCloseDrawing = false,
    bool PlaceThingsAtVertices = false)
{
    public const int MinSegmentLength = 16;
    public const int MaxSegmentLength = 4096;
    public const string SegmentLengthKey = "drawcurvemode.segmentlength";
    public const string ContinuousDrawingKey = "drawcurvemode.continuousdrawing";
    public const string AutoCloseDrawingKey = "drawcurvemode.autoclosedrawing";
    public const string PlaceThingsAtVerticesKey = "drawcurvemode.placethingsatvertices";

    public static DrawCurveModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            Math.Clamp(DrawLineModeSettings.ReadInt(settings, SegmentLengthKey, 32), MinSegmentLength, MaxSegmentLength),
            DrawLineModeSettings.ReadBool(settings, ContinuousDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, AutoCloseDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, PlaceThingsAtVerticesKey, false));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[SegmentLengthKey] = SegmentLength;
        settings[ContinuousDrawingKey] = ContinuousDrawing;
        settings[AutoCloseDrawingKey] = AutoCloseDrawing;
        settings[PlaceThingsAtVerticesKey] = PlaceThingsAtVertices;
    }

    public DrawCurveModeSettings IncreaseSegmentLength()
    {
        if (SegmentLength >= MaxSegmentLength) return this;
        int increment = SegmentLengthIncrement(SegmentLength);
        return this with { SegmentLength = Math.Min(SegmentLength + increment, MaxSegmentLength) };
    }

    public DrawCurveModeSettings DecreaseSegmentLength()
    {
        if (SegmentLength <= MinSegmentLength) return this;
        int decrement = SegmentLengthIncrement(SegmentLength);
        return this with { SegmentLength = Math.Max(SegmentLength - decrement, MinSegmentLength) };
    }

    public static int SegmentLengthIncrement(int segmentLength)
        => Math.Max(MinSegmentLength, segmentLength / 32 * 16);

    public DrawCurveModeSettings Normalized()
        => this with { SegmentLength = Math.Clamp(SegmentLength, MinSegmentLength, MaxSegmentLength) };
}

public sealed record DrawGridModeSettings(
    bool Triangulate = false,
    DrawGridLockMode GridLockMode = DrawGridLockMode.None,
    int HorizontalSlices = 3,
    int VerticalSlices = 3,
    bool RelativeInterpolation = true,
    InterpolationTools.Mode HorizontalInterpolation = InterpolationTools.Mode.LINEAR,
    InterpolationTools.Mode VerticalInterpolation = InterpolationTools.Mode.LINEAR,
    bool ContinuousDrawing = false,
    bool ShowGuidelines = false)
{
    public const string TriangulateKey = "drawgridmode.triangulate";
    public const string GridLockModeKey = "drawgridmode.gridlockmode";
    public const string HorizontalSlicesKey = "drawgridmode.horizontalslices";
    public const string VerticalSlicesKey = "drawgridmode.verticalslices";
    public const string RelativeInterpolationKey = "drawgridmode.relativeinterpolation";
    public const string HorizontalInterpolationKey = "drawgridmode.horizontalinterpolation";
    public const string VerticalInterpolationKey = "drawgridmode.verticalinterpolation";
    public const string ContinuousDrawingKey = "drawgridmode.continuousdrawing";
    public const string ShowGuidelinesKey = "drawgridmode.showguidelines";

    public static DrawGridModeSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            DrawLineModeSettings.ReadBool(settings, TriangulateKey, false),
            (DrawGridLockMode)DrawLineModeSettings.ReadInt(settings, GridLockModeKey, 0),
            Math.Max(DrawLineModeSettings.ReadInt(settings, HorizontalSlicesKey, 3), 1),
            Math.Max(DrawLineModeSettings.ReadInt(settings, VerticalSlicesKey, 3), 1),
            DrawLineModeSettings.ReadBool(settings, RelativeInterpolationKey, true),
            (InterpolationTools.Mode)DrawLineModeSettings.ReadInt(settings, HorizontalInterpolationKey, 0),
            (InterpolationTools.Mode)DrawLineModeSettings.ReadInt(settings, VerticalInterpolationKey, 0),
            DrawLineModeSettings.ReadBool(settings, ContinuousDrawingKey, false),
            DrawLineModeSettings.ReadBool(settings, ShowGuidelinesKey, false));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[TriangulateKey] = Triangulate;
        settings[GridLockModeKey] = (int)GridLockMode;
        settings[HorizontalSlicesKey] = HorizontalSlices;
        settings[VerticalSlicesKey] = VerticalSlices;
        settings[RelativeInterpolationKey] = RelativeInterpolation;
        settings[HorizontalInterpolationKey] = (int)HorizontalInterpolation;
        settings[VerticalInterpolationKey] = (int)VerticalInterpolation;
        settings[ContinuousDrawingKey] = ContinuousDrawing;
        settings[ShowGuidelinesKey] = ShowGuidelines;
    }

    public DrawGridPlanOptions ToPlanOptions()
    {
        DrawGridModeSettings settings = Normalized();
        return new DrawGridPlanOptions
        {
            HorizontalSlices = settings.HorizontalSlices,
            VerticalSlices = settings.VerticalSlices,
            Triangulate = settings.Triangulate,
            RelativeInterpolation = settings.RelativeInterpolation,
            GridLockMode = settings.GridLockMode,
            HorizontalInterpolation = settings.HorizontalInterpolation,
            VerticalInterpolation = settings.VerticalInterpolation
        };
    }

    public DrawGridModeSettings IncreaseHorizontalSlices()
        => GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Vertical
            ? this with { HorizontalSlices = HorizontalSlices + 1 }
            : this;

    public DrawGridModeSettings DecreaseHorizontalSlices()
        => GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Vertical
            ? this with { HorizontalSlices = Math.Max(1, HorizontalSlices - 1) }
            : this;

    public DrawGridModeSettings IncreaseVerticalSlices()
        => GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Horizontal
            ? this with { VerticalSlices = VerticalSlices + 1 }
            : this;

    public DrawGridModeSettings DecreaseVerticalSlices()
        => GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Horizontal
            ? this with { VerticalSlices = Math.Max(1, VerticalSlices - 1) }
            : this;

    public DrawGridModeSettings Normalized()
        => this with
        {
            GridLockMode = Enum.IsDefined(GridLockMode) ? GridLockMode : DrawGridLockMode.None,
            HorizontalSlices = Math.Max(HorizontalSlices, 1),
            VerticalSlices = Math.Max(VerticalSlices, 1),
            HorizontalInterpolation = Enum.IsDefined(HorizontalInterpolation) ? HorizontalInterpolation : InterpolationTools.Mode.LINEAR,
            VerticalInterpolation = Enum.IsDefined(VerticalInterpolation) ? VerticalInterpolation : InterpolationTools.Mode.LINEAR,
        };
}

public sealed record DrawGridOptionsPanelState(
    bool HorizontalSlicesEnabled,
    bool VerticalSlicesEnabled,
    bool HorizontalInterpolationEnabled,
    bool VerticalInterpolationEnabled,
    bool ResetEnabled,
    InterpolationTools.Mode HorizontalInterpolation,
    InterpolationTools.Mode VerticalInterpolation)
{
    public static DrawGridOptionsPanelState FromSettings(DrawGridModeSettings settings)
    {
        DrawGridModeSettings normalized = settings.Normalized();
        bool horizontalEnabled = normalized.GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Vertical;
        bool verticalEnabled = normalized.GridLockMode is DrawGridLockMode.None or DrawGridLockMode.Horizontal;

        return new DrawGridOptionsPanelState(
            horizontalEnabled,
            verticalEnabled,
            horizontalEnabled,
            verticalEnabled,
            normalized.GridLockMode != DrawGridLockMode.Both,
            horizontalEnabled ? normalized.HorizontalInterpolation : InterpolationTools.Mode.LINEAR,
            verticalEnabled ? normalized.VerticalInterpolation : InterpolationTools.Mode.LINEAR);
    }

    public static DrawGridModeSettings ResetSlices(DrawGridModeSettings settings)
    {
        DrawGridModeSettings normalized = settings.Normalized();
        return normalized.GridLockMode switch
        {
            DrawGridLockMode.None => normalized with { HorizontalSlices = 3, VerticalSlices = 3 },
            DrawGridLockMode.Horizontal => normalized with { VerticalSlices = 3 },
            DrawGridLockMode.Vertical => normalized with { HorizontalSlices = 3 },
            DrawGridLockMode.Both => normalized,
            _ => normalized,
        };
    }
}
