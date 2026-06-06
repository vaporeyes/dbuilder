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

public enum ImageExportResult
{
    OK,
    Canceled,
    OutOfMemory,
    ImageTooBig,
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

public sealed record ImageExportResultMessage(string Title, string Message, bool IsError)
{
    public static ImageExportResultMessage FromResult(ImageExportResult result)
        => result switch
        {
            ImageExportResult.OK => new ImageExportResultMessage(
                "Export to image",
                "Export successful.",
                IsError: false),
            ImageExportResult.Canceled => new ImageExportResultMessage(
                "Export to image",
                "Export canceled.",
                IsError: false),
            ImageExportResult.OutOfMemory => new ImageExportResultMessage(
                "Export failed",
                "Exporting failed. There's likely not enough consecutive free memory to create the image. Try a lower color depth or file format",
                IsError: true),
            ImageExportResult.ImageTooBig => new ImageExportResultMessage(
                "Export failed",
                "Exporting failed. The image is likely too big for the current settings. Try a lower color depth or file format",
                IsError: true),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
}

public sealed record ImageExportPluginSettings(
    bool Fullbright = true,
    bool ApplySectorColors = true,
    bool Brightmap = false,
    bool Transparency = false,
    bool Tiles = false,
    int ScaleIndex = 0)
{
    public const string FullbrightKey = "imageexportfullbright";
    public const string ApplySectorColorsKey = "imageexportapplysectorcolors";
    public const string BrightmapKey = "imageexportbrightmap";
    public const string TransparencyKey = "imageexporttransparency";
    public const string TilesKey = "imageexporttiles";
    public const string ScaleKey = "imageexportscale";

    public static ImageExportPluginSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new(
            ReadBool(settings, FullbrightKey, true),
            ReadBool(settings, ApplySectorColorsKey, true),
            ReadBool(settings, BrightmapKey, false),
            ReadBool(settings, TransparencyKey, false),
            ReadBool(settings, TilesKey, false),
            ReadInt(settings, ScaleKey, 0));

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[FullbrightKey] = Fullbright;
        settings[ApplySectorColorsKey] = ApplySectorColors;
        settings[BrightmapKey] = Brightmap;
        settings[TransparencyKey] = Transparency;
        settings[TilesKey] = Tiles;
        settings[ScaleKey] = ScaleIndex;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
        => settings.TryGetValue(key, out object? value) && value is bool result ? result : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
        => settings.TryGetValue(key, out object? value) && value is int result ? result : fallback;
}

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
    public const string FormTitle = "Image export settings";
    public const string FormDescription = "Exports selected sectors, or the whole map, as an image.";
    public const string PathLabel = "Path:";
    public const string ImageFormatLabel = "Image format:";
    public const string PixelFormatLabel = "Color depth:";
    public const string FloorText = "Floor";
    public const string CeilingText = "Ceiling";
    public const string FullbrightText = "Use fullbright";
    public const string ApplySectorColorsText = "Apply sector colors";
    public const string TransparencyText = "Allow transparency";
    public const string BrightmapText = "Create brightmap";
    public const string TilesText = "Create 64x64 tiles";
    public const string ScaleLabel = "Scale:";
    public const string ExportButtonText = "Export";
    public const string CloseButtonText = "Close";
    public const string PngText = "PNG";
    public const string JpgText = "JPG";
    public const string Format32BitText = "32 bit";
    public const string Format24BitText = "24 bit";
    public const string Format16BitText = "16 bit";
    public static readonly string[] ScaleTexts = { "100%", "200%", "400%", "800%" };

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

    public static int FormatIndexFromPath(string filePath)
    {
        string extension = Path.GetExtension(filePath.Trim());
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
    }

    public static string ChangeExtensionForFormat(string filePath, int selectedIndex)
        => Path.ChangeExtension(filePath, ExtensionForFormatIndex(selectedIndex));

