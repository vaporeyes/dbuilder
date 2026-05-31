// ABOUTME: Data-level Wavefront OBJ export settings and formatting helpers ported from UDB BuilderModes.
// ABOUTME: Preserves exporter validation, material naming, and coordinate mapping without UI dependencies.

using System.Globalization;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record WavefrontExportOptions
{
    public string FilePath { get; init; } = string.Empty;
    public double Scale { get; init; } = 1.0;
    public bool ExportForGZDoom { get; init; }
    public bool ExportTextures { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string BasePath { get; init; } = string.Empty;
    public string ActorPath { get; init; } = string.Empty;
    public string ModelPath { get; init; } = string.Empty;
    public IReadOnlyList<string> SkipTextures { get; init; } = Array.Empty<string>();
    public bool IgnoreControlSectors { get; init; }
    public bool NormalizeLowestVertex { get; init; }
    public bool CenterModel { get; init; }
    public bool ZScript { get; init; }
    public bool GenerateCode { get; init; } = true;
    public bool GenerateModeldef { get; init; } = true;
    public string Sprite { get; init; } = "PLAY";
    public bool NoGravity { get; init; }
    public bool SpawnOnCeiling { get; init; }
    public bool Solid { get; init; }
}

public sealed class WavefrontExportSettings
{
    public const string DefaultMaterial = "Default";

    private WavefrontExportSettings(WavefrontExportOptions options)
    {
        ObjName = Path.GetFileNameWithoutExtension(options.FilePath);
        ObjPath = Path.GetDirectoryName(options.FilePath) ?? string.Empty;
        Scale = options.Scale;
        ExportForGZDoom = options.ExportForGZDoom;
        ExportTextures = options.ExportTextures;
        ActorName = options.ActorName.Trim();
        BasePath = options.BasePath;
        ActorPath = options.ActorPath;
        ModelPath = options.ModelPath;
        IgnoreControlSectors = options.IgnoreControlSectors;
        NormalizeLowestVertex = options.NormalizeLowestVertex;
        CenterModel = options.CenterModel;
        ZScript = options.ZScript;
        GenerateCode = options.GenerateCode;
        GenerateModeldef = options.GenerateModeldef;
        Sprite = options.Sprite;
        NoGravity = options.NoGravity;
        SpawnOnCeiling = options.SpawnOnCeiling;
        Solid = options.Solid;

        SkipTextures = ExportForGZDoom ? options.SkipTextures.ToList() : new List<string>();
        if (ExportForGZDoom) SkipTextures.Add("-");
    }

    public string Obj { get; set; } = string.Empty;
    public string ObjName { get; }
    public string ObjPath { get; }
    public double Scale { get; }
    public bool ExportForGZDoom { get; }
    public bool ExportTextures { get; }
    public bool Valid { get; set; }
    public IReadOnlyList<string>? Textures { get; set; }
    public IReadOnlyList<string>? Flats { get; set; }
    public string ActorName { get; }
    public string BasePath { get; }
    public string ActorPath { get; }
    public string ModelPath { get; }
    public List<string> SkipTextures { get; }
    public bool IgnoreControlSectors { get; }
    public bool NormalizeLowestVertex { get; }
    public bool CenterModel { get; }
    public bool ZScript { get; }
    public bool GenerateCode { get; }
    public bool GenerateModeldef { get; }
    public int Radius { get; set; } = 20;
    public int Height { get; set; } = 16;
    public string Sprite { get; }
    public bool NoGravity { get; }
    public bool SpawnOnCeiling { get; }
    public bool Solid { get; }

    public static WavefrontExportSettings FromOptions(WavefrontExportOptions options) => new(options);

    public static string NormalizeMaterialName(string? texture)
        => string.IsNullOrEmpty(texture) || texture == "-" ? DefaultMaterial : texture;

    public static void EnsureMaterial<T>(IDictionary<string, List<T>> geometry, ref string texture)
    {
        texture = NormalizeMaterialName(texture);
        if (!geometry.ContainsKey(texture)) geometry.Add(texture, new List<T>());
    }
}

public static class WavefrontObjFormatter
{
    public static string FormatMaterialLibrary(WavefrontExportSettings settings)
        => settings.ExportForGZDoom ? string.Empty : $"mtllib {settings.ObjName}.mtl\n";

    public static string FormatVertex(Vector3D vertex, WavefrontExportSettings settings, Vector2D offset, double lowestZ)
    {
        double z = vertex.z - (settings.NormalizeLowestVertex ? lowestZ : 0);
        if (settings.ExportForGZDoom)
        {
            return Format(
                "v {0} {2} {1}\n",
                (vertex.x - offset.x) * settings.Scale,
                -(vertex.y - offset.y) * settings.Scale,
                z * settings.Scale * 1.2);
        }

        return Format(
            "v {0} {2} {1}\n",
            -(vertex.x - offset.x) * settings.Scale,
            (vertex.y - offset.y) * settings.Scale,
            z * settings.Scale);
    }

    public static string FormatNormal(Vector3D normal)
        => Format("vn {0} {2} {1}\n", normal.x, normal.y, normal.z);

    public static string FormatUv(double u, double v)
        => Format("vt {0} {1}\n", u, -v);

    private static string Format(string format, params double[] values)
        => string.Format(CultureInfo.InvariantCulture, format, values.Cast<object>().ToArray());
}

public static class WavefrontExportValidation
{
    public static IReadOnlyList<string> Validate(WavefrontExportOptions options, Func<string, bool>? directoryExists = null)
    {
        directoryExists ??= Directory.Exists;
        var errors = new List<string>();

        if (options.ExportForGZDoom)
        {
            ValidateGzdoom(options, directoryExists, errors);
        }
        else
        {
            ValidateClassic(options, directoryExists, errors);
        }

        return errors;
    }

    private static void ValidateGzdoom(WavefrontExportOptions options, Func<string, bool> directoryExists, List<string> errors)
    {
        string actorName = options.ActorName.Trim();
        if (actorName.Length == 0)
            errors.Add("Actor name is required.");
        else if (char.IsDigit(actorName[0]))
            errors.Add("Actor name must not start with a digit.");
        else if (actorName.Any(char.IsWhiteSpace))
            errors.Add("Actor name must not contain whitespace.");

        if (!directoryExists(options.BasePath))
            errors.Add("Base path does not exist.");
        if (options.GenerateCode && !directoryExists(options.ActorPath))
            errors.Add("Actor path does not exist.");
        if (options.GenerateModeldef && !directoryExists(options.ModelPath))
            errors.Add("Model path does not exist.");
        if (options.Sprite.Length != 4)
            errors.Add("Sprite must be exactly four characters.");
    }

    private static void ValidateClassic(WavefrontExportOptions options, Func<string, bool> directoryExists, List<string> errors)
    {
        if (options.Scale == 0)
            errors.Add("Scale must not be zero.");

        string? directory = Path.GetDirectoryName(options.FilePath);
        if (string.IsNullOrEmpty(directory) || !directoryExists(directory))
            errors.Add("Export path does not exist.");
    }
}
