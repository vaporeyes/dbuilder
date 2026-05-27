// ABOUTME: Tests for DehackedParser over line-oriented DeHackEd patch data.
// ABOUTME: Covers thing blocks, frame blocks, metadata rows, sprite replacements, and comments.

using DBuilder.IO;

namespace DBuilder.Tests;

public class DehackedParserTests
{
    [Fact]
    public void ParsesThingAndFrameBlocks()
    {
        const string text = @"
Patch Format = 6
Doom version = 21

Thing 2 (Former Human)
ID # = 3004 # keep this value
Initial frame = 10
#$Category = Monsters

Frame 10
Sprite number = 1
Sprite subnumber = 32768
Duration = 4
";

        var patch = DehackedParser.Parse(text);

        Assert.Equal("6", patch.PatchFormat);
        Assert.Equal("21", patch.DoomVersion);
        var thing = Assert.Single(patch.Things);
        Assert.Equal(2, thing.Number);
        Assert.Equal("Former Human", thing.Name);
        Assert.Equal("3004", thing.Properties["ID #"]);
        Assert.Equal("10", thing.Properties["Initial frame"]);
        Assert.Equal("Monsters", thing.Properties["$Category"]);

        var frame = patch.Frames[10];
        Assert.Equal("1", frame.Properties["Sprite number"]);
        Assert.Equal("32768", frame.Properties["Sprite subnumber"]);
        Assert.Equal("4", frame.Properties["Duration"]);
    }

    [Fact]
    public void ParsesSpritesAndSimpleTextReplacements()
    {
        const string text = @"
[SPRITES]
1 = POSS
150 = BOSS

Text 4 4
POSSCPOS
";

        var patch = DehackedParser.Parse(text);

        Assert.Equal("POSS", patch.Sprites[1]);
        Assert.Equal("BOSS", patch.Sprites[150]);
        Assert.Equal("CPOS", patch.Texts["POSS"]);
    }

    [Fact]
    public void ParsesLengthBasedTextReplacementsWithEmbeddedNewlines()
    {
        const string text = @"Text 5 7
AB
CDXY
ZQ!!

Frame 1
Duration = 8
";

        var patch = DehackedParser.Parse(text);

        Assert.Equal("XY\nZQ!!", patch.Texts["AB\nCD"]);
        Assert.True(patch.Frames.ContainsKey(1));
    }

    [Fact]
    public void SeparatesNumericNewSpritesFromSpriteRenames()
    {
        const string text = @"
[SPRITES]
1 = POSS
POSS = CPOS
TOOLONG = BOSS
BOSS = TOOLONG
";

        var patch = DehackedParser.Parse(text);

        Assert.Equal("POSS", patch.NewSprites[1]);
        Assert.Equal("CPOS", patch.SpriteReplacements["POSS"]);
        Assert.False(patch.SpriteReplacements.ContainsKey("TOOLONG"));
        Assert.False(patch.SpriteReplacements.ContainsKey("BOSS"));
    }

    [Fact]
    public void UsesDefaultThingNameWhenHeaderOmitsName()
    {
        const string text = @"
Thing -1
Hit points = 20
";

        var thing = Assert.Single(DehackedParser.Parse(text).Things);

        Assert.Equal(-1, thing.Number);
        Assert.Equal("<DeHackEd thing -1>", thing.Name);
        Assert.Equal("20", thing.Properties["Hit points"]);
    }
}