    public string ExportStatusText(int imageFileCount)
    {
        string brightmaps = Brightmap ? " including brightmaps" : string.Empty;
        string tiles = Tiles ? " as 64x64 tiles" : string.Empty;
        return $"Exported {CountLabel(imageFileCount, "image file")}{brightmaps}{tiles}.";
    }

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

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public sealed record ImageExportFormState(
    ImageExportOptions DefaultOptions,
    string Title,
    string Description,
    string PathLabel,
    string ImageFormatLabel,
    string PixelFormatLabel,
    string FloorText,
    string CeilingText,
    string FullbrightText,
    string ApplySectorColorsText,
    string TransparencyText,
    string BrightmapText,
    string TilesText,
    string ScaleLabel,
    string ExportButtonText,
    string CloseButtonText,
    IReadOnlyList<string> ImageFormats,
    IReadOnlyList<string> PixelFormats,
    IReadOnlyList<string> Scales)
{
    public static ImageExportFormState FromOptions(ImageExportOptions options)
        => new(
            options,
            ImageExportSettings.FormTitle,
            ImageExportSettings.FormDescription,
            ImageExportSettings.PathLabel,
            ImageExportSettings.ImageFormatLabel,
            ImageExportSettings.PixelFormatLabel,
            ImageExportSettings.FloorText,
            ImageExportSettings.CeilingText,
            ImageExportSettings.FullbrightText,
            ImageExportSettings.ApplySectorColorsText,
            ImageExportSettings.TransparencyText,
            ImageExportSettings.BrightmapText,
            ImageExportSettings.TilesText,
            ImageExportSettings.ScaleLabel,
            ImageExportSettings.ExportButtonText,
            ImageExportSettings.CloseButtonText,
            [ImageExportSettings.PngText, ImageExportSettings.JpgText],
            [ImageExportSettings.Format32BitText, ImageExportSettings.Format24BitText, ImageExportSettings.Format16BitText],
            ImageExportSettings.ScaleTexts);
}

public readonly record struct ImageExportLayout(Vector2D Size, Vector2D Offset);

public sealed record ImageExportOutputPlan(
    ImageExportLayout Layout,
    int TileCount,
    int ProgressItems,
    IReadOnlyList<string> ImageNames);

public sealed record ImageExportTextureData(int Width, int Height, byte[] Rgba, float Scale = 1.0f);
public sealed record ImageExportRaster(int Width, int Height, byte[] Rgba);
public readonly record struct ImageExportImageFile(string Path, byte[] Content, bool Brightmap, int TileIndex);

public sealed record ImageExportSectorSelection(
    IReadOnlyList<Sector> Sectors,
    string? Warning)
{
    public bool CanExport => Sectors.Count > 0;
}

public static class ImageExportPlanner
{
    public const int TileSize = 64;
    public const string NoSectorsWarning = "Image export failed. Map has no sectors!";

    public static ImageExportSectorSelection SelectSectorsForExport(MapSet map)
    {
        IReadOnlyList<Sector> sectors = map.SelectedSectorsCount == 0 ? map.Sectors : map.GetSelectedSectors();
        return sectors.Count == 0
            ? new ImageExportSectorSelection(sectors, NoSectorsWarning)
            : new ImageExportSectorSelection(sectors, null);
    }

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

public static class ImageExportRenderer
{
    public static IReadOnlyList<ImageExportImageFile> CreateImageFiles(
        IEnumerable<Sector> sectors,
        ImageExportSettings settings,
        Func<string, ImageExportTextureData?> getFlat)
    {
        var files = new List<ImageExportImageFile>();
        AddImageFiles(files, RenderImage(sectors, settings, getFlat, brightmap: false), settings, brightmap: false);
        if (settings.Brightmap)
            AddImageFiles(files, RenderImage(sectors, settings, getFlat, brightmap: true), settings, brightmap: true);
        return files;
    }

    public static ImageExportRaster RenderImage(
        IEnumerable<Sector> sectors,
        ImageExportSettings settings,
        Func<string, ImageExportTextureData?> getFlat,
        bool brightmap)
    {
        IReadOnlyList<Sector> sectorList = sectors.ToArray();
        ImageExportLayout layout = ImageExportPlanner.GetLayout(sectorList);
        int width = Math.Max(1, (int)Math.Ceiling(layout.Size.x * settings.Scale));
        int height = Math.Max(1, (int)Math.Ceiling(layout.Size.y * settings.Scale));
        byte[] rgba = new byte[width * height * 4];

        if (!settings.Transparency)
        {
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i + 3] = 255;
            }
        }

