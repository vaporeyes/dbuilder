// ABOUTME: Tests for GldefsParser - light definitions, actor light associations, and glow lists.
// ABOUTME: Covers color/size, object frame links, glow flats/textures, and skipping unknown blocks.

using DBuilder.IO;

namespace DBuilder.Tests;

public class GldefsParserTests
{
    [Fact]
    public void ParsesLightAndObjectAssociation()
    {
        const string text = @"
// a torch light
pointlight TORCH
{
    color 1.0 0.7 0.3
    size 96
    offset 0 0 32
}
object ShortRedTorch
{
    frame TRED { light TORCH }
}";
        var g = GldefsParser.Parse(text);
        Assert.True(g.Lights.ContainsKey("TORCH"));
        var l = g.Lights["TORCH"];
        Assert.Equal("pointlight", l.Type);
        Assert.Equal(1.0f, l.R, 4);
        Assert.Equal(0.7f, l.G, 4);
        Assert.Equal(192f, l.Size, 4);
        Assert.Equal(32f, l.OffsetZ, 4);

        Assert.Single(g.Objects);
        Assert.Equal("ShortRedTorch", g.Objects[0].ClassName);
        Assert.Contains("TORCH", g.Objects[0].Lights);

        var color = g.ActorLightColor("ShortRedTorch");
        Assert.NotNull(color);
        Assert.Equal(1.0f, color!.Value.R, 4);
    }

    [Fact]
    public void ClampsLightColorComponents()
    {
        const string text = "pointlight CLAMPED { color 1.5 -0.25 0.5 size 16 }";

        var light = GldefsParser.Parse(text).Lights["CLAMPED"];

        Assert.Equal(1.0f, light.R, 4);
        Assert.Equal(0.0f, light.G, 4);
        Assert.Equal(0.5f, light.B, 4);
    }

