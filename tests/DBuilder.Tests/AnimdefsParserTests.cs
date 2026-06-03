// ABOUTME: Tests for AnimdefsParser - ANIMDEFS range/block animations and switch definitions.
// ABOUTME: Covers flat/texture ranges, explicit pic blocks, switches, game qualifiers, and comments.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class AnimdefsParserTests
{
    [Fact]
    public void ParsesFlatRange()
    {
        var a = AnimdefsParser.Parse("flat FWATER1 range FWATER4 tics 8");
        Assert.Single(a.Animations);
        var d = a.Animations[0];
        Assert.Equal(AnimKind.Flat, d.Kind);
        Assert.Equal("FWATER1", d.FirstName);
        Assert.Equal("FWATER4", d.RangeLast);
        Assert.Equal(8, d.RangeTics);
        Assert.True(d.IsRange);
    }

    [Fact]
    public void ParsesTextureBlock()
    {
        const string text = @"
// animated computer screen
texture COMP1
{
    pic COMP1 tics 4
    pic COMP2 tics 4
    pic COMP3 rand 3 9
}";
        var d = AnimdefsParser.Parse(text).Animations.Single();
        Assert.Equal(AnimKind.Texture, d.Kind);
        Assert.False(d.IsRange);
        Assert.Equal(3, d.Frames.Count);
        Assert.Equal(new AnimFrame("COMP1", 4), d.Frames[0]);
        Assert.Equal("COMP3", d.Frames[2].Texture);
        Assert.Equal(3, d.Frames[2].Tics); // rand min used as the tic count
    }

    [Fact]
    public void ParsesSwitch()
    {
        var a = AnimdefsParser.Parse("switch SW1BRN1 on pic SW2BRN1 tics 0");
        var s = a.Switches.Single();
        Assert.Equal("SW1BRN1", s.OffTexture);
        Assert.Equal("SW2BRN1", s.OnTexture);
    }

    [Fact]
    public void ParsesSwitchWithGameQualifier()
    {
        var a = AnimdefsParser.Parse("switch doom SW1STON1 on pic SW2STON1 tics 0");
        var s = a.Switches.Single();
        Assert.Equal("SW1STON1", s.OffTexture);
        Assert.Equal("SW2STON1", s.OnTexture);
    }

    [Fact]
    public void ParsesMixedDefinitionsAndSkipsUnknownBlocks()
    {
        const string text = @"
flat NUKAGE1 range NUKAGE3 tics 8
cameratexture FOO 64 64 { fitwidth 64 }
texture SLADRIP { pic SLADRIP1 tics 4 pic SLADRIP2 tics 4 }
switch SW1COMP on pic SW2COMP tics 0";
        var a = AnimdefsParser.Parse(text);
        Assert.Equal(2, a.Animations.Count);
        Assert.Single(a.Switches);
        Assert.Equal("NUKAGE1", a.Animations[0].FirstName);
        Assert.Equal("SLADRIP", a.Animations[1].FirstName);
        Assert.Single(a.CameraTextures);
        Assert.Equal(new CameraTextureDef("FOO", 64, 64), a.CameraTextures[0]);
    }

    [Fact]
    public void ParsesMultipleCameraTextures()
    {
        const string text = @"
cameratexture cam1 128 64 { fitwidth 128 }
cameratexture CAM2 320 200";

        var a = AnimdefsParser.Parse(text);

        Assert.Equal(new[] { "CAM1", "CAM2" }, a.CameraTextures.Select(c => c.Name).ToArray());
        Assert.Equal(128, a.CameraTextures[0].Width);
        Assert.Equal(200, a.CameraTextures[1].Height);
    }

    [Fact]
    public void ParsesCameraTextureFitAndWorldPanning()
    {
        const string text = @"
cameratexture CAM1 64 32 fit 128 32 worldpanning
cameratexture CAM2 320 200 worldpanning";

        var a = AnimdefsParser.Parse(text);

        Assert.Equal(2.0f, a.CameraTextures[0].ScaleX);
        Assert.Equal(1.0f, a.CameraTextures[0].ScaleY);
        Assert.True(a.CameraTextures[0].FitTexture);
        Assert.True(a.CameraTextures[0].WorldPanning);
        Assert.False(a.CameraTextures[1].FitTexture);
        Assert.True(a.CameraTextures[1].WorldPanning);
    }

    [Fact]
    public void InvalidCameraTextureDimensionsStopParsingLikeUdb()
    {
        const string text = @"
cameratexture CAM1 64 64
cameratexture BAD 0 64
cameratexture CAM2 128 128";

        var a = AnimdefsParser.Parse(text);

        Assert.Equal(new[] { "CAM1" }, a.CameraTextures.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void DuplicateCameraTextureNamesStopParsingLikeUdb()
    {
        const string text = @"
cameratexture CAM1 64 64
cameratexture cam1 128 128
cameratexture CAM2 256 256";

        var a = AnimdefsParser.Parse(text);

        Assert.Equal(new[] { "CAM1" }, a.CameraTextures.Select(c => c.Name).ToArray());
    }
}
