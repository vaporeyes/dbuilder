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
    public void UnparentedDecorateActorsDefaultToActorParent()
    {
        var actor = DecorateParser.Parse("ACTOR StandaloneThing 6002 { Radius 16 }").Single();

        Assert.Equal("Actor", actor.ParentName);
    }

    [Fact]
    public void SkipsDecorateActorsWithUnexpectedHeaderTokens()
    {
        const string text = @"
ACTOR BadHeader bogus 6003
{
    Radius 64
}
ACTOR GoodHeader 6004
{
    Radius 16
}";

        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("GoodHeader", actor.ClassName);
        Assert.Equal(6004, actor.DoomEdNum);
    }

    [Fact]
    public void SkipsDecorateActorsWithEmptyHeaderNames()
    {
        const string text = @"
ACTOR """" 6005
{
    Radius 64
}
ACTOR EmptyParent : """" 6006
{
    Radius 32
}
ACTOR EmptyReplacement replaces """" 6007
{
    Radius 24
}
ACTOR GoodHeader 6008
{
    Radius 16
}";

        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("GoodHeader", actor.ClassName);
        Assert.Equal(6008, actor.DoomEdNum);
        Assert.Equal(16, actor.Radius);
    }

    [Fact]
    public void KeepsFirstDecorateActorWhenClassIsDuplicated()
    {
        const string text = @"
ACTOR DuplicateThing 6005
{
    Radius 16
}
ACTOR DuplicateThing 6006
{
    Radius 64
}";

        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("DuplicateThing", actor.ClassName);
        Assert.Equal(6005, actor.DoomEdNum);
        Assert.Equal(16, actor.Radius);
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
    public void ParsesScalarUserVariableDeclarationsAsAdditionalFields()
    {
        const string text = @"
ACTOR UserVarActor 7006
{
    var int user_score;
    var float user_speed;
    var float user_values[4];
    Radius 16
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.False(actor.Properties.ContainsKey("var"));
        Assert.False(actor.Properties.ContainsKey("user_score"));
        Assert.False(actor.Properties.ContainsKey("user_speed"));
        Assert.False(actor.Properties.ContainsKey("user_values"));
        Assert.True(actor.UserVariables.ContainsKey("user_score"));
        Assert.True(actor.UserVariables.ContainsKey("user_speed"));
        Assert.False(actor.UserVariables.ContainsKey("user_values"));
        Assert.Equal(16, actor.Radius);

        var gc = new GameConfiguration();
        gc.MergeActors(new[] { actor });
        var thing = gc.GetThing(7006)!;
        Assert.True(thing.HasAdditionalUniversalField("user_score"));
        Assert.True(thing.HasAdditionalUniversalField("user_speed"));
        Assert.False(thing.HasAdditionalUniversalField("user_values"));
        Assert.Equal((int)UniversalType.Integer, gc.UniversalFields["thing"]["user_score"].Type);
        Assert.Equal((int)UniversalType.Float, gc.UniversalFields["thing"]["user_speed"].Type);

        var fields = UniversalFieldEditorValues.ForElement(gc, "thing", new Dictionary<string, object>(), thing.AddUniversalFields);
        Assert.Contains(fields, field => field.Field.Name == "user_score");
        Assert.Contains(fields, field => field.Field.Name == "user_speed");
        Assert.DoesNotContain(fields, field => field.Field.Name == "user_values");
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
    public void SkipsUnknownTopLevelBlocks()
    {
        const string text = @"
helperblock
{
    actor HiddenActor 7012
    {
        Radius 128
    }
}
ACTOR RealActor 7013
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
    public void UsesSingleColonDecorateGotoTargetForRelevantStateSprite()
    {
        const string text = @"
ACTOR SingleColonGotoBase
{
    States { Spawn: BASE A -1 stop }
}
ACTOR SingleColonGotoChild : SingleColonGotoBase 8012
{
    States
    {
    Death:
        DEAD A -1 stop
    Spawn:
        goto Super:Spawn
    }
}";
        var actor = DecorateParser.Parse(text).Single(a => a.ClassName == "SingleColonGotoChild");

        Assert.Equal("BASEA0", actor.EditorSprite);
    }

    [Fact]
    public void InheritsRelevantParentStateSpriteBeforeUnrelatedChildState()
    {
        const string text = @"
ACTOR SpriteParent
{
    States { Spawn: PARS A -1 stop }
}
ACTOR SpriteChild : SpriteParent 8011
{
    States { Death: CHLD A -1 stop }
}";
        var actor = DecorateParser.Parse(text).Single(a => a.ClassName == "SpriteChild");

        Assert.Equal("PARSA0", actor.EditorSprite);
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
    public void UsesQuotedDecorateGotoStateSpriteOffset()
    {
        const string text = @"
ACTOR QuotedOffsetGotoThing 8015
{
    States
    {
    Ready:
        SKIP A -1
        PICK A -1
    Spawn:
        goto ""Ready""+1
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Equal("PICKA0", actor.EditorSprite);
    }

    [Fact]
    public void FollowsTargetGotoWhenStateOffsetExceedsFrames()
    {
        const string text = @"
ACTOR ChainedOffsetGotoThing 8013
{
    States
    {
    Ready:
        SKIP A -1
        goto Pickup
    Pickup:
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
    public void PlaceholderOnlyStateSpritesDoNotProvideEditorSprite()
    {
        const string text = @"
ACTOR PlaceholderOnlySpriteThing 8014
{
    States
    {
    Spawn:
        ---- A -1
        #### A -1
        stop
    }
}";
        var actor = DecorateParser.Parse(text).Single();

        Assert.Null(actor.EditorSprite);
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
    $Arg0Type 14
    $Arg0Default 7
    $Arg0Tooltip ""Pick target\nby tid""
    $Arg0TargetClasses ""MapSpot, PatrolPoint""
    $Arg0RenderStyle Circle
    $Arg0RenderColor ""#2040ff""
    $Arg0MinRange 16
    $Arg0MinRangeColor ""#102030""
    $Arg0MaxRange 256
    $Arg0MaxRangeColor ""#405060""
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
        Assert.Equal(2, info.ErrorCheck);
        Assert.Equal("Target", info.Args[0].Title);
        Assert.Equal(14, info.Args[0].Type);
        Assert.Equal(7, info.Args[0].Default);
        Assert.Equal(7, info.Args[0].DefaultValue);
        Assert.Equal("Pick target" + Environment.NewLine + "by tid" + Environment.NewLine + Environment.NewLine + "Expected range: 16 - 256", info.Args[0].ToolTip);
        Assert.Contains("MapSpot", info.Args[0].TargetClasses);
        Assert.Contains("PatrolPoint", info.Args[0].TargetClasses);
        Assert.Equal("circle", info.Args[0].RenderStyle);
        Assert.Equal(new ArgColor(0x20, 0x40, 0xff, 192), info.Args[0].RenderColor);
        Assert.Equal(16, info.Args[0].MinRange);
        Assert.Equal(new ArgColor(0x10, 0x20, 0x30, 96), info.Args[0].MinRangeColor);
        Assert.Equal(256, info.Args[0].MaxRange);
        Assert.Equal(new ArgColor(0x40, 0x50, 0x60, 96), info.Args[0].MaxRangeColor);
        Assert.True(info.Args[0].Str);
        Assert.Equal("Target Name", info.Args[0].TitleStr);
    }

    [Fact]
    public void MergeActorsIgnoresTargetClassesForNonThingTagArgs()
    {
        const string text = @"
ACTOR NonThingTagArgThing 31008
{
    $Arg0 ""Speed""
    $Arg0Type 1
    $Arg0TargetClasses ""MapSpot, PatrolPoint""
    Radius 24
    Height 48
    States { Spawn: NARG A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31008);
        Assert.NotNull(info);
        Assert.Empty(info!.Args[0].TargetClasses);
    }

    [Fact]
    public void MergeActorsFallsBackUnknownArgumentTypesToInteger()
    {
        const string text = @"
ACTOR UnknownArgTypeThing 31009
{
    $Arg0 ""Unknown""
    $Arg0Type 999
    Radius 24
    Height 48
    States { Spawn: UARG A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31009);
        Assert.NotNull(info);
        Assert.Equal(0, info!.Args[0].Type);
    }

    [Fact]
    public void MergeActorsIgnoresArgRenderHintsWithoutRenderStyle()
    {
        const string text = @"
ACTOR NoArgRenderStyleThing 31010
{
    $Arg0 ""Target""
    $Arg0RenderColor ""#2040ff""
    $Arg0MinRange 16
    $Arg0MinRangeColor ""#102030""
    $Arg0MaxRange 256
    $Arg0MaxRangeColor ""#405060""
    Radius 24
    Height 48
    States { Spawn: NRST A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31010);
        Assert.NotNull(info);
        Assert.Null(info!.Args[0].RenderColor);
        Assert.Equal(0, info.Args[0].MinRange);
        Assert.Null(info.Args[0].MinRangeColor);
        Assert.Equal(0, info.Args[0].MaxRange);
        Assert.Null(info.Args[0].MaxRangeColor);
    }

    [Fact]
    public void MergeActorsParsesInlineActorArgEnum()
    {
        const string text = @"
ACTOR InlineArgEnumThing 31011
{
    $Arg0 ""Mode""
    $Arg0Type 11
    $Arg0Enum { 0 = ""Off""; 1 = ""On""; }
    Radius 24
    Height 48
    States { Spawn: IARG A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31011);
        Assert.NotNull(info);
        var enumMap = gc.GetArgEnum(info!.Args[0]);
        Assert.NotNull(enumMap);
        Assert.Null(info.Args[0].Enum);
        Assert.Equal("Off", enumMap![0]);
        Assert.Equal("On", enumMap[1]);
    }

    [Fact]
    public void MergeActorsFiltersUnsupportedDecorateGames()
    {
        const string text = @"
ACTOR DoomGameThing 31012
{
    Game Doom
    Radius 24
    Height 48
    States { Spawn: DARG A -1 stop }
}
ACTOR HereticGameThing 31013
{
    Game Heretic
    Radius 24
    Height 48
    States { Spawn: HARG A -1 stop }
}
ACTOR NeutralGameThing 31014
{
    Radius 24
    Height 48
    States { Spawn: NARG A -1 stop }
}";
        var gc = GameConfiguration.FromText(@"decorategames = ""doom"";");
        gc.MergeActors(DecorateParser.Parse(text));

        Assert.NotNull(gc.GetThing(31012));
        Assert.Null(gc.GetThing(31013));
        Assert.NotNull(gc.GetThing(31014));
    }

    [Fact]
    public void MergeActorsAppliesDoomEdNumsOverridesFromUnsupportedActors()
    {
        const string text = @"
ACTOR HereticOnlyOverrideThing
{
    Game Heretic
    Radius 24
    Height 48
    //$Title ""Heretic Override""
    States { Spawn: HOVR A -1 stop }
}";
        var gc = GameConfiguration.FromText(@"decorategames = ""doom"";");
        gc.MergeActors(DecorateParser.Parse(text), new Dictionary<int, string>
        {
            [32000] = "HereticOnlyOverrideThing"
        });

        var info = gc.GetThing(32000);
        Assert.NotNull(info);
        Assert.Equal("HereticOnlyOverrideThing", info!.ClassName);
        Assert.Equal("Heretic Override", info.Title);
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
    public void MergeActorsPreservesExistingSpriteWhenThingLocksSprite()
    {
        const string cfg = @"
thingtypes
{
    monsters
    {
        3001
        {
            title = ""Imp"";
            sprite = ""TROOA1"";
            class = ""DoomImp"";
            locksprite = true;
        }
    }
}";
        const string decorate = @"
ACTOR FancyImp replaces DoomImp
{
    States { Spawn: FIMP A -1 stop }
}";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(3001);
        Assert.NotNull(info);
        Assert.True(info!.LockSprite);
        Assert.Equal("TROOA1", info.Sprite);
    }

    [Fact]
    public void MergeActorsUsesUnknownThingSpriteWhenActorHasNoPreview()
    {
        const string decorate = @"
ACTOR NoPreviewThing 31020
{
    Radius 24
    Height 48
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31020);
        Assert.NotNull(info);
        Assert.Equal("internal:unknownthing", info!.Sprite);
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
    public void MergeActorsUsesSelectedStateFrameBrightKeyword()
    {
        const string decorate = @"
ACTOR BrightStateThing 31015
{
    Radius 24
    Height 48
    States { Spawn: BRTS A -1 bright stop }
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31015);
        Assert.NotNull(info);
        Assert.True(info!.Bright);
    }

    [Fact]
    public void MergeActorsRecalculatesBrightFromActorMetadata()
    {
        const string brightDecorate = @"
ACTOR ConfiguredBrightThing 31016
{
    States { Spawn: BRTS A -1 bright stop }
}";
        const string dimDecorate = @"
ACTOR ConfiguredBrightThing 31016
{
    States { Spawn: DIMM A -1 stop }
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(brightDecorate));
        Assert.True(gc.GetThing(31016)!.Bright);

        gc.MergeActors(DecorateParser.Parse(dimDecorate));

        var info = gc.GetThing(31016);
        Assert.NotNull(info);
        Assert.False(info!.Bright);
    }

    [Fact]
    public void MergeActorsRecalculatesLightNameFromActorState()
    {
        const string litDecorate = @"
ACTOR ConfiguredLightThing 31017
{
    States { Spawn: LITE A -1 Light(""LITE_LIGHT"") stop }
}";
        const string dimDecorate = @"
ACTOR ConfiguredLightThing 31017
{
    States { Spawn: DIMM A -1 stop }
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(litDecorate));
        Assert.Equal("LITE_LIGHT", gc.GetThing(31017)!.LightName);

        gc.MergeActors(DecorateParser.Parse(dimDecorate));

        var info = gc.GetThing(31017);
        Assert.NotNull(info);
        Assert.Equal("", info!.LightName);
    }

    [Fact]
    public void MergeActorsMarksObsoleteActorsAndForcesRedColor()
    {
        const string text = @"
ACTOR ObsoleteThing 31007
{
    $Color 7
    $Obsolete ""Use ReplacementThing instead""
    Radius 24
    Height 48
    States { Spawn: OBSO A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31007);
        Assert.NotNull(info);
        Assert.True(info!.IsObsolete);
        Assert.Equal("Use ReplacementThing instead", info.ObsoleteMessage);
        Assert.Equal(4, info.Color);
    }

    [Fact]
    public void MergeActorsPreservesConfiguredBlockingWhenActorIsSolid()
    {
        const string cfg = @"
thingtypes
{
    decorations
    {
        31018
        {
            title = ""Custom Blocking Thing"";
            class = ""CustomBlockingThing"";
            blocking = 1;
        }
    }
}";
        const string decorate = @"
ACTOR CustomBlockingThing 31018
{
    +SOLID
    States { Spawn: BLCK A -1 stop }
}";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31018);
        Assert.NotNull(info);
        Assert.Equal(1, info!.Blocking);
    }

    [Fact]
    public void MergeActorsPreservesConfiguredErrorCheckWhenActorClearsSolid()
    {
        const string cfg = @"
thingtypes
{
    decorations
    {
        31019
        {
            title = ""Custom Error Thing"";
            class = ""CustomErrorThing"";
            blocking = 1;
            error = 2;
        }
    }
}";
        const string decorate = @"
ACTOR CustomErrorThing 31019
{
    -SOLID
    States { Spawn: ERRC A -1 stop }
}";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31019);
        Assert.NotNull(info);
        Assert.Equal(0, info!.Blocking);
        Assert.Equal(2, info.ErrorCheck);
    }

    [Fact]
    public void MergeActorsUsesStuckErrorCheckWhenActorIsSolid()
    {
        const string decorate = @"
ACTOR SolidErrorThing 31022
{
    +SOLID
    States { Spawn: SERR A -1 stop }
}";

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(decorate));

        var info = gc.GetThing(31022);
        Assert.NotNull(info);
        Assert.Equal(2, info!.ErrorCheck);
    }

    [Fact]
    public void MergeActorsUsesHereticDefaultAlpha()
    {
        const string text = @"
ACTOR HereticAlphaThing 31008
{
    DefaultAlpha
    Radius 24
    Height 48
    States { Spawn: HERA A -1 stop }
}";
        var gc = GameConfiguration.FromText("basegame = \"Heretic\";");
        gc.MergeActors(DecorateParser.Parse(text));

        var info = gc.GetThing(31008);
        Assert.NotNull(info);
        Assert.Equal("heretic", gc.BaseGame);
        Assert.Equal(0.4, info!.Alpha);
    }

    [Fact]
    public void MergeActorsPreservesGZDoomRenderFlags()
    {
        const string text = @"
ACTOR WallSpriteThing 31009
{
    +BRIGHT
    +WALLSPRITE
    +ROLLCENTER
    +FORCEXYBILLBOARD
    Radius 24
    Height 48
    States { Spawn: WSPR A -1 stop }
}

ACTOR FlatSpriteThing 31010
{
    +FLATSPRITE
    -ROLLSPRITE
    Radius 24
    Height 48
    States { Spawn: FSPR A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var wall = gc.GetThing(31009);
        Assert.NotNull(wall);
        Assert.True(wall!.Bright);
        Assert.True(wall.XYBillboard);
        Assert.Equal(ThingRenderMode.WallSprite, wall.RenderMode);
        Assert.True(wall.RollSprite);
        Assert.True(wall.RollCenter);

        var flat = gc.GetThing(31010);
        Assert.NotNull(flat);
        Assert.Equal(ThingRenderMode.FlatSprite, flat!.RenderMode);
        Assert.False(flat.RollSprite);
        Assert.False(flat.RollCenter);
    }

    [Fact]
    public void MergeActorsPreservesRenderRadiusBeforeFixedSizeSafety()
    {
        const string text = @"
ACTOR ExplicitRenderRadiusThing 31011
{
    Radius 24
    RenderRadius 40
    Height 48
    States { Spawn: RRAD A -1 stop }
}

ACTOR ZeroRenderRadiusThing 31012
{
    Radius 1
    RenderRadius 0
    Height 48
    States { Spawn: ZRRD A -1 stop }
}";
        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text));

        var explicitRadius = gc.GetThing(31011);
        Assert.NotNull(explicitRadius);
        Assert.Equal(24, explicitRadius!.Width);
        Assert.Equal(40.0, explicitRadius.RenderRadius);

        var zeroRadius = gc.GetThing(31012);
        Assert.NotNull(zeroRadius);
        Assert.Equal(14, zeroRadius!.Width);
        Assert.Equal(1.0, zeroRadius.RenderRadius);
    }

    [Fact]
    public void MergeActorsResolvesDistanceCheckFromIntegerCvar()
    {
        const string text = @"
ACTOR DistanceCheckThing 31013
{
    DistanceCheck db_check_distance
    Radius 24
    Height 48
    States { Spawn: DIST A -1 stop }
}

ACTOR FloatDistanceCheckThing 31014
{
    DistanceCheck db_float_distance
    Radius 24
    Height 48
    States { Spawn: FDST A -1 stop }
}";
        var cvars = CvarInfoParser.Parse("""
            server int db_check_distance = 64;
            server float db_float_distance = 64.0;
            """);

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(text), doomEdNums: null, cvars);

        var info = gc.GetThing(31013);
        Assert.NotNull(info);
        Assert.Equal(4096.0, info!.DistanceCheckSq);

        var invalid = gc.GetThing(31014);
        Assert.NotNull(invalid);
        Assert.Equal(double.MaxValue, invalid!.DistanceCheckSq);
    }

    [Fact]
    public void MergeActorsPreservesDistanceCheckWhenCvarIsUndefined()
    {
        const string validDecorate = @"
ACTOR DistanceCheckThing 31021
{
    DistanceCheck db_check_distance
    States { Spawn: DIST A -1 stop }
}";
        const string missingDecorate = @"
ACTOR DistanceCheckThing 31021
{
    DistanceCheck missing_check_distance
    States { Spawn: DIST A -1 stop }
}";
        var cvars = CvarInfoParser.Parse("server int db_check_distance = 64;");

        var gc = GameConfiguration.FromText("");
        gc.MergeActors(DecorateParser.Parse(validDecorate), doomEdNums: null, cvars);
        Assert.Equal(4096.0, gc.GetThing(31021)!.DistanceCheckSq);

        gc.MergeActors(DecorateParser.Parse(missingDecorate), doomEdNums: null, cvars);

        var info = gc.GetThing(31021);
        Assert.NotNull(info);
        Assert.Equal(4096.0, info!.DistanceCheckSq);
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
    $Arg0Type 14
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
