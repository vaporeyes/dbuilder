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

    [Fact]
    public void BuildsUdbStyleDecalLookupById()
    {
        const string text = @"
decal OldBlood 42 { pic OLD }
decalgroup BloodGroup 7 { Blood 1 }
decal Blood 42 { pic BLOD }
decalgroup Replaced 9 { Blood 1 }
decal Replaced 10 { pic REPL }";

        var defs = DecaldefParser.Parse(text);
        var lookup = defs.GetDecalDefsById();

        Assert.Equal("Blood", lookup[42].Name);
        Assert.False(lookup[42].IsGroup);
        Assert.Equal("42: Blood", lookup[42].Description);
        Assert.Equal("BloodGroup", lookup[7].Name);
        Assert.True(lookup[7].IsGroup);
        Assert.Equal("7: BloodGroup", lookup[7].Description);
        Assert.False(lookup.ContainsKey(9));
        Assert.Equal("Replaced", lookup[10].Name);
        Assert.False(lookup[10].IsGroup);
        Assert.True(defs.Decals.ContainsKey("Replaced"));
        Assert.False(defs.Groups.ContainsKey("Replaced"));
    }

    [Fact]
    public void MissingDecalBodyStopsParsingLikeUdb()
    {
        const string text = @"
decal Before { pic GOOD }
decal Bare 7
decal After { pic LATE }";

        var defs = DecaldefParser.Parse(text);

        Assert.True(defs.Decals.ContainsKey("Before"));
        Assert.False(defs.Decals.ContainsKey("Bare"));
        Assert.False(defs.Decals.ContainsKey("After"));
    }

    [Fact]
    public void InvalidDecalGroupWeightStopsParsingLikeUdb()
    {
        const string text = @"
decal Before { pic GOOD }
decalgroup BadGroup { Before bogus }
decal After { pic LATE }";

        var defs = DecaldefParser.Parse(text);

        Assert.True(defs.Decals.ContainsKey("Before"));
        Assert.False(defs.Groups.ContainsKey("BadGroup"));
        Assert.False(defs.Decals.ContainsKey("After"));
    }
}
