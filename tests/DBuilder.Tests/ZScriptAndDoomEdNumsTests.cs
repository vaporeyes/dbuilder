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
    public void ParsesSingleQuotedZScriptStateSpriteTokens()
    {
        const string text = @"
class SingleQuotedSpriteActor : Actor
{
    States
    {
    Spawn:
        'SQSP' 'A' -1;
        stop;
    }
}";
        var actor = ZScriptParser.Parse(text).Single();

        Assert.Equal("SQSPA0", actor.EditorSprite);
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
    public void SkipsZScriptClassesWithInvalidHeaderOrdering()
    {
        const string text = @"
class DuplicateParent : Actor : Inventory { Default { Radius 64; } }
class DuplicateReplacement replaces OldThing replaces OtherThing { Default { Radius 32; } }
class ParentAfterReplacement replaces OldThing : Actor { Default { Radius 24; } }
class ParentAfterNative native : Actor { Default { Radius 20; } }
class ValidHeader : Actor replaces OldThing native { Default { Radius 8; } }";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("ValidHeader", actor.ClassName);
        Assert.Equal("Actor", actor.ParentName);
        Assert.Equal("OldThing", actor.Replaces);
        Assert.Equal(8, actor.Radius);
    }

    [Fact]
    public void SkipsZScriptClassesWithInvalidHeaderModifiers()
    {
        const string text = @"
class UnknownModifier : Actor unexpected { Default { Radius 64; } }
class DuplicateFinal : Actor final final { Default { Radius 32; } }
class DuplicateScope : Actor ui play { Default { Radius 24; } }
class DuplicateVersion : Actor version(""4.8"") version(""4.9"") { Default { Radius 20; } }
class MissingModifierArgument : Actor deprecated { Default { Radius 16; } }
class NumericVersion : Actor version(4.8) { Default { Radius 14; } }
class UnsafeString : Actor unsafe(""Actor"") { Default { Radius 12; } }
class DeprecatedMissingComma : Actor deprecated(""4.8"" ""Other"") { Default { Radius 10; } }
class SealedString : Actor sealed(""Child"") { Default { Radius 9; } }
class ValidModifiers : Actor abstract final ui version(""4.8"") deprecated(""4.8"", ""Other"") unsafe(Actor) sealed(ValidChild, OtherChild) { Default { Radius 8; } }";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("ValidModifiers", actor.ClassName);
        Assert.Equal("Actor", actor.ParentName);
        Assert.Equal(8, actor.Radius);
    }

    [Fact]
    public void RejectsZScriptClassesThatViolateFinalOrSealedInheritance()
    {
        const string text = @"
class FinalBase : Actor final { Default { Radius 16; } }
class FinalChild : FinalBase { Default { Radius 32; } }
class SealedBase : Actor sealed(AllowedChild) { Default { Radius 24; } }
class BlockedChild : SealedBase { Default { Radius 48; } }
class BlockedGrandChild : BlockedChild { Default { Radius 64; } }
class AllowedChild : SealedBase { Default { Radius 8; } }";

        var actors = ZScriptParser.Parse(text);

        Assert.Contains(actors, actor => actor.ClassName == "FinalBase");
        Assert.Contains(actors, actor => actor.ClassName == "SealedBase");
        var allowed = Assert.Single(actors, actor => actor.ClassName == "AllowedChild");
        Assert.Equal(8, allowed.Radius);
        Assert.DoesNotContain(actors, actor => actor.ClassName == "FinalChild");
        Assert.DoesNotContain(actors, actor => actor.ClassName == "BlockedChild");
        Assert.DoesNotContain(actors, actor => actor.ClassName == "BlockedGrandChild");
    }

    [Fact]
    public void RejectsZScriptClassesThatInheritFromThemselves()
    {
        const string text = @"
class SelfParent : SelfParent { Default { Radius 64; } }
class ValidAfterSelfParent : Actor { Default { Radius 8; } }";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("ValidAfterSelfParent", actor.ClassName);
        Assert.Equal(8, actor.Radius);
    }

    [Fact]
    public void KeepsFirstZScriptActorWhenClassIsDuplicated()
    {
        const string text = @"
class DuplicateZThing : Actor
{
    Default { Radius 16; }
}
class DuplicateZThing : Actor
{
    Default { Radius 64; }
}";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("DuplicateZThing", actor.ClassName);
        Assert.Equal(16, actor.Radius);
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
    //$UserDefaultValue 7
    int user_value;
    //$UserDefaultValue 1.5
    float user_speed;
    int user_values[4];
    //$UserDefaultValue true
    bool user_flag;
    //$UserDefaultValue ""active""
    string user_label;
    //$UserReinterpret Color
    //$UserDefaultValue ""#112233""
    int user_color;
    //$UserDefaultValue 11
    private uint16 user_small;
    deprecated(""4.8"", ""user_value"") int user_old;
    version(""4.10"") int user_versioned;
    version(4.10) int user_bad_version;
    deprecated(""4.8"",) int user_bad_deprecated;
    action int user_action;
    virtual int user_virtual;
    float[] user_type_values;
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
        Assert.True(actor.UserVariables.ContainsKey("user_value"));
        Assert.True(actor.UserVariables.ContainsKey("user_speed"));
        Assert.True(actor.UserVariables.ContainsKey("user_flag"));
        Assert.True(actor.UserVariables.ContainsKey("user_label"));
        Assert.True(actor.UserVariables.ContainsKey("user_color"));
        Assert.True(actor.UserVariables.ContainsKey("user_small"));
        Assert.True(actor.UserVariables.ContainsKey("user_old"));
        Assert.True(actor.UserVariables.ContainsKey("user_versioned"));
        Assert.False(actor.UserVariables.ContainsKey("user_bad_version"));
        Assert.False(actor.UserVariables.ContainsKey("user_bad_deprecated"));
        Assert.False(actor.UserVariables.ContainsKey("user_action"));
        Assert.False(actor.UserVariables.ContainsKey("user_virtual"));
        Assert.False(actor.UserVariables.ContainsKey("user_values"));
        Assert.False(actor.UserVariables.ContainsKey("user_values[4]"));
        Assert.False(actor.UserVariables.ContainsKey("user_type_values"));
        Assert.Equal(7, actor.UserVariables["user_value"].DefaultValue);
        Assert.Equal(1.5, actor.UserVariables["user_speed"].DefaultValue);
        Assert.Equal(true, actor.UserVariables["user_flag"].DefaultValue);
        Assert.Equal("active", actor.UserVariables["user_label"].DefaultValue);
        Assert.Equal(UniversalType.Color, actor.UserVariables["user_color"].Type);
        Assert.Equal(0x112233, actor.UserVariables["user_color"].DefaultValue);
        Assert.Equal(UniversalType.Integer, actor.UserVariables["user_small"].Type);
        Assert.Equal(11, actor.UserVariables["user_small"].DefaultValue);
        Assert.Equal(UniversalType.Integer, actor.UserVariables["user_old"].Type);
        Assert.Equal(UniversalType.Integer, actor.UserVariables["user_versioned"].Type);

        var gc = new GameConfiguration();
        gc.MergeActors(new[] { actor }, new Dictionary<int, string> { [9001] = "MemberActor" });
        var thing = gc.GetThing(9001)!;
        Assert.True(thing.HasAdditionalUniversalField("user_value"));
        Assert.True(thing.HasAdditionalUniversalField("user_speed"));
        Assert.True(thing.HasAdditionalUniversalField("user_flag"));
        Assert.True(thing.HasAdditionalUniversalField("user_label"));
        Assert.True(thing.HasAdditionalUniversalField("user_color"));
        Assert.True(thing.HasAdditionalUniversalField("user_small"));
        Assert.True(thing.HasAdditionalUniversalField("user_old"));
        Assert.True(thing.HasAdditionalUniversalField("user_versioned"));
        Assert.False(thing.HasAdditionalUniversalField("user_values"));
        Assert.False(thing.HasAdditionalUniversalField("user_type_values"));
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_value"].Type);
        Assert.Equal((int)UniversalType.Float, gc.UniversalFields["thing"]["user_speed"].Type);
        Assert.Equal((int)UniversalType.Boolean, gc.UniversalFields["thing"]["user_flag"].Type);
        Assert.Equal((int)UniversalType.String, gc.UniversalFields["thing"]["user_label"].Type);
        Assert.Equal((int)UniversalType.Color, gc.UniversalFields["thing"]["user_color"].Type);
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_small"].Type);
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_old"].Type);
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_versioned"].Type);
        Assert.Equal(7, gc.UniversalFields["thing"]["user_value"].DefaultValue);
        Assert.Equal(1.5, gc.UniversalFields["thing"]["user_speed"].DefaultValue);
        Assert.Equal(true, gc.UniversalFields["thing"]["user_flag"].DefaultValue);
        Assert.Equal("active", gc.UniversalFields["thing"]["user_label"].DefaultValue);
        Assert.Equal(0x112233, gc.UniversalFields["thing"]["user_color"].DefaultValue);
        Assert.Equal(11, gc.UniversalFields["thing"]["user_small"].DefaultValue);

        var fields = UniversalFieldEditorValues.ForElement(gc, "thing", new Dictionary<string, object>(), thing.AddUniversalFields);
        Assert.Contains(fields, field => field.Field.Name == "user_value");
        Assert.Contains(fields, field => field.Field.Name == "user_speed");
        Assert.Contains(fields, field => field is { Field.Name: "user_flag", Value: true });
        Assert.Contains(fields, field => field is { Field.Name: "user_label", Value: "active" });
        Assert.Contains(fields, field => field is { Field.Name: "user_color", Value: 0x112233 });
        Assert.Contains(fields, field => field is { Field.Name: "user_small", Value: 11 });
        Assert.Contains(fields, field => field.Field.Name == "user_old");
        Assert.Contains(fields, field => field.Field.Name == "user_versioned");
        Assert.DoesNotContain(fields, field => field.Field.Name == "user_values");
        Assert.DoesNotContain(fields, field => field.Field.Name == "user_type_values");
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
    public void InheritedZScriptUserVariablesOverrideShadowingChildFields()
    {
        const string text = @"
class ParentUserActor : Actor
{
    //$UserDefaultValue 5
    int user_value;
}
class ChildUserActor : ParentUserActor
{
    //$UserDefaultValue 1.5
    float user_value;
}";

        var child = ZScriptParser.Parse(text).Single(actor => actor.ClassName == "ChildUserActor");

        Assert.True(child.UserVariables.ContainsKey("user_value"));
        Assert.Equal(UniversalType.Integer, child.UserVariables["user_value"].Type);
        Assert.Equal(5, child.UserVariables["user_value"].DefaultValue);

        var gc = new GameConfiguration();
        gc.MergeActors(new[] { child }, new Dictionary<int, string> { [9101] = "ChildUserActor" });
        var thing = gc.GetThing(9101)!;
        Assert.True(thing.HasAdditionalUniversalField("user_value"));
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_value"].Type);
        Assert.Equal(5, gc.UniversalFields["thing"]["user_value"].DefaultValue);
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
    public void ChildDoesNotInheritNonSpawnStateSpriteFromRootActor()
    {
        const string text = @"
class Actor
{
    States { See: POL5 A -1; stop; }
}
class DerivedActor : Actor { }";

        var actor = ZScriptParser.Parse(text).Single(a => a.ClassName == "DerivedActor");

        Assert.Null(actor.EditorSprite);
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
    public void MergesZScriptActorArgumentRenderColors()
    {
        const string zscript = @"
class ArgColorZThing : Actor
{
    Default
    {
        Radius 24;
        Height 48;
        $Arg0 ""Target"";
        $Arg0RenderStyle Circle;
        $Arg0RenderColor ""#2040ff"";
        $Arg0MinRange 16;
        $Arg0MinRangeColor ""#102030"";
        $Arg0MaxRange 256;
        $Arg0MaxRangeColor ""#405060"";
    }
    States { Spawn: ACZT A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9051 = ArgColorZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9051);
        Assert.NotNull(info);
        Assert.Equal("circle", info!.Args[0].RenderStyle);
        Assert.Equal(new ArgColor(0x20, 0x40, 0xff, 192), info.Args[0].RenderColor);
        Assert.Equal(16, info.Args[0].MinRange);
        Assert.Equal(new ArgColor(0x10, 0x20, 0x30, 96), info.Args[0].MinRangeColor);
        Assert.Equal(256, info.Args[0].MaxRange);
        Assert.Equal(new ArgColor(0x40, 0x50, 0x60, 96), info.Args[0].MaxRangeColor);
    }

    [Fact]
    public void MergesZScriptActorsWithSpacedNegativeNumericDefaults()
    {
        const string zscript = @"
class NegativeDefaultThing : Actor
{
    Default
    {
        Radius 24;
        Height 48;
        Alpha - 0.5;
        Scale - 0.25;
    }
    States { Spawn: NZSC A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9052 = NegativeDefaultThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9052);
        Assert.NotNull(info);
        Assert.Equal(0.0, info!.Alpha);
        Assert.Equal(-0.25, info.SpriteScale);
    }

    [Fact]
    public void MergesZScriptActorsClampsSmallRadiusToFixedEditorSize()
    {
        const string zscript = @"
class SmallRadiusZThing : Actor
{
    Default { Radius 1; Height 48; }
    States { Spawn: SZRD A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9053 = SmallRadiusZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9053);
        Assert.NotNull(info);
        Assert.Equal(14, info!.Width);
        Assert.Equal(48, info.Height);
    }

    [Fact]
    public void MergesZScriptActorsNormalizesZeroSpriteScale()
    {
        const string zscript = @"
class ZeroScaleZThing : Actor
{
    Default { Radius 24; Height 48; Scale 0; }
    States { Spawn: ZZSC A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9054 = ZeroScaleZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9054);
        Assert.NotNull(info);
        Assert.Equal(1.0, info!.SpriteScale);
    }

    [Fact]
    public void MergesZScriptActorsPreservesExistingRenderStyleWhenActorIgnoresRenderStyle()
    {
        const string cfg = @"
thingtypes
{
    decorations
    {
        9055
        {
            title = ""Glow ZThing"";
            class = ""GlowZThing"";
            renderstyle = ""add"";
        }
    }
}";
        const string zscript = @"
class GlowZThing : Actor
{
    Default { RenderStyle Translucent; $IgnoreRenderStyle true; }
    States { Spawn: GLOW A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9055 = GlowZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9055);
        Assert.NotNull(info);
        Assert.Equal("add", info!.RenderStyle);
    }

    [Fact]
    public void MergesZScriptActorsUsesSelectedStateFrameBrightKeyword()
    {
        const string zscript = @"
class BrightStateZThing : Actor
{
    Default { Radius 24; Height 48; }
    States { Spawn: BRTZ A -1 Bright; Stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9066 = BrightStateZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9066);
        Assert.NotNull(info);
        Assert.True(info!.Bright);
    }

    [Fact]
    public void ZScriptInheritsRelevantParentStateSpriteBeforeUnrelatedChildState()
    {
        const string zscript = @"
class SpriteParentZThing : Actor
{
    States { Spawn: PARZ A -1; Stop; }
}
class SpriteChildZThing : SpriteParentZThing
{
    States { Death: CHLZ A -1; Stop; }
}";
        var actor = ZScriptParser.Parse(zscript).Single(a => a.ClassName == "SpriteChildZThing");

        Assert.Equal("PARZA0", actor.EditorSprite);
    }

    [Fact]
    public void DoesNotResolveZScriptGotoWithoutRequiredSemicolon()
    {
        const string zscript = @"
class GotoTargetZThing : Actor
{
    States { Spawn: TARG A -1; Stop; }
}
class MissingGotoSemicolonZThing : Actor
{
    States { Spawn: goto GotoTargetZThing::Spawn }
}";
        var actor = ZScriptParser.Parse(zscript).Single(a => a.ClassName == "MissingGotoSemicolonZThing");

        Assert.Null(actor.EditorSprite);
    }

    [Fact]
    public void MergesZScriptActorsMarksObsoleteActorsAndForcesRedColor()
    {
        const string zscript = @"
class ObsoleteZThing : Actor
{
    Default { $Color 7; $Obsolete ""Use ReplacementZThing instead""; Radius 24; Height 48; }
    States { Spawn: OZTH A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9056 = ObsoleteZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9056);
        Assert.NotNull(info);
        Assert.True(info!.IsObsolete);
        Assert.Equal("Use ReplacementZThing instead", info.ObsoleteMessage);
        Assert.Equal(4, info.Color);
    }

    [Fact]
    public void MergesZScriptActorsUsesHereticDefaultAlpha()
    {
        const string zscript = @"
class HereticAlphaZThing : Actor
{
    Default { DefaultAlpha; Radius 24; Height 48; }
    States { Spawn: HAZT A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9057 = HereticAlphaZThing }").DoomEdNums;

        var gc = GameConfiguration.FromText("basegame = \"Heretic\";");
        gc.MergeActors(actors, doomEdNums);

        var info = gc.GetThing(9057);
        Assert.NotNull(info);
        Assert.Equal("heretic", gc.BaseGame);
        Assert.Equal(0.4, info!.Alpha);
    }

    [Fact]
    public void MergesZScriptActorsPreservesGZDoomRenderFlags()
    {
        const string zscript = @"
class WallSpriteZThing : Actor
{
    Default { +BRIGHT; +WALLSPRITE; +ROLLCENTER; +FORCEXYBILLBOARD; Radius 24; Height 48; }
    States { Spawn: WSPZ A -1; stop; }
}

class FlatSpriteZThing : Actor
{
    Default { +FLATSPRITE; -ROLLSPRITE; Radius 24; Height 48; }
    States { Spawn: FSPZ A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("""
            DoomEdNums
            {
                9058 = WallSpriteZThing
                9059 = FlatSpriteZThing
            }
            """).DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var wall = gc.GetThing(9058);
        Assert.NotNull(wall);
        Assert.True(wall!.Bright);
        Assert.True(wall.XYBillboard);
        Assert.Equal(ThingRenderMode.WallSprite, wall.RenderMode);
        Assert.True(wall.RollSprite);
        Assert.True(wall.RollCenter);

        var flat = gc.GetThing(9059);
        Assert.NotNull(flat);
        Assert.Equal(ThingRenderMode.FlatSprite, flat!.RenderMode);
        Assert.False(flat.RollSprite);
        Assert.False(flat.RollCenter);
    }

    [Fact]
    public void MergesZScriptActorsPreservesRenderRadiusBeforeFixedSizeSafety()
    {
        const string zscript = @"
class ExplicitRenderRadiusZThing : Actor
{
    Default { Radius 24; RenderRadius 40; Height 48; }
    States { Spawn: RRZS A -1; stop; }
}

class ZeroRenderRadiusZThing : Actor
{
    Default { Radius 1; RenderRadius 0; Height 48; }
    States { Spawn: ZRZS A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("""
            DoomEdNums
            {
                9062 = ExplicitRenderRadiusZThing
                9063 = ZeroRenderRadiusZThing
            }
            """).DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums);

        var explicitRadius = gc.GetThing(9062);
        Assert.NotNull(explicitRadius);
        Assert.Equal(24, explicitRadius!.Width);
        Assert.Equal(40.0, explicitRadius.RenderRadius);

        var zeroRadius = gc.GetThing(9063);
        Assert.NotNull(zeroRadius);
        Assert.Equal(14, zeroRadius!.Width);
        Assert.Equal(1.0, zeroRadius.RenderRadius);
    }

    [Fact]
    public void MergesZScriptActorsResolvesDistanceCheckFromIntegerCvar()
    {
        const string zscript = @"
class DistanceCheckZThing : Actor
{
    Default { DistanceCheck db_check_distance; Radius 24; Height 48; }
    States { Spawn: DCZT A -1; stop; }
}

class MissingDistanceCheckZThing : Actor
{
    Default { DistanceCheck missing_distance; Radius 24; Height 48; }
    States { Spawn: MDZT A -1; stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var doomEdNums = MapInfo.Parse("""
            DoomEdNums
            {
                9064 = DistanceCheckZThing
                9065 = MissingDistanceCheckZThing
            }
            """).DoomEdNums;
        var cvars = CvarInfoParser.Parse("server int db_check_distance = 32;");

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(actors, doomEdNums, cvars);

        var info = gc.GetThing(9064);
        Assert.NotNull(info);
        Assert.Equal(1024.0, info!.DistanceCheckSq);

        var missing = gc.GetThing(9065);
        Assert.NotNull(missing);
        Assert.Equal(double.MaxValue, missing!.DistanceCheckSq);
    }

    [Fact]
    public void MergesZScriptStateLightName()
    {
        const string zscript = @"
class LitZScriptThing : Actor
{
    States { Spawn: LITZ A -1 Light(""LITZ_LIGHT""); stop; }
}";
        var actors = ZScriptParser.Parse(zscript);
        var gc = GameConfiguration.FromText("");

        gc.MergeActors(actors, new Dictionary<int, string> { [9060] = "LitZScriptThing" });

        var info = gc.GetThing(9060);
        Assert.NotNull(info);
        Assert.Equal("LITZA0", info!.Sprite);
        Assert.Equal("LITZ_LIGHT", info.LightName);
    }

    [Fact]
    public void StopsZScriptStateLightScanAtFrameSemicolon()
    {
        const string zscript = @"
class FrameBoundaryLightThing : Actor
{
    States
    {
    Spawn:
        LITA A -1;
        LITB A -1 Light(""SECOND_FRAME_LIGHT"");
        stop;
    }
}";
        var actors = ZScriptParser.Parse(zscript);
        var gc = GameConfiguration.FromText("");

        gc.MergeActors(actors, new Dictionary<int, string> { [9061] = "FrameBoundaryLightThing" });

        var info = gc.GetThing(9061);
        Assert.NotNull(info);
        Assert.Equal("LITAA0", info!.Sprite);
        Assert.Equal("", info.LightName);
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
    public void DoomEdNumNoneOverridesDecorateActorNumbers()
    {
        const string decorate = @"
ACTOR DisabledDecorateThing 31015
{
    Radius 24
    Height 48
    States { Spawn: DISA A -1 stop }
}";
        var doomEdNums = MapInfo.Parse("DoomEdNums { 31015 = none }").DoomEdNums;

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(decorate), doomEdNums);

        Assert.Null(gc.GetThing(31015));
    }

    [Fact]
    public void MergesZScriptActorsWithConfiguredParentThingDefaults()
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
            fixedsize = true;
        }
    }
}";
        const string zscript = "class FancyZImp : DoomImp { }";
        var doomEdNums = MapInfo.Parse("DoomEdNums { 9062 = FancyZImp }").DoomEdNums;

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(ZScriptParser.Parse(zscript), doomEdNums);

        var info = gc.GetThing(9062);
        Assert.NotNull(info);
        Assert.Equal("FancyZImp", info!.Title);
        Assert.Equal("monsters", info.Category);
        Assert.Equal("TROOA1", info.Sprite);
        Assert.Equal(14, info.Width);
        Assert.Equal(56, info.Height);
        Assert.Equal(4, info.Color);
        Assert.True(info.FixedSize);
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
    public void ZScriptMixinWithoutSpawnStateDoesNotSupplyEditorSprite()
    {
        const string zscript = @"
mixin class DeathOnlyMixin
{
    States { Death: MIXD A -1; Stop; }
}
class MixedActor : Actor
{
    mixin DeathOnlyMixin;
}";

        var actor = ZScriptParser.Parse(zscript).Single();

        Assert.Null(actor.EditorSprite);
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
    public void ZScriptExtensionWithoutSpawnStateDoesNotReplaceEditorSprite()
    {
        const string zscript = @"
class BaseActor : Actor
{
    States { Spawn: BASE A -1; Stop; }
}
extend class BaseActor
{
    States { Death: EXTD A -1; Stop; }
}";

        var actor = ZScriptParser.Parse(zscript).Single();

        Assert.Equal("BASEA0", actor.EditorSprite);
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

    [Fact]
    public void AllowsTopLevelZScriptVersionDeclaration()
    {
        const string text = @"
version ""4.8""
class VersionedActor : Actor
{
    Default { Radius 8; }
}";

        var actor = Assert.Single(ZScriptParser.Parse(text));

        Assert.Equal("VersionedActor", actor.ClassName);
        Assert.Equal(8, actor.Radius);
    }

    [Fact]
    public void UnknownTopLevelPreprocessorDirectiveStopsZScriptParsing()
    {
        const string text = @"
#library ""helpers""
class AfterUnknownDirective : Actor
{
    Default { Radius 8; }
}";

        var actors = ZScriptParser.Parse(text);

        Assert.Empty(actors);
    }

    [Fact]
    public void UnknownTopLevelZScriptIdentifierStopsParsing()
    {
        const string text = @"
library ""helpers""
class AfterUnknownIdentifier : Actor
{
    Default { Radius 8; }
}";

        var actors = ZScriptParser.Parse(text);

        Assert.Empty(actors);
    }
}