        foreach (Sector sector in sectorList)
        {
            IReadOnlyList<Vector2D> vertices = Triangulation.Create(sector).Vertices;
            for (int i = 0; i + 2 < vertices.Count; i += 3)
            {
                DrawTriangle(rgba, width, height, layout.Offset, settings, sector, vertices[i], vertices[i + 1], vertices[i + 2], getFlat, brightmap);
            }
        }

        return new ImageExportRaster(width, height, rgba);
    }

    public static void WriteImageFiles(IEnumerable<ImageExportImageFile> files)
    {
        foreach (ImageExportImageFile file in files)
        {
            string? directory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(file.Path, file.Content);
        }
    }

    private static void AddImageFiles(
        List<ImageExportImageFile> files,
        ImageExportRaster raster,
        ImageExportSettings settings,
        bool brightmap)
    {
        if (!settings.Tiles)
        {
            string suffix = brightmap ? "_brightmap" : "";
            ImageExportRaster output = ApplyPixelFormat(raster, settings.PixelFormat);
            files.Add(new ImageExportImageFile(
                Path.Combine(settings.Directory, settings.Name) + suffix + settings.Extension,
                WavefrontPngEncoder.EncodeRgba(output.Width, output.Height, output.Rgba),
                brightmap,
                TileIndex: 0));
            return;
        }

        int xnum = (int)Math.Ceiling(raster.Width / (double)ImageExportPlanner.TileSize);
        int ynum = (int)Math.Ceiling(raster.Height / (double)ImageExportPlanner.TileSize);
        int tileIndex = 1;
        for (int y = 0; y < ynum; y++)
        {
            for (int x = 0; x < xnum; x++)
            {
                byte[] tile = CreateTile(raster, x, y);
                string suffix = brightmap ? "_brightmap" : "";
                tile = ApplyPixelFormat(tile, settings.PixelFormat);
                files.Add(new ImageExportImageFile(
                    $"{Path.Combine(settings.Directory, settings.Name)}{tileIndex}{suffix}{settings.Extension}",
                    WavefrontPngEncoder.EncodeRgba(ImageExportPlanner.TileSize, ImageExportPlanner.TileSize, tile),
                    brightmap,
                    tileIndex));
                tileIndex++;
            }
        }
    }

    private static ImageExportRaster ApplyPixelFormat(ImageExportRaster raster, ImageExportPixelFormat pixelFormat)
        => pixelFormat == ImageExportPixelFormat.Format32BppArgb
            ? raster
            : raster with { Rgba = ApplyPixelFormat(raster.Rgba, pixelFormat) };

    private static byte[] ApplyPixelFormat(byte[] rgba, ImageExportPixelFormat pixelFormat)
    {
        if (pixelFormat == ImageExportPixelFormat.Format32BppArgb) return rgba;

        byte[] result = (byte[])rgba.Clone();
        for (int i = 0; i < result.Length; i += 4)
        {
            if (pixelFormat == ImageExportPixelFormat.Format16BppRgb555)
            {
                result[i] = QuantizeRgb555(result[i]);
                result[i + 1] = QuantizeRgb555(result[i + 1]);
                result[i + 2] = QuantizeRgb555(result[i + 2]);
            }
            result[i + 3] = 255;
        }

        return result;
    }

    private static byte QuantizeRgb555(byte value)
        => (byte)((value >> 3) * 255 / 31);

    private static byte[] CreateTile(ImageExportRaster raster, int tileX, int tileY)
    {
        byte[] tile = new byte[ImageExportPlanner.TileSize * ImageExportPlanner.TileSize * 4];
        for (int i = 0; i < tile.Length; i += 4)
            tile[i + 3] = 255;

        int sourceX = tileX * ImageExportPlanner.TileSize;
        int sourceY = tileY * ImageExportPlanner.TileSize;
        int copyWidth = Math.Min(ImageExportPlanner.TileSize, raster.Width - sourceX);
        int copyHeight = Math.Min(ImageExportPlanner.TileSize, raster.Height - sourceY);

        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                int source = ((sourceY + y) * raster.Width + sourceX + x) * 4;
                int target = (y * ImageExportPlanner.TileSize + x) * 4;
                Array.Copy(raster.Rgba, source, tile, target, 4);
            }
        }

