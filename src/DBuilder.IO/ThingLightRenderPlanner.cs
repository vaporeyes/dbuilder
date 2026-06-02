// ABOUTME: Plans UDB-style dynamic light render-mode cycling for thing markers and billboards.
// ABOUTME: Keeps dynamic light display gating independent from editor rendering hosts.

namespace DBuilder.IO;

public enum ThingLightRenderMode
{
    None,
    All,
    Animated,
}

public static class ThingLightRenderPlanner
{
    public static ThingLightRenderMode NextMode(ThingLightRenderMode mode)
        => mode switch
        {
            ThingLightRenderMode.None => ThingLightRenderMode.All,
            ThingLightRenderMode.All => ThingLightRenderMode.Animated,
            ThingLightRenderMode.Animated => ThingLightRenderMode.None,
            _ => ThingLightRenderMode.All,
        };

    public static string StatusLabel(ThingLightRenderMode mode)
        => mode switch
        {
            ThingLightRenderMode.None => "NONE",
            ThingLightRenderMode.All => "ALL",
            ThingLightRenderMode.Animated => "ANIMATED",
            _ => "ALL",
        };

    public static bool ShouldRender(ThingLightRenderMode mode)
        => mode is ThingLightRenderMode.All or ThingLightRenderMode.Animated;
}
