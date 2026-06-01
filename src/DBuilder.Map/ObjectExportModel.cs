// ABOUTME: Models the legacy UDB BuilderModes object export settings form without UI dependencies.
// ABOUTME: Preserves OBJ output naming, texture export, GZDoom scale option, and path validation.

using System.IO;

namespace DBuilder.Map;

public sealed record ObjectExportOptions(
    string FilePath,
    bool FixScale = false,
    bool ExportTextures = false);

public sealed record ObjectExportSettings(
    string FilePath,
    bool FixScale,
    bool ExportTextures)
{
    public const string DefaultExtension = ".obj";
    public const string DialogFilter = "Wavefront obj files|*.obj";
    public const string DialogTitle = "Select save location:";
    public const string InvalidPathMessage = "Selected path does not exist!";

    public static ObjectExportSettings FromOptions(ObjectExportOptions options)
        => new(options.FilePath.Trim(), options.FixScale, options.ExportTextures);

    public static string DefaultFileName(string mapTitle, string levelName)
        => string.Concat(mapTitle, "_", levelName);

    public static string DefaultFilePath(string mapDirectory, string mapTitle, string levelName)
        => Path.Combine(mapDirectory, DefaultFileName(mapTitle, levelName) + DefaultExtension);

    public static IReadOnlyList<string> Validate(ObjectExportOptions options, Func<string, bool>? directoryExists = null)
    {
        directoryExists ??= Directory.Exists;
        var errors = new List<string>();
        string path = options.FilePath.Trim();
        string? directory = Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(directory) || !directoryExists(directory))
            errors.Add(InvalidPathMessage);

        return errors;
    }
}

public static class ObjectExportWriter
{
    public static string CreateObjFromMap(
        MapSet map,
        IEnumerable<Sector> sectors,
        ObjectExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
        => CreateWavefrontExport(map, sectors, settings, mapTitle, levelName, productVersion).Obj;

    public static WavefrontExportSettings CreateWavefrontExport(
        MapSet map,
        IEnumerable<Sector> sectors,
        ObjectExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        WavefrontExportSettings wavefront = CreateWavefrontSettings(settings);
        WavefrontGeometryCollector.CreateObjFromMap(map, sectors, wavefront, mapTitle, levelName, productVersion);
        return wavefront;
    }

    public static IReadOnlyList<WavefrontExportFile> CreateFilePlan(
        MapSet map,
        IEnumerable<Sector> sectors,
        ObjectExportSettings settings,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        WavefrontExportSettings wavefront = CreateWavefrontExport(map, sectors, settings, mapTitle, levelName, productVersion);
        return WavefrontExportPlanner.CreateFilePlan(wavefront, mapTitle, levelName, productVersion);
    }

    public static IReadOnlyList<WavefrontExportFile> CreateFilePlan(
        ObjectExportSettings settings,
        string obj,
        string mapTitle,
        string levelName,
        string productVersion = "")
    {
        WavefrontExportSettings wavefront = CreateWavefrontSettings(settings);
        wavefront.Obj = obj;
        return WavefrontExportPlanner.CreateFilePlan(wavefront, mapTitle, levelName, productVersion);
    }

    public static WavefrontExportSettings CreateWavefrontSettings(ObjectExportSettings settings)
        => WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = settings.FilePath,
            ExportForGZDoom = settings.FixScale,
            ExportTextures = settings.ExportTextures,
            Scale = 1.0,
        });
}
