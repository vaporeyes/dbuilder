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

public static class WavefrontExportContent
{
    public static string BuildMaterialLibrary(
        WavefrontExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        var mtl = new StringWriter(CultureInfo.InvariantCulture);
        mtl.WriteLine($"# MTL for {mapTitle}, map {levelName}");
        mtl.WriteLine($"# Created by Ultimate Doom Builder {productVersion}");
        mtl.WriteLine();

        if (settings.Textures != null)
        {
            foreach (string texture in settings.Textures)
            {
                if (texture == WavefrontExportSettings.DefaultMaterial) continue;
                WriteMaterial(mtl, texture, settings.ExportTextures ? GetTextureExportPath(settings, texture) : null);
            }
        }

        if (settings.Flats != null)
        {
            foreach (string flat in settings.Flats)
            {
                if (flat == WavefrontExportSettings.DefaultMaterial) continue;
                string? imagePath = null;
                if (settings.ExportTextures)
                {
                    bool hasTextureWithSameName = settings.Textures != null && settings.Textures.Contains(flat);
                    imagePath = GetFlatExportPath(settings, flat, hasTextureWithSameName);
                }

                WriteMaterial(mtl, flat, imagePath);
            }
        }

        return mtl.ToString();
    }

    public static string BuildActorCode(WavefrontExportSettings settings)
        => settings.ZScript ? BuildZScript(settings) : BuildDecorate(settings);

    public static string BuildModeldef(WavefrontExportSettings settings)
    {
        string modelPath = GetRelativeDirectoryPath(settings.BasePath, settings.ModelPath);
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine($"Model {settings.ActorName}");
        writer.WriteLine("{");
        writer.WriteLine($"\tModel 0 \"{modelPath}{settings.ActorName}.obj\"");
        writer.WriteLine();
        writer.WriteLine("\tUSEACTORPITCH");
        writer.WriteLine("\tUSEACTORROLL");
        writer.WriteLine("   ");
        writer.WriteLine($"\tFrameIndex {settings.Sprite} A 0 0");
        writer.WriteLine("}");
        return writer.ToString();
    }

    public static string GetTextureExportPath(WavefrontExportSettings settings, string texture)
        => Path.Combine(
            settings.ObjPath,
            Path.GetDirectoryName(texture) ?? string.Empty,
            Path.GetFileNameWithoutExtension(texture) + ".png");

    public static string GetFlatExportPath(WavefrontExportSettings settings, string flat, bool hasTextureWithSameName)
    {
        string suffix = hasTextureWithSameName ? "_FLAT" : string.Empty;
        string flatPath = Path.Combine(
            settings.ObjPath,
            Path.GetDirectoryName(flat) ?? string.Empty,
            Path.GetFileNameWithoutExtension(flat) + suffix + ".png");
        return Path.Combine(settings.ObjPath, flatPath);
    }

    private static void WriteMaterial(TextWriter writer, string name, string? imagePath)
    {
        writer.WriteLine($"newmtl {name}");
        writer.WriteLine("Kd 1.0 1.0 1.0");
        if (imagePath != null) writer.WriteLine($"map_Kd {imagePath}");
        writer.WriteLine();
    }

    private static string BuildZScript(WavefrontExportSettings settings)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine($"Class {settings.ActorName} : Actor {{");
        writer.WriteLine("\tDefault {");
        writer.WriteLine($"\t\tRadius {settings.Radius};");
        writer.WriteLine($"\t\tHeight {settings.Height};");
        writer.WriteLine($"\t\t{Flag(settings.NoGravity, "+NOGRAVITY")}");
        writer.WriteLine($"\t\t{Flag(settings.SpawnOnCeiling, "+SPAWNCEILING")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+SOLID")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+INVULNERABLE")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+NODAMAGE")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+SHOOTABLE")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+NOTAUTOAIMED")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+NEVERTARGET")}");
        writer.WriteLine($"\t\t{Flag(settings.Solid, "+DONTTHRUST")}");
        writer.WriteLine("\t}");
        writer.WriteLine();
        writer.WriteLine("\tStates {");
        writer.WriteLine("\t\tSpawn:");
        writer.WriteLine($"\t\t\t{settings.Sprite} A -1;");
        writer.WriteLine("\t\t\tStop;");
        writer.WriteLine("\t}");
        writer.WriteLine("}");
        return writer.ToString();
    }

    private static string BuildDecorate(WavefrontExportSettings settings)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine($"ACTOR {settings.ActorName}");
        writer.WriteLine("{");
        writer.WriteLine($"\tRadius {settings.Radius}");
        writer.WriteLine($"\tHeight {settings.Height}");
        writer.WriteLine($"\t{Flag(settings.NoGravity, "+NOGRAVITY")}");
        writer.WriteLine($"\t{Flag(settings.SpawnOnCeiling, "+SPAWNCEILING")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+SOLID")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+INVULNERABLE")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+NODAMAGE")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+SHOOTABLE")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+NOTAUTOAIMED")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+NEVERTARGET")}");
        writer.WriteLine($"\t{Flag(settings.Solid, "+DONTTHRUST")}");
        writer.WriteLine("  ");
        writer.WriteLine("\tStates {");
        writer.WriteLine("\t\tSpawn:");
        writer.WriteLine($"\t\t\t{settings.Sprite} A -1");
        writer.WriteLine("\t\t\tStop");
        writer.WriteLine("\t}");
        writer.WriteLine("}");
        return writer.ToString();
    }

    private static string Flag(bool enabled, string flag) => enabled ? flag : string.Empty;

    private static string GetRelativeDirectoryPath(string basePath, string modelPath)
    {
        string trimmedBasePath = EnsureDirectorySeparator(basePath.Trim());
        string trimmedModelPath = EnsureDirectorySeparator(modelPath.Trim());
        var baseUri = new Uri(trimmedBasePath);
        var modelUri = new Uri(trimmedModelPath);
        Uri relativeUri = baseUri.MakeRelativeUri(modelUri);
        return Uri.UnescapeDataString(relativeUri.OriginalString);
    }

    private static string EnsureDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
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
