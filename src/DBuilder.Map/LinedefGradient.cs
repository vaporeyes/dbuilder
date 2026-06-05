// ABOUTME: Applies UDB-style selected-linedef brightness gradients to visible sidedef parts.
// ABOUTME: Keeps UDMF light and lightabsolute field handling independent of editor UI.

using DBuilder.Geometry;

namespace DBuilder.Map;

public readonly record struct LinedefGradientResult(bool Applied, int LinedefCount, string Message);

public static class LinedefGradient
{
    public const int MinimumLinedefCount = 3;

    public static LinedefGradientResult ApplyBrightness(
        IReadOnlyList<Linedef> linedefs,
        InterpolationTools.Mode interpolationMode = InterpolationTools.Mode.LINEAR)
    {
        if (linedefs.Count < MinimumLinedefCount)
            return new LinedefGradientResult(false, linedefs.Count, "Select at least 3 linedefs first!");

        double startBrightness = ReadLinedefBrightness(linedefs[0]);
        if (double.IsNaN(startBrightness))
            return new LinedefGradientResult(false, linedefs.Count, "Start linedef doesn't have visible parts!");

        double endBrightness = ReadLinedefBrightness(linedefs[^1]);
        if (double.IsNaN(endBrightness))
            return new LinedefGradientResult(false, linedefs.Count, "End linedef doesn't have visible parts!");

        interpolationMode = InterpolationTools.NormalizeMode(interpolationMode);

        for (int i = 0; i < linedefs.Count; i++)
        {
            double u = i / (double)(linedefs.Count - 1);
            int brightness = (int)Math.Round(InterpolationTools.Interpolate(startBrightness, endBrightness, u, interpolationMode));
            ApplyVisibleSidedefBrightness(linedefs[i].Front, brightness);
            ApplyVisibleSidedefBrightness(linedefs[i].Back, brightness);
        }

        return new LinedefGradientResult(true, linedefs.Count, "Created gradient brightness over selected linedefs.");
    }

    private static double ReadLinedefBrightness(Linedef line)
    {
        double frontBrightness = SidedefHasVisibleParts(line.Front) ? ReadSidedefBrightness(line.Front!) : double.NaN;
        double backBrightness = SidedefHasVisibleParts(line.Back) ? ReadSidedefBrightness(line.Back!) : double.NaN;

        if (double.IsNaN(frontBrightness) && double.IsNaN(backBrightness)) return double.NaN;
        if (double.IsNaN(frontBrightness)) return backBrightness;
        if (double.IsNaN(backBrightness)) return frontBrightness;
        return (frontBrightness + backBrightness) / 2.0;
    }

    private static bool SidedefHasVisibleParts(Sidedef? side)
        => side?.Sector != null
            && (side.HighRequired()
                || side.LowRequired()
                || side.MiddleRequired()
                || (side.Other != null && side.MidTexture != "-"));

    private static double ReadSidedefBrightness(Sidedef side)
        => side.GetField("lightabsolute", false)
            ? side.GetIntegerField("light")
            : Math.Clamp(side.Sector!.Brightness + side.GetIntegerField("light"), 0, 255);

    private static void ApplyVisibleSidedefBrightness(Sidedef? side, int brightness)
    {
        if (!SidedefHasVisibleParts(side)) return;
        ApplySidedefBrightness(side!, brightness);
    }

    private static void ApplySidedefBrightness(Sidedef side, int brightness)
    {
        if (side.GetField("lightabsolute", false))
            side.SetIntegerField("light", brightness);
        else
            side.SetIntegerField("light", brightness - side.Sector!.Brightness);
    }
}
