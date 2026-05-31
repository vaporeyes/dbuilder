// ABOUTME: Models UDB BuilderModes image export settings without renderer or file IO dependencies.
// ABOUTME: Preserves UDB form defaults, format indexes, scale indexes, and default output naming.

namespace DBuilder.Map;

public enum ImageExportImageFormat
{
    Png,
    Jpeg,
}

public enum ImageExportPixelFormat
{
    Format32BppArgb,
    Format24BppRgb,
    Format16BppRgb555,
}

public sealed record ImageExportOptions(
    string FilePath,
    bool Floor = true,
    bool Fullbright = true,
    bool ApplySectorColors = true,
    bool Brightmap = false,
    bool Transparency = false,
    bool Tiles = false,
    int ScaleIndex = 0,
    int ImageFormatIndex = 0,
    int PixelFormatIndex = 0);

public sealed record ImageExportSettings(
    string Directory,
    string Name,
    string Extension,
    bool Floor,
    bool Fullbright,
    bool ApplySectorColors,
    bool Brightmap,
    bool Transparency,
    bool Tiles,
    float Scale,
    ImageExportPixelFormat PixelFormat,
    ImageExportImageFormat ImageFormat)
{
    public static ImageExportSettings FromOptions(ImageExportOptions options)
    {
        string filePath = options.FilePath.Trim();
        return new ImageExportSettings(
            Path.GetDirectoryName(filePath) ?? "",
            Path.GetFileNameWithoutExtension(filePath),
            Path.GetExtension(filePath),
            options.Floor,
            options.Fullbright,
            options.ApplySectorColors,
            options.Brightmap,
            options.Transparency,
            options.Tiles,
            ScaleFromIndex(options.ScaleIndex),
            PixelFormatFromIndex(options.PixelFormatIndex),
            ImageFormatFromIndex(options.ImageFormatIndex));
    }

    public static ImageExportImageFormat ImageFormatFromIndex(int selectedIndex)
        => selectedIndex == 1 ? ImageExportImageFormat.Jpeg : ImageExportImageFormat.Png;

    public static ImageExportPixelFormat PixelFormatFromIndex(int selectedIndex)
        => selectedIndex switch
        {
            1 => ImageExportPixelFormat.Format24BppRgb,
            2 => ImageExportPixelFormat.Format16BppRgb555,
            _ => ImageExportPixelFormat.Format32BppArgb,
        };

    public static float ScaleFromIndex(int selectedIndex)
        => MathF.Pow(2.0f, Math.Max(0, selectedIndex));

    public static string ExtensionForFormatIndex(int selectedIndex)
        => selectedIndex == 1 ? ".jpg" : ".png";

    public static string ChangeExtensionForFormat(string filePath, int selectedIndex)
        => Path.ChangeExtension(filePath, ExtensionForFormatIndex(selectedIndex));

    public static string DefaultFileName(string mapFileTitle, string levelName, string randomStem)
        => string.Join(
            "_",
            Path.GetFileNameWithoutExtension(mapFileTitle),
            levelName,
            Path.GetFileNameWithoutExtension(randomStem));

    public static string DefaultFilePath(string? mapFilePath, string mapFileTitle, string levelName, string randomStem)
    {
        string fileName = DefaultFileName(mapFileTitle, levelName, randomStem);
        if (string.IsNullOrEmpty(mapFilePath)) return fileName;

        string directory = Path.GetDirectoryName(mapFilePath) ?? "";
        return Path.Combine(directory, fileName + ".png");
    }
}
