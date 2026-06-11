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
    TextLabelSkipReason SkipReason,
    FlatVertex[] Vertices);

public enum TextLabelImageStyle
{
    Plain,
    Background,
}

public enum TextLabelSkipReason
{
    None,
    Empty,
    Offscreen,
}

public sealed record TextLabelImagePlan(
    TextLabelImageStyle Style,
    TextLabelSize TextureSize,
    TextLabelRectangle BackgroundFillRectangle,
    TextLabelRectangle TextDrawRectangle,
    PixelColor BackgroundFillColor,
    PixelColor TextColor,
    PixelColor? BorderColor,
    double CornerRadius);

public sealed record TextLabelRenderCommand(
    int LabelIndex,
    TextLabelSize TextureSize,
    TextLabelRectangle ScreenRectangle,
    int PrimitiveCount);

public sealed record TextLabelRenderPlan(
    IReadOnlyList<TextLabelRenderCommand> Commands,
    int SkippedLabels)
{
    public bool ShouldRender => Commands.Count > 0;
}

public sealed record TextLabelRenderStatePlan(
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    string ShaderName,
    bool WorldTransformation,
    float Alpha,
    float Brightness,
    float TextureOffset,
    float TextureScale,
    bool TextureTransformEnabled);

public readonly record struct TextLabelInvalidation(bool LayoutUpdateNeeded, bool TextureUpdateNeeded)
{
    public static TextLabelInvalidation Initial => new(LayoutUpdateNeeded: true, TextureUpdateNeeded: true);
    public static TextLabelInvalidation Clean => new(LayoutUpdateNeeded: false, TextureUpdateNeeded: false);
}

public readonly record struct TextLabelTransformCache(
    bool HasTransform,
    double TranslateX,
    double TranslateY,
    double ScaleX,
    double ScaleY);

public sealed record TextLabelTransformUpdatePlan(
    TextLabelTransformCache Cache,
    TextLabelInvalidation Invalidation);

public sealed record TextLabelResourceUpdatePlan(
    bool DisposeTexture,
    bool CreateLabelImage,
    bool CreateTexture,
    bool CreateVertexBuffer,
    bool UploadQuadBuffer,
    TextLabelInvalidation ResultInvalidation);

public sealed record TextLabelFontPlan(
    string RequestedFamily,
    string ResolvedFamily,
    double Size,
    bool Bold,
    bool UsedFallback);

public enum TextLabelPropertyChangeKind
{
    None,
    Layout,
    Texture,
    Font,
}

public sealed record TextLabelPropertyChangePlan(
    TextLabelInvalidation Invalidation,
    TextLabelPropertyChangeKind ChangeKind,
    bool DisposeCurrentFont = false);

public static class TextLabelPlan
{
    public const string DefaultTextLabelFontName = "Microsoft Sans Serif";
    public const double DefaultTextLabelFontSize = 10.0;
    public const bool DefaultTextLabelFontBold = false;
    public const double TextOriginX = 4.0;
    public const double TextOriginY = 3.0;
    public const double BackgroundBorderWidth = 1.0;
    public const string Display2DNormalShaderName = "display2d_normal";

    public static TextLabelFontPlan BuildDefaultFontPlan(
        IReadOnlyCollection<string>? availableFamilies = null,
        string fallbackFamily = DefaultTextLabelFontName)
        => BuildFontPlan(
            DefaultTextLabelFontName,
            DefaultTextLabelFontSize,
            DefaultTextLabelFontBold,
            availableFamilies,
            fallbackFamily);

    public static TextLabelFontPlan BuildFontPlan(
        string? requestedFamily,
        double size,
        bool bold,
        IReadOnlyCollection<string>? availableFamilies = null,
        string fallbackFamily = DefaultTextLabelFontName)
    {
        string requested = string.IsNullOrWhiteSpace(requestedFamily)
            ? DefaultTextLabelFontName
            : requestedFamily.Trim();
        string fallback = string.IsNullOrWhiteSpace(fallbackFamily)
            ? DefaultTextLabelFontName
            : fallbackFamily.Trim();
        bool requestedAvailable = availableFamilies == null
            || availableFamilies.Any(family => string.Equals(family, requested, StringComparison.OrdinalIgnoreCase));
        string resolved = requestedAvailable ? requested : fallback;

        return new TextLabelFontPlan(
            requested,
            resolved,
            size,
            bold,
            UsedFallback: !requestedAvailable);
    }

    public static TextLabelFontPlan BuildLegacyScaleFontPlan(
        double scale,
        string? requestedFamily = DefaultTextLabelFontName,
        bool bold = DefaultTextLabelFontBold,
        IReadOnlyCollection<string>? availableFamilies = null,
        string fallbackFamily = DefaultTextLabelFontName)
        => BuildFontPlan(
            requestedFamily,
            Math.Round(scale * 0.75),
            bold,
            availableFamilies,
            fallbackFamily);

