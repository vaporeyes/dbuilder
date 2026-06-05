// ABOUTME: Defines 2D editor thing icon sizing rules for sprite and compact marker rendering.
// ABOUTME: Keeps zoom-dependent thing icon behavior testable outside the OpenGL map control.

namespace DBuilder.IO;

public static class ThingIconRenderPolicy
{
    public const double CompactMarkerScaleThreshold = 6.0;
    public const double CompactMarkerBaseSize = 4.0;
    public const double RegularMarkerBaseSize = 10.0;
    public const double CompactDirectionTickBaseSize = 7.0;
    public const double RegularDirectionTickBaseSize = 18.0;

    public static bool UseCompactMarkers(double viewScale, bool fixedThingsScale, bool thingArrows)
        => fixedThingsScale && !thingArrows && viewScale >= CompactMarkerScaleThreshold;

    public static double MarkerBaseSize(bool compactMarkers)
        => compactMarkers ? CompactMarkerBaseSize : RegularMarkerBaseSize;

    public static double DirectionTickBaseSize(bool compactMarkers)
        => compactMarkers ? CompactDirectionTickBaseSize : RegularDirectionTickBaseSize;
}
