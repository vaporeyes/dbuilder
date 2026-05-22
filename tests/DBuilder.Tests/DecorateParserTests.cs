// ABOUTME: Tests for DecorateParser - parsing DECORATE actors (header, //$ keys, props, spawn sprite, inheritance).
// ABOUTME: Also checks merging parsed actors into GameConfiguration's thing catalog.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DecorateParserTests
{
    [Fact]
    public void ParsesHeaderEditorKeysAndStateSprite()
    {
        const string text = @"
ACTOR FancyImp : Imp replaces Imp 3001
{
    //$Title ""Fancy Imp""
    //$Category ""Monsters""
    Radius 20
    Height 56
    States
    {
    Spawn:
        TROO AB 10 A_Look
        loop
    }
}";
        var actors = DecorateParser.Parse(text);
        Assert.Single(actors);
        var a = actors[0];
        Assert.Equal("FancyImp", a.ClassName);
        Assert.Equal("Imp", a.ParentName);
        Assert.Equal("Imp", a.Replaces);
        Assert.Equal(3001, a.DoomEdNum);
        Assert.Equal("Fancy Imp", a.Title);
        Assert.Equal("Monsters", a.Category);
        Assert.Equal(20, a.Radius);
        Assert.Equal(56, a.Height);
        Assert.Equal("TROOA0", a.EditorSprite); // first spawn frame: TROO + A + rotation 0
    }

    [Fact]
    public void DollarSpriteOverridesStateSprite()
    {
        const string text = @"
ACTOR Thing 5000
{
    //$Sprite ""ARTIA0""
    States { Spawn: TROO A -1 stop }
}";
        var a = DecorateParser.Parse(text)[0];
        Assert.Equal("ARTIA0", a.EditorSprite);
    }

    [Fact]
    public void TitleFallsBackToClassName()
    {
        var a = DecorateParser.Parse("ACTOR Gadget 6000 { }")[0];
        Assert.Equal("Gadget", a.Title);
        Assert.Equal(6000, a.DoomEdNum);
    }

    [Fact]
    public void NoEditorNumberLeavesDoomEdNumNegative()
    {
        var a = DecorateParser.Parse("ACTOR AbstractBase { Radius 16 }")[0];
        Assert.True(a.DoomEdNum < 0);
    }

    [Fact]
    public void ChildInheritsSpriteAndSizeFromParent()
    {
        const string text = @"
ACTOR Base
{
    Radius 31
    Height 64
    States { Spawn: BOSS A -1 stop }
}
ACTOR Derived : Base 7001 { }";
        var actors = DecorateParser.Parse(text);
        var derived = actors.First(x => x.ClassName == "Derived");
        Assert.Equal("BOSSA0", derived.EditorSprite);
        Assert.Equal(31, derived.Radius);
        Assert.Equal(64, derived.Height);
    }

    [Fact]
    public void IgnoresFlowKeywordsAsSprites()
    {
        // "goto" is 4 chars but must not be mistaken for a sprite frame.
        const string text = "ACTOR X 8000 { States { Spawn: goto Super::Spawn\n MNST A 1 stop } }";
        var a = DecorateParser.Parse(text)[0];
        Assert.Equal("MNSTA0", a.EditorSprite);
    }

    [Fact]
    public void MergeActorsPopulatesGameConfiguration()
    {
        const string text = @"
ACTOR CoolMonster 31000
{
    //$Title ""Cool Monster""
    Radius 24
    Height 48
    States { Spawn: COOL A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31000);
        Assert.NotNull(info);
        Assert.Equal("Cool Monster", gc.ThingTitle(31000));
        Assert.Equal("COOLA0", info!.Sprite);
        Assert.Equal(24, info.Width);
        Assert.Equal(48, info.Height);
    }
}
