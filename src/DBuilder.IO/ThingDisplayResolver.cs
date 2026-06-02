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
    string? VoxelModelName = null);

public static class ThingDisplayResolver
{
    public static ThingDisplaySource Resolve(ThingTypeInfo? thingInfo, ResourceManager? resources)
    {
        if (thingInfo == null || resources == null)
            return new ThingDisplaySource(ThingDisplayKind.Marker);

        if (ResolveModel(thingInfo, resources) is { } model)
            return new ThingDisplaySource(ThingDisplayKind.Model, SpriteName: thingInfo.Sprite, Model: model);

        string? voxel = ResolveVoxel(thingInfo.Sprite, resources);
        if (voxel != null)
            return new ThingDisplaySource(ThingDisplayKind.Voxel, SpriteName: thingInfo.Sprite, VoxelModelName: voxel);

        if (!string.IsNullOrWhiteSpace(thingInfo.Sprite) && resources.GetSprite(thingInfo.Sprite) != null)
            return new ThingDisplaySource(ThingDisplayKind.Sprite, SpriteName: thingInfo.Sprite);

        return new ThingDisplaySource(ThingDisplayKind.Marker);
    }

    public static Modeldef? ResolveModel(ThingTypeInfo thingInfo, ResourceManager resources)
    {
        if (string.IsNullOrWhiteSpace(thingInfo.ClassName)) return null;

        IReadOnlyList<Modeldef> defs = resources.GetModelDefs();
        for (int i = defs.Count - 1; i >= 0; i--)
        {
            Modeldef def = defs[i];
            if (!string.Equals(def.ActorName, thingInfo.ClassName, StringComparison.OrdinalIgnoreCase)) continue;
            if (def.Models.Count == 0) continue;

            foreach (ModeldefModel model in def.Models)
                if (resources.GetModelResourceBytes(def, model.File) != null)
                    return def;
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
}
