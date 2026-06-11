// ABOUTME: Defines 2D editor thing icon sizing rules for sprite and compact marker rendering.
// ABOUTME: Keeps zoom-dependent thing icon behavior testable outside the OpenGL map control.

namespace DBuilder.IO;

public static class ThingIconRenderPolicy
{
    public const double MinimumInteractiveViewScale = 0.02;
    public const double SpriteIconScaleThreshold = 0.025;
    public const double CompactMarkerScaleThreshold = SpriteIconScaleThreshold;
    public const double FixedThingScreenRadius = 48.0;
    public const double ThingSpriteShrink = 2.0;
    public const double MinimumThingScreenRadius = 1.5;
    public const double MinimumFarOverviewThingScreenRadius = 4.0;
    public const double MinimumSpriteScreenRadius = 8.0;
    public const double OverlapCullScaleThreshold = CompactMarkerScaleThreshold;
    public const double OverviewMarkerScaleThreshold = CompactMarkerScaleThreshold;
    public const double FarOverviewMarkerScaleThreshold = 1.0;
    public const double FarOverviewMarkerBaseSize = 0.5;
    public const double OverviewMarkerBaseSize = 0.75;
    public const double OverviewCullCellPixels = 48.0;
    public const double FarOverviewCullCellPixels = 192.0;
    public const double MaxFarOverviewCullCellPixels = 320.0;
    public const double CompactMarkerBaseSize = 4.0;
    public const double RegularMarkerBaseSize = 10.0;
    public const double CompactDirectionTickBaseSize = 7.0;
    public const double RegularDirectionTickBaseSize = 18.0;

    public static bool UseCompactMarkers(double viewScale, bool fixedThingsScale, bool thingArrows)
        => viewScale >= CompactMarkerScaleThreshold;

    public static bool UseOverviewMarkers(double viewScale, bool thingArrows)
        => viewScale >= OverviewMarkerScaleThreshold;

    public static bool UseFarOverviewMarkers(double viewScale, bool thingArrows)
        => viewScale >= FarOverviewMarkerScaleThreshold;

    public static bool ShouldDrawDirectionTicks(double viewScale, bool thingArrows)
        => !thingArrows && viewScale < CompactMarkerScaleThreshold;

    public static bool ShouldCullOverlappingOverviewThings(double viewScale, bool thingArrows)
        => viewScale >= OverlapCullScaleThreshold;

    public static int OverviewCullCell(double screenCoordinate)
        => (int)Math.Floor(screenCoordinate / OverviewCullCellPixels);

    public static int OverviewCullCell(double screenCoordinate, double viewScale, bool thingArrows)
        => (int)Math.Floor(screenCoordinate / OverviewCullCellPixelsFor(viewScale, thingArrows));

    public static double OverviewCullCellPixelsFor(double viewScale, bool thingArrows)
    {
        if (!UseOverviewMarkers(viewScale, thingArrows)) return OverviewCullCellPixels;

        double scale = Math.Max(1.0, viewScale / OverviewMarkerScaleThreshold);
        return Math.Min(MaxFarOverviewCullCellPixels, OverviewCullCellPixels * scale);
    }

    public static bool ShouldRenderOverviewCellThing(bool thingSelected, bool cellHasSelectedThing, bool cellAlreadyRendered)
    {
        if (cellAlreadyRendered) return false;
        return !cellHasSelectedThing || thingSelected;
    }

    public static bool ShouldReplaceOverviewCellThing(
        bool existingSelected,
        double existingMapRadius,
        bool candidateSelected,
        double candidateMapRadius)
    {
        if (candidateSelected != existingSelected) return candidateSelected;
        return candidateMapRadius > existingMapRadius;
    }

    public static bool ShouldRenderThing(double mapRadius, double viewScale, bool fixedThingsScale, bool fixedSize = false)
        => ProjectedThingScreenRadius(mapRadius, viewScale, fixedThingsScale, fixedSize)
            >= MinimumThingScreenRadiusFor(viewScale);

    public static double MinimumThingScreenRadiusFor(double viewScale)
        => UseFarOverviewMarkers(viewScale, thingArrows: false)
            ? MinimumFarOverviewThingScreenRadius
            : MinimumThingScreenRadius;

    public static bool ShouldRenderSpriteIcon(
        double mapRadius,
        double viewScale,
        bool fixedThingsScale,
        bool fixedSize = false)
        => viewScale < SpriteIconScaleThreshold
            && ProjectedThingScreenRadius(mapRadius, viewScale, fixedThingsScale, fixedSize) >= MinimumSpriteScreenRadius;

    public static double MarkerBaseSize(bool compactMarkers)
        => compactMarkers ? CompactMarkerBaseSize : RegularMarkerBaseSize;

    public static double MarkerBaseSize(bool compactMarkers, bool overviewMarkers)
        => overviewMarkers ? OverviewMarkerBaseSize : MarkerBaseSize(compactMarkers);

    public static double MarkerBaseSize(bool compactMarkers, bool overviewMarkers, bool farOverviewMarkers)
        => farOverviewMarkers ? FarOverviewMarkerBaseSize : MarkerBaseSize(compactMarkers, overviewMarkers);

    public static double DirectionTickBaseSize(bool compactMarkers)
        => compactMarkers ? CompactDirectionTickBaseSize : RegularDirectionTickBaseSize;

    public static double MarkerWorldSize(double baseSize, double viewScale, bool fixedThingsScale, bool compactMarkers)
        => fixedThingsScale || compactMarkers ? baseSize * Math.Max(0.001, viewScale) : baseSize;

    public static double ScaledWorldRadius(double mapRadius, double viewScale, bool fixedThingsScale, bool fixedSize = false)
    {
        double radius = Math.Max(1.0, mapRadius);
        double scale = Math.Max(0.001, viewScale);

        if (fixedSize && scale < 1.0) return radius * scale;
        if (fixedThingsScale && radius / scale > FixedThingScreenRadius) return FixedThingScreenRadius * scale;
        return radius;
    }

    public static double ProjectedThingScreenRadius(double mapRadius, double viewScale, bool fixedThingsScale, bool fixedSize = false)
        => ScaledWorldRadius(mapRadius, viewScale, fixedThingsScale, fixedSize) / Math.Max(0.001, viewScale);

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
