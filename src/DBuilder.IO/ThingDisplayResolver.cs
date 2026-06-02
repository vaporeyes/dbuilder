// ABOUTME: Resolves UDB-style thing display sources from game configuration and loaded model, voxel, and sprite resources.
// ABOUTME: Keeps model and voxel display selection testable before full 3D model rendering is ported.

namespace DBuilder.IO;

public enum ThingDisplayKind
{
    Marker,
    Sprite,
    Model,
    Voxel,
}

public sealed record ThingDisplaySource(
    ThingDisplayKind Kind,
    string? SpriteName = null,
    Modeldef? Model = null,
    string? VoxelModelName = null,
    ThingModelDisplay? ModelDisplay = null);

public sealed record ThingModelDisplay(
    Modeldef Definition,
    string Path,
    ModeldefVector Scale,
    ModeldefVector Offset,
    ModeldefVector RotationCenter,
    float AngleOffset,
    float PitchOffset,
    float RollOffset,
    bool InheritActorPitch,
    bool UseActorPitch,
    bool UseActorRoll,
    bool UseRotationCenter,
    IReadOnlyList<ThingModelDisplayPart> Parts);

public sealed record ThingModelDisplayPart(
    string ModelName,
    string SkinName,
    IReadOnlyDictionary<int, string> SurfaceSkinNames,
    string FrameName,
    int FrameIndex)
{
    public IReadOnlyDictionary<int, string> EffectiveSurfaceSkinNames
        => string.IsNullOrEmpty(SkinName) ? SurfaceSkinNames : EmptySurfaceSkinNames;

    private static readonly IReadOnlyDictionary<int, string> EmptySurfaceSkinNames = new Dictionary<int, string>();
}

public sealed record ThingModelData(
    string Path,
    ModeldefVector Scale,
    ModeldefVector Offset,
    ModeldefVector RotationCenter,
    float AngleOffset,
    float PitchOffset,
    float RollOffset,
    bool InheritActorPitch,
    bool UseActorPitch,
    bool UseActorRoll,
    bool UseRotationCenter,
    IReadOnlyList<string> ModelNames,
    IReadOnlyList<string> SkinNames,
    IReadOnlyList<IReadOnlyDictionary<int, string>> SurfaceSkinNames,
    IReadOnlyList<string> FrameNames,
    IReadOnlyList<int> FrameIndices)
{
    public static ThingModelData FromDisplay(ThingModelDisplay display)
    {
        var modelNames = new List<string>(display.Parts.Count);
        var skinNames = new List<string>(display.Parts.Count);
        var surfaceSkinNames = new List<IReadOnlyDictionary<int, string>>(display.Parts.Count);
        var frameNames = new List<string>(display.Parts.Count);
        var frameIndices = new List<int>(display.Parts.Count);

        foreach (ThingModelDisplayPart part in display.Parts)
        {
            modelNames.Add(part.ModelName.ToLowerInvariant());
            skinNames.Add(part.SkinName.ToLowerInvariant());
            surfaceSkinNames.Add(new Dictionary<int, string>(part.EffectiveSurfaceSkinNames));
            frameNames.Add(part.FrameName);
            frameIndices.Add(part.FrameIndex);
        }

        return new ThingModelData(
            display.Path,
            display.Scale,
            display.Offset,
            display.RotationCenter,
            display.AngleOffset,
            display.PitchOffset,
            display.RollOffset,
            display.InheritActorPitch,
            display.UseActorPitch,
            display.UseActorRoll,
            display.UseRotationCenter,
            modelNames,
            skinNames,
            surfaceSkinNames,
            frameNames,
            frameIndices);
    }
}

public static class ThingDisplayResolver
{
    public static ThingDisplaySource Resolve(ThingTypeInfo? thingInfo, ResourceManager? resources)
    {
        if (thingInfo == null || resources == null)
            return new ThingDisplaySource(ThingDisplayKind.Marker);

        if (ResolveModel(thingInfo, resources) is { } model)
            return new ThingDisplaySource(
                ThingDisplayKind.Model,
                SpriteName: thingInfo.Sprite,
                Model: model.Definition,
                ModelDisplay: model);

        string? voxel = ResolveVoxel(thingInfo.Sprite, resources);
        if (voxel != null)
            return new ThingDisplaySource(ThingDisplayKind.Voxel, SpriteName: thingInfo.Sprite, VoxelModelName: voxel);

        if (!string.IsNullOrWhiteSpace(thingInfo.Sprite) && resources.GetSprite(thingInfo.Sprite) != null)
            return new ThingDisplaySource(ThingDisplayKind.Sprite, SpriteName: thingInfo.Sprite);

        return new ThingDisplaySource(ThingDisplayKind.Marker);
    }

