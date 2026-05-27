// ABOUTME: Tests for DecaldefParser over DECALDEF decal and generator declarations.
// ABOUTME: Covers image metadata, scale forms, translucency, additive flags, and generators.

using DBuilder.IO;

namespace DBuilder.Tests;

public class DecaldefParserTests
{
    [Fact]
    public void ParsesDecalsAndGenerators()
    {
        const string text = @"
decal BloodSplat
{
    pic BLUD1
    scale 0.5 0.75
    translucent 0.6
    add
    animator { random }
}
generator ZombieMan
{
    decal BloodSplat
}
generator ImpScorch ScorchMark";

        var defs = DecaldefParser.Parse(text);

        var decal = defs.Decals["BloodSplat"];
        Assert.Equal("BLUD1", decal.Pic);
        Assert.Equal(0.5f, decal.ScaleX);
        Assert.Equal(0.75f, decal.ScaleY);
        Assert.Equal(0.6f, decal.Alpha);
        Assert.True(decal.Additive);
        Assert.Contains(defs.Generators, g => g.ActorClass == "ZombieMan" && g.DecalName == "BloodSplat");
        Assert.Contains(defs.Generators, g => g.ActorClass == "ImpScorch" && g.DecalName == "ScorchMark");
    }

    [Fact]
    public void ParsesSeparateScaleProperties()
    {
        const string text = @"decal BulletHole { pic BULLET x-scale 0.25 yscale 0.5 }";

        var decal = DecaldefParser.Parse(text).Decals["BulletHole"];

        Assert.Equal(0.25f, decal.ScaleX);
        Assert.Equal(0.5f, decal.ScaleY);
    }
}
