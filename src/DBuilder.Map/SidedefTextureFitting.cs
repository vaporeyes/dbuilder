// ABOUTME: Applies UDB-style UDMF texture fit fields to sidedef wall parts.
// ABOUTME: Provides the map-level scale and offset math used by visual texture fitting workflows.

namespace DBuilder.Map;

public readonly record struct TextureFitImage(int Width, int Height, double ScaleX = 1.0, double ScaleY = 1.0)
{
    public double ScaledWidth => Width * ScaleX;
    public double ScaledHeight => Height * ScaleY;
}

public sealed class SidedefTextureFitOptions
{
    public double HorizontalRepeat { get; init; } = 1.0;
    public double VerticalRepeat { get; init; } = 1.0;
    public int PatternWidth { get; init; }
    public int PatternHeight { get; init; }
    public bool FitWidth { get; init; } = true;
    public bool FitHeight { get; init; } = true;
    public bool AutoWidth { get; init; }
    public bool AutoHeight { get; init; }
    public bool FitAcrossSurfaces { get; init; }
    public double BoundsX { get; init; }
    public double BoundsY { get; init; }
    public double GlobalBoundsWidth { get; init; }
    public double GlobalBoundsHeight { get; init; }
    public double ControlSideOffsetX { get; init; }
    public double ControlSideOffsetY { get; init; }
    public double InitialOffsetX { get; init; }
    public double InitialOffsetY { get; init; }
    public double InitialScaleX { get; init; } = 1.0;
    public double InitialScaleY { get; init; } = 1.0;
}

public static class SidedefTextureFitting
{
    public static bool Fit(Sidedef side, SidedefPart part, TextureFitImage texture, SidedefTextureFitOptions? options = null, int decimals = 3)
    {
        if (part == SidedefPart.None) return false;
        if (texture.Width <= 0 || texture.Height <= 0) return false;

        options ??= new SidedefTextureFitOptions();
        bool changed = false;
        double lineLength = Math.Round(side.Line.Length);

        if (options.FitWidth && lineLength > 0.0)
        {
            bool useSurfaceBounds = options.FitAcrossSurfaces && options.GlobalBoundsWidth > 0.0;
            double fitWidth = useSurfaceBounds ? options.GlobalBoundsWidth : lineLength;
            double repeat = HorizontalRepeat(options, texture.Width, fitWidth);
            double scaleX = texture.ScaledWidth / fitWidth * repeat;
            double offsetX = useSurfaceBounds
                ? options.BoundsX * scaleX - side.OffsetX - options.ControlSideOffsetX
                : -side.OffsetX - options.ControlSideOffsetX;

            changed |= SetFloat(side, ScaleXField(part), Math.Round(scaleX, decimals), 1.0);
            changed |= SetFloat(side, OffsetXField(part), Math.Round(offsetX, decimals), 0.0);
        }
        else
        {
            changed |= SetFloat(side, ScaleXField(part), options.InitialScaleX, 1.0);
            changed |= SetFloat(side, OffsetXField(part), options.InitialOffsetX, 0.0);
        }

        double partHeight = side.GetPartHeight(part);
        if (options.FitHeight && partHeight > 0.0)
        {
            bool useSurfaceBounds = options.FitAcrossSurfaces && options.GlobalBoundsHeight > 0.0;
            double fitHeight = useSurfaceBounds ? options.GlobalBoundsHeight : partHeight;
            double repeat = VerticalRepeat(options, texture.Height, fitHeight);
            double scaleY = texture.ScaledHeight / fitHeight * repeat;
            double offsetY = useSurfaceBounds
                ? options.BoundsY * scaleY - side.OffsetY - options.ControlSideOffsetY
                : -side.OffsetY - options.ControlSideOffsetY;

            changed |= SetFloat(side, ScaleYField(part), Math.Round(scaleY, decimals), 1.0);
            changed |= SetFloat(side, OffsetYField(part), Math.Round(offsetY, decimals), 0.0);
        }
        else
        {
            changed |= SetFloat(side, ScaleYField(part), options.InitialScaleY, 1.0);
            changed |= SetFloat(side, OffsetYField(part), options.InitialOffsetY, 0.0);
        }

        return changed;
    }

    private static double HorizontalRepeat(SidedefTextureFitOptions options, int textureWidth, double lineLength)
    {
        if (!options.AutoWidth) return NormalizedRepeat(options.HorizontalRepeat);

        double patternWidth = options.PatternWidth > 0 ? options.PatternWidth : textureWidth;
        double repeat = Math.Round(lineLength / patternWidth);
        if (repeat == 0.0) repeat = 1.0;
        if (options.PatternWidth > 0) repeat /= textureWidth / patternWidth;
        return repeat;
    }

    private static double VerticalRepeat(SidedefTextureFitOptions options, int textureHeight, double partHeight)
    {
        if (!options.AutoHeight) return NormalizedRepeat(options.VerticalRepeat);

        double patternHeight = options.PatternHeight > 0 ? options.PatternHeight : textureHeight;
        double repeat = Math.Round(partHeight / patternHeight);
        if (repeat == 0.0) repeat = 1.0;
        if (options.PatternHeight > 0) repeat /= textureHeight / patternHeight;
        return repeat;
    }

    private static double NormalizedRepeat(double repeat)
        => double.IsNaN(repeat) || repeat == 0.0 ? 1.0 : repeat;

    private static bool SetFloat(Sidedef side, string field, double value, double defaultValue)
    {
        bool hadOld = side.Fields.ContainsKey(field);
        double old = side.GetFloatField(field, defaultValue);
        side.SetFloatField(field, value, defaultValue);
        bool hasNew = side.Fields.ContainsKey(field);
        return hadOld != hasNew || !old.Equals(value);
    }

    private static string ScaleXField(SidedefPart part) => "scalex_" + PartName(part);
    private static string ScaleYField(SidedefPart part) => "scaley_" + PartName(part);
    private static string OffsetXField(SidedefPart part) => "offsetx_" + PartName(part);
    private static string OffsetYField(SidedefPart part) => "offsety_" + PartName(part);

    private static string PartName(SidedefPart part) => part switch
    {
        SidedefPart.Upper => "top",
        SidedefPart.Middle => "mid",
        SidedefPart.Lower => "bottom",
        _ => "",
    };
}
