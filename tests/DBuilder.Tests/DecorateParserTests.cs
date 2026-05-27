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
    public void DollarPropertiesPopulateEditorDisplayFields()
    {
        const string text = @"
ACTOR PropertyThing 5001
{
    $Title ""Property Thing""
    $Category ""Property Category""
    $Sprite ""PROPA0""
    States { Spawn: TROO A -1 stop }
}";
        var a = DecorateParser.Parse(text)[0];

        Assert.Equal("Property Thing", a.Title);
        Assert.Equal("Property Category", a.Category);
        Assert.Equal("PROPA0", a.EditorSprite);
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
    public void ParsesFlagsAndProperties()
    {
        const string text = @"
ACTOR Flagged 7002
{
    +SOLID
    -NOGRAVITY
    RenderStyle Translucent
    Alpha 0.5
    Radius 12
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.True(actor.Flags["SOLID"]);
        Assert.False(actor.Flags["NOGRAVITY"]);
        Assert.Equal("Translucent", actor.Properties["RenderStyle"].Single());
        Assert.Equal("0.5", actor.Properties["Alpha"].Single());
        Assert.Equal("12", actor.Properties["radius"].Single());
    }

    [Fact]
    public void ScalePopulatesXScaleAndYScaleProperties()
    {
        const string text = "ACTOR Scaled 7004 { Scale 1.5 }";

        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("1.5", actor.Properties["xscale"].Single());
        Assert.Equal("1.5", actor.Properties["yscale"].Single());
        Assert.False(actor.Properties.ContainsKey("scale"));
    }

    [Fact]
    public void ChildInheritsFlagsAndPropertiesFromParent()
    {
        const string text = @"
ACTOR Base
{
    +SOLID
    RenderStyle Add
}
ACTOR Derived : Base 7003 { -SOLID }";
        var derived = DecorateParser.Parse(text).First(a => a.ClassName == "Derived");

        Assert.False(derived.Flags["SOLID"]);
        Assert.Equal("Add", derived.Properties["RenderStyle"].Single());
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
    Tag ""Cool Tag""
    RenderStyle Add
    Alpha 0.5
    Scale 1.25
    $Color 4
    $Angled
    +SOLID
    +SPAWNCEILING
    $Arg0 ""Target""
    $Arg0Type 25
    $Arg0Default 7
    $Arg0Tooltip ""Pick target\nby tid""
    $Arg0TargetClasses ""MapSpot, PatrolPoint""
    $Arg0RenderStyle Circle
    $Arg0MinRange 16
    $Arg0MaxRange 256
    $Arg0Str ""Target Name""
    Radius 24
    Height 48
    States { Spawn: COOL A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31000);
        Assert.NotNull(info);
        Assert.Equal("Cool Tag", gc.ThingTitle(31000));
        Assert.Equal("COOLA0", info!.Sprite);
        Assert.Equal(24, info.Width);
        Assert.Equal(48, info.Height);
        Assert.Equal("add", info.RenderStyle);
        Assert.Equal(0.5, info.Alpha);
        Assert.Equal(1.25, info.SpriteScale);
        Assert.Equal(4, info.Color);
        Assert.True(info.Arrow);
        Assert.True(info.Hangs);
        Assert.Equal(2, info.Blocking);
        Assert.Equal(1, info.ErrorCheck);
        Assert.Equal("Target", info.Args[0].Title);
        Assert.Equal(25, info.Args[0].Type);
        Assert.Equal(7, info.Args[0].Default);
        Assert.Equal(7, info.Args[0].DefaultValue);
        Assert.Equal("Pick target\nby tid", info.Args[0].ToolTip);
        Assert.Contains("MapSpot", info.Args[0].TargetClasses);
        Assert.Contains("PatrolPoint", info.Args[0].TargetClasses);
        Assert.Equal("circle", info.Args[0].RenderStyle);
        Assert.Equal(16, info.Args[0].MinRange);
        Assert.Equal(256, info.Args[0].MaxRange);
        Assert.True(info.Args[0].Str);
        Assert.Equal("Target Name", info.Args[0].TitleStr);
    }

    [Fact]
    public void DefaultAlphaPopulatesCatalogAlphaWhenNoExplicitAlphaExists()
    {
        const string text = @"
ACTOR GhostThing 31002
{
    DefaultAlpha
    Radius 12
    Height 24
    States { Spawn: GHST A -1 stop }
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31002);
        Assert.NotNull(info);
        Assert.True(DecorateParser.Parse(text).Single().Properties.ContainsKey("DefaultAlpha"));
        Assert.Equal(0.6, info!.Alpha);
    }

    [Fact]
    public void StopsAtGzdbSkipDirective()
    {
        const string text = @"
ACTOR BeforeSkip 31003 { Radius 8 }
$gzdb_skip
ACTOR AfterSkip 31004 { Radius 16 }";

        var actor = Assert.Single(DecorateParser.Parse(text));

        Assert.Equal("BeforeSkip", actor.ClassName);
    }

    [Fact]
    public void ParsesActorDefinitionsFromIncludes()
    {
        const string root = @"
#include ""actors/base.dec""
ACTOR IncludedChild : IncludedBase 31001 { }";
        const string included = @"
ACTOR IncludedBase
{
    Radius 16
    States { Spawn: BASE A -1 stop }
}";

        var actors = DecorateParser.Parse(root, path => path == "actors/base.dec" ? included : null);

        var child = actors.Single(a => a.ClassName == "IncludedChild");
        Assert.Equal("BASEA0", child.EditorSprite);
        Assert.Equal(16, child.Radius);
        Assert.Equal(31001, child.DoomEdNum);
    }
}
