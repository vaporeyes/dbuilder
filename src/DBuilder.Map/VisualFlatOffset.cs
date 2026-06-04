// ABOUTME: Applies UDB-style visual flat texture offset nudges to floor and ceiling surfaces.
// ABOUTME: Handles camera-angle correction, surface rotation, and UDMF panning field updates.

namespace DBuilder.Map;

using DBuilder.Geometry;

public static class VisualFlatOffset
{
    public static bool Nudge(
        Sector sector,
        bool ceiling,
        int horizontal,
        int vertical,
        double scaledTextureWidth,
        double scaledTextureHeight,
        double cameraAngleXY)
    {
        if (horizontal == 0 && vertical == 0) return false;
        if (scaledTextureWidth <= 0.0 || scaledTextureHeight <= 0.0) return false;

        (horizontal, vertical) = CorrectForView(sector, ceiling, horizontal, vertical, cameraAngleXY);
        horizontal = -horizontal;
        vertical = -vertical;

        string xPanningField = ceiling ? "xpanningceiling" : "xpanningfloor";
        string yPanningField = ceiling ? "ypanningceiling" : "ypanningfloor";
        string xScaleField = ceiling ? "xscaleceiling" : "xscalefloor";
        string yScaleField = ceiling ? "yscaleceiling" : "yscalefloor";

        double xScale = sector.GetFloatField(xScaleField, 1.0);
        double yScale = sector.GetFloatField(yScaleField, 1.0);
        double xSpan = scaledTextureWidth / xScale;
        double ySpan = scaledTextureHeight / yScale;
        if (xSpan == 0.0 || ySpan == 0.0) return false;

        double nextX = (sector.GetFloatField(xPanningField, 0.0) + horizontal) % xSpan;
        double nextY = (sector.GetFloatField(yPanningField, 0.0) + vertical) % ySpan;
        sector.SetFloatField(xPanningField, nextX, 0.0);
        sector.SetFloatField(yPanningField, nextY, 0.0);
        return true;
    }

    private static (int Horizontal, int Vertical) CorrectForView(
        Sector sector,
        bool ceiling,
        int horizontal,
        int vertical,
        double cameraAngleXY)
    {
        double angle = Angle2D.RadToDeg(cameraAngleXY);
        angle += sector.GetFloatField(ceiling ? "rotationceiling" : "rotationfloor", 0.0);
        angle = ClampAngle(angle);

        if (angle > 315.0 || angle < 46.0) return (horizontal, vertical);
        if (angle > 225.0) return (-vertical, horizontal);
        if (angle > 135.0) return (-horizontal, -vertical);
        return (vertical, -horizontal);
    }

    private static double ClampAngle(double angle)
    {
        while (angle < 0.0) angle += 360.0;
        while (angle >= 360.0) angle -= 360.0;
        return angle;
    }
}
