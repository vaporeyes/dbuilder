// ABOUTME: Plans the sprite image used for 2D and 3D thing billboard fallback rendering.
// ABOUTME: Reuses UDB-style MODELDEF, VOXELDEF, sprite, and marker display-source resolution before mesh rendering exists.

namespace DBuilder.IO;

public sealed record ThingBillboardDisplay(
    ThingDisplayKind Kind,
    string SpriteName,
    ImageData Image);

public static class ThingBillboardDisplayPlanner
{
    public static ThingBillboardDisplay? Plan(ThingTypeInfo? thingInfo, ResourceManager? resources)
        => Plan(thingInfo, resources, ThingModelRenderMode.All, new ThingModelRenderInput(), visual3D: false);

    public static ThingBillboardDisplay? Plan(
        ThingTypeInfo? thingInfo,
        ResourceManager? resources,
        ThingModelRenderMode modelRenderMode,
        ThingModelRenderInput modelInput,
        bool visual3D)
    {
        if (thingInfo == null || resources == null) return null;

        ThingDisplaySource display = ThingDisplayResolver.Resolve(thingInfo, resources);
        if (display.Kind == ThingDisplayKind.Model)
        {
            bool renderModel = visual3D
                ? ThingModelRenderPlanner.ShouldRender3D(modelRenderMode, modelInput.Selected)
                : ThingModelRenderPlanner.ShouldRender(modelRenderMode, modelInput.Selected, modelInput.ActiveFilterAlpha);
            if (!renderModel)
                display = new ThingDisplaySource(ThingDisplayKind.Sprite, SpriteName: display.SpriteName);
        }

        string? sprite = display.Kind is ThingDisplayKind.Sprite or ThingDisplayKind.Voxel or ThingDisplayKind.Model
            ? display.SpriteName
            : thingInfo.Sprite;

        if (string.IsNullOrEmpty(sprite)) return null;
        ImageData? image = resources.GetSprite(sprite);
        return image == null ? null : new ThingBillboardDisplay(display.Kind, sprite, image);
    }
}
