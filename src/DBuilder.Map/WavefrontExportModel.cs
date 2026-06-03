// ABOUTME: Data-level Wavefront OBJ export settings and formatting helpers ported from UDB BuilderModes.
// ABOUTME: Preserves exporter validation, material naming, and coordinate mapping without UI dependencies.

using System.Globalization;
using System.Collections;
using System.IO.Compression;
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

public sealed record WavefrontPluginSettings(
    bool ExportTextures = false,
    bool ExportForGZDoom = false,
    float Scale = 1.0f,
    string BasePath = "",
    string ActorPath = "",
    string ModelPath = "",
    string Sprite = "PLAY",
    bool GenerateCode = true,
    bool GenerateModeldef = true,
    IReadOnlyList<string>? SkipTextures = null)
{
    public const string ExportTexturesKey = "objexporttextures";
    public const string ExportForGZDoomKey = "objgzdoomscale";
    public const string ScaleKey = "objscale";
    public const string BasePathKey = "objbasepath";
    public const string ActorPathKey = "objactorpath";
    public const string ModelPathKey = "objmodelpath";
    public const string SpriteKey = "objsprite";
    public const string GenerateCodeKey = "objgeneratecode";
    public const string GenerateModeldefKey = "objgeneratemodeldef";
    public const string SkipTexturesKey = "objskiptextures";

    public IReadOnlyList<string> NormalizedSkipTextures => SkipTextures ?? Array.Empty<string>();

    public static WavefrontPluginSettings FromDictionary(
        IReadOnlyDictionary<string, object?> settings,
        string initialDirectory)
        => new(
            ReadBool(settings, ExportTexturesKey, false),
            ReadBool(settings, ExportForGZDoomKey, false),
            ReadFloat(settings, ScaleKey, 1.0f),
            ReadString(settings, BasePathKey, initialDirectory),
            ReadString(settings, ActorPathKey, initialDirectory),
            ReadString(settings, ModelPathKey, initialDirectory),
            ReadString(settings, SpriteKey, "PLAY"),
            ReadBool(settings, GenerateCodeKey, true),
            ReadBool(settings, GenerateModeldefKey, true),
            ReadSkipTextures(settings));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[ExportTexturesKey] = ExportTextures;
        settings[ExportForGZDoomKey] = ExportForGZDoom;
        settings[ScaleKey] = Scale;
        settings[BasePathKey] = BasePath;
        settings[ActorPathKey] = ActorPath;
        settings[ModelPathKey] = ModelPath;
        settings[SpriteKey] = Sprite.ToUpperInvariant();
        settings[GenerateCodeKey] = GenerateCode;
        settings[GenerateModeldefKey] = GenerateModeldef;

        var skip = new Dictionary<string, string>(StringComparer.Ordinal);
        int index = 0;
        foreach (string texture in NormalizedSkipTextures)
        {
            skip["texture" + index] = texture;
            index++;
        }

        settings[SkipTexturesKey] = skip;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
        => settings.TryGetValue(key, out object? value) && value is bool result ? result : fallback;

    private static float ReadFloat(IReadOnlyDictionary<string, object?> settings, string key, float fallback)
        => settings.TryGetValue(key, out object? value) ? value switch
        {
            float result => result,
            double result => (float)result,
            decimal result => (float)result,
            int result => result,
            _ => fallback,
        } : fallback;

    private static string ReadString(IReadOnlyDictionary<string, object?> settings, string key, string fallback)
        => settings.TryGetValue(key, out object? value) && value is string result ? result : fallback;

    private static IReadOnlyList<string> ReadSkipTextures(IReadOnlyDictionary<string, object?> settings)
    {
        if (!settings.TryGetValue(SkipTexturesKey, out object? value)) return Array.Empty<string>();

        if (value is IDictionary dictionary)
        {
            var result = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Value is string texture) result.Add(texture);
            return result;
        }

        if (value is IEnumerable<KeyValuePair<string, string>> typed)
            return typed.Select(entry => entry.Value).ToArray();

        return Array.Empty<string>();
    }
}

