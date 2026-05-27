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
Flat C, 64, 64 { Patch P3, 0, 0 }
Texture optional D, 4, 4 { Patch P4, 0, 0 }";
        var defs = TexturesParser.Parse(text);
        Assert.Equal(4, defs.Count);
        Assert.Equal(TexturesType.Texture, defs[0].Type);
        Assert.Equal(TexturesType.Sprite, defs[1].Type);
        Assert.Equal("B", defs[1].Name);
        Assert.True(defs[1].Optional);
        Assert.Equal(TexturesType.Flat, defs[2].Type);
        Assert.Equal(64, defs[2].Width);
        Assert.True(defs[3].Optional);
        Assert.Equal("D", defs[3].Name);
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

    [Fact]
    public void ParsesTextureAndPatchMetadata()
    {
        const string text = @"
Texture META, 8, 8
{
    WorldPanning
    NullTexture
    Patch P, 0, 0
    {
        Alpha 1.5
        Rotate -90
        Style Add
        Blend 255, 128, 0, 0.5
    }
    Patch TNT1A0, 0, 0
    Patch ""patches/BLUE"", 1, 1
    {
        Blend ""#112233""
    }
}";

        var def = TexturesParser.Parse(text).Single();

        Assert.True(def.WorldPanning);
        Assert.True(def.NullTexture);
        Assert.Equal(1.0, def.Patches[0].Alpha);
        Assert.Equal(270, def.Patches[0].Rotation);
        Assert.Equal("Add", def.Patches[0].Style);
        Assert.Equal(TexturesPatchRenderStyle.Add, def.Patches[0].RenderStyle);
        Assert.Equal(TexturesPatchBlendStyle.Tint, def.Patches[0].BlendStyle);
        Assert.Equal(255, def.Patches[0].BlendRed);
        Assert.Equal(128, def.Patches[0].BlendGreen);
        Assert.Equal(0, def.Patches[0].BlendBlue);
        Assert.Equal(127, def.Patches[0].BlendAlpha);
        Assert.True(def.Patches[1].Skip);
        Assert.Equal(Path.Combine("patches", "BLUE"), def.Patches[2].Name);
        Assert.Equal(TexturesPatchBlendStyle.Blend, def.Patches[2].BlendStyle);
        Assert.Equal(0x11, def.Patches[2].BlendRed);
        Assert.Equal(0x22, def.Patches[2].BlendGreen);
        Assert.Equal(0x33, def.Patches[2].BlendBlue);
        Assert.Equal(255, def.Patches[2].BlendAlpha);
    }
}