    public static ThingModelDisplay? ResolveModel(ThingTypeInfo thingInfo, ResourceManager resources)
    {
        if (string.IsNullOrWhiteSpace(thingInfo.ClassName)) return null;
        string? modelFrameKey = ResolveModelFrameKey(thingInfo.Sprite);
        if (modelFrameKey == null) return null;

        IReadOnlyList<Modeldef> defs = resources.GetModelDefs();
        for (int i = defs.Count - 1; i >= 0; i--)
        {
            Modeldef def = defs[i];
            if (!string.Equals(def.ActorName, thingInfo.ClassName, StringComparison.OrdinalIgnoreCase)) continue;
            if (def.Models.Count == 0) continue;

            var parts = new List<ThingModelDisplayPart>();
            foreach (ModeldefFrame frame in def.Frames)
            {
                if (!IsTargetFrame(frame, modelFrameKey)) continue;
                if (frame.FrameIndex < 0) continue;
                ModeldefModel? model = FindModel(def, frame.ModelIndex);
                if (model != null && resources.GetModelResourceBytes(def, model.File) != null)
                    parts.Add(CreateModelPart(def, frame, model));
            }

            if (parts.Count > 0) return CreateModelDisplay(def, parts);
        }

        return null;
    }

    public static string? ResolveVoxel(string spriteName, ResourceManager resources)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;

        string? mapped = resources.GetVoxelModelForSprite(spriteName);
        if (mapped != null && resources.GetVoxelBytes(mapped) != null) return mapped;

        string direct = NormalizeVoxelName(spriteName);
        return resources.GetVoxelBytes(direct) != null ? direct : null;
    }

    private static string NormalizeVoxelName(string spriteName)
    {
        string name = Path.GetFileNameWithoutExtension(spriteName.Replace('\\', '/')).ToUpperInvariant();
        return name.Length > 4 ? name[..4] : name;
    }

    private static string? ResolveModelFrameKey(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        if (spriteName.StartsWith("internal:", StringComparison.OrdinalIgnoreCase)) return null;
        if (spriteName.Length != 6 && spriteName.Length != 8) return null;
        return spriteName[..5].ToUpperInvariant();
    }

    private static bool IsTargetFrame(ModeldefFrame frame, string modelFrameKey)
    {
        if (frame.Sprite.Length != 4 || frame.Frame.Length != 1) return false;
        return string.Equals(frame.Sprite + frame.Frame, modelFrameKey, StringComparison.OrdinalIgnoreCase);
    }

    private static ModeldefModel? FindModel(Modeldef def, int modelIndex)
    {
        foreach (ModeldefModel model in def.Models)
            if (model.Index == modelIndex)
                return model;

        return null;
    }

    private static ThingModelDisplay CreateModelDisplay(Modeldef def, IReadOnlyList<ThingModelDisplayPart> parts)
        => new(
            def,
            def.Path,
            def.Scale,
            def.Offset,
            def.RotationCenter,
            def.AngleOffset,
            def.PitchOffset,
            def.RollOffset,
            def.InheritActorPitch,
            def.UseActorPitch,
            def.UseActorRoll,
            def.UseRotationCenter,
            parts);

    private static ThingModelDisplayPart CreateModelPart(Modeldef def, ModeldefFrame frame, ModeldefModel model)
    {
        string skinName = "";
        foreach (ModeldefSkin skin in def.Skins)
        {
            if (skin.Index == frame.ModelIndex)
            {
                skinName = ResourceManager.CombineModelPath(def.Path, skin.File);
                break;
            }
        }

        var surfaceSkinNames = new Dictionary<int, string>();
        foreach (ModeldefSurfaceSkin skin in def.SurfaceSkins)
            if (skin.ModelIndex == frame.ModelIndex)
                surfaceSkinNames[skin.SurfaceIndex] = ResourceManager.CombineModelPath(def.Path, skin.File);

        return new ThingModelDisplayPart(
            ResourceManager.CombineModelPath(def.Path, model.File),
            skinName,
            surfaceSkinNames,
            frame.ModelFrame ?? "",
            frame.FrameIndex);
    }
}
