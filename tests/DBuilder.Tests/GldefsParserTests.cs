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
        Assert.Equal(32f, l.OffsetY, 4);

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
    public void SkipsLightsWithNonNumericColorComponents()
    {
        const string text = @"
pointlight BAD { color 1.0 0.5 bogus size 16 }
pointlight GOOD { color 0.1 0.2 0.3 size 16 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BAD"));
        Assert.True(g.Lights.ContainsKey("GOOD"));
    }

    [Fact]
    public void MapsLightOffsetToUdbCoordinateAxes()
    {
        const string text = "pointlight OFFSET { color 1.0 1.0 1.0 size 16 offset 1 2 3 }";

        var light = GldefsParser.Parse(text).Lights["OFFSET"];

        Assert.Equal(1.0f, light.OffsetX, 4);
        Assert.Equal(2.0f, light.OffsetZ, 4);
        Assert.Equal(3.0f, light.OffsetY, 4);
    }

    [Fact]
    public void ParsesSeparatedNegativeSignedLightOffsets()
    {
        const string text = "pointlight OFFSET { color 1.0 1.0 1.0 size 16 offset - 1 -2 - 3 }";

        var light = GldefsParser.Parse(text).Lights["OFFSET"];

        Assert.Equal(-1.0f, light.OffsetX, 4);
        Assert.Equal(-2.0f, light.OffsetZ, 4);
        Assert.Equal(-3.0f, light.OffsetY, 4);
    }

    [Fact]
    public void SkipsLightsWithNonNumericOffsetComponents()
    {
        const string text = @"
pointlight BAD { color 1.0 0.5 0.25 size 16 offset 1 bogus 3 }
pointlight GOOD { color 0.1 0.2 0.3 size 16 offset 1 2 3 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BAD"));
        Assert.True(g.Lights.ContainsKey("GOOD"));
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
    public void SkipsAnimatedLightsWithNonNumericInterval()
    {
        const string text = @"
pulselight BADPULSE { color 0.1 0.1 0.1 size 16 secondarysize 8 interval bogus }
flickerlight2 BADFLICKER { color 0.1 0.1 0.1 size 16 secondarysize 8 interval bogus }
pulselight GOOD { color 0.1 0.2 0.3 size 16 secondarysize 8 interval 0.5 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BADPULSE"));
        Assert.False(g.Lights.ContainsKey("BADFLICKER"));
        Assert.True(g.Lights.ContainsKey("GOOD"));
    }

    [Fact]
    public void SkipsFlickerLightsWithNonNumericChance()
    {
        const string text = @"
flickerlight BAD { color 0.1 0.1 0.1 size 16 chance bogus }
flickerlight GOOD { color 0.1 0.2 0.3 size 16 chance 0.5 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BAD"));
        Assert.True(g.Lights.ContainsKey("GOOD"));
    }

    [Fact]
    public void SkipsSectorLightsWithNonNumericScale()
    {
        const string text = @"
sectorlight BAD { color 0.1 0.1 0.1 scale bogus }
sectorlight GOOD { color 0.1 0.2 0.3 scale 0.5 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("BAD"));
        Assert.True(g.Lights.ContainsKey("GOOD"));
    }

    [Fact]
    public void ParsesGlow()
    {
        const string text = @"
glow
{
    flats { NUKAGE1 NUKAGE2 LAVA1 }
    walls { GLOWWALL }
    texture GLOWTEX, ""8080ff""
    texture GLOWINT, ""ff0000""
    texture GLOWHEX, ""#2040ff"", 32, fullbright
}";
        var g = GldefsParser.Parse(text);
        Assert.Contains("NUKAGE1", g.GlowFlats);
        Assert.Contains("LAVA1", g.GlowFlats);
        Assert.DoesNotContain("GLOWWALL", g.GlowFlats);
        Assert.Contains("GLOWWALL", g.GlowTextures);
        Assert.Contains("GLOWTEX", g.GlowTextures);
        Assert.Contains("GLOWHEX", g.GlowTextures);
        Assert.True(g.Glows["GLOWWALL"].CalculateTextureColor);
        Assert.True(g.Glows["NUKAGE1"].CalculateTextureColor);
        Assert.Equal(128, g.Glows["NUKAGE1"].Height);
        Assert.Equal(0x80 / 255.0f, g.Glows["GLOWTEX"].R, 4);
        Assert.Equal(1.0f, g.Glows["GLOWINT"].R, 4);
        Assert.Equal(0.0f, g.Glows["GLOWINT"].G, 4);
        Assert.Equal(128, g.Glows["GLOWTEX"].Height);
        Assert.Equal(64, g.Glows["GLOWHEX"].Height);
        Assert.True(g.Glows["GLOWHEX"].Fullbright);
    }

    [Fact]
    public void SkipsGlowTexturesWithoutCommaColorSyntax()
    {
        const string text = @"
glow
{
    texture NOCOMMA ""#2040ff""
    texture COLORKEY, color
    texture RGBTRIPLET, 0.5 0.5 1.0
    texture VALID, ""#2040ff""
}";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Glows.ContainsKey("NOCOMMA"));
        Assert.False(g.Glows.ContainsKey("COLORKEY"));
        Assert.False(g.Glows.ContainsKey("RGBTRIPLET"));
        Assert.True(g.Glows.ContainsKey("VALID"));
    }

    [Fact]
    public void RequiresValidGlowTextureCommaSuffix()
    {
        const string text = @"
glow
{
    texture BADFLAG, ""#2040ff"", bogus
    texture BADHEIGHTFLAG, ""#2040ff"", 32, bogus
    texture MISSINGFLAG, ""#2040ff"", 32,
    texture HEIGHT, ""#2040ff"", 32
    texture ""FULLBRIGHT"", ""#2040ff"", fullbright
}";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Glows.ContainsKey("BADFLAG"));
        Assert.False(g.Glows.ContainsKey("BADHEIGHTFLAG"));
        Assert.False(g.Glows.ContainsKey("MISSINGFLAG"));
        Assert.Equal(64, g.Glows["HEIGHT"].Height);
        Assert.True(g.Glows["FULLBRIGHT"].Fullbright);
    }

    [Fact]
    public void ParsesZdTextGlowColorStrings()
    {
        const string text = @"
glow
{
    texture ""GLOWSHORT"", ""#28f""
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
    public void RequiresQuotesForLongSkyboxNames()
    {
        const string text = @"
skybox LONGSKYBOX { SKY1 SKY2 SKY3 }
skybox ""LONGSKYBOX"" { SKY1 SKY2 SKY3 }
skybox SHORTSKY { SKY1 SKY2 SKY3 }";

        var g = GldefsParser.Parse(text);

        Assert.Equal(2, g.Skyboxes.Count);
        Assert.True(g.Skyboxes.ContainsKey("LONGSKYBOX"));
        Assert.True(g.Skyboxes.ContainsKey("SHORTSKY"));
    }

    [Fact]
    public void RequiresQuotesForLongGlowTextureNames()
    {
        const string text = @"
glow
{
    texture LONGGLOWBAD, ""#2040ff""
    texture ""LONGGLOWOK"", ""#2040ff""
    texture SHORTTEX, ""#2040ff""
}";

        var g = GldefsParser.Parse(text);

        Assert.Equal(2, g.Glows.Count);
        Assert.False(g.Glows.ContainsKey("LONGGLOWBAD"));
        Assert.True(g.Glows.ContainsKey("LONGGLOWOK"));
        Assert.True(g.Glows.ContainsKey("SHORTTEX"));
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

    [Theory]
    [InlineData("../lights/gldefs.txt")]
    [InlineData("./lights/gldefs.txt")]
    [InlineData("lights\\gldefs.txt")]
    [InlineData("/lights/gldefs.txt")]
    public void RejectsInvalidIncludePaths(string includePath)
    {
        string root = "#include \"" + includePath + "\"";

        var g = GldefsParser.Parse(root, _ => "pointlight BAD { color 1 1 1 size 32 }");

        Assert.Empty(g.Lights);
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
    public void ObjectFindsFirstRelevantFrameLightAcrossNestedBody()
    {
        const string text = @"
pointlight FIRST { color 1 0 0 size 16 }
pointlight SECOND { color 0 1 0 size 16 }
object LooseActor
{
    frame LOOSA
    light FIRST
}
object NestedActor
{
    frame NESTA
    {
        state
        {
            light SECOND
        }
    }
}";

        var g = GldefsParser.Parse(text);

        Assert.Equal(2, g.Objects.Count);
        Assert.Equal("FIRST", g.Objects.Single(o => o.ClassName == "LooseActor").Lights[0]);
        Assert.Equal("SECOND", g.Objects.Single(o => o.ClassName == "NestedActor").Lights[0]);
    }

    [Fact]
    public void LaterObjectDefinitionReplacesEarlierObjectDefinition()
    {
        const string text = @"
pointlight FIRST { color 1 0 0 size 16 }
pointlight SECOND { color 0 1 0 size 16 }
object TorchActor { frame TRCHA { light FIRST } }
object TorchActor { frame TRCHA { light SECOND } }";

        var g = GldefsParser.Parse(text);

        var obj = Assert.Single(g.Objects);
        Assert.Equal("TorchActor", obj.ClassName);
        Assert.Equal("SECOND", obj.Lights.Single());
        Assert.Equal(0.0f, g.ActorLightColor("TorchActor")!.Value.R, 4);
        Assert.Equal(1.0f, g.ActorLightColor("TorchActor")!.Value.G, 4);
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
    public void SkipsLightsWithNonIntegralSizes()
    {
        const string text = @"
pointlight FRACTIONALSIZE { color 1.0 1.0 1.0 size 8.5 }
pulselight FRACTIONALSECONDARY { color 1.0 1.0 1.0 size 8 secondarysize 4.5 interval 1 }
pointlight VALID { color 1.0 1.0 1.0 size 8 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("FRACTIONALSIZE"));
        Assert.False(g.Lights.ContainsKey("FRACTIONALSECONDARY"));
        Assert.True(g.Lights.ContainsKey("VALID"));
    }

    [Fact]
    public void SkipsLightsWithPropertiesForWrongLightType()
    {
        const string text = @"
sectorlight SECTORSIZE { color 1.0 1.0 1.0 size 8 scale 0.5 }
pointlight POINTSECONDARY { color 1.0 1.0 1.0 size 8 secondarysize 4 }
pointlight POINTINTERVAL { color 1.0 1.0 1.0 size 8 interval 1 }
pulselight PULSECHANCE { color 1.0 1.0 1.0 size 8 secondarysize 4 interval 1 chance 0.5 }
pointlight POINTSCALE { color 1.0 1.0 1.0 size 8 scale 0.5 }
pulselight VALIDPULSE { color 1.0 1.0 1.0 size 8 secondarysize 4 interval 1 }
sectorlight VALIDSECTOR { color 1.0 1.0 1.0 scale 0.5 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("SECTORSIZE"));
        Assert.False(g.Lights.ContainsKey("POINTSECONDARY"));
        Assert.False(g.Lights.ContainsKey("POINTINTERVAL"));
        Assert.False(g.Lights.ContainsKey("PULSECHANCE"));
        Assert.False(g.Lights.ContainsKey("POINTSCALE"));
        Assert.True(g.Lights.ContainsKey("VALIDPULSE"));
        Assert.True(g.Lights.ContainsKey("VALIDSECTOR"));
    }

    [Fact]
    public void RequiresIntegerLightRenderFlags()
    {
        const string text = @"
pointlight FALSEFLAGS { color 1.0 1.0 1.0 size 8 subtractive 0 attenuate 0 dontlightself 0 }
pointlight TRUEFLAGS { color 1.0 1.0 1.0 size 8 subtractive 1 attenuate 1 dontlightself 1 }
pointlight BADBOOL { color 1.0 1.0 1.0 size 8 subtractive true }
pointlight MISSING { color 1.0 1.0 1.0 size 8 attenuate }";

        var g = GldefsParser.Parse(text);

        Assert.True(g.Lights.ContainsKey("FALSEFLAGS"));
        Assert.False(g.Lights["FALSEFLAGS"].Subtractive);
        Assert.False(g.Lights["FALSEFLAGS"].Attenuate);
        Assert.Equal(GldefsLightRenderStyle.Normal, g.Lights["FALSEFLAGS"].RenderStyle);
        Assert.False(g.Lights["FALSEFLAGS"].DontLightSelf);
        Assert.True(g.Lights.ContainsKey("TRUEFLAGS"));
        Assert.False(g.Lights["TRUEFLAGS"].Subtractive);
        Assert.True(g.Lights["TRUEFLAGS"].Attenuate);
        Assert.Equal(GldefsLightRenderStyle.Attenuated, g.Lights["TRUEFLAGS"].RenderStyle);
        Assert.True(g.Lights["TRUEFLAGS"].DontLightSelf);
        Assert.False(g.Lights.ContainsKey("BADBOOL"));
        Assert.False(g.Lights.ContainsKey("MISSING"));
    }

    [Fact]
    public void UsesLastLightRenderStyleFlagLikeUdb()
    {
        const string text = @"
pointlight SUBTRACTIVE { color 1.0 1.0 1.0 size 8 attenuate 1 subtractive 1 }
pointlight NORMALIZED { color 1.0 1.0 1.0 size 8 subtractive 1 attenuate 0 }";

        var g = GldefsParser.Parse(text);

        Assert.Equal(GldefsLightRenderStyle.Subtractive, g.Lights["SUBTRACTIVE"].RenderStyle);
        Assert.True(g.Lights["SUBTRACTIVE"].Subtractive);
        Assert.False(g.Lights["SUBTRACTIVE"].Attenuate);
        Assert.Equal(GldefsLightRenderStyle.Normal, g.Lights["NORMALIZED"].RenderStyle);
        Assert.False(g.Lights["NORMALIZED"].Subtractive);
        Assert.False(g.Lights["NORMALIZED"].Attenuate);
    }

    [Fact]
    public void SkipsUnsupportedSpotlightBlocks()
    {
        const string text = @"
spotlight SPOT { color 1.0 1.0 1.0 size 8 }
pointlight VALID { color 1.0 1.0 1.0 size 8 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("SPOT"));
        Assert.True(g.Lights.ContainsKey("VALID"));
    }

    [Fact]
    public void SkipsUnknownTopLevelBlocksWithArguments()
    {
        const string text = @"
brightmap texture pointlight
{
    pointlight HIDDEN { color 1.0 0.0 0.0 size 8 }
}
pointlight VALID { color 0.0 1.0 0.0 size 8 }";

        var g = GldefsParser.Parse(text);

        Assert.False(g.Lights.ContainsKey("HIDDEN"));
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
