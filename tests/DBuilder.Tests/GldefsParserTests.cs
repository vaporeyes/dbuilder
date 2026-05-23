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
        Assert.Equal(96f, l.Size, 4);

        Assert.Single(g.Objects);
        Assert.Equal("ShortRedTorch", g.Objects[0].ClassName);
        Assert.Contains("TORCH", g.Objects[0].Lights);

        var color = g.ActorLightColor("ShortRedTorch");
        Assert.NotNull(color);
        Assert.Equal(1.0f, color!.Value.R, 4);
    }

    [Fact]
    public void ParsesGlow()
    {
        const string text = @"
glow
{
    flats { NUKAGE1 NUKAGE2 LAVA1 }
    texture GLOWTEX color 0.5 0.5 1.0
}";
        var g = GldefsParser.Parse(text);
        Assert.Contains("NUKAGE1", g.GlowFlats);
        Assert.Contains("LAVA1", g.GlowFlats);
        Assert.Contains("GLOWTEX", g.GlowTextures);
    }

    [Fact]
    public void SkipsUnknownBlocks()
    {
        const string text = @"
skybox SKY1 { tex SKYBOX1 }
pulselight LAMP { color 0.2 0.2 1.0 size 64 }
brightmap texture FOO { map FOO_BR }";
        var g = GldefsParser.Parse(text);
        Assert.Single(g.Lights);
        Assert.True(g.Lights.ContainsKey("LAMP"));
    }
}
