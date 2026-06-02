// ABOUTME: Coordinates UDB-style MODELDEF model part loading across supported parser formats.
// ABOUTME: Preserves model format dispatch, frame constraints, skin selection, texture error planning, bounds, and radius.

namespace DBuilder.Rendering;

public sealed record GzModelLoadRequest(
    string Path,
    ModelLoadVector Scale,
    IReadOnlyList<string> ModelNames,
    IReadOnlyList<string> SkinNames,
    IReadOnlyList<IReadOnlyDictionary<int, string>> SurfaceSkinNames,
    IReadOnlyList<string> FrameNames,
    IReadOnlyList<int> FrameIndices,
    bool IsVoxel = false);

public readonly record struct ModelLoadVector(float X, float Y, float Z)
{
    public static ModelLoadVector One { get; } = new(1, 1, 1);
}

public sealed record GzLoadedModel(
    IReadOnlyList<GzModelMesh> Meshes,
    IReadOnlyList<string?> TexturePaths,
    IReadOnlyList<string> Errors,
    GzModelBounds Bounds,
    int Radius);

public static class GzModelLoadCoordinator
{
    public static GzLoadedModel Load(
        GzModelLoadRequest request,
        Func<string, byte[]?> loadModelBytes,
        Func<string, bool>? textureExists = null,
        IReadOnlyList<PixelColor>? voxelPalette = null)
    {
        textureExists ??= _ => true;
        var meshes = new List<GzModelMesh>();
        var texturePaths = new List<string?>();
        var errors = new List<string>();
        var bounds = new BoundsBuilder();

        if (request.IsVoxel)
            LoadVoxelModels(request, loadModelBytes, voxelPalette, meshes, texturePaths, errors, bounds);
        else
            LoadMeshModels(request, loadModelBytes, textureExists, meshes, texturePaths, errors, bounds);

        GzModelBounds modelBounds = bounds.Build();
        int radius = CalculateRadius(modelBounds, request.Scale);
        return new GzLoadedModel(meshes.ToArray(), texturePaths.ToArray(), errors.ToArray(), modelBounds, radius);
    }

    private static void LoadVoxelModels(
        GzModelLoadRequest request,
        Func<string, byte[]?> loadModelBytes,
        IReadOnlyList<PixelColor>? voxelPalette,
        List<GzModelMesh> meshes,
        List<string?> texturePaths,
        List<string> errors,
        BoundsBuilder bounds)
    {
        foreach (string modelName in request.ModelNames)
        {
            byte[]? bytes = loadModelBytes(modelName);
            if (bytes == null) continue;

            KvxModelLoadResult result = KvxModelLoader.Load(bytes, voxelPalette);
            if (!string.IsNullOrEmpty(result.Errors))
            {
                errors.Add(ModelError(modelName, result.Errors));
                continue;
            }

            meshes.AddRange(result.Meshes);
            texturePaths.Add(modelName);
            bounds.Include(result.Bounds);
        }
    }

    private static void LoadMeshModels(
        GzModelLoadRequest request,
        Func<string, byte[]?> loadModelBytes,
        Func<string, bool> textureExists,
        List<GzModelMesh> meshes,
        List<string?> texturePaths,
        List<string> errors,
        BoundsBuilder bounds)
    {
        for (int i = 0; i < request.ModelNames.Count; i++)
        {
            string modelName = request.ModelNames[i];
            string skinName = GetOrDefault(request.SkinNames, i, "");
            string frameName = GetOrDefault(request.FrameNames, i, "");
            int frameIndex = GetOrDefault(request.FrameIndices, i, 0);
            IReadOnlyDictionary<int, string> surfaceSkins = GetOrDefault(request.SurfaceSkinNames, i, EmptySurfaceSkins);
            IReadOnlyDictionary<int, string>? parserSkins = string.IsNullOrEmpty(skinName)
                ? surfaceSkins
                : null;

            byte[]? modelBytes = loadModelBytes(modelName);
            if (modelBytes == null)
            {
                errors.Add(ModelError(modelName, "unable to find file."));
                continue;
            }

            ModelPartLoadResult? result = LoadPart(modelName, modelBytes, frameIndex, frameName, parserSkins, loadModelBytes, errors);
            if (result == null)
                continue;

            if (!string.IsNullOrEmpty(result.Errors))
            {
                errors.Add(ModelError(modelName, result.Errors));
                continue;
            }

            meshes.AddRange(result.Meshes);
            bounds.Include(result.Bounds);

            if (parserSkins != null)
                AddModelSkinTextures(request.Path, modelName, result, textureExists, texturePaths, errors);
            else
                AddModeldefSkinTexture(modelName, skinName, textureExists, texturePaths, errors);
        }
    }

