// ABOUTME: Plans UDB-style legacy text font glyph sizing and vertex emission.
// ABOUTME: Keeps TextFont character metrics testable before live font atlas rendering is wired.

using System.Text;

namespace DBuilder.Rendering;

public readonly record struct TextFontGlyph(float Width, float Height, float U1, float V1, float U2, float V2);

public sealed record TextFontGlyphSource(int Width, int Height, float U1, float V1, float U2, float V2);

public sealed record TextFontGlyphVertexPlan(FlatVertex[] Vertices, float NextTextX);

public static class TextFontPlan
{
    public const string FontResource = "Font.cfg";
    public const string FONT_RESOURCE = FontResource;
    public const float AdjustSpacing = -0.08f;
    public const float ADJUST_SPACING = AdjustSpacing;
    public const float VScale = 0.5f;
    public const float WidthNormalization = 40.0f;
    public const float HeightNormalization = 30.0f;

    public static TextFontGlyph BuildGlyph(TextFontGlyphSource source)
        => new(
            source.Width / WidthNormalization,
            source.Height / HeightNormalization,
            source.U1,
            source.V1,
            source.U2,
            source.V2);

    public static TextFontGlyph[] BuildGlyphTable(IReadOnlyDictionary<int, TextFontGlyphSource> configuredGlyphs)
    {
        var glyphs = new TextFontGlyph[256];
        foreach ((int index, TextFontGlyphSource source) in configuredGlyphs)
        {
            if (index < 0 || index > 255) continue;

            glyphs[index] = BuildGlyph(source);
        }

        return glyphs;
    }

    public static bool Contains(TextFontGlyph glyph)
        => glyph.Width > 0.000000001f || glyph.Height > 0.000000001f;

    public static bool Contains(IReadOnlyList<TextFontGlyph> glyphs, char c)
    {
        byte[] keyBytes = Encoding.ASCII.GetBytes(c.ToString());
        return Contains(glyphs, keyBytes[0]);
    }

    public static bool Contains(IReadOnlyList<TextFontGlyph> glyphs, byte b)
        => b < glyphs.Count && Contains(glyphs[b]);

    public static TextLabelSize GetTextSize(string text, float scale, IReadOnlyList<TextFontGlyph> glyphs)
    {
        float sizeX = 0.0f;
        float sizeY = 0.0f;

        foreach (byte b in Encoding.ASCII.GetBytes(text))
        {
            TextFontGlyph glyph = b < glyphs.Count ? glyphs[b] : new TextFontGlyph(0, 0, 0, 0, 0, 0);
            sizeX += glyph.Width * scale;
            sizeY = glyph.Height * scale;
        }

        return new TextLabelSize(sizeX, sizeY);
    }

    public static TextLabelSize GetTextSize(string text, float scale, IReadOnlyDictionary<byte, TextFontGlyph> glyphs)
    {
        float sizeX = 0.0f;
        float sizeY = 0.0f;

        foreach (byte b in Encoding.ASCII.GetBytes(text))
        {
            TextFontGlyph glyph = GetGlyph(glyphs, b);
            sizeX += glyph.Width * scale;
            sizeY = glyph.Height * scale;
        }

        return new TextLabelSize(sizeX, sizeY);
    }

    public static TextFontGlyphVertexPlan SetupVertices(
        TextFontGlyph glyph,
        float scale,
        int color,
        float textX,
        float textY,
        float textHeight,
        float offsetV)
    {
        float charWidth = glyph.Width * scale;
        float v1 = glyph.V1 * VScale + offsetV;
        float v2 = glyph.V2 * VScale + offsetV;
        var vertices = new[]
        {
            Vertex(textX, textY, color, glyph.U1, v1),
            Vertex(textX, textY + textHeight, color, glyph.U1, v2),
            Vertex(textX + charWidth, textY, color, glyph.U2, v1),
            Vertex(textX, textY + textHeight, color, glyph.U1, v2),
            Vertex(textX + charWidth, textY, color, glyph.U2, v1),
            Vertex(textX + charWidth, textY + textHeight, color, glyph.U2, v2),
        };

        return new TextFontGlyphVertexPlan(vertices, textX + charWidth + AdjustSpacing * scale);
    }

    private static TextFontGlyph GetGlyph(IReadOnlyDictionary<byte, TextFontGlyph> glyphs, byte key)
        => glyphs.TryGetValue(key, out TextFontGlyph glyph)
            ? glyph
            : new TextFontGlyph(0, 0, 0, 0, 0, 0);

    private static FlatVertex Vertex(float x, float y, int color, float u, float v)
        => new()
        {
            x = x,
            y = y,
            c = color,
            u = u,
            v = v,
        };
}
