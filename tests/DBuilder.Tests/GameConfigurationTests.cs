// ABOUTME: Tests for GameConfiguration - parsing thingtypes/linedeftypes/sectortypes into catalogs with inheritance.
// ABOUTME: Inline cfg covers the schema deterministically; an opportunistic test loads a real Doom config when present.

using DBuilder.IO;

namespace DBuilder.Tests;

public class GameConfigurationTests
{
    private const string SampleCfg = """
        thingtypes
        {
            players
            {
                color = 10;
                width = 16;
                height = 56;
                1
                {
                    title = "Player 1 start";
                    sprite = "PLAYA2A8";
                    class = "$Player1Start";
                }
            }
            monsters
            {
                color = 4;
                width = 20;
                height = 56;
                3001
                {
                    title = "Imp";
                    sprite = "TROOA1";
                    class = "DoomImp";
                }
                3002
                {
                    title = "Demon";
                    sprite = "SARGA1";
                    width = 30;
                }
            }
        }

        linedeftypes
        {
            door
            {
                title = "Door";
                1 { title = "Door Open Wait Close (also monsters)"; prefix = "DR"; }
            }
            exit
            {
                title = "Exit";
                11 { title = "Exit Level"; prefix = "S1"; }
            }
        }

        sectortypes
        {
            0 = "None";
            9 = "Secret";
            11 = "Damage and End level";
        }

        linedefflags
        {
            1 = "Impassable";
            2 = "Block Monsters";
            4 = "Double Sided";
            8 = "Upper Unpegged";
        }

        thingflags
        {
            1 = "Easy";
            8 = "Ambush players";
        }
        """;

    [Fact]
    public void ParsesThingTypesWithTitlesAndSprites()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("Imp", gc.ThingTitle(3001));
        var imp = gc.GetThing(3001)!;
        Assert.Equal("TROOA1", imp.Sprite);
        Assert.Equal("DoomImp", imp.ClassName);
        Assert.Equal("monsters", imp.Category);
    }

    [Fact]
    public void ThingsInheritCategoryDefaults()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        // Imp has no explicit width/height/color -> inherits the monsters category defaults.
        var imp = gc.GetThing(3001)!;
        Assert.Equal(20, imp.Width);
        Assert.Equal(56, imp.Height);
        Assert.Equal(4, imp.Color);
        // Demon overrides width but inherits the rest.
        var demon = gc.GetThing(3002)!;
        Assert.Equal(30, demon.Width);
        Assert.Equal(56, demon.Height);
    }

    [Fact]
    public void UnknownThingFallsBackToPlaceholderTitle()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("Unknown (9999)", gc.ThingTitle(9999));
        Assert.Null(gc.GetThing(9999));
    }

    [Fact]
    public void ParsesLinedefActions()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("Door Open Wait Close (also monsters)", gc.LinedefActionTitle(1));
        Assert.Equal("Exit Level", gc.LinedefActionTitle(11));
        Assert.Equal("DR", gc.GetLinedefAction(1)!.Prefix);
        Assert.Equal("Door", gc.GetLinedefAction(1)!.Category);
        Assert.Equal("Exit", gc.GetLinedefAction(11)!.Category);
    }

    [Fact]
    public void LinedefActionZeroIsNone()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("None", gc.LinedefActionTitle(0));
        Assert.Equal("Unknown (777)", gc.LinedefActionTitle(777));
    }

    [Fact]
    public void ParsesSectorEffects()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("None", gc.SectorEffectTitle(0));
        Assert.Equal("Secret", gc.SectorEffectTitle(9));
        Assert.Equal("Damage and End level", gc.SectorEffectTitle(11));
        Assert.Equal("Unknown (5)", gc.SectorEffectTitle(5));
    }

    [Fact]
    public void ParsesFlagDefinitions()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal("Impassable", gc.LinedefFlags[1]);
        Assert.Equal("Double Sided", gc.LinedefFlags[4]);
        Assert.Equal("Ambush players", gc.ThingFlags[8]);
    }

    [Fact]
    public void DescribeLinedefFlagsListsSetBitsInOrder()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        // bits 1 (Impassable) + 4 (Double Sided) set; 2 and 8 clear.
        var names = System.Linq.Enumerable.ToList(gc.DescribeLinedefFlags(1 | 4));
        Assert.Equal(new[] { "Impassable", "Double Sided" }, names);
        Assert.Empty(gc.DescribeLinedefFlags(0));
    }

    [Fact]
    public void DescribeThingFlagsListsSetBits()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        Assert.Equal(new[] { "Easy", "Ambush players" }, System.Linq.Enumerable.ToList(gc.DescribeThingFlags(1 | 8)));
    }

    [Fact]
    public void EmptyConfigYieldsEmptyCatalogs()
    {
        var gc = GameConfiguration.FromText("gameformat = \"DOOM\";");
        Assert.Empty(gc.Things);
        Assert.Empty(gc.LinedefActions);
        Assert.Empty(gc.SectorEffects);
        Assert.Equal("Unknown (1)", gc.ThingTitle(1));
    }

    [Fact]
    public void MergeDehackedAddsThingDefinitions()
    {
        const string text = @"
Thing 2 (Former Human Captain)
ID # = 31000
Initial frame = 10
Width = 1310720
Height = 3670016
#$Category = Monsters

Frame 10
Sprite number = 1
Sprite subnumber = 0

[SPRITES]
1 = POSS
";

        var gc = GameConfiguration.FromText("");
        gc.MergeDehacked(DehackedParser.Parse(text));

        var thing = gc.GetThing(31000);
        Assert.NotNull(thing);
        Assert.Equal("Former Human Captain", gc.ThingTitle(31000));
        Assert.Equal("POSSA0", thing!.Sprite);
        Assert.Equal(20, thing.Width);
        Assert.Equal(56, thing.Height);
        Assert.Equal("Monsters", thing.Category);
    }

    [Fact]
    public void MergeDehackedUpdatesExistingThingAndAppliesSpriteReplacement()
    {
        var gc = GameConfiguration.FromText(SampleCfg);
        const string text = @"
Thing 2 (Renamed Imp)
ID # = 3001
Width = 1572864

Text 4 4
TROOCPOS
";

        gc.MergeDehacked(DehackedParser.Parse(text));

        var thing = gc.GetThing(3001);
        Assert.NotNull(thing);
        Assert.Equal("Renamed Imp", thing!.Title);
        Assert.Equal("CPOSA1", thing.Sprite);
        Assert.Equal(24, thing.Width);
        Assert.Equal(56, thing.Height);
        Assert.Equal("monsters", thing.Category);
    }

    [Fact]
    public void LoadsRealDoomConfigWhenAvailable()
    {
        // Opportunistic: only runs when the UDB asset tree is present on this machine.
        const string path = "/Users/jsh/dev/projects/claude_directed_5/UltimateDoomBuilder/Assets/Common/Configurations/Doom_DoomDoom.cfg";
        if (!System.IO.File.Exists(path)) return;

        var gc = GameConfiguration.FromFile(path);
        // Imp (3001) and Player 1 start (1) are canonical Doom thing numbers.
        Assert.Equal("Imp", gc.GetThing(3001)?.Title);
        Assert.NotNull(gc.GetThing(1));
        // Sector effect 9 is the secret sector in Doom.
        Assert.Contains("Secret", gc.SectorEffectTitle(9), System.StringComparison.OrdinalIgnoreCase);
    }
}
