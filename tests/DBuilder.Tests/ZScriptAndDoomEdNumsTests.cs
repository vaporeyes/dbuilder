// ABOUTME: Tests for ZScript actor parsing, MAPINFO DoomEdNums parsing, and merging the two into the config.
// ABOUTME: Verifies class header/Default props/spawn sprite, number assignment by class name, and overrides.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ZScriptAndDoomEdNumsTests
{
    [Fact]
    public void ParsesZScriptClassDefaultsAndSprite()
    {
        const string text = @"
version ""4.10""
class FancyImp : Actor replaces Imp
{
    //$Title ""Fancy Imp""
    Default
    {
        Radius 20;
        Height 56;
    }
    States
    {
    Spawn:
        TROO AB 10 A_Look;
        loop;
    }
}";
        var actors = ZScriptParser.Parse(text);
        Assert.Single(actors);
        var a = actors[0];
        Assert.Equal("FancyImp", a.ClassName);
        Assert.Equal("Actor", a.ParentName);
        Assert.Equal("Imp", a.Replaces);
        Assert.True(a.DoomEdNum < 0); // ZScript: number comes from MAPINFO, not the header
        Assert.Equal("Fancy Imp", a.Title);
        Assert.Equal(20, a.Radius);
        Assert.Equal(56, a.Height);
        Assert.Equal("TROOA0", a.EditorSprite);
    }

    [Fact]
    public void HandlesForwardDeclarationAndModifiers()
    {
        const string text = "class Foo;\nclass Bar : Actor abstract { Default { Radius 8; } }";
        var actors = ZScriptParser.Parse(text);
        Assert.Equal(2, actors.Count);
        Assert.Equal("Foo", actors[0].ClassName);
        Assert.Equal("Bar", actors[1].ClassName);
        Assert.Equal(8, actors[1].Radius);
    }

    [Fact]
    public void StopsAtGzdbSkipLineComment()
    {
        const string text = @"
class BeforeSkip : Actor { Default { Radius 8; } }
//$gzdb_skip
class AfterSkip : Actor { Default { Radius 16; } }";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("BeforeSkip", actor.ClassName);
    }

    [Fact]
    public void ParsesDefaultAssignmentsFlagsAndProperties()
    {
        const string text = @"
class Assigned : Actor
{
    Default
    {
        Radius = 12;
        Height = 34;
        +SOLID;
        RenderStyle = Translucent;
    }
}";

        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal(12, actor.Radius);
        Assert.Equal(34, actor.Height);
        Assert.True(actor.Flags["SOLID"]);
        Assert.Equal("Translucent", actor.Properties["RenderStyle"].Single());
    }

    [Fact]
    public void ParsesSemicolonTerminatedDefaultPropertyValues()
    {
        const string text = @"
class MultiValueDefault : Actor
{
    Default
    {
        DamageFactor ""Fire"", 0.5;
        Scale 1.5, 0.75;
    }
}";

        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal(new[] { "Fire", "0.5" }, actor.Properties["DamageFactor"]);
        Assert.Equal(new[] { "1.5", "0.75" }, actor.Properties["xscale"]);
        Assert.Equal(new[] { "1.5", "0.75" }, actor.Properties["yscale"]);
    }

    [Fact]
    public void SkipsZScriptClassBodyMembersOutsideDefaultsAndStates()
    {
        const string text = @"
class MemberActor : Actor
{
    mixin InventoryMixin;
    property CustomThing: user_value;
    flagdef CUSTOMFLAG: bflags, 7;
    const int LocalConst = 3;
    int user_value;
    void Helper()
    {
        int local_value;
    }
    Default
    {
        Radius 12;
        +SOLID;
    }
    States { Spawn: MEMB A -1; stop; }
}";

        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal(12, actor.Radius);
        Assert.True(actor.Flags["SOLID"]);
        Assert.Equal("MEMBA0", actor.EditorSprite);
        Assert.False(actor.Properties.ContainsKey("mixin"));
        Assert.False(actor.Properties.ContainsKey("property"));
        Assert.False(actor.Properties.ContainsKey("flagdef"));
        Assert.False(actor.Properties.ContainsKey("int"));
        Assert.False(actor.Properties.ContainsKey("void"));
    }

    [Fact]
    public void ParsesMapInfoDoomEdNums()
    {
        const string text = @"
DoomEdNums
{
    9050 = ""MyMonster""
    9051 = OtherThing, 0, 0
}
map MAP01 ""x"" { }";
        var mi = MapInfo.Parse(text);
        Assert.Equal(2, mi.DoomEdNums.Count);
        Assert.Equal("mymonster", mi.DoomEdNums[9050]);
        Assert.Equal("otherthing", mi.DoomEdNums[9051]);
        Assert.Single(mi.Maps); // the map directive still parses
    }

    [Fact]
    public void MergesZScriptActorsUsingDoomEdNums()
    {
        const string zscript = @"
class MyMonster : Actor
{
    //$Title ""My Monster""
    Default { Radius 24; Height 48; XScale 0.75; RenderStyle Translucent; Alpha 0.25; }
    States { Spawn: COOL A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9050 = MyMonster }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9050);
        Assert.NotNull(info);
        Assert.Equal("My Monster", gc.ThingTitle(9050));
        Assert.Equal("COOLA0", info!.Sprite);
        Assert.Equal(24, info.Width);
        Assert.Equal("translucent", info.RenderStyle);
        Assert.Equal(0.25, info.Alpha);
        Assert.Equal(0.75, info.SpriteScale);
    }

    [Fact]
    public void ActorWithoutNumberIsSkippedWhenNoMapping()
    {
        var actors = ZScriptParser.Parse("class Lonely : Actor { Default { Radius 8; } }");
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, new System.Collections.Generic.Dictionary<int, string>());
        Assert.Empty(gc.Things);
    }

    [Fact]
    public void ParsesClassDefinitionsFromIncludes()
    {
        const string root = @"
#include ""zscript/base.zs""
class IncludedChild : IncludedBase { }";
        const string included = @"
class IncludedBase : Actor
{
    Default { Height 42; }
    States { Spawn: INCL A -1; stop; }
}";

        var actors = ZScriptParser.Parse(root, path => path == "zscript/base.zs" ? included : null);

        var child = actors.Single(a => a.ClassName == "IncludedChild");
        Assert.Equal("INCLA0", child.EditorSprite);
        Assert.Equal(42, child.Height);
    }
}
