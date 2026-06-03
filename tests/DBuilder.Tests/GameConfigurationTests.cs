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
                1
                {
                    title = "Door Open Wait Close (also monsters)";
                    id = "Door_Open";
                    prefix = "DR";
                    requiresactivation = false;
                    linetolinetag = true;
                    errorchecker
                    {
                        ignoreuppertexture = true;
                        floorraisetonexthigher = true;
                    }
                }
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
    public void ParsesMapNameFormat()
    {
        var gc = GameConfiguration.FromText("mapnameformat = \"ExMy\";");
        Assert.Equal("ExMy", gc.MapNameFormat);
    }

    [Fact]
    public void ExposesUdbMapFormatInterfaceCapabilities()
    {
        var doom = GameConfiguration.FromText("""formatinterface = "DoomMapSetIO";""");
        var hexen = GameConfiguration.FromText("""formatinterface = "HexenMapSetIO";""");
        var udmf = GameConfiguration.FromText("""formatinterface = "UniversalMapSetIO";""");

        Assert.True(doom.HasLinedefTag);
        Assert.False(doom.HasThingTag);
        Assert.False(doom.HasActionArgs);
        Assert.False(hexen.HasLinedefTag);
        Assert.True(hexen.HasThingTag);
        Assert.True(hexen.HasActionArgs);
        Assert.False(hexen.HasCustomFields);
        Assert.True(udmf.HasLinedefTag);
        Assert.True(udmf.HasThingTag);
        Assert.True(udmf.HasCustomFields);
    }

    [Theory]
    [InlineData("DoomMapSetIO", MapFormat.Doom)]
    [InlineData("HexenMapSetIO", MapFormat.Hexen)]
    [InlineData("UniversalMapSetIO", MapFormat.Udmf)]
    [InlineData("", MapFormat.Doom)]
    [InlineData("UnknownMapSetIO", MapFormat.Doom)]
    public void MapsUdbFormatInterfaceToMapFormat(string formatInterface, MapFormat expected)
    {
        var gc = GameConfiguration.FromText($"""formatinterface = "{formatInterface}";""");

        Assert.Equal(expected, gc.MapFormat);
        Assert.Equal(expected, GameConfiguration.MapFormatFromInterface(formatInterface));
    }

    [Fact]
    public void ParsesGameAndEngineNames()
    {
        var gc = GameConfiguration.FromText("""
            game = "Doom: Doom 2";
            engine = "GZDoom";
            """);

        Assert.Equal("Doom: Doom 2", gc.GameName);
        Assert.Equal("GZDoom", gc.EngineName);
    }

    [Fact]
    public void ParsesMixTexturesFlatsFlag()
    {
        var gc = GameConfiguration.FromText("mixtexturesflats = true;");
        Assert.True(gc.MixTexturesFlats);
    }

    [Fact]
    public void ParsesEditorBehaviorSettings()
    {
        const string cfg = """
            scaledtextureoffsets = false;
            formatinterface = "DoomMapSetIO";
            defaultlinedefactivation = "playercross";
            singlesidedflag = 1;
            doublesidedflag = "twosided";
            impassableflag = "blocking";
            upperunpeggedflag = "dontpegtop";
            lowerunpeggedflag = 16;
            generalizedlinedefs = true;
            generalizedsectors = true;
            start3dmode = 32000;
            linedefactivationsfilter = 7;
            visplaneexplorer
            {
                viewheightdefault = 64;
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.False(gc.ScaledTextureOffsets);
        Assert.Equal("DoomMapSetIO", gc.FormatInterface);
        Assert.Equal("playercross", gc.DefaultLinedefActivationFlag);
        Assert.Equal("1", gc.SingleSidedFlag);
        Assert.Equal("twosided", gc.DoubleSidedFlag);
        Assert.Equal("blocking", gc.ImpassableFlag);
        Assert.Equal("dontpegtop", gc.UpperUnpeggedFlag);
        Assert.Equal("16", gc.LowerUnpeggedFlag);
        Assert.True(gc.GeneralizedActions);
        Assert.True(gc.GeneralizedEffects);
        Assert.Equal(32000, gc.Start3DModeThingType);
        Assert.Equal(7, gc.LinedefActivationsFilter);
        Assert.Equal(64, gc.VisplaneViewHeightDefault);
    }

    [Fact]
    public void ParsesMakeDoorAndDefaultThingFlags()
    {
        const string cfg = """
            makedoortrack = "DOORTRAK";
            makedoordoor = "BIGDOOR2";
            makedoorceil = "CEIL5_1";
            makedooraction = 202;
            makedooractivate = 1024;
            makedoorarg0 = 0;
            makedoorarg1 = 16;
            makedoorarg2 = 150;
            makedoorarg3 = 34;
            makedoorarg4 = 1;
            makedoorflags
            {
                playeruse;
                repeatspecial;
                -monsteractivate;
            }
            defaultthingflags
            {
                1;
                2;
                skill1;
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal("DOORTRAK", gc.MakeDoorTrack);
        Assert.Equal("BIGDOOR2", gc.MakeDoorDoor);
        Assert.Equal("CEIL5_1", gc.MakeDoorCeiling);
        Assert.Equal(202, gc.MakeDoorAction);
        Assert.Equal(1024, gc.MakeDoorActivate);
        Assert.Equal(new[] { 0, 16, 150, 34, 1 }, gc.MakeDoorArgs);
        Assert.True(gc.MakeDoorFlags["playeruse"]);
        Assert.True(gc.MakeDoorFlags["repeatspecial"]);
        Assert.False(gc.MakeDoorFlags["monsteractivate"]);
        Assert.Equal(new[] { "1", "2", "skill1" }, gc.DefaultThingFlags);
    }

    [Fact]
    public void ParsesMapFormatAndTestingSettings()
    {
        const string cfg = """
            testparameters = "-iwad doom2.wad";
            testshortpaths = true;
            testlinuxpaths = true;
            filetitlestyle = "ZDoom";
            linetagindicatesectors = true;
            doomthingrotationangles = true;
            actionspecialhelp = "https://zdoom.org/wiki/%K";
            thingclasshelp = "https://zdoom.org/wiki/Classes:%K";
            sidedefcompressionignoresaction = true;
            decorategames = "doom,heretic";
            skyflatname = "F_SKY2";
            leftboundary = -1024;
            rightboundary = 2048;
            topboundary = 4096;
            bottomboundary = -2048;
            safeboundary = 1536;
            doomlightlevels = false;
            longtexturenames = true;
            localsidedeftextureoffsets = true;
            effect3dfloorsupport = true;
            planeequationsupport = true;
            vertexheightsupport = true;
            sidedeftextureskewing = true;
            distinctfloorandceilingbrightness = true;
            distinctwallbrightness = true;
            distinctsidedefpartbrightness = true;
            sectormultitag = true;
            buggymodeldefpitch = true;
            compatibility
            {
                fixnegativepatchoffsets = true;
                fixmaskedpatchoffsets = true;
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal("-iwad doom2.wad", gc.TestParameters);
        Assert.True(gc.TestShortPaths);
        Assert.True(gc.TestLinuxPaths);
        Assert.Equal(FileTitleStyle.ZDOOM, gc.FileTitleStyle);
        Assert.True(gc.LineTagIndicatesSectors);
        Assert.True(gc.DoomThingRotationAngles);
        Assert.Equal("https://zdoom.org/wiki/%K", gc.ActionSpecialHelp);
        Assert.Equal("https://zdoom.org/wiki/Classes:%K", gc.ThingClassHelp);
        Assert.True(gc.SidedefCompressionIgnoresAction);
        Assert.Equal("doom,heretic", gc.DecorateGames);
        Assert.Equal("F_SKY2", gc.SkyFlatName);
        Assert.Equal(-1024, gc.LeftBoundary);
        Assert.Equal(2048, gc.RightBoundary);
        Assert.Equal(4096, gc.TopBoundary);
        Assert.Equal(-2048, gc.BottomBoundary);
        Assert.Equal(1536, gc.SafeBoundary);
        Assert.False(gc.DoomLightLevels);
        Assert.True(gc.UseLongTextureNames);
        Assert.Equal(short.MaxValue, gc.MaxTextureNameLength);
        Assert.True(gc.UseLocalSidedefTextureOffsets);
        Assert.True(gc.Effect3DFloorSupport);
        Assert.True(gc.PlaneEquationSupport);
        Assert.True(gc.VertexHeightSupport);
        Assert.True(gc.SidedefTextureSkewing);
        Assert.True(gc.DistinctFloorAndCeilingBrightness);
        Assert.True(gc.DistinctWallBrightness);
        Assert.True(gc.DistinctSidedefPartBrightness);
        Assert.True(gc.SectorMultiTag);
        Assert.True(gc.BuggyModelDefPitch);
        Assert.True(gc.FixNegativePatchOffsets);
        Assert.True(gc.FixMaskedPatchOffsets);
    }

    [Fact]
    public void InvalidFileTitleStyleFallsBackToDefault()
    {
        var gc = GameConfiguration.FromText("filetitlestyle = \"Unknown\";");

        Assert.Equal(FileTitleStyle.DEFAULT, gc.FileTitleStyle);
    }

    [Fact]
    public void DefaultsToClassicTextureNameLength()
    {
        var gc = GameConfiguration.FromText("");

        Assert.False(gc.UseLongTextureNames);
        Assert.Equal(8, gc.MaxTextureNameLength);
    }

    [Fact]
    public void ParsesRenderStyleFlagAndBrightnessMetadata()
    {
        const string cfg = """
            visplaneexplorer
            {
                viewheights
                {
                    eye = "41";
                    crouch = "25";
                }
            }
            thingrenderstyles { additive = "Additive"; }
            linedefrenderstyles { translucent = "Translucent"; }
            sidedefflags { lightabsolute = "Absolute light"; }
            sectorflags { secret = "Secret"; }
            ceilingportalflags { portal_ceil_nopass = "Impassable"; }
            floorportalflags { portal_floor_norender = "Not rendered"; }
            sectorrenderstyles { add = "Add"; }
            sectorportalrenderstyles { translucent = "Translucent"; }
            sectorbrightness
            {
                255;
                0;
                128;
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal("41", gc.VisplaneViewHeights["eye"]);
        Assert.Equal("25", gc.VisplaneViewHeights["crouch"]);
        Assert.Equal("Additive", gc.ThingRenderStyles["additive"]);
        Assert.Equal("Translucent", gc.LinedefRenderStyles["translucent"]);
        Assert.Equal("Absolute light", gc.SidedefFlags["lightabsolute"]);
        Assert.Equal("Secret", gc.SectorFlags["secret"]);
        Assert.Equal("Impassable", gc.CeilingPortalFlags["portal_ceil_nopass"]);
        Assert.Equal("Not rendered", gc.FloorPortalFlags["portal_floor_norender"]);
        Assert.Equal("Add", gc.SectorRenderStyles["add"]);
        Assert.Equal("Translucent", gc.SectorPortalRenderStyles["translucent"]);
        Assert.Equal(new[] { 0, 128, 255 }, gc.BrightnessLevels);
    }

    [Fact]
    public void ParsesMetadataStringSets()
    {
        const string cfg = """
            damagetypes = "BFGSplash Drowning Slime";
            internalsoundnames = "*death *pain75 misc/i_pkup";
            ignoreddirectories = ".svn .git";
            ignoredextensions = "wad pk3 zip";
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Contains("bfgsplash", gc.DamageTypes);
        Assert.Contains("Drowning", gc.DamageTypes);
        Assert.Contains("*pain75", gc.InternalSoundNames);
        Assert.Contains("misc/i_pkup", gc.InternalSoundNames);
        Assert.Contains(".GIT", gc.IgnoredDirectories);
        Assert.Contains("PK3", gc.IgnoredExtensions);
    }

    [Fact]
    public void DefaultsDamageTypesToNone()
    {
        var gc = GameConfiguration.FromText("");

        Assert.Contains("None", gc.DamageTypes);
        Assert.Empty(gc.InternalSoundNames);
        Assert.Empty(gc.IgnoredDirectories);
        Assert.Empty(gc.IgnoredExtensions);
    }

    [Fact]
    public void ParsesThingFlagsCompareMetadata()
    {
        const string cfg = """
            thingflagscompare
            {
                skills
                {
                    skill1
                    {
                        comparemethod = "equal";
                        invert = true;
                        requiredgroups = "classes,gamemodes";
                        ignoredgroups = "coop";
                        requiredflag = "skill2";
                        ingnorethisgroupwhenunset = true;
                    }
                    skill2;
                }
                classes
                {
                    optional = true;
                    fighter;
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(2, gc.ThingFlagsCompare.Count);
        var skills = gc.ThingFlagsCompare["skills"];
        Assert.False(skills.IsOptional);
        Assert.Equal(2, skills.Flags.Count);
        var skill1 = skills.Flags["skill1"];
        Assert.Equal("equal", skill1.CompareMethod);
        Assert.True(skill1.Invert);
        Assert.Contains("classes", skill1.RequiredGroups);
        Assert.Contains("gamemodes", skill1.RequiredGroups);
        Assert.Contains("coop", skill1.IgnoredGroups);
        Assert.Equal("skill2", skill1.RequiredFlag);
        Assert.True(skill1.IgnoreGroupWhenUnset);
        Assert.Equal("and", skills.Flags["skill2"].CompareMethod);

        var classes = gc.ThingFlagsCompare["classes"];
        Assert.True(classes.IsOptional);
        Assert.Contains("fighter", classes.Flags.Keys);
    }

    [Fact]
    public void ParsesUniversalFieldMetadata()
    {
        const string cfg = """
            universalfields
            {
                linedef
                {
                    automapstyle
                    {
                        type = 11;
                        default = 0;
                        enum
                        {
                            0 = "Default";
                            1 = "One-sided wall";
                        }
                    }
                }
                sector
                {
                    portalfloor
                    {
                        type = 0;
                        default = 0;
                        managed = false;
                        associations
                        {
                            0
                            {
                                property = "portalfloor";
                                modify = "abs";
                                nevershoweventlines = true;
                                consolidateeventlines = true;
                            }
                        }
                    }
                }
                thing
                {
                    species
                    {
                        type = 2;
                        default = "DoomImp";
                        thingtypespecific = true;
                        enum = "thingtypes";
                    }
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        var automap = gc.UniversalFields["linedef"]["automapstyle"];
        Assert.Equal("automapstyle", automap.Name);
        Assert.Equal(11, automap.Type);
        Assert.Equal(0, automap.DefaultValue);
        Assert.True(automap.Managed);
        Assert.Null(automap.EnumName);
        Assert.Equal("Default", automap.InlineEnumItems[0].Title);
        Assert.Equal("One-sided wall", automap.InlineEnumItems[1].Title);

        var portal = gc.UniversalFields["sector"]["portalfloor"];
        Assert.False(portal.Managed);
        var association = portal.Associations["portalfloor"];
        Assert.Equal("abs", association.Modify);
        Assert.True(association.NeverShowEventLines);
        Assert.True(association.ConsolidateEventLines);

        var species = gc.UniversalFields["thing"]["species"];
        Assert.True(species.ThingTypeSpecific);
        Assert.Equal("thingtypes", species.EnumName);
        Assert.Equal("DoomImp", species.DefaultValue);
    }

    [Fact]
    public void ParsesThingsFilterMetadata()
    {
        const string cfg = """
            thingsfilters
            {
                filter0
                {
                    name = "Keys only";
                    category = "keys";
                    type = -1;
                    angle = 90;
                    zheight = 32;
                    action = 80;
                    arg0 = 1;
                    arg3 = 4;
                    tag = 7;
                    invert = true;
                    displaymode = 2;

                    fields
                    {
                        skill1 = true;
                        dm = false;
                    }

                    customfieldvalues
                    {
                        species = "DoomImp";
                        count = 3;
                    }

                    customfieldtypes
                    {
                        species = 2;
                        count = 0;
                    }
                }

                filter1
                {
                    name = "Unnamed action";
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(2, gc.ThingsFilters.Count);
        var keys = gc.ThingsFilters[0];
        Assert.Equal("filter0", keys.Key);
        Assert.Equal("Keys only", keys.Name);
        Assert.Equal("keys", keys.Category);
        Assert.True(keys.Invert);
        Assert.Equal(2, keys.DisplayMode);
        Assert.Equal(-1, keys.ThingType);
        Assert.Equal(90, keys.ThingAngle);
        Assert.Equal(32, keys.ThingZHeight);
        Assert.Equal(80, keys.ThingAction);
        Assert.Equal(new[] { 1, -1, -1, 4, -1 }, keys.ThingArgs);
        Assert.Equal(7, keys.ThingTag);
        Assert.Contains("skill1", keys.RequiredFields);
        Assert.Contains("dm", keys.ForbiddenFields);
        Assert.Equal(2, keys.CustomFields["species"].Type);
        Assert.Equal("DoomImp", keys.CustomFields["species"].Value);
        Assert.Equal(0, keys.CustomFields["count"].Type);
        Assert.Equal(3, keys.CustomFields["count"].Value);

        var defaulted = gc.ThingsFilters[1];
        Assert.Equal("", defaulted.Category);
        Assert.False(defaulted.Invert);
        Assert.Equal(0, defaulted.DisplayMode);
        Assert.Equal(-1, defaulted.ThingType);
        Assert.Equal(-1, defaulted.ThingAngle);
        Assert.Equal(int.MinValue, defaulted.ThingZHeight);
        Assert.Equal(-1, defaulted.ThingAction);
        Assert.Equal(new[] { -1, -1, -1, -1, -1 }, defaulted.ThingArgs);
        Assert.Equal(-1, defaulted.ThingTag);
    }

    [Fact]
    public void ParsesTextureDefaultsAndDefaultSkyMappings()
    {
        const string cfg = """
            defaulttexturescale = 0.5;
            defaultflatscale = 2.0;
            defaultwalltexture = "STONE2";
            defaultfloortexture = "FLOOR4_8";
            defaultceilingtexture = "CEIL3_5";
            defaultskytextures
            {
                SKY1 = "MAP01, MAP02";
                SKY2 = "E1M1";
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(0.5, gc.DefaultTextureScale);
        Assert.Equal(2.0, gc.DefaultFlatScale);
        Assert.Equal("STONE2", gc.DefaultWallTexture);
        Assert.Equal("FLOOR4_8", gc.DefaultFloorTexture);
        Assert.Equal("CEIL3_5", gc.DefaultCeilingTexture);
        Assert.Equal("SKY1", gc.DefaultSkyTextures["MAP01"]);
        Assert.Equal("SKY1", gc.DefaultSkyTextures["MAP02"]);
        Assert.Equal("SKY2", gc.DefaultSkyTextures["E1M1"]);
    }

    [Fact]
    public void ParsesResourceRanges()
    {
        const string cfg = """
            textures
            {
                walls { start = "TX_START"; end = "TX_END"; }
            }
            hires
            {
                detail { start = "HI_START"; end = "HI_END"; }
            }
            flats
            {
                floors { start = "F_START"; end = "F_END"; }
            }
            patches
            {
                art { start = "P_START"; end = "P_END"; }
            }
            sprites
            {
                actors { start = "S_START"; end = "S_END"; }
            }
            colormaps
            {
                standard1
                {
                    start = "C_START";
                    end = "C_END";
                }
                ignored
                {
                    start = "ONLY_START";
                }
            }
            voxels
            {
                models { start = "VX_START"; end = "VX_END"; }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        AssertRange(Assert.Single(gc.TextureRanges), "walls", "TX_START", "TX_END");
        AssertRange(Assert.Single(gc.HiResRanges), "detail", "HI_START", "HI_END");
        AssertRange(Assert.Single(gc.FlatRanges), "floors", "F_START", "F_END");
        AssertRange(Assert.Single(gc.PatchRanges), "art", "P_START", "P_END");
        AssertRange(Assert.Single(gc.SpriteRanges), "actors", "S_START", "S_END");
        AssertRange(Assert.Single(gc.ColormapRanges), "standard1", "C_START", "C_END");
        AssertRange(Assert.Single(gc.VoxelRanges), "models", "VX_START", "VX_END");
    }

    private static void AssertRange(ResourceRangeInfo range, string name, string start, string end)
    {
        Assert.Equal(name, range.Name);
        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
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
    public void ParsesThingCategoriesAndNestedCategoryDefaults()
    {
        const string cfg = """
            thingtypes
            {
                monsters
                {
                    title = "Monsters";
                    sprite = "TROOA1";
                    color = 12;
                    width = 20;
                    height = 56;
                    alpha = 0.75;
                    renderstyle = "Translucent";
                    sort = 1;
                    arrow = 1;
                    blocking = 1;
                    error = 2;
                    fixedrotation = true;
                    spritescale = 1.5;

                    bosses
                    {
                        title = "Bosses";
                        color = 4;
                        fixedsize = true;
                        optional = true;

                        3003
                        {
                            title = "Baron of Hell";
                            sprite = "BOSSA1";
                            renderstyle = "Add";
                            absolutez = true;
                            locksprite = true;
                            thinglink = 7;
                            adduniversalfields
                            {
                                argspeed = true;
                                customflag = true;
                            }
                            flagsrename
                            {
                                UniversalMapSetIO
                                {
                                    dormant = "Starts dormant";
                                    friendly = "Friendly actor";
                                }
                                UnsupportedMapSetIO
                                {
                                    ignored = "Ignored";
                                }
                            }
                        }
                        3004 = "Cyberdemon";
                    }
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        var monsters = gc.ThingCategories["monsters"];
        Assert.Equal("Monsters", monsters.Title);
        Assert.Equal("TROOA1", monsters.Sprite);
        Assert.Equal(0.75, monsters.Alpha);
        Assert.Equal("translucent", monsters.RenderStyle);
        Assert.True(monsters.Sorted);
        Assert.Equal(1, monsters.Arrow);
        Assert.Equal(1, monsters.Blocking);
        Assert.Equal(2, monsters.ErrorCheck);
        Assert.True(monsters.FixedRotation);
        Assert.Equal(1.5, monsters.SpriteScale);

        var bosses = gc.ThingCategories["monsters.bosses"];
        Assert.Equal("monsters", bosses.ParentKey);
        Assert.Equal("Bosses", bosses.Title);
        Assert.Equal(4, bosses.Color);
        Assert.Equal(20, bosses.Width);
        Assert.Equal(56, bosses.Height);
        Assert.True(bosses.FixedSize);

        var baron = gc.GetThing(3003)!;
        Assert.Equal("monsters.bosses", baron.Category);
        Assert.Equal("add", baron.RenderStyle);
        Assert.Equal(0.75, baron.Alpha);
        Assert.True(baron.Arrow);
        Assert.Equal(1, baron.Blocking);
        Assert.Equal(2, baron.ErrorCheck);
        Assert.True(baron.FixedRotation);
        Assert.True(baron.AbsoluteZ);
        Assert.Equal(1.5, baron.SpriteScale);
        Assert.True(baron.LockSprite);
        Assert.Equal(7, baron.ThingLink);
        Assert.Equal(4, baron.Color);
        Assert.Equal(14, baron.Width);
        Assert.Equal(56, baron.Height);
        Assert.Contains("argspeed", baron.AddUniversalFields);
        Assert.True(baron.HasAdditionalUniversalField("CUSTOMFLAG"));
        var renamed = baron.FlagsRename["universalmapsetio"];
        Assert.Equal("Starts dormant", renamed["dormant"]);
        Assert.Equal("Friendly actor", renamed["friendly"]);
        Assert.False(baron.FlagsRename.ContainsKey("unsupportedmapsetio"));
        Assert.True(baron.Optional);

        var cyberdemon = gc.GetThing(3004)!;
        Assert.Equal("Cyberdemon", cyberdemon.Title);
        Assert.Equal("monsters.bosses", cyberdemon.Category);
        Assert.Equal("TROOA1", cyberdemon.Sprite);
        Assert.Equal(4, cyberdemon.Color);
        Assert.Equal(14, cyberdemon.Width);
        Assert.Equal(56, cyberdemon.Height);
        Assert.True(cyberdemon.FixedSize);
        Assert.False(cyberdemon.Optional);
    }

    [Fact]
    public void ThingTypeSafetyMatchesUdbForFixedSizeAndAbsoluteZ()
    {
        const string cfg = """
            thingtypes
            {
                decorations
                {
                    width = 20;
                    height = 56;
                    hangs = 1;
                    absolutez = true;

                    9001
                    {
                        title = "Fixed decoration";
                        fixedsize = true;
                    }
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        var thing = gc.GetThing(9001);
        Assert.NotNull(thing);
        Assert.Equal(14, thing!.Width);
        Assert.False(thing.Hangs);
        Assert.True(thing.AbsoluteZ);
        Assert.True(thing.FixedSize);
    }

    [Fact]
    public void IgnoresInvalidThingCategoryBlocks()
    {
        const string cfg = """
            thingtypes
            {
                grouping
                {
                    child
                    {
                        title = "Child";
                        1002 = "Also ignored";
                    }
                }

                valid
                {
                    color = 4;
                    1003 = "Parsed";
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.False(gc.ThingCategories.ContainsKey("grouping"));
        Assert.False(gc.ThingCategories.ContainsKey("grouping.child"));
        Assert.Null(gc.GetThing(1002));
        Assert.Equal("Parsed", gc.GetThing(1003)!.Title);
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
    public void ParsesLinedefActionCategoriesAndMetadata()
    {
        var gc = GameConfiguration.FromText(SampleCfg);

        var doorCategory = gc.LinedefActionCategories["door"];
        Assert.Equal("Door", doorCategory.Title);
        Assert.Contains(1, doorCategory.Actions);

        var action = gc.GetLinedefAction(1)!;
        Assert.Equal("door", action.CategoryKey);
        Assert.Equal("Door Open Wait Close (also monsters)", action.Name);
        Assert.Equal("DR Door Open Wait Close (also monsters)", action.DisplayTitle);
        Assert.Equal("Door_Open", action.Id);
        Assert.True(action.IsKnown);
        Assert.False(action.IsGeneralized);
        Assert.False(action.IsNull);
        Assert.False(action.RequiresActivation);
        Assert.True(action.LineToLineTag);
        Assert.False(action.LineToLineSameAction);
        Assert.True(action.ErrorChecker.IgnoreUpperTexture);
        Assert.True(action.ErrorChecker.FloorRaiseToNextHigher);
        Assert.False(action.ErrorChecker.IgnoreLowerTexture);
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
        Assert.True(gc.GetSectorEffect(0)!.IsNull);
        Assert.True(gc.GetSectorEffect(9)!.IsKnown);
        Assert.False(gc.GetSectorEffect(9)!.IsGeneralized);
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
    public void MergeDehackedAddsUncategorizedThingsAsUserDefined()
    {
        const string text = @"
Thing 3 (Loose Dehacked Thing)
ID # = 31001
Width = 1048576
Height = 2097152
";

        var gc = GameConfiguration.FromText("");
        gc.MergeDehacked(DehackedParser.Parse(text));

        var thing = gc.GetThing(31001);
        Assert.NotNull(thing);
        Assert.Equal("Loose Dehacked Thing", thing!.Title);
        Assert.Equal("User-defined", thing.Category);
        Assert.Equal("internal:unknownthing", thing.Sprite);
        Assert.Equal(16, thing.Width);
        Assert.Equal(32, thing.Height);
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
    public void MergeDehackedAppliesThingBitsToBlockingAndHanging()
    {
        const string cfg = @"
thingtypes
{
    monsters
    {
        31002
        {
            title = ""Base Thing"";
            class = ""BaseThing"";
            blocking = 2;
            hangs = true;
        }
        31003
        {
            title = ""Air Thing"";
            class = ""AirThing"";
        }
    }
}";
        const string text = @"
Thing 4 (Non Solid Thing)
ID # = 31002
Bits = ambush

Thing 5 (Hanging Solid Thing)
ID # = 31003
Bits = solid+spawnceiling
";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeDehacked(DehackedParser.Parse(text));

        var nonSolid = gc.GetThing(31002);
        Assert.NotNull(nonSolid);
        Assert.Equal(0, nonSolid!.Blocking);
        Assert.False(nonSolid.Hangs);

        var hangingSolid = gc.GetThing(31003);
        Assert.NotNull(hangingSolid);
        Assert.Equal(1, hangingSolid!.Blocking);
        Assert.True(hangingSolid.Hangs);
    }

    [Fact]
    public void MergeDehackedAppliesEditorAngledAndColor()
    {
        const string cfg = @"
thingtypes
{
    misc
    {
        31004
        {
            title = ""Old Angled Thing"";
            class = ""OldAngledThing"";
            arrow = false;
            color = 4;
        }
        31005
        {
            title = ""Old Plain Thing"";
            class = ""OldPlainThing"";
            arrow = true;
            color = 6;
        }
    }
}";
        const string text = @"
Thing 6 (Angled Thing)
ID # = 31004
#$Editor Angled = true
#$Editor Color ID = 12

Thing 7 (Plain Thing)
ID # = 31005
#$Editor Angled = false
#$Editor Color ID = blue
";

        var gc = GameConfiguration.FromText(cfg);
        gc.MergeDehacked(DehackedParser.Parse(text));

        var angled = gc.GetThing(31004);
        Assert.NotNull(angled);
        Assert.True(angled!.Arrow);
        Assert.Equal(12, angled.Color);

        var plain = gc.GetThing(31005);
        Assert.NotNull(plain);
        Assert.False(plain!.Arrow);
        Assert.Equal(18, plain.Color);
    }

    [Fact]
    public void ParsesCompilerDefaultsStaticLimitsAndRequiredArchives()
    {
        const string cfg = """
            defaultsavecompiler = "zdbsp_normal";
            defaulttestcompiler = "zdbsp_fast";
            defaultscriptcompiler = "acc";
            nodebuildersave = "custom_save";
            nodebuildertest = "custom_test";

            staticlimits
            {
                visplanes = 128;
                drawsegs = 256;
            }

            requiredarchives
            {
                gzdoom
                {
                    filename = "gzdoom.pk3";
                    need_exclude = false;
                    actors { lump = "DECORATE"; class = "Actor"; }
                    zscript { lump = "ZSCRIPT"; }
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal("zdbsp_normal", gc.DefaultSaveCompiler);
        Assert.Equal("zdbsp_fast", gc.DefaultTestCompiler);
        Assert.Equal("acc", gc.DefaultScriptCompiler);
        Assert.Equal("custom_save", gc.NodeBuilderSave);
        Assert.Equal("custom_test", gc.NodeBuilderTest);
        Assert.Equal(128, gc.StaticLimits.Get("visplanes"));
        Assert.Equal(256, gc.StaticLimits.Get("drawsegs"));
        Assert.Equal(128, gc.StaticLimits.Visplanes);
        Assert.Equal(256, gc.StaticLimits.Drawsegs);
        Assert.Equal(32, gc.StaticLimits.Solidsegs);
        Assert.Equal(320 * 64, gc.StaticLimits.Openings);
        Assert.Equal(64, gc.StaticLimits.InterpolateVisplanes(64));

        var archive = Assert.Single(gc.RequiredArchives);
        Assert.Equal("gzdoom", archive.Name);
        Assert.Equal("gzdoom.pk3", archive.Filename);
        Assert.False(archive.NeedExclude);
        Assert.Equal(2, archive.Entries.Count);
        var actors = archive.Entries.Single(e => e.Name == "actors");
        Assert.Equal("DECORATE", actors.Lump);
        Assert.Equal("Actor", actors.ClassName);
    }

    [Fact]
    public void StaticLimitsInterpolateVisplanesMatchesUdbScaling()
    {
        const string cfg = """
            staticlimits
            {
                visplanes = 256;
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(32, gc.StaticLimits.InterpolateVisplanes(64));
        Assert.Equal(1, gc.StaticLimits.InterpolateVisplanes(1));
    }

    [Fact]
    public void ParsesTextureSetsAndMatchesWildcards()
    {
        const string cfg = """
            texturesets
            {
                set0
                {
                    name = "Rock";
                    filter0 = "ASHWALL*";
                    filter1 = "FLAT1_?";
                    filter2 = "RROCK10";
                }

                set1
                {
                    name = "Switches";
                    filter0 = "SW1*";
                    filter1 = "SW2*";
                }
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(2, gc.TextureSets.Count);
        var rock = gc.TextureSets.Single(s => s.Key == "set0");
        Assert.Equal("Rock", rock.Name);
        Assert.Contains("ASHWALL*", rock.Filters);
        Assert.Contains("FLAT1_?", rock.Filters);
        Assert.Contains("RROCK10", rock.Filters);
        Assert.True(rock.Matches("ashwall2"));
        Assert.True(rock.Matches("FLAT1_3"));
        Assert.True(rock.Matches("RROCK10"));
        Assert.False(rock.Matches("FLAT10"));

        var switches = gc.TextureSets.Single(s => s.Name == "Switches");
        Assert.True(switches.Matches("SW1BRCOM"));
        Assert.False(switches.Matches("ROCKRED1"));
    }

    [Fact]
    public void ParsesLinedefActivationInfo()
    {
        const string cfg = """
            linedefactivations
            {
                1024 = "Player presses Use";

                repeatspecial
                {
                    name = "Repeatable action";
                    istrigger = false;
                }

                playercross = "When player walks over";
            }
            """;

        var gc = GameConfiguration.FromText(cfg);

        Assert.Equal(3, gc.LinedefActivations.Count);
        var use = gc.LinedefActivations.Single(a => a.Key == "1024");
        Assert.Equal(1024, use.Index);
        Assert.Equal("Player presses Use", use.Title);
        Assert.True(use.IsTrigger);

        var repeat = gc.LinedefActivations.Single(a => a.Key == "repeatspecial");
        Assert.Equal(0, repeat.Index);
        Assert.Equal("Repeatable action", repeat.Title);
        Assert.False(repeat.IsTrigger);

        var cross = gc.LinedefActivations.Single(a => a.Key == "playercross");
        Assert.Equal("When player walks over", cross.Title);
        Assert.True(cross.IsTrigger);
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