        return tile;
    }

    private static void DrawTriangle(
        byte[] rgba,
        int width,
        int height,
        Vector2D offset,
        ImageExportSettings settings,
        Sector sector,
        Vector2D a,
        Vector2D b,
        Vector2D c,
        Func<string, ImageExportTextureData?> getFlat,
        bool brightmap)
    {
        (double ax, double ay) = ToPixel(a, offset, settings.Scale);
        (double bx, double by) = ToPixel(b, offset, settings.Scale);
        (double cx, double cy) = ToPixel(c, offset, settings.Scale);
        int minX = Math.Clamp((int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))), 0, width - 1);
        int maxX = Math.Clamp((int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))), 0, width - 1);
        int minY = Math.Clamp((int)Math.Floor(Math.Min(ay, Math.Min(by, cy))), 0, height - 1);
        int maxY = Math.Clamp((int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))), 0, height - 1);
        double area = Edge(ax, ay, bx, by, cx, cy);
        if (area == 0) return;

        string textureName = settings.Floor ? sector.FloorTexture : sector.CeilTexture;
        ImageExportTextureData? texture = brightmap ? null : getFlat(textureName);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double px = x + 0.5;
                double py = y + 0.5;
                double w0 = Edge(bx, by, cx, cy, px, py);
                double w1 = Edge(cx, cy, ax, ay, px, py);
                double w2 = Edge(ax, ay, bx, by, px, py);
                bool inside = area > 0
                    ? w0 >= 0 && w1 >= 0 && w2 >= 0
                    : w0 <= 0 && w1 <= 0 && w2 <= 0;
                if (!inside) continue;

                double mapX = offset.x + px / settings.Scale;
                double mapY = offset.y - py / settings.Scale;
                WritePixel(rgba, width, x, y, sector, texture, mapX, mapY, settings, brightmap);
            }
        }
    }

    private static void WritePixel(
        byte[] rgba,
        int width,
        int x,
        int y,
        Sector sector,
        ImageExportTextureData? texture,
        double mapX,
        double mapY,
        ImageExportSettings settings,
        bool brightmap)
    {
        int target = (y * width + x) * 4;
        byte r;
        byte g;
        byte b;

        if (brightmap)
        {
            r = g = b = (byte)Math.Clamp(sector.Brightness, 0, 255);
        }
        else if (texture is not null && texture.Width > 0 && texture.Height > 0 && texture.Rgba.Length >= texture.Width * texture.Height * 4)
        {
            (r, g, b) = SampleTexture(texture, mapX, mapY, settings.Floor);
            if (!settings.Fullbright)
            {
                double factor = Math.Clamp(sector.Brightness, 0, 255) / 255.0;
                r = (byte)Math.Round(r * factor);
                g = (byte)Math.Round(g * factor);
                b = (byte)Math.Round(b * factor);
            }
        }
        else
        {
            (r, g, b) = FallbackColor(settings.Floor ? sector.FloorTexture : sector.CeilTexture);
        }

        rgba[target] = r;
        rgba[target + 1] = g;
        rgba[target + 2] = b;
        rgba[target + 3] = 255;
    }

    private static (byte R, byte G, byte B) SampleTexture(ImageExportTextureData texture, double mapX, double mapY, bool floor)
    {
        double scale = texture.Scale == 0 ? 1.0 : texture.Scale;
        int tx = PositiveModulo((int)Math.Floor(mapX / scale), texture.Width);
        int ty = PositiveModulo((int)Math.Floor((floor ? -mapY : mapY) / scale), texture.Height);
        int source = (ty * texture.Width + tx) * 4;
        return (texture.Rgba[source], texture.Rgba[source + 1], texture.Rgba[source + 2]);
    }

    private static (byte R, byte G, byte B) FallbackColor(string textureName)
    {
        int hash = StableHash(textureName);
        return (
            (byte)(64 + (hash & 0x7f)),
            (byte)(64 + ((hash >> 8) & 0x7f)),
            (byte)(64 + ((hash >> 16) & 0x7f)));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char ch in value)
                hash = hash * 31 + char.ToUpperInvariant(ch);
            return hash;
        }
    }

    private static (double X, double Y) ToPixel(Vector2D point, Vector2D offset, double scale)
        => ((point.x - offset.x) * scale, (offset.y - point.y) * scale);

    private static double Edge(double ax, double ay, double bx, double by, double cx, double cy)
        => (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