    public static PixelColor BuildBackcolorCompatibilityValue(PixelColor value)
        => value.WithAlpha(128);

    public static TextLabelPropertyChangePlan BuildTextChangePlan(
        string? currentText,
        string? nextText,
        TextLabelInvalidation invalidation)
        => string.Equals(currentText, nextText, StringComparison.Ordinal)
            ? new TextLabelPropertyChangePlan(invalidation, TextLabelPropertyChangeKind.None)
            : new TextLabelPropertyChangePlan(InvalidateTexture(invalidation), TextLabelPropertyChangeKind.Texture);

    public static TextLabelPropertyChangePlan BuildLocationChangePlan(
        TextLabelPoint currentLocation,
        TextLabelPoint nextLocation,
        TextLabelInvalidation invalidation)
        => new(InvalidateLayout(invalidation), TextLabelPropertyChangeKind.Layout);

    public static TextLabelPropertyChangePlan BuildRectangleCompatibilityChangePlan(
        TextLabelPoint currentLocation,
        TextLabelRectangle rectangle,
        TextLabelInvalidation invalidation)
        => BuildLocationChangePlan(
            currentLocation,
            new TextLabelPoint(rectangle.X, rectangle.Y),
            invalidation);

    public static TextLabelPropertyChangePlan BuildColorChangePlan(
        PixelColor currentColor,
        PixelColor nextColor,
        TextLabelInvalidation invalidation)
        => currentColor.Equals(nextColor)
            ? new TextLabelPropertyChangePlan(invalidation, TextLabelPropertyChangeKind.None)
            : new TextLabelPropertyChangePlan(InvalidateTexture(invalidation), TextLabelPropertyChangeKind.Texture);

    public static TextLabelPropertyChangePlan BuildBackColorChangePlan(
        PixelColor currentBackColor,
        PixelColor nextBackColor,
        TextLabelInvalidation invalidation)
        => currentBackColor.Equals(nextBackColor)
            ? new TextLabelPropertyChangePlan(invalidation, TextLabelPropertyChangeKind.None)
            : new TextLabelPropertyChangePlan(InvalidateTexture(invalidation), TextLabelPropertyChangeKind.Texture);

    public static TextLabelPropertyChangePlan BuildDrawBackgroundChangePlan(
        bool currentDrawBackground,
        bool nextDrawBackground,
        TextLabelInvalidation invalidation)
        => currentDrawBackground == nextDrawBackground
            ? new TextLabelPropertyChangePlan(invalidation, TextLabelPropertyChangeKind.None)
            : new TextLabelPropertyChangePlan(InvalidateTexture(invalidation), TextLabelPropertyChangeKind.Texture);

    public static TextLabelPropertyChangePlan BuildFontChangePlan(TextLabelInvalidation invalidation)
        => new(InvalidateTexture(invalidation), TextLabelPropertyChangeKind.Font, DisposeCurrentFont: true);

    public static TextLabelPropertyChangePlan BuildTransformCoordinatesChangePlan(TextLabelInvalidation invalidation)
        => new(InvalidateLayout(invalidation), TextLabelPropertyChangeKind.Layout);