    [Fact]
    public void SkipsBlackLights()
    {
        const string text = @"
pointlight BLACK { color 0 0 0 size 16 }
pointlight VISIBLE { color 0.1 0 0 size 16 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BLACK"));
        Assert.True(g.Lights.ContainsKey("VISIBLE"));
    }

    [Fact]
    public void SkipsLightsWithoutVisibleRadius()
    {
        const string text = @"
pointlight NOSIZE { color 0.1 0.1 0.1 }
pulselight NOANIMSIZE { color 0.1 0.1 0.1 }
pulselight SECONDARY { color 0.1 0.1 0.1 secondarysize 8 interval 1 }
sectorlight SECTOR { color 0.1 0.1 0.1 scale 0.5 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("NOSIZE"));
        Assert.False(g.Lights.ContainsKey("NOANIMSIZE"));
        Assert.True(g.Lights.ContainsKey("SECONDARY"));
        Assert.True(g.Lights.ContainsKey("SECTOR"));
    }

    [Fact]
    public void ParsesGlow()
    {
        const string text = @"
glow
{
    flats { NUKAGE1 NUKAGE2 LAVA1 }
    texture GLOWTEX color 0.5 0.5 1.0
    texture GLOWINT color 1 0 0
    texture GLOWHEX, ""#2040ff"", 32, fullbright
}";
        var g = GldefsParser.Parse(text);
        Assert.Contains("NUKAGE1", g.GlowFlats);
        Assert.Contains("LAVA1", g.GlowFlats);
        Assert.Contains("GLOWTEX", g.GlowTextures);
        Assert.Contains("GLOWHEX", g.GlowTextures);
        Assert.True(g.Glows["NUKAGE1"].CalculateTextureColor);
        Assert.Equal(128, g.Glows["NUKAGE1"].Height);
        Assert.Equal(0.5f, g.Glows["GLOWTEX"].R, 4);
        Assert.Equal(1.0f, g.Glows["GLOWINT"].R, 4);
        Assert.Equal(0.0f, g.Glows["GLOWINT"].G, 4);
        Assert.Equal(128, g.Glows["GLOWTEX"].Height);
        Assert.Equal(64, g.Glows["GLOWHEX"].Height);
        Assert.True(g.Glows["GLOWHEX"].Fullbright);
    }

    [Fact]
    public void ParsesZdTextGlowColorStrings()
    {
        const string text = @"
glow
{
    texture GLOWSHORT, ""#28f""
    texture GLOWBARE, ff4000
}";

        var g = GldefsParser.Parse(text);

        Assert.Equal(0x22 / 255.0f, g.Glows["GLOWSHORT"].R, 4);
        Assert.Equal(0x88 / 255.0f, g.Glows["GLOWSHORT"].G, 4);
        Assert.Equal(1.0f, g.Glows["GLOWSHORT"].B, 4);
        Assert.Equal(1.0f, g.Glows["GLOWBARE"].R, 4);
        Assert.Equal(0x40 / 255.0f, g.Glows["GLOWBARE"].G, 4);
        Assert.Equal(0.0f, g.Glows["GLOWBARE"].B, 4);
    }

    [Fact]
    public void ParsesSkyboxesAndSkipsUnknownBlocks()
    {
        const string text = @"
skybox SKY1 fliptop { SKYRIGHT SKYLEFT SKYTOP SKYBOTTOM SKYFRONT SKYBACK }
pulselight LAMP { color 0.2 0.2 1.0 size 64 secondarysize 24 interval 0.5 subtractive 1 dontlightself 1 }
brightmap texture FOO { map FOO_BR }";
        var g = GldefsParser.Parse(text);
        Assert.Single(g.Lights);
        Assert.True(g.Lights.ContainsKey("LAMP"));
        Assert.Equal(128f, g.Lights["LAMP"].Size, 4);
        Assert.Equal(48f, g.Lights["LAMP"].SecondarySize, 4);
        Assert.Equal(17f, g.Lights["LAMP"].Interval, 4);
        Assert.True(g.Lights["LAMP"].Subtractive);
        Assert.True(g.Lights["LAMP"].DontLightSelf);
        Assert.True(g.Skyboxes["SKY1"].FlipTop);
        Assert.Equal(6, g.Skyboxes["SKY1"].Textures.Count);
    }

    [Fact]
    public void ParsesIncludesOnce()
    {
        const string root = @"
#include ""lights/gldefs.txt""
#include ""lights/gldefs.txt""
object LampActor { frame LAMP { light LAMP_LIGHT } }";

        var g = GldefsParser.Parse(root, include => include == "lights/gldefs.txt"
            ? "pointlight LAMP_LIGHT { color 0.1 0.2 0.3 size 32 }"
            : null);

        Assert.Single(g.Lights);
        Assert.True(g.Lights.ContainsKey("LAMP_LIGHT"));
        Assert.Equal(0.2f, g.ActorLightColor("LampActor")!.Value.G, 4);
    }

    [Fact]
    public void ObjectUsesFirstRelevantFrameLight()
    {
        const string text = @"
pointlight FIRST { color 1 0 0 size 16 }
pointlight SECOND { color 0 1 0 size 16 }
object EmptyActor { frame EMPTB { light SECOND } }
object TorchActor
{
    frame TRCHB { light SECOND }
    frame TRCHA { light FIRST }
    frame TRCHB { light SECOND }
}";

        var g = GldefsParser.Parse(text);

        Assert.Single(g.Objects);
        Assert.Single(g.Objects[0].Lights);
        Assert.Equal("FIRST", g.Objects[0].Lights[0]);
        Assert.Equal(1.0f, g.ActorLightColor("TorchActor")!.Value.R, 4);
    }

    [Fact]
    public void NormalizesFlickerChanceAndSectorScale()
    {
        const string text = @"
flickerlight SPARK { color 1.0 1.0 1.0 size 32 secondarysize 8 chance 0.5 }
sectorlight SECTOR { color 0.5 0.5 0.5 scale 0.7 }";

        var g = GldefsParser.Parse(text);

        Assert.Equal(179f, g.Lights["SPARK"].Interval, 4);
        Assert.Equal(16f, g.Lights["SPARK"].SecondarySize, 4);
        Assert.Equal(7f, g.Lights["SECTOR"].Interval, 4);
        Assert.Equal(0.7f, g.Lights["SECTOR"].Scale, 4);
    }

    [Fact]
    public void SkipsLightsWithInvalidRanges()
    {
        const string text = @"
pointlight NEGSIZE { color 1.0 1.0 1.0 size -1 }
pulselight NEGSECONDARY { color 1.0 1.0 1.0 size 8 secondarysize -1 interval 1 }
pulselight ZEROINTERVAL { color 1.0 1.0 1.0 size 8 secondarysize 4 interval 0 }
flickerlight BADCHANCE { color 1.0 1.0 1.0 size 8 secondarysize 4 chance 1.5 }
sectorlight BADSCALE { color 1.0 1.0 1.0 scale -0.1 }
pointlight VALID { color 1.0 1.0 1.0 size 8 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("NEGSIZE"));
        Assert.False(g.Lights.ContainsKey("NEGSECONDARY"));
        Assert.False(g.Lights.ContainsKey("ZEROINTERVAL"));
        Assert.False(g.Lights.ContainsKey("BADCHANCE"));
        Assert.False(g.Lights.ContainsKey("BADSCALE"));
        Assert.True(g.Lights.ContainsKey("VALID"));
    }

    [Fact]
    public void StopsAtGzdbSkipDirective()
    {
        const string text = @"
pointlight BEFORE { color 1 0 0 size 16 }
$gzdb_skip
pointlight AFTER { color 0 1 0 size 16 }";

        var g = GldefsParser.Parse(text);

        Assert.True(g.Lights.ContainsKey("BEFORE"));
        Assert.False(g.Lights.ContainsKey("AFTER"));
    }
}
