// ABOUTME: Defines 2D editor thing icon sizing rules for sprite and compact marker rendering.
// ABOUTME: Keeps zoom-dependent thing icon behavior testable outside the OpenGL map control.

namespace DBuilder.IO;

public static class ThingIconRenderPolicy
{
    public const double CompactMarkerScaleThreshold = 0.25;
    public const double FixedThingScreenRadius = 48.0;
    public const double ThingSpriteShrink = 2.0;
    public const double OverviewMarkerScaleThreshold = 1.0;
    public const double OverviewMarkerBaseSize = 2.0;
    public const double OverviewCullCellPixels = 48.0;
    public const double CompactMarkerBaseSize = 4.0;
    public const double RegularMarkerBaseSize = 10.0;
    public const double CompactDirectionTickBaseSize = 7.0;
    public const double RegularDirectionTickBaseSize = 18.0;

    public static bool UseCompactMarkers(double viewScale, bool fixedThingsScale, bool thingArrows)
        => !thingArrows && viewScale >= CompactMarkerScaleThreshold;

    public static bool UseOverviewMarkers(double viewScale, bool thingArrows)
        => !thingArrows && viewScale >= OverviewMarkerScaleThreshold;

    public static bool ShouldDrawDirectionTicks(double viewScale, bool thingArrows)
        => !thingArrows && viewScale < CompactMarkerScaleThreshold;

    public static bool ShouldCullOverlappingOverviewThings(double viewScale, bool thingArrows)
        => viewScale >= CompactMarkerScaleThreshold;

    public static int OverviewCullCell(double screenCoordinate)
        => (int)Math.Floor(screenCoordinate / OverviewCullCellPixels);

    public static double MarkerBaseSize(bool compactMarkers)
        => compactMarkers ? CompactMarkerBaseSize : RegularMarkerBaseSize;

    public static double MarkerBaseSize(bool compactMarkers, bool overviewMarkers)
        => overviewMarkers ? OverviewMarkerBaseSize : MarkerBaseSize(compactMarkers);

    public static double DirectionTickBaseSize(bool compactMarkers)
        => compactMarkers ? CompactDirectionTickBaseSize : RegularDirectionTickBaseSize;

    public static double ScaledWorldRadius(double mapRadius, double viewScale, bool fixedThingsScale, bool fixedSize = false)
    {
        double radius = Math.Max(1.0, mapRadius);
        double scale = Math.Max(0.001, viewScale);

        if (fixedSize && scale < 1.0) return radius * scale;
        if (fixedThingsScale && radius / scale > FixedThingScreenRadius) return FixedThingScreenRadius * scale;
        return radius;
    }

    public static (double HalfWidth, double HalfHeight) SpriteHalfSize(
        int imageWidth,
        int imageHeight,
        double thingRadius,
        double viewScale,
        bool fixedThingsScale,
        bool fixedSize = false)
    {
        double baseRadius = Math.Max(1.0, thingRadius - ThingSpriteShrink);
        double scaledRadius = ScaledWorldRadius(baseRadius, viewScale, fixedThingsScale, fixedSize);
        if (imageWidth <= 0 || imageHeight <= 0) return (scaledRadius, scaledRadius);

        if (imageWidth > imageHeight) return (scaledRadius, scaledRadius * imageHeight / imageWidth);
        if (imageHeight > imageWidth) return (scaledRadius * imageWidth / imageHeight, scaledRadius);
        return (scaledRadius, scaledRadius);
    }
}
