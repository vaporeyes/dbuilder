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
    public void SkipsZScriptPlaceholderStateSprites()
    {
        const string text = @"
class PlaceholderSpriteActor : Actor
{
    States
    {
    Spawn:
        ---- A 0;
        #### A 0;
        REAL A -1;
        stop;
    }
}";
        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal("REALA0", actor.EditorSprite);
    }

    [Fact]
    public void ParsesSpacedZScriptEditorLineComments()
    {
        const string text = @"
class SpacedCommentActor : Actor
{
    // $Title ""Spaced Comment Actor""
    Default { Radius 16; }
}";
        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal("Spaced Comment Actor", actor.Title);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void HandlesForwardDeclarationAndModifiers()
    {
        const string text = "class Foo;\nclass Bar : Actor abstract { Default { Radius 8; } }";
        var actors = ZScriptParser.Parse(text);
        var actor = Assert.Single(actors);
        Assert.Equal("Bar", actor.ClassName);
        Assert.Equal(8, actor.Radius);
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
    public void KeepsZScriptDefaultExpressionsAsSingleValues()
    {
        const string text = @"
class ExpressionDefault : Actor
{
    Default
    {
        Alpha 0.5 + 0.25;
        DamageFactor ""Fire"", 1.0 / 2.0;
    }
}";

        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal(new[] { "0.5 + 0.25" }, actor.Properties["Alpha"]);
        Assert.Equal(new[] { "Fire", "1.0 / 2.0" }, actor.Properties["DamageFactor"]);
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
    public void SkipsTopLevelZScriptStructContents()
    {
        const string text = @"
struct HelperStruct
{
    class NotAnActor : Actor
    {
        Default { Radius 128; }
    }
}
class RealActor : Actor
{
    Default { Radius 16; }
}";

        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal("RealActor", actor.ClassName);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void SkipsZScriptClassesWithKnownNonActorAncestry()
    {
        const string text = @"
class HelperBase
{
    int Value;
}
class HelperChild : HelperBase
{
    Default { Radius 128; }
}
class InventoryChild : Inventory
{
    Default { Radius 12; }
}
class RealActor : Actor
{
    Default { Radius 16; }
}";

        var actors = ZScriptParser.Parse(text);

        Assert.DoesNotContain(actors, actor => actor.ClassName == "HelperBase");
        Assert.DoesNotContain(actors, actor => actor.ClassName == "HelperChild");
        Assert.Contains(actors, actor => actor.ClassName == "InventoryChild");
        Assert.Contains(actors, actor => actor.ClassName == "RealActor");
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
    public void AppliesDoomEdNumOverridesToExistingConfigThings()
    {
        const string cfg = @"
thingtypes
{
    monsters
    {
        color = 4;
        width = 20;
        height = 56;
        3001
        {
            title = ""Imp"";
            sprite = ""TROOA1"";
            class = ""DoomImp"";
        }
        3002
        {
            title = ""Demon"";
            sprite = ""SARGA1"";
            class = ""Demon"";
        }
    }
}";
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9050 = DoomImp 3002 = none }").DoomEdNums;

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(Array.Empty<ActorInfo>(), doomEdNums);

        var copied = gc.GetThing(9050);
        Assert.NotNull(copied);
        Assert.Equal("Imp", copied!.Title);
        Assert.Equal("DoomImp", copied.ClassName);
        Assert.Equal("TROOA1", copied.Sprite);
        Assert.Equal(20, copied.Width);
        Assert.Null(gc.GetThing(3002));
    }

    [Fact]
    public void SkipsMixinAndExtensionClassesAsPlaceableActors()
    {
        const string zscript = @"
mixin class HelperMixin
{
    Default { Radius 64; }
}
extend class DoomImp
{
    Default { Radius 96; }
}
class RealActor : Actor
{
    Default { Radius 24; }
}";

        var actors = ZScriptParser.Parse(zscript);

        var actor = Assert.Single(actors);
        Assert.Equal("RealActor", actor.ClassName);
        Assert.Equal(24, actor.Radius);
    }

    [Fact]
    public void AppliesZScriptMixinEditorDefaultsToConcreteActors()
    {
        const string zscript = @"
mixin class EditorDefaults
{
    Default
    {
        Radius 32;
        Height 72;
        +SOLID;
    }
    States { Spawn: MIXN A -1; stop; }
}
class MixedActor : Actor
{
    mixin EditorDefaults;
}";

        var actor = ZScriptParser.Parse(zscript).Single();

        Assert.Equal("MixedActor", actor.ClassName);
        Assert.Equal(32, actor.Radius);
        Assert.Equal(72, actor.Height);
        Assert.True(actor.Flags["SOLID"]);
        Assert.Equal("MIXNA0", actor.EditorSprite);
    }

    [Fact]
    public void AppliesZScriptExtensionDefaultsToTargetClass()
    {
        const string zscript = @"
class BaseActor : Actor
{
    Default { Radius 16; Height 32; }
    States { Spawn: BASE A -1; stop; }
}
extend class BaseActor
{
    Default { Height 64; +SOLID; }
    States { Spawn: EXTN A -1; stop; }
}";

        var actor = ZScriptParser.Parse(zscript).Single();

        Assert.Equal("BaseActor", actor.ClassName);
        Assert.Equal(16, actor.Radius);
        Assert.Equal(64, actor.Height);
        Assert.True(actor.Flags["SOLID"]);
        Assert.Equal("EXTNA0", actor.EditorSprite);
    }

    [Fact]
    public void ZScriptExtensionDefaultsOverrideMixinDefaults()
    {
        const string zscript = @"
mixin class BaseMixin
{
    Default { Height 40; }
    States { Spawn: MIXN A -1; stop; }
}
class BaseActor : Actor
{
    mixin BaseMixin;
}
extend class BaseActor
{
    Default { Height 64; }
    States { Spawn: EXTN A -1; stop; }
}";

        var actor = ZScriptParser.Parse(zscript).Single();

        Assert.Equal(64, actor.Height);
        Assert.Equal("EXTNA0", actor.EditorSprite);
    }

    [Fact]
    public void AppliesZScriptRegionCategoriesAndEditorDefaults()
    {
        const string zscript = @"
#region Monsters/Bosses
// $Color 12
// $Sprite ""BOSSA0""
class RegionZScriptActor : Actor
{
    Default { Radius 32; }
}
#endregion";

        var actor = ZScriptParser.Parse(zscript).Single();
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(new[] { actor }, new Dictionary<int, string> { [9100] = "RegionZScriptActor" });

        var info = gc.GetThing(9100);
        Assert.NotNull(info);
        Assert.Equal("Monsters.Bosses", info!.Category);
        Assert.Equal(12, info.Color);
        Assert.Equal("BOSSA0", info.Sprite);
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

    [Fact]
    public void ParsesZScriptIncludesAfterContainingFile()
    {
        const string root = @"
#include ""zscript/included.zs""
class RootActor : Actor { Default { Radius 8; } }";
        const string included = "class IncludedActor : Actor { Default { Radius 16; } }";

        var actors = ZScriptParser.Parse(root, path => path == "zscript/included.zs" ? included : null);

        Assert.Collection(actors,
            actor => Assert.Equal("RootActor", actor.ClassName),
            actor => Assert.Equal("IncludedActor", actor.ClassName));
    }

    [Fact]
    public void ParsesDeferredZScriptIncludesBeforeGzdbSkip()
    {
        const string root = @"
#include ""zscript/included.zs""
//$gzdb_skip
class HiddenActor : Actor { Default { Radius 128; } }";
        const string included = "class IncludedActor : Actor { Default { Radius 16; } }";

        var actor = Assert.Single(ZScriptParser.Parse(root, path => path == "zscript/included.zs" ? included : null));

        Assert.Equal("IncludedActor", actor.ClassName);
    }

    [Fact]
    public void AllowsRelativeClassIncludes()
    {
        const string root = @"
#include ""./base.zs""
class IncludedChild : IncludedBase { }";
        const string included = @"
class IncludedBase : Actor
{
    Default { Radius 24; }
}";

        var actors = ZScriptParser.Parse(root, path => path == "./base.zs" ? included : null);

        var child = actors.Single(a => a.ClassName == "IncludedChild");
        Assert.Equal(24, child.Radius);
    }

    [Theory]
    [InlineData("zscript\\base.zs")]
    [InlineData("/zscript/base.zs")]
    public void RejectsInvalidClassIncludePaths(string includePath)
    {
        string root = "#include \"" + includePath + "\"";

        var actors = ZScriptParser.Parse(root, _ => "class Bad : Actor { Default { Radius 8; } }");

        Assert.Empty(actors);
    }
}