    private static ModelPartLoadResult? LoadPart(
        string modelName,
        byte[] modelBytes,
        int frameIndex,
        string frameName,
        IReadOnlyDictionary<int, string>? parserSkins,
        Func<string, byte[]?> loadModelBytes,
        List<string> errors)
    {
        string extension = Path.GetExtension(modelName).ToLowerInvariant();
        return extension switch
        {
            ".md3" => LoadMd3(modelName, modelBytes, frameIndex, frameName, parserSkins, errors),
            ".md2" => From(Md2ModelLoader.Load(modelBytes, frameIndex, frameName)),
            ".3d" => LoadUnreal(modelName, modelBytes, frameIndex, parserSkins, loadModelBytes, errors),
            ".obj" => LoadObj(modelName, modelBytes, frameIndex, parserSkins, errors),
            ".iqm" => LoadIqm(modelName, modelBytes, frameIndex, frameName, errors),
            _ => new ModelPartLoadResult(Array.Empty<string>(), Array.Empty<GzModelMesh>(), "model format is not supported", GzModelBounds.Empty),
        };
    }

    private static ModelPartLoadResult? LoadMd3(
        string modelName,
        byte[] modelBytes,
        int frameIndex,
        string frameName,
        IReadOnlyDictionary<int, string>? parserSkins,
        List<string> errors)
    {
        if (!string.IsNullOrEmpty(frameName))
        {
            errors.Add(ModelError(modelName, "frame names are not supported for MD3 models!"));
            return null;
        }

        return From(Md3ModelLoader.Load(modelBytes, parserSkins, frameIndex));
    }

    private static ModelPartLoadResult? LoadObj(
        string modelName,
        byte[] modelBytes,
        int frameIndex,
        IReadOnlyDictionary<int, string>? parserSkins,
        List<string> errors)
    {
        if (frameIndex > 0)
        {
            errors.Add($"Trying to load frame {frameIndex} of model \"{modelName}\", but OBJ doesn't support frames!");
            return null;
        }

        using var stream = new MemoryStream(modelBytes);
        return From(ObjModelLoader.Load(stream, parserSkins));
    }

    private static ModelPartLoadResult? LoadIqm(
        string modelName,
        byte[] modelBytes,
        int frameIndex,
        string frameName,
        List<string> errors)
    {
        if (!string.IsNullOrEmpty(frameName))
        {
            errors.Add(ModelError(modelName, "frame names are not supported for IQM models!"));
            return null;
        }

        return From(IqmModelLoader.Load(modelBytes, frameIndex));
    }

    private static ModelPartLoadResult? LoadUnreal(
        string modelName,
        byte[] modelBytes,
        int frameIndex,
        IReadOnlyDictionary<int, string>? parserSkins,
        Func<string, byte[]?> loadModelBytes,
        List<string> errors)
    {
        bool isDataFile = modelName.EndsWith("_d.3d", StringComparison.OrdinalIgnoreCase);
        string companionName = isDataFile
            ? ReplaceSuffix(modelName, "_d.3d", "_a.3d")
            : ReplaceSuffix(modelName, "_a.3d", "_d.3d");
        byte[]? companionBytes = loadModelBytes(companionName);
        if (companionBytes == null)
        {
            errors.Add(ModelError(modelName, $"unable to find corresponding \"{Path.GetFileName(companionName)}\" file."));
            return null;
        }

        return isDataFile
            ? From(UnrealModelLoader.Load(companionBytes, modelBytes, frameIndex, parserSkins))
            : From(UnrealModelLoader.Load(modelBytes, companionBytes, frameIndex, parserSkins));
    }

    private static string ReplaceSuffix(string value, string oldSuffix, string newSuffix)
    {
        if (value.EndsWith(oldSuffix, StringComparison.OrdinalIgnoreCase))
            return value[..^oldSuffix.Length] + newSuffix;

        return Path.ChangeExtension(value, null) + newSuffix;
    }

