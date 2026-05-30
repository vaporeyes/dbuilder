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
    public void ParsesHeaderDollarProperties()
    {
        const string text = @"
ACTOR HeaderPropertyThing $Title ""Header Title""
                          $Category ""Header Category""
                          $Sprite ""HEADA0""
                          5003
{
    States { Spawn: TROO A -1 stop }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal(5003, actor.DoomEdNum);
        Assert.Equal("Header Title", actor.Title);
        Assert.Equal("Header Category", actor.Category);
        Assert.Equal("HEADA0", actor.EditorSprite);
    }

    [Fact]
    public void DollarPropertiesKeepUnquotedLineValues()
    {
        const string text = @"
ACTOR EditorTextThing 5002
{
    $Title Fancy Imp
    $Category Large Monsters
    $Sprite PROPA0
    States { Spawn: TROO A -1 stop }
}";
        var actor = DecorateParser.Parse(text)[0];

        Assert.Equal("Fancy Imp", actor.Title);
        Assert.Equal("Large Monsters", actor.Category);
        Assert.Equal("PROPA0", actor.EditorSprite);
    }

    [Fact]
    public void RegionProvidesDefaultCategory()
    {
        const string text = @"
#region Imp Balls
ACTOR RegionThing 5003
{
    Radius 16
}
#endregion";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("Imp Balls", actor.Category);
    }

    [Fact]
    public void DollarCategoryOverridesRegionCategory()
    {
        const string text = @"
#region Imp Balls
ACTOR RegionOverrideThing 5004
{
    //$Category ""Explicit Category""
    Radius 16
}
#endregion";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("Explicit Category", actor.Category);
    }

    [Fact]
    public void RegionCategorySplitsPathSeparators()
    {
        const string text = @"
#region Monsters/Bosses
ACTOR BaronThing 5005
{
    Radius 16
}
#endregion
ACTOR PlainThing 5006
{
    Radius 8
}";
        var actors = DecorateParser.Parse(text);

        Assert.Equal("Monsters.Bosses", actors.Single(a => a.ClassName == "BaronThing").Category);
        Assert.Null(actors.Single(a => a.ClassName == "PlainThing").Category);
    }

    [Fact]
    public void NestedRegionCategoriesAreCombined()
    {
        const string text = @"
#region Monsters
#region Bosses
ACTOR NestedRegionThing 5007
{
    Radius 16
}
#endregion
#endregion";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("Monsters.Bosses", actor.Category);
    }

    [Fact]
    public void RegionEditorKeysProvideThingDefaults()
    {
        const string text = @"
#region Region Defaults
//$Color 4
//$Sprite ""BALLA0""
ACTOR RegionDefaultThing 5008
{
    Radius 16
}
#endregion";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var thing = gc.GetThing(5008);
        Assert.NotNull(thing);
        Assert.Equal("Region Defaults", thing!.Category);
        Assert.Equal(4, thing.Color);
        Assert.Equal("BALLA0", thing.Sprite);
    }

    [Fact]
    public void RegionDollarPropertiesProvideThingDefaults()
    {
        const string text = @"
#region Region Defaults
$Color 4
$Sprite ""BALLA0""
ACTOR RegionDollarDefaultThing 5012
{
    Radius 16
}
#endregion";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var thing = gc.GetThing(5012);
        Assert.NotNull(thing);
        Assert.Equal("Region Defaults", thing!.Category);
        Assert.Equal(4, thing.Color);
        Assert.Equal("BALLA0", thing.Sprite);
    }

    [Fact]
    public void ActorEditorKeysOverrideRegionEditorKeys()
    {
        const string text = @"
#region Region Defaults
//$Color 4
//$Sprite ""BALLA0""
ACTOR RegionOverrideDefaults 5009
{
    $Color 6
    //$Sprite ""ACTRA0""
    Radius 16
}
#endregion";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var thing = gc.GetThing(5009);
        Assert.NotNull(thing);
        Assert.Equal(6, thing!.Color);
        Assert.Equal("ACTRA0", thing.Sprite);
    }

    [Fact]
    public void RegionEditorKeysProvideThingBehaviorDefaults()
    {
        const string text = @"
#region Region Defaults
//$Arrow 1
//$Error 2
//$FixedSize true
//$FixedRotation true
//$AbsoluteZ true
ACTOR RegionBehaviorThing 5010
{
    Radius 16
}
#endregion";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var thing = gc.GetThing(5010);
        Assert.NotNull(thing);
        Assert.True(thing!.Arrow);
        Assert.Equal(2, thing.ErrorCheck);
        Assert.True(thing.FixedSize);
        Assert.True(thing.FixedRotation);
        Assert.True(thing.AbsoluteZ);
    }

    [Fact]
    public void ActorAngledOverridesRegionArrowDefault()
    {
        const string text = @"
#region Region Defaults
//$Arrow 1
ACTOR RegionArrowOverrideThing 5011
{
    $NotAngled
    Radius 16
}
#endregion";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var thing = gc.GetThing(5011);
        Assert.NotNull(thing);
        Assert.False(thing!.Arrow);
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
    public void SeparatedNegativeEditorNumberLeavesDoomEdNumNegative()
    {
        var actor = DecorateParser.Parse("ACTOR AbstractBase - 1 { Radius 16 }")[0];

        Assert.True(actor.DoomEdNum < 0);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void ParsesQuotedHeaderClassNames()
    {
        const string text = @"
ACTOR ""QuotedChild"" : ""QuotedBase"" replaces ""OldThing"" 6001
{
    Radius 16
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("QuotedChild", actor.ClassName);
        Assert.Equal("QuotedBase", actor.ParentName);
        Assert.Equal("OldThing", actor.Replaces);
        Assert.Equal(6001, actor.DoomEdNum);
        Assert.Equal(16, actor.Radius);
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
    public void ChildDoesNotInheritNonSpawnStateSpriteFromRootActor()
    {
        const string text = @"
ACTOR Actor
{
    States { See: POL5 A -1 stop }
}
ACTOR Derived : Actor 7014 { }";
        var derived = DecorateParser.Parse(text).First(x => x.ClassName == "Derived");

        Assert.Null(derived.EditorSprite);
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
    public void ParsesSeparatedFlagSignTokens()
    {
        const string text = @"
ACTOR SeparatedFlags 7004
{
    + SOLID
    - NOGRAVITY
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.True(actor.Flags["SOLID"]);
        Assert.False(actor.Flags["NOGRAVITY"]);
        Assert.False(actor.Properties.ContainsKey("+"));
        Assert.False(actor.Properties.ContainsKey("-"));
    }

    [Fact]
    public void ParsesGamePropertyValuesUntilLineEnd()
    {
        const string text = @"
ACTOR GameScoped 7003
{
    Game Doom, Heretic
    Radius 16
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal(new[] { "doom", "heretic" }, actor.Properties["Game"]);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void IgnoresActionAndNativeDeclarations()
    {
        const string text = @"
ACTOR NativeActor 7005
{
    action native A_CustomAction(int amount);
    native int NativeField;
    Radius 16
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.False(actor.Properties.ContainsKey("action"));
        Assert.False(actor.Properties.ContainsKey("native"));
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void IgnoresUserVariableDeclarations()
    {
        const string text = @"
ACTOR UserVarActor 7006
{
    var int user_score;
    var float user_values[4];
    Radius 16
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.False(actor.Properties.ContainsKey("var"));
        Assert.False(actor.Properties.ContainsKey("user_score"));
        Assert.False(actor.Properties.ContainsKey("user_values"));
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void SkipsTopLevelEnumNativeAndConstDeclarations()
    {
        const string text = @"
enum HelperEnum
{
    actor HiddenActor { Radius 128 }
};
native actor NativeActor { Radius 64 };
const actor ConstActor { Radius 32 };
ACTOR RealActor 7012
{
    Radius 16
}";

        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("RealActor", actor.ClassName);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void ParsesTopLevelDamageTypes()
    {
        const string text = @"
DamageType Fire
{
    Factor = 0.5
}
DamageType ""Ice""
{
}
ACTOR RealActor 7013
{
    Radius 16
}";

        var data = DecorateParser.ParseDocument(text);

        Assert.Contains("Fire", data.DamageTypes);
        Assert.Contains("Ice", data.DamageTypes);
        Assert.Equal("RealActor", data.Actors.Single().ClassName);
    }

    [Fact]
    public void MergeDamageTypesAddsDecorateTypesToConfiguration()
    {
        var config = GameConfiguration.FromText("damagetypes = \"None\";");
        var data = DecorateParser.ParseDocument("DamageType Fire { }");

        config.MergeDamageTypes(data.DamageTypes);

        Assert.Contains("None", config.DamageTypes);
        Assert.Contains("Fire", config.DamageTypes);
    }

    [Fact]
    public void MonsterDirectiveSetsActorFlags()
    {
        const string text = "ACTOR MonsterActor 7007 { Monster }";

        var actor = DecorateParser.Parse(text).Single();

        Assert.True(actor.Flags["shootable"]);
        Assert.True(actor.Flags["countkill"]);
        Assert.True(actor.Flags["solid"]);
        Assert.True(actor.Flags["canpushwalls"]);
        Assert.True(actor.Flags["canusewalls"]);
        Assert.True(actor.Flags["activatemcross"]);
        Assert.True(actor.Flags["canpass"]);
        Assert.True(actor.Flags["ismonster"]);
        Assert.False(actor.Properties.ContainsKey("Monster"));
    }

    [Fact]
    public void ProjectileDirectiveSetsActorFlags()
    {
        const string text = "ACTOR ProjectileActor 7008 { Projectile }";

        var actor = DecorateParser.Parse(text).Single();

        Assert.True(actor.Flags["noblockmap"]);
        Assert.True(actor.Flags["nogravity"]);
        Assert.True(actor.Flags["dropoff"]);
        Assert.True(actor.Flags["missile"]);
        Assert.True(actor.Flags["activateimpact"]);
        Assert.True(actor.Flags["activatepcross"]);
        Assert.True(actor.Flags["noteleport"]);
        Assert.False(actor.Properties.ContainsKey("Projectile"));
    }

    [Fact]
    public void ClearFlagsDirectiveClearsExistingActorFlags()
    {
        const string text = "ACTOR ClearActor 7009 { +SOLID Monster ClearFlags +NOGRAVITY }";

        var actor = DecorateParser.Parse(text).Single();

        Assert.DoesNotContain("solid", actor.Flags.Keys);
        Assert.DoesNotContain("shootable", actor.Flags.Keys);
        Assert.True(actor.Flags["NOGRAVITY"]);
        Assert.False(actor.Properties.ContainsKey("ClearFlags"));
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
    public void ClearArgsPreventsChildFromInheritingParentArgs()
    {
        const string text = @"
ACTOR ArgBase
{
    $Arg0 ""Inherited Target""
}
ACTOR ArgChild : ArgBase 7010
{
    $ClearArgs
    States { Spawn: ARGC A -1 stop }
}";
        var child = DecorateParser.Parse(text).Single(a => a.ClassName == "ArgChild");

        Assert.True(child.Properties.ContainsKey("$ClearArgs"));
        Assert.False(child.Properties.ContainsKey("$Arg0"));

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(new[] { child });

        Assert.Empty(gc.GetThing(7010)!.Args);
    }

    [Fact]
    public void SkipSuperPreventsChildFromInheritingParentArgs()
    {
        const string text = @"
ACTOR ArgBase
{
    $Arg0 ""Inherited Target""
}
ACTOR ArgChild : ArgBase 7011
{
    skip_super
    States { Spawn: ARGC A -1 stop }
}";
        var child = DecorateParser.Parse(text).Single(a => a.ClassName == "ArgChild");

        Assert.True(child.Properties.ContainsKey("skip_super"));
        Assert.False(child.Properties.ContainsKey("$Arg0"));

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(new[] { child });

        Assert.Empty(gc.GetThing(7011)!.Args);
    }

    [Fact]
    public void SkipSuperPreventsParsedParentInheritance()
    {
        const string text = @"
ACTOR Base
{
    //$Category ""Inherited Category""
    +SOLID
    Radius 31
    Height 64
    RenderStyle Add
    States { Spawn: BOSS A -1 stop }
}
ACTOR Derived : Base 7015
{
    skip_super
}";
        var child = DecorateParser.Parse(text).Single(a => a.ClassName == "Derived");

        Assert.True(child.Properties.ContainsKey("skip_super"));
        Assert.Null(child.EditorSprite);
        Assert.Equal(0, child.Radius);
        Assert.Equal(0, child.Height);
        Assert.DoesNotContain("SOLID", child.Flags.Keys);
        Assert.False(child.Properties.ContainsKey("RenderStyle"));
        Assert.Null(child.Category);
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
    public void ParsesStateBlocksWithCastTypes()
    {
        const string text = @"
ACTOR CastStateThing 8001
{
    States(Actor, Item)
    {
    Spawn:
        CSTT A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("CSTTA0", actor.EditorSprite);
    }

    [Fact]
    public void PrefersSpawnStateSpriteOverEarlierNonDisplayState()
    {
        const string text = @"
ACTOR StatePriorityThing 8002
{
    States
    {
    Death:
        DEAT A -1 stop
    Spawn:
        SPWN A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("SPWNA0", actor.EditorSprite);
    }

    [Fact]
    public void PrefersNonEmptyRelevantStateSprite()
    {
        const string text = @"
ACTOR EmptySpawnThing 8003
{
    States
    {
    Spawn:
        TNT1 A 0
        loop
    See:
        SEEE A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("SEEEA0", actor.EditorSprite);
    }

    [Fact]
    public void SkipsZeroDurationStateSpritesWhenBetterExists()
    {
        const string text = @"
ACTOR ZeroDurationSpawnThing 8006
{
    States
    {
    Spawn:
        REAL A 0
        loop
    See:
        SEEE A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("SEEEA0", actor.EditorSprite);
    }

    [Fact]
    public void ParsesStateFrameLightNameFromSelectedSprite()
    {
        const string text = @"
ACTOR LitThing 8007
{
    States
    {
    Spawn:
        LITE A -1 Light(""LITE_LIGHT"")
        stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("LITEA0", actor.EditorSprite);
        Assert.Equal("LITE_LIGHT", actor.LightName);
    }

    [Fact]
    public void UsesGotoTargetForRelevantStateSprite()
    {
        const string text = @"
ACTOR GotoBase
{
    States { Spawn: BASE A -1 stop }
}
ACTOR GotoChild : GotoBase 8008
{
    States
    {
    Death:
        DEAD A -1 stop
    Spawn:
        goto Super::Spawn
    }
}";
        var actor = DecorateParser.Parse(text).Single(a => a.ClassName == "GotoChild");

        Assert.Equal("BASEA0", actor.EditorSprite);
    }

    [Fact]
    public void UsesGotoStateSpriteOffsetForRelevantStateSprite()
    {
        const string text = @"
ACTOR OffsetGotoThing 8009
{
    States
    {
    Ready:
        SKIP A -1
        PICK A -1
    Death:
        DEAD A -1 stop
    Spawn:
        goto Ready+1
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("PICKA0", actor.EditorSprite);
    }

    [Fact]
    public void TrimsEmptyFramesBeforeResolvingGotoSpriteOffset()
    {
        const string text = @"
ACTOR TrimmedGotoThing 8010
{
    States
    {
    Ready:
        TNT1 A 0
        PICK A -1
    Death:
        DEAD A -1 stop
    Spawn:
        goto Ready
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("PICKA0", actor.EditorSprite);
    }

    [Fact]
    public void SkipsPlaceholderStateSprites()
    {
        const string text = @"
ACTOR PlaceholderSpriteThing 8004
{
    States
    {
    Spawn:
        ---- A 0
        #### A 0
        REAL A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("REALA0", actor.EditorSprite);
    }

    [Fact]
    public void AllowsFlowKeywordStateSpriteNames()
    {
        const string text = @"
ACTOR FlowSpriteThing 8005
{
    States
    {
    Spawn:
        FAIL A -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("FAILA0", actor.EditorSprite);
    }

    [Fact]
    public void ParsesQuotedStateSpriteAndFrameTokens()
    {
        const string text = @"
ACTOR QuotedSpriteThing 8006
{
    States
    {
    Spawn:
        ""QTSP"" ""A"" -1 stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("QTSPA0", actor.EditorSprite);
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
    public void MergeActorsParsesSeparatedNegativeNumericProperties()
    {
        const string text = @"
ACTOR NegativePropertyThing 31001
{
    Alpha - 0.5
    Scale - 0.25
    $Arg0 ""Signed Default""
    $Arg0Default - 7
    Radius 24
    Height 48
    States { Spawn: NEGA A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31001);
        Assert.NotNull(info);
        Assert.Equal(0.0, info!.Alpha);
        Assert.Equal(-0.25, info.SpriteScale);
        Assert.Equal(-7, info.Args[0].Default);
        Assert.Equal(-7, info.Args[0].DefaultValue);
    }

    [Fact]
    public void MergeActorsClampsSmallActorRadiusToFixedEditorSize()
    {
        const string text = @"
ACTOR SmallRadiusThing 31002
{
    Radius 1
    Height 48
    States { Spawn: SRAD A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31002);
        Assert.NotNull(info);
        Assert.Equal(14, info!.Width);
        Assert.Equal(48, info.Height);
    }

    [Fact]
    public void MergeActorsNormalizesZeroSpriteScale()
    {
        const string text = @"
ACTOR ZeroScaleThing 31003
{
    Scale 0
    Radius 24
    Height 48
    States { Spawn: ZSCL A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31003);
        Assert.NotNull(info);
        Assert.Equal(1.0, info!.SpriteScale);
    }

    [Fact]
    public void MergeActorsAppliesReplacementActorToExistingThingClass()
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
            fixedrotation = true;
            absolutez = true;
        }
    }
}";
        const string decorate = @"
ACTOR FancyImp replaces DoomImp
{
    Tag ""Fancy Imp""
    Radius 24
    Height 64
    States { Spawn: FIMP A -1 stop }
}";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(3001);
        Assert.NotNull(info);
        Assert.Equal("Fancy Imp", info!.Title);
        Assert.Equal("FancyImp", info.ClassName);
        Assert.Equal("FIMPA0", info.Sprite);
        Assert.Equal(14, info.Width);
        Assert.Equal(64, info.Height);
        Assert.Equal("monsters", info.Category);
        Assert.Equal(4, info.Color);
        Assert.True(info.FixedSize);
        Assert.True(info.FixedRotation);
        Assert.True(info.AbsoluteZ);
    }

    [Fact]
    public void MergeActorsPreservesExistingRenderStyleWhenActorIgnoresRenderStyle()
    {
        const string cfg = @"
thingtypes
{
    decorations
    {
        31004
        {
            title = ""Glow Thing"";
            class = ""GlowThing"";
            renderstyle = ""add"";
        }
    }
}";
        const string decorate = @"
ACTOR GlowThing 31004
{
    RenderStyle Translucent
    $IgnoreRenderStyle true
    States { Spawn: GLOW A -1 stop }
}";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31004);
        Assert.NotNull(info);
        Assert.Equal("add", info!.RenderStyle);
    }

    [Fact]
    public void MergeActorsInheritsConfiguredParentThingDefaults()
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
            arg0
            {
                title = ""Patrol Target"";
                type = 25;
            }
        }
    }
}";
        const string decorate = "ACTOR FancyImp : DoomImp 31006 { }";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31006);
        Assert.NotNull(info);
        Assert.Equal("FancyImp", info!.Title);
        Assert.Equal("monsters", info.Category);
        Assert.Equal("TROOA1", info.Sprite);
        Assert.Equal(14, info.Width);
        Assert.Equal(56, info.Height);
        Assert.Equal(4, info.Color);
        Assert.True(info.FixedSize);
        Assert.Equal("Patrol Target", info.Args[0].Title);
        Assert.Equal(25, info.Args[0].Type);
    }

    [Fact]
    public void MergeActorsKeepsUnquotedDollarArgumentLineValues()
    {
        const string text = @"
ACTOR LineArgThing 31005
{
    $Arg0 Target Thing
    $Arg0Tooltip Pick target thing
    $Arg0TargetClasses MapSpot, PatrolPoint
    $Arg0Str Target Name
    Radius 24
    Height 48
    States { Spawn: COOL A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31005);
        Assert.NotNull(info);
        Assert.Equal("Target Thing", info!.Args[0].Title);
        Assert.Equal("Pick target thing", info.Args[0].ToolTip);
        Assert.Contains("MapSpot", info.Args[0].TargetClasses);
        Assert.Contains("PatrolPoint", info.Args[0].TargetClasses);
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

    [Theory]
    [InlineData("../actors/base.dec")]
    [InlineData("./actors/base.dec")]
    [InlineData("actors\\base.dec")]
    [InlineData("/actors/base.dec")]
    public void RejectsInvalidIncludePaths(string includePath)
    {
        string root = "#include \"" + includePath + "\"";

        var actors = DecorateParser.Parse(root, _ => "ACTOR Bad 32000 { Radius 8 }");

        Assert.Empty(actors);
    }
}
