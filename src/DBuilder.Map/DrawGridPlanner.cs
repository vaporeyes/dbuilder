// ABOUTME: Plans UDB BuilderModes draw-grid geometry from two picked points.
// ABOUTME: Produces rectangle, subdivision, line-segment, and triangulation shapes without editor UI.

namespace DBuilder.Map;

using DBuilder.Geometry;

public enum DrawGridLockMode
{
    None,
    Horizontal,
    Vertical,
    Both,
}

public sealed class DrawGridPlanOptions
{
    public int HorizontalSlices { get; init; } = 3;
    public int VerticalSlices { get; init; } = 3;
    public bool Triangulate { get; init; }
    public bool RelativeInterpolation { get; init; } = true;
    public DrawGridLockMode GridLockMode { get; init; }
    public InterpolationTools.Mode HorizontalInterpolation { get; init; } = InterpolationTools.Mode.LINEAR;
    public InterpolationTools.Mode VerticalInterpolation { get; init; } = InterpolationTools.Mode.LINEAR;
    public double GridSize { get; init; } = 32.0;
    public double GridSizeF { get; init; } = 32.0;
}

public sealed record DrawGridPlan(IReadOnlyList<IReadOnlyList<Vector2D>> Shapes, int HorizontalSlices, int VerticalSlices);

public static class DrawGridPlanner
{
    public static DrawGridPlan Create(Vector2D first, Vector2D second, DrawGridPlanOptions? options = null)
    {
        options ??= new DrawGridPlanOptions();
        Vector2D start;
        Vector2D end;

        if (options.RelativeInterpolation)
        {
            start = first;
            end = second;
        }
        else
        {
            start = new Vector2D(Math.Min(first.x, second.x), Math.Min(first.y, second.y));
            end = new Vector2D(Math.Max(first.x, second.x), Math.Max(first.y, second.y));
        }

        int width = (int)(end.x - start.x);
        int height = (int)(end.y - start.y);
        int slicesH = Math.Max(1, options.HorizontalSlices);
        int slicesV = Math.Max(1, options.VerticalSlices);
        double gridSize = options.GridSize <= 0.0 || double.IsNaN(options.GridSize) ? 1.0 : options.GridSize;

        switch (options.GridLockMode)
        {
            case DrawGridLockMode.Horizontal:
                slicesH = (int)Math.Ceiling(Math.Abs(width) / gridSize);
                break;
            case DrawGridLockMode.Vertical:
                slicesV = (int)Math.Ceiling(Math.Abs(height) / gridSize);
                break;
            case DrawGridLockMode.Both:
                slicesH = (int)Math.Ceiling(Math.Abs(width) / gridSize);
                slicesV = (int)Math.Ceiling(Math.Abs(height) / gridSize);
                break;
        }

        var shapes = Shapes(start, end, width, height, Math.Max(1, slicesH), Math.Max(1, slicesV), options);
        return new DrawGridPlan(shapes, Math.Max(1, slicesH), Math.Max(1, slicesV));
    }

    private static List<IReadOnlyList<Vector2D>> Shapes(
        Vector2D start,
        Vector2D end,
        int width,
        int height,
        int slicesH,
        int slicesV,
        DrawGridPlanOptions options)
    {
        if (start == end) return new List<IReadOnlyList<Vector2D>>();

        if (width == 0 || height == 0)
        {
            if (slicesH > 0 && width > 0)
            {
                int step = width / slicesH;
                return Enumerable.Range(0, slicesH)
                    .Select(w => (IReadOnlyList<Vector2D>)new[]
                    {
                        new Vector2D(start.x + step * w, start.y),
                        new Vector2D(start.x + step * w + step, start.y),
                    })
                    .ToList();
            }

            if (slicesV > 0 && height > 0)
            {
                int step = height / slicesV;
                return Enumerable.Range(0, slicesV)
                    .Select(h => (IReadOnlyList<Vector2D>)new[]
                    {
                        new Vector2D(start.x, start.y + step * h),
                        new Vector2D(start.x, start.y + step * h + step),
                    })
                    .ToList();
            }

            return new List<IReadOnlyList<Vector2D>> { new[] { start, end } };
        }

        var rect = new List<Vector2D>
        {
            start,
            new(start.x, end.y),
            end,
            new(end.x, start.y),
            start,
        };

        if (slicesH == 1 && slicesV == 1)
        {
            if (options.Triangulate) rect.AddRange(new[] { start, end });
            return new List<IReadOnlyList<Vector2D>> { rect };
        }

        var shapes = new List<IReadOnlyList<Vector2D>> { rect };
        var blocks = new GridBlock[slicesH, slicesV];
        for (int w = 0; w < slicesH; w++)
        {
            for (int h = 0; h < slicesV; h++)
            {
                double left = InterpolationTools.Interpolate(start.x, end.x, (double)w / slicesH, options.HorizontalInterpolation);
                double top = InterpolationTools.Interpolate(start.y, end.y, (double)h / slicesV, options.VerticalInterpolation);
                double right = InterpolationTools.Interpolate(start.x, end.x, (w + 1.0) / slicesH, options.HorizontalInterpolation);
                double bottom = InterpolationTools.Interpolate(start.y, end.y, (h + 1.0) / slicesV, options.VerticalInterpolation);
                blocks[w, h] = new GridBlock(left, top, right, bottom);
            }
        }

        for (int w = 1; w < slicesH; w++)
        {
            double x = blocks[w, 0].Left;
            shapes.Add(new[] { new Vector2D(x, start.y), new Vector2D(x, end.y) });
        }

        for (int h = 1; h < slicesV; h++)
        {
            double y = blocks[0, h].Top;
            shapes.Add(new[] { new Vector2D(start.x, y), new Vector2D(end.x, y) });
        }

        if (options.Triangulate)
            AddTriangulation(shapes, blocks, slicesH, slicesV, start, end, options.GridSizeF);

        return shapes;
    }

    private static void AddTriangulation(
        List<IReadOnlyList<Vector2D>> shapes,
        GridBlock[,] blocks,
        int slicesH,
        int slicesV,
        Vector2D start,
        Vector2D end,
        double gridSizeF)
    {
        double grid = gridSizeF <= 0.0 || double.IsNaN(gridSizeF) ? 1.0 : gridSizeF;
        bool startFlip = (int)Math.Round(((start.x + end.y) / grid) % 2.0) == 0;
        bool flip = startFlip;

        for (int w = 0; w < slicesH; w++)
        {
            for (int h = slicesV - 1; h > -1; h--)
            {
                GridBlock block = blocks[w, h];
                shapes.Add(flip
                    ? new[] { new Vector2D(block.Left, block.Top), new Vector2D(block.Right, block.Bottom) }
                    : new[] { new Vector2D(block.Right, block.Top), new Vector2D(block.Left, block.Bottom) });

                flip = !flip;
            }

            startFlip = !startFlip;
            flip = startFlip;
        }
    }

    private readonly record struct GridBlock(double Left, double Top, double Right, double Bottom);
}
