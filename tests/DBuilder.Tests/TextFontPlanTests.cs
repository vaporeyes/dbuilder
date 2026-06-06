// ABOUTME: Verifies UDB-style legacy text font glyph metric and vertex planning.
// ABOUTME: Covers glyph containment, text sizing, UV offsets, and character advance.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class TextFontPlanTests
{
    [Fact]
    public void BuildGlyphNormalizesFontCfgMetricsLikeUdb()
    {
        TextFontGlyph glyph = TextFontPlan.BuildGlyph(new TextFontGlyphSource(
            Width: 80,
            Height: 45,
            U1: 0.1f,
            V1: 0.2f,
            U2: 0.3f,
            V2: 0.4f));

        Assert.Equal(new TextFontGlyph(2.0f, 1.5f, 0.1f, 0.2f, 0.3f, 0.4f), glyph);
    }

    [Fact]
    public void ContainsMatchesUdbWidthOrHeightThreshold()
    {
        Assert.False(TextFontPlan.Contains(new TextFontGlyph(0, 0, 0, 0, 0, 0)));
        Assert.False(TextFontPlan.Contains(new TextFontGlyph(0.0000000005f, 0, 0, 0, 0, 0)));
        Assert.True(TextFontPlan.Contains(new TextFontGlyph(0.000000002f, 0, 0, 0, 0, 0)));
        Assert.True(TextFontPlan.Contains(new TextFontGlyph(0, 0.000000002f, 0, 0, 0, 0)));
    }

    [Fact]
    public void GetTextSizeSumsWidthsAndUsesLastCharacterHeightLikeUdb()
    {
        var glyphs = new Dictionary<byte, TextFontGlyph>
        {
            [(byte)'A'] = new(1.5f, 2.0f, 0, 0, 0, 0),
            [(byte)'B'] = new(2.5f, 3.0f, 0, 0, 0, 0),
        };

        TextLabelSize size = TextFontPlan.GetTextSize("AB", 4.0f, glyphs);

        Assert.Equal(new TextLabelSize(16.0, 12.0), size);
    }

    [Fact]
    public void GetTextSizeUsesAsciiReplacementForNonAsciiCharacters()
    {
        var glyphs = new Dictionary<byte, TextFontGlyph>
        {
            [(byte)'?'] = new(2.0f, 4.0f, 0, 0, 0, 0),
        };

        TextLabelSize size = TextFontPlan.GetTextSize("é", 3.0f, glyphs);

        Assert.Equal(new TextLabelSize(6.0, 12.0), size);
    }

    [Fact]
    public void SetupVerticesCreatesTwoTrianglesWithHalfHeightVOffset()
    {
        var glyph = new TextFontGlyph(2.0f, 3.0f, 0.1f, 0.2f, 0.4f, 0.8f);

        TextFontGlyphVertexPlan plan = TextFontPlan.SetupVertices(
            glyph,
            scale: 5.0f,
            color: unchecked((int)0xff102030),
            textX: 7.0f,
            textY: 11.0f,
            textHeight: 13.0f,
            offsetV: 0.25f);

        Assert.Equal(7.0f + 10.0f + TextFontPlan.AdjustSpacing * 5.0f, plan.NextTextX);
        Assert.Equal(6, plan.Vertices.Length);
        AssertVertex(plan.Vertices[0], 7.0f, 11.0f, 0.1f, 0.35f);
        AssertVertex(plan.Vertices[1], 7.0f, 24.0f, 0.1f, 0.65f);
        AssertVertex(plan.Vertices[2], 17.0f, 11.0f, 0.4f, 0.35f);
        AssertVertex(plan.Vertices[3], 7.0f, 24.0f, 0.1f, 0.65f);
        AssertVertex(plan.Vertices[4], 17.0f, 11.0f, 0.4f, 0.35f);
        AssertVertex(plan.Vertices[5], 17.0f, 24.0f, 0.4f, 0.65f);
        Assert.All(plan.Vertices, vertex => Assert.Equal(unchecked((int)0xff102030), vertex.c));
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, float u, float v)
    {
        Assert.Equal(x, vertex.x);
        Assert.Equal(y, vertex.y);
        Assert.Equal(u, vertex.u);
        Assert.Equal(v, vertex.v);
    }
}