    public static TextLabelPropertyChangePlan BuildAlignmentChangePlan(TextLabelInvalidation invalidation)
        => new(InvalidateLayout(invalidation), TextLabelPropertyChangeKind.Layout);

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
                TextLabelSkipReason.Empty,
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
            skipRendering ? TextLabelSkipReason.Offscreen : TextLabelSkipReason.None,
            skipRendering ? Array.Empty<FlatVertex>() : BuildVertices(screenRectangle, color ?? new PixelColor(255, 255, 255, 255)));
    }

    public static TextLabelImagePlan BuildImagePlan(
        string text,
        TextLabelLayout layout,
        bool drawBackground,
        PixelColor color,
        PixelColor backColor)
    {
        if (drawBackground)
        {
            return new TextLabelImagePlan(
                TextLabelImageStyle.Background,
                layout.TextureSize,
                layout.BackgroundRectangle with
                {
                    Width = Math.Max(0.0, layout.BackgroundRectangle.Width - BackgroundBorderWidth),
                    Height = Math.Max(0.0, layout.BackgroundRectangle.Height - BackgroundBorderWidth),
                },
                Inflate(layout.TextRectangle, 4.0, 2.0),
                BackgroundFillColor: color,
                TextColor: backColor,
                BorderColor: backColor,
                CornerRadius: TextOriginX);
        }

        TextLabelRectangle backgroundRectangle = text.Length > 1
            ? Inflate(layout.TextRectangle, 6.0, 2.0)
            : layout.TextRectangle;

        return new TextLabelImagePlan(
            TextLabelImageStyle.Plain,
            layout.TextureSize,
            backgroundRectangle,
            Inflate(layout.TextRectangle, 6.0, 4.0),
            BackgroundFillColor: backColor,
            TextColor: color,
            BorderColor: null,
            CornerRadius: 0.0);
    }

    public static TextLabelRenderPlan BuildRenderPlan(IReadOnlyList<TextLabelLayout> labels)
    {
        var commands = new List<TextLabelRenderCommand>();
        int skipped = 0;

        for (int i = 0; i < labels.Count; i++)
        {
            TextLabelLayout label = labels[i];
            if (label.SkipRendering)
            {
                skipped++;
                continue;
            }

            commands.Add(new TextLabelRenderCommand(
                i,
                label.TextureSize,
                label.ScreenRectangle,
                PrimitiveCount: 2));
        }

        return new TextLabelRenderPlan(commands, skipped);
    }

    public static TextLabelRenderStatePlan BuildRenderStatePlan(TextLabelRenderPlan renderPlan)
        => new(
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: renderPlan.ShouldRender,
            AlphaTestEnabled: false,
            ShaderName: Display2DNormalShaderName,
            WorldTransformation: false,
            Alpha: 1.0f,
            Brightness: 1.0f,
            TextureOffset: 0.0f,
            TextureScale: 1.0f,
            TextureTransformEnabled: false);

    public static TextLabelInvalidation InvalidateLayout(TextLabelInvalidation state)
        => state with { LayoutUpdateNeeded = true };

    public static TextLabelInvalidation InvalidateTexture(TextLabelInvalidation state)
        => state with { TextureUpdateNeeded = true };

    public static TextLabelInvalidation InvalidateResources()
        => TextLabelInvalidation.Initial;

    public static TextLabelInvalidation MarkUpdated()
        => TextLabelInvalidation.Clean;

    public static TextLabelTransformUpdatePlan UpdateTransformCache(
        bool transformCoordinates,
        TextLabelTransformCache cache,
        TextLabelInvalidation invalidation,
        double translateX,
        double translateY,
        double scaleX,
        double scaleY)
    {
        if (!transformCoordinates)
        {
            return new TextLabelTransformUpdatePlan(cache, invalidation);
        }

        if (cache.HasTransform
            && cache.TranslateX == translateX
            && cache.TranslateY == translateY
            && cache.ScaleX == scaleX
            && cache.ScaleY == scaleY)
        {
            return new TextLabelTransformUpdatePlan(cache, invalidation);
        }

        return new TextLabelTransformUpdatePlan(
            new TextLabelTransformCache(true, translateX, translateY, scaleX, scaleY),
            InvalidateLayout(invalidation));
    }

    public static TextLabelResourceUpdatePlan BuildResourceUpdatePlan(
        TextLabelInvalidation invalidation,
        TextLabelLayout layout,
        bool hasTexture,
        bool hasVertexBuffer,
        bool vertexBufferDisposed)
    {
        if (layout.SkipRendering)
        {
            TextLabelInvalidation resultInvalidation = layout.SkipReason == TextLabelSkipReason.Empty
                ? TextLabelInvalidation.Clean
                : invalidation;

            return new TextLabelResourceUpdatePlan(
                DisposeTexture: false,
                CreateLabelImage: false,
                CreateTexture: false,
                CreateVertexBuffer: false,
                UploadQuadBuffer: false,
                resultInvalidation);
        }

        bool updateTexture = invalidation.TextureUpdateNeeded;
        bool createVertexBuffer = !hasVertexBuffer || vertexBufferDisposed;
        return new TextLabelResourceUpdatePlan(
            DisposeTexture: updateTexture && hasTexture,
            CreateLabelImage: updateTexture,
            CreateTexture: updateTexture,
            CreateVertexBuffer: createVertexBuffer,
            UploadQuadBuffer: invalidation.LayoutUpdateNeeded || createVertexBuffer,
            TextLabelInvalidation.Clean);
    }

    public static bool IsInViewport(TextLabelPoint location, TextLabelSize textureSize, TextLabelRectangle viewport)
    {
        double width = textureSize.IsEmpty ? 0.0 : textureSize.Width;
        double height = textureSize.IsEmpty ? 0.0 : textureSize.Height;

        return location.X >= viewport.X - width
            && location.X < viewport.X + viewport.Width + width
            && location.Y <= viewport.Y - height
            && location.Y > viewport.Y + viewport.Height + height;
    }

    public static int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;

        int result = 1;
        while (result < value) result <<= 1;
        return result;
    }

    public static TextLabelRectangle Inflate(TextLabelRectangle rectangle, double width, double height)
        => new(
            rectangle.X - width,
            rectangle.Y - height,
            rectangle.Width + width * 2.0,
            rectangle.Height + height * 2.0);

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