    private static void AddModelSkinTextures(
        string modelPath,
        string modelName,
        ModelPartLoadResult result,
        Func<string, bool> textureExists,
        List<string?> texturePaths,
        List<string> errors)
    {
        for (int meshIndex = 0; meshIndex < result.Meshes.Count; meshIndex++)
        {
            if (meshIndex >= result.Skins.Count)
            {
                errors.Add(ModelError(modelName, "no skin defined for mesh " + meshIndex + "."));
                texturePaths.Add(null);
                continue;
            }

            string skin = result.Skins[meshIndex];
            if (string.IsNullOrEmpty(skin))
            {
                errors.Add(ModelError(modelName, "texture not found in MODELDEF or model skin."));
                texturePaths.Add(null);
                continue;
            }

            string path = CombineModelPath(modelPath, skin);
            if (textureExists(path))
            {
                texturePaths.Add(path);
                continue;
            }

            path = NormalizePath(skin);
            if (textureExists(path))
            {
                texturePaths.Add(path);
                continue;
            }

            texturePaths.Add(null);
            errors.Add(ModelError(modelName, "unable to load skin \"" + path + "\""));
        }
    }

    private static void AddModeldefSkinTexture(
        string modelName,
        string skinName,
        Func<string, bool> textureExists,
        List<string?> texturePaths,
        List<string> errors)
    {
        if (textureExists(skinName))
        {
            texturePaths.Add(skinName);
            return;
        }

        texturePaths.Add(null);
        errors.Add(ModelError(modelName, "unable to load skin \"" + skinName + "\""));
    }

    private static int CalculateRadius(GzModelBounds bounds, ModelLoadVector scale)
    {
        int minX = (int)(bounds.MinX * scale.X);
        int maxX = (int)(bounds.MaxX * scale.X);
        int minY = (int)(bounds.MinY * scale.Y);
        int maxY = (int)(bounds.MaxY * scale.Y);
        return Math.Max(Math.Max(Math.Abs(minY), Math.Abs(maxY)), Math.Max(Math.Abs(minX), Math.Abs(maxX)));
    }

    private static string CombineModelPath(string path, string file)
    {
        if (string.IsNullOrEmpty(path)) return NormalizePath(file);
        return NormalizePath(Path.Combine(path, file));
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private static string ModelError(string modelName, string error)
        => $"Error while loading \"{modelName}\": {error}";

    private static string GetOrDefault(IReadOnlyList<string> values, int index, string fallback)
        => index >= 0 && index < values.Count ? values[index] : fallback;

    private static int GetOrDefault(IReadOnlyList<int> values, int index, int fallback)
        => index >= 0 && index < values.Count ? values[index] : fallback;

    private static IReadOnlyDictionary<int, string> GetOrDefault(IReadOnlyList<IReadOnlyDictionary<int, string>> values, int index, IReadOnlyDictionary<int, string> fallback)
        => index >= 0 && index < values.Count ? values[index] : fallback;

    private static ModelPartLoadResult From(Md2ModelLoadResult result)
        => new(result.Skins, result.Meshes, result.Errors, result.Bounds);

    private static ModelPartLoadResult From(Md3ModelLoadResult result)
        => new(result.Skins, result.Meshes, result.Errors, result.Bounds);

    private static ModelPartLoadResult From(ObjModelLoadResult result)
        => new(result.Skins, result.Meshes, result.Errors, result.Bounds);

    private static ModelPartLoadResult From(IqmModelLoadResult result)
        => new(result.Skins, result.Meshes, result.Errors, result.Bounds);

    private static ModelPartLoadResult From(UnrealModelLoadResult result)
        => new(result.Skins, result.Meshes, result.Errors, result.Bounds);

    private static readonly IReadOnlyDictionary<int, string> EmptySurfaceSkins = new Dictionary<int, string>();

    private sealed record ModelPartLoadResult(
        IReadOnlyList<string> Skins,
        IReadOnlyList<GzModelMesh> Meshes,
        string? Errors,
        GzModelBounds Bounds);

    private sealed class BoundsBuilder
    {
        private bool hasBounds;
        private float minX;
        private float minY;
        private float minZ;
        private float maxX;
        private float maxY;
        private float maxZ;

        public void Include(GzModelBounds bounds)
        {
            if (bounds == GzModelBounds.Empty) return;
            Include(bounds.MinX, bounds.MinY, bounds.MinZ);
            Include(bounds.MaxX, bounds.MaxY, bounds.MaxZ);
        }

        private void Include(float x, float y, float z)
        {
            if (!hasBounds)
            {
                minX = maxX = x;
                minY = maxY = y;
                minZ = maxZ = z;
                hasBounds = true;
                return;
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }

        public GzModelBounds Build()
            => hasBounds ? new GzModelBounds(minX, minY, minZ, maxX, maxY, maxZ) : GzModelBounds.Empty;
    }
}
