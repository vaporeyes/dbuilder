// ABOUTME: Plans UDB-style text label texture size, screen rectangle, and quad vertices.
// ABOUTME: Keeps label layout behavior testable before live font texture rendering is complete.

namespace DBuilder.Rendering;

public enum TextLabelAlignmentX
{
    Left,
    Center,
    Right,
}

public enum TextLabelAlignmentY
{
    Top,
    Middle,
    Bottom,
}

public readonly record struct TextLabelPoint(double X, double Y);

public readonly record struct TextLabelSize(double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct TextLabelRectangle(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed record TextLabelLayout(
    TextLabelSize TextSize,
    TextLabelSize TextureSize,
    TextLabelRectangle TextRectangle,
    TextLabelRectangle BackgroundRectangle,
    TextLabelRectangle ScreenRectangle,
    bool SkipRendering,
    FlatVertex[] Vertices);

public static class TextLabelPlan
{
    public const double TextOriginX = 4.0;
    public const double TextOriginY = 3.0;

    public static TextLabelLayout Build(
        string? text,
        TextLabelSize measuredText,
        TextLabelPoint location,
        TextLabelAlignmentX alignX = TextLabelAlignmentX.Center,
        TextLabelAlignmentY alignY = TextLabelAlignmentY.Top,
        bool transformCoordinates = false,
        double translateX = 0.0,
        double translateY = 0.0,
        double scaleX = 1.0,
        double scaleY = 1.0,
        double viewportWidth = double.PositiveInfinity,
        double viewportHeight = double.PositiveInfinity,
        PixelColor? color = null)
    {
        if (string.IsNullOrEmpty(text) || measuredText.IsEmpty)
        {
            return new TextLabelLayout(
                new TextLabelSize(0, 0),
                new TextLabelSize(0, 0),
                new TextLabelRectangle(0, 0, 0, 0),
                new TextLabelRectangle(0, 0, 0, 0),
                new TextLabelRectangle(0, 0, 0, 0),
                SkipRendering: true,
                Array.Empty<FlatVertex>());
        }

        double roundedTextWidth = Math.Round(measuredText.Width);
        double roundedTextHeight = Math.Round(measuredText.Height);
        var textSize = new TextLabelSize(
            roundedTextWidth + TextOriginX * 2.0,
            roundedTextHeight + TextOriginY * 2.0);
        var textureSize = new TextLabelSize(
            NextPowerOfTwo((int)textSize.Width),
            NextPowerOfTwo((int)textSize.Height));

        double backgroundX = alignX switch
        {
            TextLabelAlignmentX.Center => (textureSize.Width - textSize.Width) * 0.5,
            TextLabelAlignmentX.Right => textureSize.Width - textSize.Width,
            _ => 0.0,
        };
        double backgroundY = alignY switch
        {
            TextLabelAlignmentY.Middle => (textureSize.Height - textSize.Height) * 0.5,
            TextLabelAlignmentY.Bottom => textureSize.Height - textSize.Height,
            _ => 0.0,
        };

        var backgroundRectangle = new TextLabelRectangle(backgroundX, backgroundY, textSize.Width, textSize.Height);
        var textRectangle = new TextLabelRectangle(
            TextOriginX + backgroundX,
            TextOriginY + backgroundY,
            roundedTextWidth,
            roundedTextHeight);

        TextLabelPoint absoluteLocation = transformCoordinates
            ? new TextLabelPoint((location.X + translateX) * scaleX, (location.Y + translateY) * scaleY)
            : location;

        double beginX = alignX switch
        {
            TextLabelAlignmentX.Center => absoluteLocation.X - textureSize.Width * 0.5,
            TextLabelAlignmentX.Right => absoluteLocation.X - textureSize.Width,
            _ => absoluteLocation.X,
        };
        double beginY = alignY switch
        {
            TextLabelAlignmentY.Middle => absoluteLocation.Y - textureSize.Height * 0.5,
            TextLabelAlignmentY.Bottom => absoluteLocation.Y - textureSize.Height,
            _ => absoluteLocation.Y,
        };

        var screenRectangle = new TextLabelRectangle(beginX, beginY, textureSize.Width, textureSize.Height);
        bool skipRendering = screenRectangle.Right < 0.1
            || screenRectangle.Left > viewportWidth
            || screenRectangle.Bottom < 0.1
            || screenRectangle.Top > viewportHeight;

        return new TextLabelLayout(
            textSize,
            textureSize,
            textRectangle,
            backgroundRectangle,
            screenRectangle,
            skipRendering,
            skipRendering ? Array.Empty<FlatVertex>() : BuildVertices(screenRectangle, color ?? new PixelColor(255, 255, 255, 255)));
    }

    public static int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;

        int result = 1;
        while (result < value) result <<= 1;
        return result;
    }

    private static FlatVertex[] BuildVertices(TextLabelRectangle rectangle, PixelColor color)
    {
        int argb = color.ToArgb();
        return new[]
        {
            Vertex(rectangle.Left, rectangle.Top, argb, 0.0f, 0.0f),
            Vertex(rectangle.Right, rectangle.Top, argb, 1.0f, 0.0f),
            Vertex(rectangle.Left, rectangle.Bottom, argb, 0.0f, 1.0f),
            Vertex(rectangle.Right, rectangle.Bottom, argb, 1.0f, 1.0f),
        };
    }

    private static FlatVertex Vertex(double x, double y, int color, float u, float v)
        => new()
        {
            x = (float)x,
            y = (float)y,
            z = 0.0f,
            w = 1.0f,
            c = color,
            u = u,
            v = v,
        };
}
