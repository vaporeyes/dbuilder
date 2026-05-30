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
    YScale 0.5
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
        Assert.Equal(0.5, d.ScaleX, 6);
        Assert.Equal(2.0, d.ScaleY, 6);
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
optional Sprite SPRTA0, 16, 16 { Patch P2, 1, 2 }
Flat C, 64, 64 { Patch P3, 0, 0 }
Texture optional D, 4, 4 { Patch P4, 0, 0 }";
        var defs = TexturesParser.Parse(text);
        Assert.Equal(4, defs.Count);
        Assert.Equal(TexturesType.Texture, defs[0].Type);
        Assert.Equal(TexturesType.Sprite, defs[1].Type);
        Assert.Equal("SPRTA0", defs[1].Name);
        Assert.True(defs[1].Optional);
        Assert.Equal(TexturesType.Flat, defs[2].Type);
        Assert.Equal(64, defs[2].Width);
        Assert.True(defs[3].Optional);
        Assert.Equal("D", defs[3].Name);
    }

    [Fact]
    public void SkipsSpriteDefinitionsWithInvalidNameLength()
    {
        const string text = @"
Sprite BAD, 16, 16 { Patch P1, 0, 0 }
Sprite SPRTA0, 16, 16 { Patch P2, 0, 0 }
Sprite SPRTA0B0, 16, 16 { Patch P3, 0, 0 }";

        var defs = TexturesParser.Parse(text);

        Assert.Equal(2, defs.Count);
        Assert.DoesNotContain(defs, d => d.Name == "BAD");
        Assert.Contains(defs, d => d.Name == "SPRTA0");
        Assert.Contains(defs, d => d.Name == "SPRTA0B0");
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
    public void SkipsUnknownTopLevelBlocks()
    {
        const string text = @"
UnknownBlock
{
    Texture BAD, 2, 2 { Patch P, 0, 0 }
}
Texture OK, 2, 2 { Patch P, 0, 0 }";

        var def = Assert.Single(TexturesParser.Parse(text));

        Assert.Equal("OK", def.Name);
    }

    [Fact]
    public void SkipsUnknownTopLevelBlocksWithArguments()
    {
        const string text = @"
UnknownBlock SomeArg
{
    Texture BAD, 2, 2 { Patch P, 0, 0 }
}
Texture OK, 2, 2 { Patch P, 0, 0 }";

        var def = Assert.Single(TexturesParser.Parse(text));

        Assert.Equal("OK", def.Name);
    }

    [Fact]
    public void StopsAtGzdbSkipDirective()
    {
        const string text = @"
Texture Before, 2, 2 { Patch P, 0, 0 }
$gzdb_skip
Texture After, 2, 2 { Patch P, 0, 0 }";

        var def = Assert.Single(TexturesParser.Parse(text));

        Assert.Equal("Before", def.Name);
    }

    [Fact]
    public void SkipsDefinitionsWithoutBody()
    {
        var defs = TexturesParser.Parse("Graphic G, 10, 12");
        Assert.Empty(defs);
    }

    [Fact]
    public void SkipsDefinitionsWithNonIntegralSize()
    {
        const string text = @"
Texture BAD, 8.5, 8 { Patch P, 0, 0 }
Texture OK, 8, 8 { Patch P, 0, 0 }";

        var def = Assert.Single(TexturesParser.Parse(text));

        Assert.Equal("OK", def.Name);
    }

    [Fact]
    public void RequiresCommasInDefinitionSize()
    {
        const string text = @"
Texture MISSINGFIRST 8, 8 { Patch P, 0, 0 }
Texture MISSINGSECOND, 8 8 { Patch P, 0, 0 }
Texture OK, 8, 8 { Patch P, 0, 0 }";

        var def = Assert.Single(TexturesParser.Parse(text));

        Assert.Equal("OK", def.Name);
    }

    [Fact]
    public void RequiresQuotesForLongTextureNames()
    {
        const string text = @"
Texture LONGTEXTURE, 8, 8 { Patch P, 0, 0 }
Texture ""LONGTEXTURE"", 8, 8 { Patch P, 0, 0 }
Texture SHORT, 8, 8 { Patch P, 0, 0 }";

        var defs = TexturesParser.Parse(text);

        Assert.Equal(2, defs.Count);
        Assert.Equal("LONGTEXTURE", defs[0].Name);
        Assert.Equal("SHORT", defs[1].Name);
    }

    [Fact]
    public void SkipsPatchesWithNonIntegralOffsets()
    {
        const string text = @"
Texture PATCHES, 8, 8
{
    Patch BADX, 0.5, 0
    Patch BADY, 1, 2.5
    Patch OK, 2, 3
}";

        var def = TexturesParser.Parse(text).Single();

        var patch = Assert.Single(def.Patches);
        Assert.Equal("OK", patch.Name);
        Assert.Equal(2, patch.X);
        Assert.Equal(3, patch.Y);
    }

    [Fact]
    public void RequiresCommasInPatchOffsets()
    {
        const string text = @"
Texture PATCHES, 8, 8
{
    Patch BADX 0, 0
    Patch BADY, 1 2
    Patch OK, 2, 3
}";

        var def = TexturesParser.Parse(text).Single();

        var patch = Assert.Single(def.Patches);
        Assert.Equal("OK", patch.Name);
        Assert.Equal(2, patch.X);
        Assert.Equal(3, patch.Y);
    }

    [Fact]
    public void RequiresQuotesForLongPatchNames()
    {
        const string text = @"
Texture PATCHES, 8, 8
{
    Patch LONGPATCH, 0, 0
    Patch ""LONGPATCH"", 1, 2
    Patch SHORT, 3, 4
}";

        var def = TexturesParser.Parse(text).Single();

        Assert.Equal(2, def.Patches.Count);
        Assert.Equal("LONGPATCH", def.Patches[0].Name);
        Assert.Equal("SHORT", def.Patches[1].Name);
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
    Patch ""patches/Blue"", 1, 1
    {
        Blend ""#112233""
    }
    Patch SHORT, 2, 2
    {
        Blend ""#28f""
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
        Assert.Equal(Path.Combine("PATCHES", "BLUE"), def.Patches[2].Name);
        Assert.Equal(TexturesPatchBlendStyle.Blend, def.Patches[2].BlendStyle);
        Assert.Equal(0x11, def.Patches[2].BlendRed);
        Assert.Equal(0x22, def.Patches[2].BlendGreen);
        Assert.Equal(0x33, def.Patches[2].BlendBlue);
        Assert.Equal(255, def.Patches[2].BlendAlpha);
        Assert.Equal(0x22, def.Patches[3].BlendRed);
        Assert.Equal(0x88, def.Patches[3].BlendGreen);
        Assert.Equal(0xff, def.Patches[3].BlendBlue);
    }
}
