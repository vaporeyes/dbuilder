// ABOUTME: Tests for TexturesParser - parsing the ZDoom TEXTURES lump into composite definitions.
// ABOUTME: Covers type/name/size, offsets/scale, patches with flip flags, comments, and the optional keyword.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class TexturesParserTests
{
    [Fact]
    public void ParsesTextureWithPatchesAndFlags()
    {
        const string text = @"
// a composite wall texture
WallTexture COMPO, 4, 2
{
    XScale 2.0
    Offset 1, 3
    Patch ""PAT"", 0, 0
    Patch PAT, 2, 0
    {
        FlipX
        Rotate 90
    }
}";
        var defs = TexturesParser.Parse(text);
        Assert.Single(defs);
        var d = defs[0];
        Assert.Equal(TexturesType.WallTexture, d.Type);
        Assert.Equal("COMPO", d.Name);
        Assert.Equal(4, d.Width);
        Assert.Equal(2, d.Height);
        Assert.Equal(1, d.OffsetX);
        Assert.Equal(3, d.OffsetY);
        Assert.Equal(2.0, d.ScaleX, 6);
        Assert.Equal(2, d.Patches.Count);

        Assert.Equal("PAT", d.Patches[0].Name);
        Assert.Equal(0, d.Patches[0].X);
        Assert.False(d.Patches[0].FlipX);

        Assert.Equal(2, d.Patches[1].X);
        Assert.True(d.Patches[1].FlipX);
        Assert.False(d.Patches[1].FlipY);
    }

    [Fact]
    public void ParsesMultipleDefinitionsAndOptionalKeyword()
    {
        const string text = @"
Texture A, 8, 8 { Patch P1, 0, 0 }
optional Sprite B, 16, 16 { Patch P2, 1, 2 }
Flat C, 64, 64 { Patch P3, 0, 0 }";
        var defs = TexturesParser.Parse(text);
        Assert.Equal(3, defs.Count);
        Assert.Equal(TexturesType.Texture, defs[0].Type);
        Assert.Equal(TexturesType.Sprite, defs[1].Type);
        Assert.Equal("B", defs[1].Name);
        Assert.Equal(TexturesType.Flat, defs[2].Type);
        Assert.Equal(64, defs[2].Width);
    }

    [Fact]
    public void SkipsUnknownTopLevelTokens()
    {
        const string text = "garbage 1 2 3\nTexture OK, 2, 2 { Patch P, 0, 0 }";
        var defs = TexturesParser.Parse(text);
        Assert.Single(defs);
        Assert.Equal("OK", defs[0].Name);
    }

    [Fact]
    public void HandlesNoBodyDefinition()
    {
        var defs = TexturesParser.Parse("Graphic G, 10, 12");
        Assert.Single(defs);
        Assert.Equal(10, defs[0].Width);
        Assert.Empty(defs[0].Patches);
    }
}
