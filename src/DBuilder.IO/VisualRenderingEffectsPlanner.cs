// ABOUTME: Plans UDB-style enhanced visual rendering effect bundle state transitions.
// ABOUTME: Keeps renderer effect toggles testable outside the Avalonia map control.

namespace DBuilder.IO;

public sealed record VisualRenderingEffectsState(
    bool EnhancedRenderingEffects,
    ThingLightRenderMode LightRenderMode,
    ThingModelRenderMode ModelRenderMode,
    bool Show3DFloors);

public static class VisualRenderingEffectsPlanner
{
    public static VisualRenderingEffectsState Toggle(VisualRenderingEffectsState current)
        => current.EnhancedRenderingEffects
            ? Disabled()
            : Enabled();

    public static VisualRenderingEffectsState Enabled()
        => new(
            EnhancedRenderingEffects: true,
            LightRenderMode: ThingLightRenderMode.All,
            ModelRenderMode: ThingModelRenderMode.All,
            Show3DFloors: true);

    public static VisualRenderingEffectsState Disabled()
        => new(
            EnhancedRenderingEffects: false,
            LightRenderMode: ThingLightRenderMode.None,
            ModelRenderMode: ThingModelRenderMode.None,
            Show3DFloors: false);
}
