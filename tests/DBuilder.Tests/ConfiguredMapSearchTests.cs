// ABOUTME: Tests game-configuration-aware map find/replace behavior outside direct tag searches.
// ABOUTME: Covers UDB generalized action/effect matching while keeping core MapSearch config-free.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ConfiguredMapSearchTests
{
    private const string Cfg = """
        generalizedlinedefs = true;
        generalizedsectors = true;
        gen_linedeftypes
        {
            floors
            {
                title = "Floor";
                offset = 24576;
                length = 8192;
                trigger
                {
                    0 = "Walk Over Once";
                    2 = "Switch Once";
                    3 = "Switch Repeatable";
                }
                speed
                {
                    0 = "Slow";
                    8 = "Normal";
                    16 = "Fast";
                    24 = "Turbo";
                }
                direction
                {
                    0 = "Down";
                    64 = "Up";
                }
            }
        }
        sectortypes
        {
            9 = "Secret";
            11 = "End damage";
        }
        gen_sectortypes
        {
            damage
            {
                0 = "None";
                32 = "5 per second";
                64 = "10 per second";
                96 = "20 per second";
            }
            friction
            {
                0 = "Disabled";
                256 = "Friction";
            }
        }
        """;

    private const string FlagCfg = """
        linedefactivations
        {
            playeruse = "Player use";
            monsteruse = "Monster use";
        }
        sidedefflags
        {
            clipmidtex = "Clip middle";
            wrapmidtex = "Wrap middle";
        }
        sectorflags
        {
            secret = "Secret";
            damagehazard = "Damage hazard";
        }
        thingflags
        {
            ambush = "Ambush";
            skill2 = "Skill 2";
        }
        """;

    [Fact]
    public void ReplaceAnyTextureOrFlatHonorsMixedTexturesAndFlatsConfig()
    {
        var config = GameConfiguration.FromText("mixtexturesflats = true;");
        var map = BuildMap();
        map.Sectors[0].FloorTexture = "FLOOR4_8";
        map.Linedefs[0].Front!.MidTexture = "FLOOR4_8";

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.TextureOrFlat, "FLOOR4_8", "STONE1", config);

        Assert.Equal(2, changed);
        Assert.Equal("STONE1", map.Sectors[0].FloorTexture);
        Assert.Equal("STONE1", map.Linedefs[0].Front!.MidTexture);
    }

    [Fact]
    public void ReplaceAnyTextureOrFlatIsDisabledWithoutMixedTexturesAndFlatsConfig()
    {
        var config = GameConfiguration.FromText("");
        var map = BuildMap();
        map.Sectors[0].FloorTexture = "FLOOR4_8";
        map.Linedefs[0].Front!.MidTexture = "FLOOR4_8";

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.TextureOrFlat, "FLOOR4_8", "STONE1", config);

        Assert.Equal(0, changed);
        Assert.Equal("FLOOR4_8", map.Sectors[0].FloorTexture);
        Assert.Equal("FLOOR4_8", map.Linedefs[0].Front!.MidTexture);
    }

    [Fact]
    public void ReplaceTextureHonorsConfiguredLongTextureNames()
    {
        var config = GameConfiguration.FromText("longtexturenames = true;");
        var map = BuildMap();
        map.Linedefs[0].Front!.MidTexture = "STARTAN3";

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.Texture, "STARTAN3", "LONGTEX01", config);

        Assert.Equal(1, changed);
        Assert.Equal("LONGTEX01", map.Linedefs[0].Front!.MidTexture);
    }

    [Fact]
    public void ReplaceThingTypeHonorsUdmfThingTypeRange()
    {
        var config = GameConfiguration.FromText("formatinterface = \"UniversalMapSetIO\";");
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.ThingType, "3001", "32768", config);

        Assert.Equal(1, changed);
        Assert.Equal(32768, map.Things[0].Type);
    }

    [Fact]
    public void ReplaceFlagsRejectsUnknownConfiguredReplacementFlags()
    {
        var config = GameConfiguration.FromText(FlagCfg);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);
        map.Linedefs[0].SetFlag("playeruse", true);
        map.Sidedefs[0].SetFlag("clipmidtex", true);
        map.Sectors[0].SetFlag("secret", true);
        map.Things[0].SetFlag("ambush", true);

        Assert.Equal(0, ConfiguredMapSearch.Replace(map, FindCategory.LinedefFlags, "playeruse", "missing", config));
        Assert.Equal(0, ConfiguredMapSearch.Replace(map, FindCategory.SidedefFlags, "clipmidtex", "missing", config));
        Assert.Equal(0, ConfiguredMapSearch.Replace(map, FindCategory.SectorFlags, "secret", "missing", config));
        Assert.Equal(0, ConfiguredMapSearch.Replace(map, FindCategory.ThingFlags, "ambush", "missing", config));

        Assert.False(map.Linedefs[0].IsFlagSet("missing"));
        Assert.False(map.Sidedefs[0].IsFlagSet("missing"));
        Assert.False(map.Sectors[0].IsFlagSet("missing"));
        Assert.False(map.Things[0].IsFlagSet("missing"));
    }

    [Fact]
    public void ReplaceFlagsAcceptsKnownConfiguredReplacementFlags()
    {
        var config = GameConfiguration.FromText(FlagCfg);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);
        map.Linedefs[0].SetFlag("playeruse", true);
        map.Sidedefs[0].SetFlag("clipmidtex", true);
        map.Sectors[0].SetFlag("secret", true);
        map.Things[0].SetFlag("ambush", true);

        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.LinedefFlags, "playeruse", "monsteruse", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.SidedefFlags, "clipmidtex", "wrapmidtex", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.SectorFlags, "secret", "damagehazard", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.ThingFlags, "ambush", "skill2", config));

        Assert.True(map.Linedefs[0].IsFlagSet("monsteruse"));
        Assert.True(map.Sidedefs[0].IsFlagSet("wrapmidtex"));
        Assert.True(map.Sectors[0].IsFlagSet("damagehazard"));
        Assert.True(map.Things[0].IsFlagSet("skill2"));
    }

    [Fact]
    public void FindFlagsIgnoresUnknownConfiguredFindFlagsWhenKnownFlagsRemain()
    {
        var config = GameConfiguration.FromText(FlagCfg);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);
        map.Linedefs[0].SetFlag("playeruse", true);
        map.Sidedefs[0].SetFlag("clipmidtex", true);
        map.Sectors[0].SetFlag("secret", true);
        map.Things[0].SetFlag("ambush", true);

        Assert.Equal(1, ConfiguredMapSearch.Find(map, FindCategory.LinedefFlags, "playeruse, missing", config).Count);
        Assert.Equal(1, ConfiguredMapSearch.Find(map, FindCategory.SidedefFlags, "clipmidtex, missing", config).Count);
        Assert.Equal(1, ConfiguredMapSearch.Find(map, FindCategory.SectorFlags, "secret, missing", config).Count);
        Assert.Equal(1, ConfiguredMapSearch.Find(map, FindCategory.ThingFlags, "ambush, missing", config).Count);
    }

    [Fact]
    public void ReplaceFlagsIgnoresUnknownConfiguredFindFlagsWhenKnownFlagsRemain()
    {
        var config = GameConfiguration.FromText(FlagCfg);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);
        map.Linedefs[0].SetFlag("playeruse", true);
        map.Sidedefs[0].SetFlag("clipmidtex", true);
        map.Sectors[0].SetFlag("secret", true);
        map.Things[0].SetFlag("ambush", true);

        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.LinedefFlags, "playeruse, missing", "monsteruse", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.SidedefFlags, "clipmidtex, missing", "wrapmidtex", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.SectorFlags, "secret, missing", "damagehazard", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.ThingFlags, "ambush, missing", "skill2", config));

        Assert.True(map.Linedefs[0].IsFlagSet("monsteruse"));
        Assert.True(map.Sidedefs[0].IsFlagSet("wrapmidtex"));
        Assert.True(map.Sectors[0].IsFlagSet("damagehazard"));
        Assert.True(map.Things[0].IsFlagSet("skill2"));
    }

    [Fact]
    public void FindFlagsWithOnlyUnknownConfiguredFlagsMatchesNothing()
    {
        var config = GameConfiguration.FromText(FlagCfg);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);

        Assert.Equal(0, ConfiguredMapSearch.Find(map, FindCategory.LinedefFlags, "missing", config).Count);
        Assert.Equal(0, ConfiguredMapSearch.Find(map, FindCategory.SidedefFlags, "missing", config).Count);
        Assert.Equal(0, ConfiguredMapSearch.Find(map, FindCategory.SectorFlags, "missing", config).Count);
        Assert.Equal(0, ConfiguredMapSearch.Find(map, FindCategory.ThingFlags, "missing", config).Count);
    }

    [Fact]
    public void CategoryDescriptorsHideUnsupportedDoomFormatCategories()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "DoomMapSetIO";
            linedefactivations { playeruse = "Player use"; }
            """);

        var categories = ConfiguredMapSearch.CategoryDescriptors(config)
            .Select(descriptor => descriptor.Category)
            .ToHashSet();

        Assert.Contains(FindCategory.LinedefTag, categories);
        Assert.DoesNotContain(FindCategory.ThingTag, categories);
        Assert.DoesNotContain(FindCategory.ThingActionArguments, categories);
        Assert.DoesNotContain(FindCategory.LinedefSectorReference, categories);
        Assert.DoesNotContain(FindCategory.AnyUdmfField, categories);
        Assert.DoesNotContain(FindCategory.SidedefFlags, categories);
        Assert.DoesNotContain(FindCategory.SectorFlags, categories);
        Assert.DoesNotContain(FindCategory.ThingFlags, categories);
    }

    [Fact]
    public void CategoryDescriptorsHideUnsupportedHexenFormatCategories()
    {
        var config = GameConfiguration.FromText("""
            formatinterface = "HexenMapSetIO";
            thingflags { skill1 = "Skill 1"; }
            """);

        var categories = ConfiguredMapSearch.CategoryDescriptors(config)
            .Select(descriptor => descriptor.Category)
            .ToHashSet();

        Assert.DoesNotContain(FindCategory.LinedefTag, categories);
        Assert.Contains(FindCategory.ThingTag, categories);
        Assert.Contains(FindCategory.ThingActionArguments, categories);
        Assert.Contains(FindCategory.LinedefSectorReference, categories);
        Assert.DoesNotContain(FindCategory.AnyUdmfField, categories);
        Assert.Contains(FindCategory.ThingFlags, categories);
    }

    [Fact]
    public void CategoryDescriptorsKeepUdmfAndConfiguredFlagCategories()
    {
        var config = GameConfiguration.FromText("formatinterface = \"UniversalMapSetIO\";\n" + FlagCfg);

        var categories = ConfiguredMapSearch.CategoryDescriptors(config)
            .Select(descriptor => descriptor.Category)
            .ToHashSet();

        Assert.Contains(FindCategory.LinedefTag, categories);
        Assert.Contains(FindCategory.ThingTag, categories);
        Assert.Contains(FindCategory.ThingActionArguments, categories);
        Assert.Contains(FindCategory.ThingThingReference, categories);
        Assert.Contains(FindCategory.AnyUdmfField, categories);
        Assert.Contains(FindCategory.SidedefFlags, categories);
        Assert.Contains(FindCategory.SectorFlags, categories);
        Assert.Contains(FindCategory.ThingFlags, categories);
    }

    [Fact]
    public void FindLinedefActionMatchesGeneralizedActionsWithSharedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8 + 64;
        map.Linedefs[1].Action = 24576 + 3 + 16;
        map.Linedefs[2].Action = 24576 + 8;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.LinedefAction, (24576 + 2).ToString(), config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Linedefs[0].Selected);
        Assert.True(map.Linedefs[1].Selected);
        Assert.False(map.Linedefs[2].Selected);
    }

    [Fact]
    public void ReplaceLinedefActionArgumentsMatchesGeneralizedActionsWithSharedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8 + 64;
        map.Linedefs[0].Args[0] = 17;
        map.Linedefs[1].Action = 24576 + 3 + 16;
        map.Linedefs[1].Args[0] = 17;
        map.Linedefs[2].Action = 24576 + 8;
        map.Linedefs[2].Args[0] = 17;

        int changed = ConfiguredMapSearch.Replace(
            map,
            FindCategory.LinedefActionArguments,
            (24576 + 2) + " 17",
            "80 23",
            config);

        Assert.Equal(2, changed);
        Assert.Equal(80, map.Linedefs[0].Action);
        Assert.Equal(23, map.Linedefs[0].Args[0]);
        Assert.Equal(80, map.Linedefs[1].Action);
        Assert.Equal(23, map.Linedefs[1].Args[0]);
        Assert.Equal(24576 + 8, map.Linedefs[2].Action);
        Assert.Equal(17, map.Linedefs[2].Args[0]);
    }

    [Fact]
    public void ReplaceActionArgumentsWritesArg0StringOnlyWhenReplacementActionSupportsIt()
    {
        var config = GameConfiguration.FromText("""
            linedeftypes
            {
                scripts
                {
                    80
                    {
                        title = "Script string";
                        arg0 { str = true; }
                    }
                    82
                    {
                        title = "Script number";
                        arg0 { title = "Script"; }
                    }
                }
            }
            """);
        var map = BuildMap();
        map.AddThing(new Vector2D(16, 16), 3001);
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].Fields["arg0str"] = "CurrentLine";
        map.Linedefs[0].Args[1] = 5;
        map.Linedefs[1].Action = 80;
        map.Linedefs[1].Fields["arg0str"] = "CurrentBlockedLine";
        map.Linedefs[1].Args[1] = 5;
        map.Things[0].Action = 80;
        map.Things[0].Fields["arg0str"] = "CurrentThing";
        map.Things[0].Args[1] = 5;

        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 CurrentLine 5", "80 NextLine 9", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.LinedefActionArguments, "80 CurrentBlockedLine 5", "82 BlockedLine 9", config));
        Assert.Equal(1, ConfiguredMapSearch.Replace(map, FindCategory.ThingActionArguments, "80 CurrentThing 5", "82 BlockedThing 9", config));

        Assert.Equal("NextLine", map.Linedefs[0].Fields["arg0str"]);
        Assert.Equal(9, map.Linedefs[0].Args[1]);
        Assert.Equal("CurrentBlockedLine", map.Linedefs[1].Fields["arg0str"]);
        Assert.Equal(82, map.Linedefs[1].Action);
        Assert.Equal(9, map.Linedefs[1].Args[1]);
        Assert.Equal("CurrentThing", map.Things[0].Fields["arg0str"]);
        Assert.Equal(82, map.Things[0].Action);
        Assert.Equal(9, map.Things[0].Args[1]);
    }

    [Fact]
    public void GeneralizedMatchingDoesNotApplyWithoutConfiguredGeneralizedActions()
    {
        var config = GameConfiguration.FromText(Cfg.Replace("generalizedlinedefs = true;", ""));
        var map = BuildMap();
        map.Linedefs[0].Action = 24576 + 2 + 8;
        map.Linedefs[1].Action = 24576 + 3 + 16;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.LinedefAction, (24576 + 2).ToString(), config);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void FindSectorEffectMatchesGeneralizedEffectSubsets()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "32", config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.True(map.Sectors[1].Selected);
        Assert.False(map.Sectors[2].Selected);
    }

    [Fact]
    public void FindSectorEffectMatchesNormalBaseWithAdditionalGeneralizedBits()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "9", config);

        Assert.Equal(2, result.Count);
        Assert.True(map.Sectors[0].Selected);
        Assert.False(map.Sectors[1].Selected);
        Assert.True(map.Sectors[2].Selected);
    }

    [Fact]
    public void ReplaceSectorEffectMatchesGeneralizedEffectSubsets()
    {
        var config = GameConfiguration.FromText(Cfg);
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        int changed = ConfiguredMapSearch.Replace(map, FindCategory.SectorEffect, "32", "11", config);

        Assert.Equal(2, changed);
        Assert.Equal(11, map.Sectors[0].Special);
        Assert.Equal(11, map.Sectors[1].Special);
        Assert.Equal(9 + 64, map.Sectors[2].Special);
    }

    [Fact]
    public void GeneralizedSectorEffectMatchingDoesNotApplyWithoutConfiguredGeneralizedEffects()
    {
        var config = GameConfiguration.FromText(Cfg.Replace("generalizedsectors = true;", ""));
        var map = BuildMap();
        map.Sectors[0].Special = 9 + 32 + 256;
        map.Sectors[1].Special = 32 + 256;
        map.Sectors[2].Special = 9 + 64;

        SearchResult result = ConfiguredMapSearch.Find(map, FindCategory.SectorEffect, "32", config);

        Assert.Equal(0, result.Count);
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        map.AddSector();
        map.AddSector();
        var v1 = map.AddVertex(new Vector2D(0, 0));
        var v2 = map.AddVertex(new Vector2D(64, 0));
        var v3 = map.AddVertex(new Vector2D(64, 64));
        var v4 = map.AddVertex(new Vector2D(0, 64));
        map.AddSidedef(map.AddLinedef(v1, v2), true, sector);
        map.AddSidedef(map.AddLinedef(v2, v3), true, sector);
        map.AddSidedef(map.AddLinedef(v3, v4), true, sector);
        map.BuildIndexes();
        return map;
    }
}