public sealed record WavefrontExportUiState(
    bool GzdoomOptionsEnabled,
    bool ActorSettingsEnabled,
    bool ActorFormatEnabled,
    bool ActorPathEnabled,
    bool ModelPathEnabled,
    bool ClassicExportPathEnabled,
    bool ClassicExportTexturesEnabled,
    bool ScaleEnabled)
{
    public static WavefrontExportUiState FromOptions(bool exportForGZDoom, bool generateCode)
    {
        bool actorOptionsEnabled = exportForGZDoom && generateCode;
        bool classicOptionsEnabled = !exportForGZDoom;

        return new WavefrontExportUiState(
            GzdoomOptionsEnabled: exportForGZDoom,
            ActorSettingsEnabled: actorOptionsEnabled,
            ActorFormatEnabled: actorOptionsEnabled,
            ActorPathEnabled: actorOptionsEnabled,
            ModelPathEnabled: exportForGZDoom,
            ClassicExportPathEnabled: classicOptionsEnabled,
            ClassicExportTexturesEnabled: classicOptionsEnabled,
            ScaleEnabled: classicOptionsEnabled);
    }
}

public sealed record WavefrontExportPreflight(
    IReadOnlyList<Sector> Sectors,
    int DialogSectorCount,
    string? Warning)
{
    public bool CanExport => Sectors.Count > 0;
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

    public string ExportStatusText(string label, WavefrontImagePlan imagePlan)
    {
        string images = imagePlan.Files.Count == 0 ? string.Empty : $" {CountLabel(imagePlan.Files.Count, "image file")}.";
        string warnings = imagePlan.Warnings.Count == 0 ? string.Empty : $" {CountLabel(imagePlan.Warnings.Count, "image warning")}.";
        return $"Exported {label}: {CountLabel(Textures?.Count ?? 0, "texture material")}, {CountLabel(Flats?.Count ?? 0, "flat material")}.{images}{warnings}";
    }

    public static string NormalizeMaterialName(string? texture)
        => string.IsNullOrEmpty(texture) || texture == "-" ? DefaultMaterial : texture;

    public static void EnsureMaterial<T>(IDictionary<string, List<T>> geometry, ref string texture)
    {
        texture = NormalizeMaterialName(texture);
        if (!geometry.ContainsKey(texture)) geometry.Add(texture, new List<T>());
    }

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
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

public readonly record struct WavefrontExportFile(string Path, string Content);
public sealed record WavefrontImageData(int Width, int Height, byte[] PngBytes);
public readonly record struct WavefrontExportImageFile(string Path, byte[] Content, string MaterialName, bool IsFlat);
public sealed record WavefrontImagePlan(IReadOnlyList<WavefrontExportImageFile> Files, IReadOnlyList<string> Warnings);
public sealed record WavefrontGeometryCollection(
    IReadOnlyList<IDictionary<string, List<WavefrontSurfaceVertex[]>>> GeometryByTexture,
    IReadOnlyList<string> Textures,
    IReadOnlyList<string> Flats,
    string? Error)
{
    public bool Valid => Error == null;
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

public static class WavefrontGeometryCollector
{
    public const string NoVisualSectorsError = "OBJ Exporter: no visual sectors to export!";
    public const string EmptyGeometryError = "OBJ Exporter: failed to create geometry!";

    public static WavefrontGeometryCollection Collect(
        IEnumerable<Sector> sectors,
        WavefrontExportSettings settings)
        => Collect(sectors, settings, floorsBySector: null);

    public static WavefrontGeometryCollection Collect(
        MapSet map,
        IEnumerable<Sector> sectors,
        WavefrontExportSettings settings)
        => Collect(sectors, settings, ThreeDFloors.Resolve(map, udmf: true));

    private static WavefrontGeometryCollection Collect(
        IEnumerable<Sector> sectors,
        WavefrontExportSettings settings,
        IReadOnlyDictionary<Sector, List<ThreeDFloor>>? floorsBySector)
    {
        var textureGeometry = new Dictionary<string, List<WavefrontSurfaceVertex[]>>(StringComparer.Ordinal);
        var flatGeometry = new Dictionary<string, List<WavefrontSurfaceVertex[]>>(StringComparer.Ordinal);

        if (!settings.ExportForGZDoom)
        {
            textureGeometry.Add(WavefrontExportSettings.DefaultMaterial, new List<WavefrontSurfaceVertex[]>());
            flatGeometry.Add(WavefrontExportSettings.DefaultMaterial, new List<WavefrontSurfaceVertex[]>());
        }

        int visualSectorCount = 0;
        foreach (Sector sector in sectors)
        {
            if (ShouldSkipSector(sector, settings)) continue;
            visualSectorCount++;
            AddFloorAndCeilingGeometry(sector, settings, flatGeometry);
            AddWallGeometry(sector, settings, textureGeometry);
            if (floorsBySector?.TryGetValue(sector, out List<ThreeDFloor>? floors) == true)
                AddThreeDFloorGeometry(sector, floors, settings, textureGeometry, flatGeometry);
        }

        if (visualSectorCount == 0)
            return new WavefrontGeometryCollection(
                [textureGeometry, flatGeometry],
                SortedKeys(textureGeometry),
                SortedKeys(flatGeometry),
                NoVisualSectorsError);

        if (!HasGeometry(textureGeometry) && !HasGeometry(flatGeometry))
            return new WavefrontGeometryCollection(
                [textureGeometry, flatGeometry],
                SortedKeys(textureGeometry),
                SortedKeys(flatGeometry),
                EmptyGeometryError);

        return new WavefrontGeometryCollection(
            [textureGeometry, flatGeometry],
            SortedKeys(textureGeometry),
            SortedKeys(flatGeometry),
            null);
    }

    public static string CreateObjFromSectors(
        IEnumerable<Sector> sectors,
        WavefrontExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        WavefrontGeometryCollection collection = Collect(sectors, settings);
        if (!collection.Valid) return string.Empty;

        string obj = WavefrontGeometryExporter.CreateObjGeometry(collection.GeometryByTexture, settings);
        if (obj.Length == 0) return string.Empty;

        settings.Textures = collection.Textures;
        settings.Flats = collection.Flats;
        settings.Obj = BuildHeader(mapTitle, levelName, productVersion) + obj;
        settings.Valid = true;
        return settings.Obj;
    }

    public static string CreateObjFromMap(
        MapSet map,
        IEnumerable<Sector> sectors,
        WavefrontExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        WavefrontGeometryCollection collection = Collect(map, sectors, settings);
        if (!collection.Valid) return string.Empty;

        string obj = WavefrontGeometryExporter.CreateObjGeometry(collection.GeometryByTexture, settings);
        if (obj.Length == 0) return string.Empty;

        settings.Textures = collection.Textures;
        settings.Flats = collection.Flats;
        settings.Obj = BuildHeader(mapTitle, levelName, productVersion) + obj;
        settings.Valid = true;
        return settings.Obj;
    }

    private static bool ShouldSkipSector(Sector sector, WavefrontExportSettings settings)
    {
        if (!settings.ExportForGZDoom || !settings.IgnoreControlSectors) return false;
        return sector.Sidedefs.Any(side => side.Line?.Action == 160);
    }

    private static void AddFloorAndCeilingGeometry(
        Sector sector,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> flatGeometry)
    {
        Triangulation triangulation = Triangulation.Create(sector);
        for (int i = 0; i + 2 < triangulation.Vertices.Count; i += 3)
        {
            string floorTexture = sector.FloorTexture;
            if (!settings.SkipTextures.Contains(floorTexture))
            {
                WavefrontExportSettings.EnsureMaterial(flatGeometry, ref floorTexture);
                flatGeometry[floorTexture].Add(
                [
                    FlatVertex(sector, triangulation.Vertices[i], floor: true),
                    FlatVertex(sector, triangulation.Vertices[i + 1], floor: true),
                    FlatVertex(sector, triangulation.Vertices[i + 2], floor: true),
                ]);
            }

            string ceilingTexture = sector.CeilTexture;
            if (!settings.SkipTextures.Contains(ceilingTexture))
            {
                WavefrontExportSettings.EnsureMaterial(flatGeometry, ref ceilingTexture);
                flatGeometry[ceilingTexture].Add(
                [
                    FlatVertex(sector, triangulation.Vertices[i + 2], floor: false),
                    FlatVertex(sector, triangulation.Vertices[i + 1], floor: false),
                    FlatVertex(sector, triangulation.Vertices[i], floor: false),
                ]);
            }
        }
    }

    private static void AddWallGeometry(
        Sector sector,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> textureGeometry)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.MiddleRequired())
            {
                AddWallPart(side, settings, textureGeometry, side.MidTexture, SidedefPart.Middle);
                continue;
            }

            if (side.HighRequired())
                AddWallPart(side, settings, textureGeometry, side.HighTexture, SidedefPart.Upper);

            if (side.LowRequired())
                AddWallPart(side, settings, textureGeometry, side.LowTexture, SidedefPart.Lower);
        }
    }

    private static void AddThreeDFloorGeometry(
        Sector sector,
        IReadOnlyList<ThreeDFloor> floors,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> textureGeometry,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> flatGeometry)
    {
        foreach (ThreeDFloor floor in floors)
        {
            AddThreeDFloorFlats(sector, floor, settings, flatGeometry);
            AddThreeDFloorSides(sector, floor, settings, textureGeometry);
        }
    }

    private static void AddThreeDFloorFlats(
        Sector sector,
        ThreeDFloor floor,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> flatGeometry)
    {
        Triangulation triangulation = Triangulation.Create(sector);
        for (int i = 0; i + 2 < triangulation.Vertices.Count; i += 3)
        {
            string topFlat = floor.TopFlat;
            if (!settings.SkipTextures.Contains(topFlat))
            {
                WavefrontExportSettings.EnsureMaterial(flatGeometry, ref topFlat);
                flatGeometry[topFlat].Add(
                [
                    FlatVertex(floor.Control, triangulation.Vertices[i], floor: false),
                    FlatVertex(floor.Control, triangulation.Vertices[i + 1], floor: false),
                    FlatVertex(floor.Control, triangulation.Vertices[i + 2], floor: false),
                ]);
            }

            string bottomFlat = floor.BottomFlat;
            if (!settings.SkipTextures.Contains(bottomFlat))
            {
                WavefrontExportSettings.EnsureMaterial(flatGeometry, ref bottomFlat);
                flatGeometry[bottomFlat].Add(
                [
                    FlatVertex(floor.Control, triangulation.Vertices[i + 2], floor: true),
                    FlatVertex(floor.Control, triangulation.Vertices[i + 1], floor: true),
                    FlatVertex(floor.Control, triangulation.Vertices[i], floor: true),
                ]);
            }
        }
    }

    private static void AddThreeDFloorSides(
        Sector sector,
        ThreeDFloor floor,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> textureGeometry)
    {
        string texture = floor.SideTexture;
        if (settings.SkipTextures.Contains(texture)) return;

        foreach (Sidedef side in sector.Sidedefs)
        {
            Vector2D start = side.IsFront ? side.Line.Start.Position : side.Line.End.Position;
            Vector2D end = side.IsFront ? side.Line.End.Position : side.Line.Start.Position;
            double startBottom = floor.Control.GetFloorZ(start);
            double endBottom = floor.Control.GetFloorZ(end);
            double startTop = floor.Control.GetCeilZ(start);
            double endTop = floor.Control.GetCeilZ(end);
            if (startTop <= startBottom && endTop <= endBottom) continue;

            AddWallQuad(side, settings, textureGeometry, texture, start, end, startBottom, endBottom, startTop, endTop, skipRectangleOptimization: true);
        }
    }

    private static void AddWallPart(
        Sidedef side,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> textureGeometry,
        string texture,
        SidedefPart part)
    {
        if (settings.SkipTextures.Contains(texture)) return;

        Vector2D start = side.IsFront ? side.Line.Start.Position : side.Line.End.Position;
        Vector2D end = side.IsFront ? side.Line.End.Position : side.Line.Start.Position;
        (double startBottom, double startTop) = WallPartHeights(side, part, start);
        (double endBottom, double endTop) = WallPartHeights(side, part, end);
        if (startTop <= startBottom && endTop <= endBottom) return;

        AddWallQuad(side, settings, textureGeometry, texture, start, end, startBottom, endBottom, startTop, endTop, skipRectangleOptimization: false);
    }

    private static void AddWallQuad(
        Sidedef side,
        WavefrontExportSettings settings,
        Dictionary<string, List<WavefrontSurfaceVertex[]>> textureGeometry,
        string texture,
        Vector2D start,
        Vector2D end,
        double startBottom,
        double endBottom,
        double startTop,
        double endTop,
        bool skipRectangleOptimization)
    {
        WavefrontExportSettings.EnsureMaterial(textureGeometry, ref texture);
        var vertices = new List<WavefrontSurfaceVertex>
        {
            WallVertex(side, start, startBottom, u: 0, v: (float)startBottom),
            WallVertex(side, end, endBottom, u: (float)side.Line.Length, v: (float)endBottom),
            WallVertex(side, end, endTop, u: (float)side.Line.Length, v: (float)endTop),
            WallVertex(side, start, startBottom, u: 0, v: (float)startBottom),
            WallVertex(side, end, endTop, u: (float)side.Line.Length, v: (float)endTop),
            WallVertex(side, start, startTop, u: 0, v: (float)startTop),
        };

        textureGeometry[texture].AddRange(WavefrontGeometryExporter.OptimizeGeometry(vertices, WavefrontSurfaceType.Wall, skipRectangleOptimization));
    }

    private static (double Bottom, double Top) WallPartHeights(Sidedef side, SidedefPart part, Vector2D position)
    {
        Sector sector = side.Sector!;
        Sector? other = side.Other?.Sector;
        return part switch
        {
            SidedefPart.Upper when other != null => (other.GetCeilZ(position), sector.GetCeilZ(position)),
            SidedefPart.Lower when other != null => (sector.GetFloorZ(position), other.GetFloorZ(position)),
            _ => (sector.GetFloorZ(position), sector.GetCeilZ(position)),
        };
    }

    private static WavefrontSurfaceVertex FlatVertex(Sector sector, Vector2D position, bool floor)
    {
        double z = floor ? sector.GetFloorZ(position) : sector.GetCeilZ(position);
        Vector3D normal = floor ? SurfaceNormal(sector, floor: true) : SurfaceNormal(sector, floor: false);
        return new WavefrontSurfaceVertex(
            (float)position.x,
            (float)position.y,
            (float)z,
            (float)position.x,
            (float)position.y,
            (float)normal.x,
            (float)normal.y,
            (float)normal.z);
    }

    private static WavefrontSurfaceVertex WallVertex(Sidedef side, Vector2D position, double z, float u, float v)
    {
        Vector2D delta = side.IsFront
            ? side.Line.End.Position - side.Line.Start.Position
            : side.Line.Start.Position - side.Line.End.Position;
        double length = Math.Sqrt(delta.GetLengthSq());
        Vector3D normal = length == 0
            ? new Vector3D(0, -1, 0)
            : new Vector3D(delta.y / length, -delta.x / length, 0);

        return new WavefrontSurfaceVertex(
            (float)position.x,
            (float)position.y,
            (float)z,
            side.OffsetX + u,
            side.OffsetY + v,
            (float)normal.x,
            (float)normal.y,
            (float)normal.z);
    }

    private static Vector3D SurfaceNormal(Sector sector, bool floor)
    {
        if (floor && sector.HasFloorSlope) return sector.FloorSlope.GetNormal();
        if (!floor && sector.HasCeilSlope) return -sector.CeilSlope.GetNormal();
        return floor ? new Vector3D(0, 0, 1) : new Vector3D(0, 0, -1);
    }

    private static bool HasGeometry(Dictionary<string, List<WavefrontSurfaceVertex[]>> geometry)
        => geometry.Values.Any(group => group.Count > 0);

    private static IReadOnlyList<string> SortedKeys(Dictionary<string, List<WavefrontSurfaceVertex[]>> geometry)
    {
        string[] keys = geometry.Keys.ToArray();
        Array.Sort(keys, StringComparer.Ordinal);
        return keys;
    }

    private static string BuildHeader(string mapTitle, string levelName, string productVersion)
        => $"# {mapTitle}, map {levelName}\n"
            + $"# Created by Ultimate Doom Builder {productVersion}\n\n"
            + $"o {levelName}\n";
}

