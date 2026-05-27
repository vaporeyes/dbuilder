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
decal BloodSplat 12
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
        Assert.Equal(12, decal.Id);
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

    [Fact]
    public void ParsesDecalGroups()
    {
        const string text = @"
decal BloodA { pic BLDA }
decal BloodB { pic BLDB }
decalgroup BloodPool 99
{
    BloodA 3
    BloodB 1
}";

        var group = DecaldefParser.Parse(text).Groups["BloodPool"];

        Assert.Equal(99, group.Id);
        Assert.Equal(2, group.Entries.Count);
        Assert.Contains(group.Entries, e => e.DecalName == "BloodA" && e.Weight == 3);
        Assert.Contains(group.Entries, e => e.DecalName == "BloodB" && e.Weight == 1);
    }
}
