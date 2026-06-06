// ABOUTME: Plans UDB-style legacy text font glyph sizing and vertex emission.
// ABOUTME: Keeps TextFont character metrics testable before live font atlas rendering is wired.

using System.Text;

namespace DBuilder.Rendering;

public sealed record TextFontGlyph(float Width, float Height, float U1, float V1, float U2, float V2);

public sealed record TextFontGlyphVertexPlan(FlatVertex[] Vertices, float NextTextX);

public static class TextFontPlan
{
    public const float AdjustSpacing = -0.08f;
    public const float VScale = 0.5f;

    public static bool Contains(TextFontGlyph glyph)
        => glyph.Width > 0.000000001f || glyph.Height > 0.000000001f;

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
        => glyphs.TryGetValue(key, out TextFontGlyph? glyph)
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
