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
