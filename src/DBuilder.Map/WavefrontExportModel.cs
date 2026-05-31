// ABOUTME: Data-level Wavefront OBJ export settings and formatting helpers ported from UDB BuilderModes.
// ABOUTME: Preserves exporter validation, material naming, and coordinate mapping without UI dependencies.

using System.Globalization;
using System.Text;
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

public enum WavefrontSurfaceType
{
    Wall,
    Floor,
    Ceiling
}

public readonly record struct WavefrontSurfaceVertex(
    float X,
    float Y,
    float Z,
    float U,
    float V,
    float NormalX,
    float NormalY,
    float NormalZ);

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

public static class WavefrontGeometryExporter
{
    public static List<WavefrontSurfaceVertex[]> OptimizeGeometry(
        IReadOnlyList<WavefrontSurfaceVertex> vertices,
        WavefrontSurfaceType surfaceType,
        bool skipRectangleOptimization = false)
    {
        var groups = new List<WavefrontSurfaceVertex[]>();

        if (!skipRectangleOptimization
            && vertices.Count == 6
            && surfaceType != WavefrontSurfaceType.Ceiling
            && surfaceType != WavefrontSurfaceType.Floor)
        {
            groups.Add([vertices[5], vertices[2], vertices[1], vertices[0]]);
            return groups;
        }

        for (int i = 0; i < vertices.Count; i += 3)
        {
            groups.Add([vertices[i + 2], vertices[i + 1], vertices[i]]);
        }

        return groups;
    }

    public static string CreateObjGeometry(
        IReadOnlyList<IDictionary<string, List<WavefrontSurfaceVertex[]>>> geometryByTexture,
        WavefrontExportSettings settings)
    {
        var uniqueVertices = new Dictionary<Vector3D, int>();
        var uniqueNormals = new Dictionary<Vector3D, int>();
        var uniqueUvs = new Dictionary<WavefrontUv, int>();
        var vertexDataByTexture = new Dictionary<string, Dictionary<WavefrontSurfaceVertex, VertexIndices>>(StringComparer.Ordinal);
        int positionCount = 0;
        int normalCount = 0;
        int uvCount = 0;
        var topLeft = new Vector3D(double.MaxValue, double.MinValue, double.MinValue);
        var bottomRight = new Vector3D(double.MinValue, double.MaxValue, double.MaxValue);

        foreach (IDictionary<string, List<WavefrontSurfaceVertex[]>> dictionary in geometryByTexture)
        {
            foreach (KeyValuePair<string, List<WavefrontSurfaceVertex[]>> group in dictionary)
            {
                var vertexData = new Dictionary<WavefrontSurfaceVertex, VertexIndices>();
                foreach (WavefrontSurfaceVertex[] vertices in group.Value)
                {
                    Vector3D normal = new Vector3D(vertices[0].NormalX, vertices[0].NormalY, vertices[0].NormalZ).GetNormal();
                    normal.y *= -1;
                    int normalIndex = GetIndex(uniqueNormals, normal, ref normalCount);

                    foreach (WavefrontSurfaceVertex vertex in vertices)
                    {
                        if (vertexData.ContainsKey(vertex)) continue;
                        var position = new Vector3D(vertex.X, vertex.Y, vertex.Z);
                        var uv = new WavefrontUv(vertex.U, vertex.V);
                        vertexData.Add(
                            vertex,
                            new VertexIndices(
                                GetIndex(uniqueVertices, position, ref positionCount),
                                GetIndex(uniqueUvs, uv, ref uvCount),
                                normalIndex));
                    }
                }

                if (vertexData.Count == 0) continue;
                if (!vertexDataByTexture.TryAdd(group.Key, vertexData))
                {
                    foreach (KeyValuePair<WavefrontSurfaceVertex, VertexIndices> item in vertexData)
                    {
                        vertexDataByTexture[group.Key].Add(item.Key, item.Value);
                    }
                }
            }
        }

        foreach (Dictionary<WavefrontSurfaceVertex, VertexIndices> vertexData in vertexDataByTexture.Values)
        {
            foreach (WavefrontSurfaceVertex vertex in vertexData.Keys)
            {
                topLeft.x = Math.Min(topLeft.x, vertex.X);
                bottomRight.x = Math.Max(bottomRight.x, vertex.X);
                topLeft.y = Math.Max(topLeft.y, vertex.Y);
                bottomRight.y = Math.Min(bottomRight.y, vertex.Y);
                topLeft.z = Math.Max(topLeft.z, vertex.Z);
                bottomRight.z = Math.Min(bottomRight.z, vertex.Z);
            }
        }

        settings.Radius = bottomRight.x - topLeft.x > topLeft.y - bottomRight.y
            ? (int)(topLeft.y - bottomRight.y) / 2
            : (int)(bottomRight.x - topLeft.x) / 2;
        settings.Height = (int)(topLeft.z - bottomRight.z);

        var offset = settings.CenterModel
            ? new Vector2D(topLeft.x + (bottomRight.x - topLeft.x) / 2.0, topLeft.y + (bottomRight.y - topLeft.y) / 2.0)
            : new Vector2D(0.0, 0.0);

        var obj = new StringBuilder();
        foreach (KeyValuePair<Vector3D, int> vertex in uniqueVertices)
        {
            obj.Append(WavefrontObjFormatter.FormatVertex(vertex.Key, settings, offset, bottomRight.z));
        }

        foreach (KeyValuePair<Vector3D, int> normal in uniqueNormals)
        {
            obj.Append(WavefrontObjFormatter.FormatNormal(normal.Key));
        }

        foreach (KeyValuePair<WavefrontUv, int> uv in uniqueUvs)
        {
            obj.Append(WavefrontObjFormatter.FormatUv(uv.Key.U, uv.Key.V));
        }

        obj.Append(WavefrontObjFormatter.FormatMaterialLibrary(settings));
        foreach (IDictionary<string, List<WavefrontSurfaceVertex[]>> dictionary in geometryByTexture)
        {
            foreach (KeyValuePair<string, List<WavefrontSurfaceVertex[]>> group in dictionary)
            {
                obj.Append("usemtl ").Append(group.Key).Append('\n');
                foreach (WavefrontSurfaceVertex[] vertices in group.Value)
                {
                    obj.Append('f');
                    foreach (WavefrontSurfaceVertex vertex in vertices)
                    {
                        VertexIndices indices = vertexDataByTexture[group.Key][vertex];
                        obj.Append(' ')
                            .Append(indices.PositionIndex)
                            .Append('/')
                            .Append(indices.UvIndex)
                            .Append('/')
                            .Append(indices.NormalIndex);
                    }

                    obj.Append('\n');
                }
            }
        }

        return obj.ToString();
    }

    private static int GetIndex<T>(Dictionary<T, int> lookup, T value, ref int count)
        where T : notnull
    {
        if (lookup.TryGetValue(value, out int existing)) return existing;
        lookup.Add(value, ++count);
        return count;
    }

    private readonly record struct WavefrontUv(float U, float V);

    private readonly record struct VertexIndices(int PositionIndex, int UvIndex, int NormalIndex);
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