public static class WavefrontPngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeRgba(int width, int height, IReadOnlyList<byte> rgba)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (rgba.Count != width * height * 4)
            throw new ArgumentException("RGBA buffer length must match the image dimensions.", nameof(rgba));

        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", Header(width, height));
        WriteChunk(output, "IDAT", CompressRows(width, height, rgba));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] Header(int width, int height)
    {
        byte[] header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        return header;
    }

    private static byte[] CompressRows(int width, int height, IReadOnlyList<byte> rgba)
    {
        using var data = new MemoryStream();
        int stride = width * 4;
        for (int y = 0; y < height; y++)
        {
            data.WriteByte(0);
            for (int x = 0; x < stride; x++)
                data.WriteByte(rgba[y * stride + x]);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data.GetBuffer(), 0, (int)data.Length);
        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32(typeBytes, data);
        byte[] crcBytes = new byte[4];
        WriteInt(crcBytes, 0, unchecked((int)crc));
        output.Write(crcBytes);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xff);
        buffer[offset + 1] = (byte)((value >> 16) & 0xff);
        buffer[offset + 2] = (byte)((value >> 8) & 0xff);
        buffer[offset + 3] = (byte)(value & 0xff);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xffffffffu;
        foreach (byte b in type) crc = UpdateCrc(crc, b);
        foreach (byte b in data) crc = UpdateCrc(crc, b);
        return crc ^ 0xffffffffu;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
        return crc;
    }
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

    public static string GetFlatImageExportPath(WavefrontExportSettings settings, string flat, bool hasTextureWithSameName)
    {
        string suffix = hasTextureWithSameName ? "_FLAT" : string.Empty;
        string flatName = Path.GetFileNameWithoutExtension(flat) + suffix + ".PNG";
        return Path.Combine(settings.ObjPath, flatName);
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

public static class WavefrontExportPlanner
{
    public const string NoSectorsWarning = "OBJ export failed. Map has no sectors!";

    public static WavefrontExportPreflight PrepareExportSelection(MapSet map)
    {
        map.ConvertSelection(SelectionType.Sectors);

        IReadOnlyList<Sector> sectors = map.SelectedSectorsCount == 0 ? map.Sectors : map.GetSelectedSectors();
        if (sectors.Count == 0)
            return new WavefrontExportPreflight(sectors, DialogSectorCount: -1, NoSectorsWarning);

        int dialogSectorCount = map.SelectedSectorsCount == 0 ? -1 : sectors.Count;
        return new WavefrontExportPreflight(sectors, dialogSectorCount, null);
    }

    public static WavefrontImagePlan CreateImagePlan(
        WavefrontExportSettings settings,
        Func<string, WavefrontImageData?> getTexture,
        Func<string, WavefrontImageData?> getFlat)
    {
        var files = new List<WavefrontExportImageFile>();
        var warnings = new List<string>();
        if (!settings.ExportTextures || settings.ExportForGZDoom) return new WavefrontImagePlan(files, warnings);

        if (settings.Textures != null)
        {
            foreach (string texture in settings.Textures)
            {
                if (texture == WavefrontExportSettings.DefaultMaterial) continue;
                WavefrontImageData? image = getTexture(texture);
                if (image == null)
                {
                    warnings.Add($"OBJ Exporter: texture \"{texture}\" does not exist!");
                    continue;
                }

                if (!HasValidSize(image))
                {
                    warnings.Add($"OBJ Exporter: texture \"{texture}\" has invalid size ({image.Width}x{image.Height})!");
                    continue;
                }

                files.Add(new WavefrontExportImageFile(
                    WavefrontExportContent.GetTextureExportPath(settings, texture),
                    image.PngBytes,
                    texture,
                    IsFlat: false));
            }
        }

        if (settings.Flats != null)
        {
            foreach (string flat in settings.Flats)
            {
                if (flat == WavefrontExportSettings.DefaultMaterial) continue;
                WavefrontImageData? image = getFlat(flat);
                if (image == null)
                {
                    warnings.Add($"OBJ Exporter: flat \"{flat}\" does not exist!");
                    continue;
                }

                if (!HasValidSize(image))
                {
                    warnings.Add($"OBJ Exporter: flat \"{flat}\" has invalid size ({image.Width}x{image.Height})!");
                    continue;
                }

                bool hasTextureWithSameName = settings.Textures != null && settings.Textures.Contains(flat);
                files.Add(new WavefrontExportImageFile(
                    WavefrontExportContent.GetFlatImageExportPath(settings, flat, hasTextureWithSameName),
                    image.PngBytes,
                    flat,
                    IsFlat: true));
            }
        }

        return new WavefrontImagePlan(files, warnings);
    }

    public static IReadOnlyList<WavefrontExportFile> CreateFilePlan(
        WavefrontExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        var files = new List<WavefrontExportFile>();
        string objPath = settings.ExportForGZDoom
            ? Path.Combine(settings.ModelPath, settings.ActorName + ".obj")
            : Path.Combine(settings.ObjPath, settings.ObjName + ".obj");
        files.Add(new WavefrontExportFile(objPath, settings.Obj));

        if (!settings.ExportForGZDoom)
        {
            string? directory = Path.GetDirectoryName(objPath);
            string mtlPath = Path.Combine(
                directory ?? string.Empty,
                Path.GetFileNameWithoutExtension(objPath) + ".mtl");
            files.Add(new WavefrontExportFile(
                mtlPath,
                WavefrontExportContent.BuildMaterialLibrary(settings, mapTitle, levelName, productVersion)));
            return files;
        }

        if (settings.GenerateCode)
        {
            string extension = settings.ZScript ? ".zs" : ".txt";
            files.Add(new WavefrontExportFile(
                Path.Combine(settings.ActorPath, settings.ActorName + extension),
                WavefrontExportContent.BuildActorCode(settings)));
        }

        if (settings.GenerateModeldef)
        {
            files.Add(new WavefrontExportFile(
                Path.Combine(settings.BasePath, "modeldef." + settings.ActorName + ".txt"),
                WavefrontExportContent.BuildModeldef(settings)));
        }

        return files;
    }

    public static void WriteFiles(IEnumerable<WavefrontExportFile> files)
    {
        foreach (WavefrontExportFile file in files)
        {
            string? directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(file.Path, file.Content);
        }
    }

    public static void WriteImageFiles(IEnumerable<WavefrontExportImageFile> files)
    {
        foreach (WavefrontExportImageFile file in files)
        {
            string? directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(file.Path, file.Content);
        }
    }

    private static bool HasValidSize(WavefrontImageData image) => image.Width > 0 && image.Height > 0;
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
