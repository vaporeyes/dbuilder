// ABOUTME: Models UDB BuilderModes image export settings without renderer or file IO dependencies.
// ABOUTME: Preserves UDB form defaults, format indexes, scale indexes, and default output naming.

namespace DBuilder.Map;

using DBuilder.Geometry;

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

public readonly record struct ImageExportLayout(Vector2D Size, Vector2D Offset);

public sealed record ImageExportOutputPlan(
    ImageExportLayout Layout,
    int TileCount,
    int ProgressItems,
    IReadOnlyList<string> ImageNames);

public static class ImageExportPlanner
{
    public const int TileSize = 64;

    public static ImageExportOutputPlan CreateOutputPlan(IEnumerable<Sector> sectors, ImageExportSettings settings)
    {
        ImageExportLayout layout = GetLayout(sectors);
        return new ImageExportOutputPlan(
            layout,
            GetNumTiles(layout, settings),
            GetProgressItems(sectors, layout, settings),
            GetImageNames(layout, settings));
    }

    public static ImageExportLayout GetLayout(IEnumerable<Sector> sectors)
    {
        Vector2D offset = new(double.MaxValue, double.MinValue);
        Vector2D size = new(double.MinValue, double.MaxValue);

        foreach (Sector sector in sectors)
        {
            foreach (Sidedef sidedef in sector.Sidedefs)
            {
                UpdateBounds(sidedef.Line.Start.Position, ref offset, ref size);
                UpdateBounds(sidedef.Line.End.Position, ref offset, ref size);
            }
        }

        size -= offset;
        size.y *= -1.0;

        return new ImageExportLayout(size, offset);
    }

    public static int GetNumTiles(ImageExportLayout layout, ImageExportSettings settings)
    {
        int xnum = (int)Math.Ceiling(layout.Size.x * settings.Scale / TileSize);
        int ynum = (int)Math.Ceiling(layout.Size.y * settings.Scale / TileSize);
        return xnum * ynum;
    }

    public static int GetProgressItems(IEnumerable<Sector> sectors, ImageExportLayout layout, ImageExportSettings settings)
    {
        int count = 0;
        foreach (Sector sector in sectors)
            count += Triangulation.Create(sector).Vertices.Count / 3;

        if (settings.Tiles)
            count += GetNumTiles(layout, settings);

        if (settings.Brightmap)
            count *= 2;

        return count;
    }

    public static IReadOnlyList<string> GetImageNames(ImageExportLayout layout, ImageExportSettings settings)
    {
        var names = new List<string>();
        string basePath = Path.Combine(settings.Directory, settings.Name);

        if (settings.Tiles)
        {
            int xnum = (int)Math.Ceiling(layout.Size.x / TileSize);
            int ynum = (int)Math.Ceiling(layout.Size.y / TileSize);

            for (int i = 1; i <= xnum * ynum; i++)
                names.Add($"{basePath}{i}{settings.Extension}");

            if (settings.Brightmap)
            {
                for (int i = 1; i <= xnum * ynum; i++)
                    names.Add($"{basePath}{i}_brightmap{settings.Extension}");
            }
        }
        else
        {
            names.Add($"{basePath}{settings.Extension}");

            if (settings.Brightmap)
                names.Add($"{basePath}_brightmap{settings.Extension}");
        }

        return names;
    }

    private static void UpdateBounds(Vector2D position, ref Vector2D offset, ref Vector2D size)
    {
        if (position.x < offset.x) offset.x = position.x;
        if (position.x > size.x) size.x = position.x;
        if (position.y > offset.y) offset.y = position.y;
        if (position.y < size.y) size.y = position.y;
    }
}
