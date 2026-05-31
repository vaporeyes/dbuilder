// ABOUTME: UDB-compatible persisted grid setup and snap math.
// ABOUTME: Stores grid size, transform and background metadata without depending on editor UI.

using DBuilder.Geometry;

namespace DBuilder.IO;

public sealed class GridSetup
{
    public const double DefaultGridSize = 32.0;
    public const double MinimumGridSize = 1.0;
    public const double MinimumUdmfGridSize = 0.125;
    public const double MaximumGridSize = 1024.0;
    public const int SourceTextures = 0;
    public const int SourceFlats = 1;
    public const int SourceFile = 2;

    private readonly double minimumGridSize;
    private double gridSizeF;
    private double gridSizeFInv;

    public int GridSize { get; private set; }
    public double GridSizeF => gridSizeF;
    public double GridRotate { get; private set; }
    public double GridOriginX { get; private set; }
    public double GridOriginY { get; private set; }
    public string BackgroundName { get; private set; } = "";
    public int BackgroundSource { get; private set; }
    public int BackgroundX { get; private set; }
    public int BackgroundY { get; private set; }
    public double BackgroundScaleX { get; private set; } = 1.0;
    public double BackgroundScaleY { get; private set; } = 1.0;

    public GridSetup(bool udmf = false)
    {
        minimumGridSize = udmf ? MinimumUdmfGridSize : MinimumGridSize;
        SetGridSize(DefaultGridSize);
    }

    public void WriteToConfig(Configuration configuration, string path)
    {
        configuration.WriteSetting(path + ".background", BackgroundName);
        configuration.WriteSetting(path + ".backsource", BackgroundSource);
        configuration.WriteSetting(path + ".backoffsetx", BackgroundX);
        configuration.WriteSetting(path + ".backoffsety", BackgroundY);
        configuration.WriteSetting(path + ".backscalex", (int)(BackgroundScaleX * 100.0));
        configuration.WriteSetting(path + ".backscaley", (int)(BackgroundScaleY * 100.0));
        configuration.WriteSetting(path + ".gridsize", GridSizeF);
        configuration.WriteSetting(path + ".gridrotate", GridRotate);
        configuration.WriteSetting(path + ".gridoriginx", GridOriginX);
        configuration.WriteSetting(path + ".gridoriginy", GridOriginY);
    }

    public void ReadFromConfig(Configuration configuration, string path)
    {
        BackgroundName = configuration.ReadSetting(path + ".background", "") ?? "";
        BackgroundSource = configuration.ReadSetting(path + ".backsource", 0);
        BackgroundX = configuration.ReadSetting(path + ".backoffsetx", 0);
        BackgroundY = configuration.ReadSetting(path + ".backoffsety", 0);
        BackgroundScaleX = configuration.ReadSetting(path + ".backscalex", 100) / 100.0;
        BackgroundScaleY = configuration.ReadSetting(path + ".backscaley", 100) / 100.0;
        GridOriginX = configuration.ReadSetting(path + ".gridoriginx", 0.0);
        GridOriginY = configuration.ReadSetting(path + ".gridoriginy", 0.0);
        GridRotate = configuration.ReadSetting(path + ".gridrotate", 0.0);
        SetGridSize(configuration.ReadSetting(path + ".gridsize", DefaultGridSize));
    }

    public void SetGridSize(double size)
    {
        if (!double.IsFinite(size)) size = MaximumGridSize;
        gridSizeF = Math.Max(size, minimumGridSize);
        double roundedSize = Math.Round(gridSizeF);
        GridSize = roundedSize >= int.MaxValue ? int.MaxValue : (int)Math.Max(1, roundedSize);
        gridSizeFInv = 1.0 / gridSizeF;
    }

    public bool TryStepGridSize(bool larger)
    {
        if (!double.IsFinite(gridSizeF))
        {
            SetGridSize(MaximumGridSize);
            return true;
        }

        double nextSize;
        if (larger)
        {
            if (gridSizeF > MaximumGridSize * 0.5) return false;
            nextSize = gridSizeF * 2.0;
        }
        else
        {
            if (gridSizeF < minimumGridSize * 2.0) return false;
            nextSize = gridSizeF * 0.5;
        }

        SetGridSize(nextSize);
        return true;
    }

    public void SetGridRotation(double angle)
    {
        GridRotate = angle;
    }

    public void SetGridOrigin(double x, double y)
    {
        GridOriginX = x;
        GridOriginY = y;
    }

    public void SetBackground(string? name, int source)
    {
        BackgroundName = name ?? "";
        BackgroundSource = source;
    }

    public void SetBackgroundView(int offsetX, int offsetY, double scaleX, double scaleY)
    {
        BackgroundX = offsetX;
        BackgroundY = offsetY;
        BackgroundScaleX = scaleX;
        BackgroundScaleY = scaleY;
    }

    public double GetHigher(double offset)
        => Math.Round((offset + (gridSizeF * 0.5)) * gridSizeFInv) * gridSizeF;

    public double GetLower(double offset)
        => Math.Round((offset - (gridSizeF * 0.5)) * gridSizeFInv) * gridSizeF;

    public Vector2D SnappedToGrid(Vector2D point)
        => SnappedToGrid(point, gridSizeF, gridSizeFInv, GridRotate, GridOriginX, GridOriginY);

    public static Vector2D SnappedToGrid(
        Vector2D point,
        double gridSize,
        double gridSizeInv,
        double gridRotate = 0.0,
        double gridOriginX = 0.0,
        double gridOriginY = 0.0,
        double leftBoundary = double.NegativeInfinity,
        double rightBoundary = double.PositiveInfinity,
        double bottomBoundary = double.NegativeInfinity,
        double topBoundary = double.PositiveInfinity)
    {
        var origin = new Vector2D(gridOriginX, gridOriginY);
        bool transformed = Math.Abs(gridRotate) > 1e-4 || gridOriginX != 0.0 || gridOriginY != 0.0;
        if (transformed) point = (point - origin).GetRotated(-gridRotate);

        var snapped = new Vector2D(
            Math.Round(point.x * gridSizeInv) * gridSize,
            Math.Round(point.y * gridSizeInv) * gridSize);

        if (transformed) snapped = snapped.GetRotated(gridRotate) + origin;

        if (snapped.x < leftBoundary) snapped.x = leftBoundary;
        else if (snapped.x > rightBoundary) snapped.x = rightBoundary;

        if (snapped.y > topBoundary) snapped.y = topBoundary;
        else if (snapped.y < bottomBoundary) snapped.y = bottomBoundary;

        return snapped;
    }
}
