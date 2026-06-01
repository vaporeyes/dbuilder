// ABOUTME: Tests minimal MapOptions persistence backed by Configuration.
// ABOUTME: Covers UDB-compatible identity, selection group, tag-label, drawing-option, script-tab, command and resource settings.

using System.Collections;
using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapOptionsTests
{
    [Fact]
    public void CurrentNameTracksPreviousNameOnFirstRename()
    {
        var options = new MapOptions();

        options.CurrentName = "MAP01";
        options.CurrentName = "MAP02";
        options.CurrentName = "MAP03";

        Assert.Equal("MAP01", options.PreviousName);
        Assert.Equal("MAP03", options.CurrentName);
        Assert.Equal("MAP03", options.LevelName);
        Assert.True(options.LevelNameChanged);
    }

    [Fact]
    public void CurrentNameDoesNotMarkChangedWhenNameStaysSame()
    {
        var options = new MapOptions();

        options.CurrentName = "";

        Assert.Equal("", options.PreviousName);
        Assert.Equal("", options.CurrentName);
        Assert.False(options.LevelNameChanged);
    }

    [Fact]
    public void DataLocationInferTypeRecognizesArchiveExtensions()
    {
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.pk3"));
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.pk7"));
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.zip"));
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.pke"));
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.ipk3"));
        Assert.Equal(DataLocationType.Pk3, DataLocation.InferType("/tmp/resource.ipk7"));
        Assert.Equal(DataLocationType.Wad, DataLocation.InferType("/tmp/resource.wad"));
    }

    [Fact]
    public void DataLocationInferTypePrefersExistingDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"dbuilder-data-location-type-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(DataLocationType.Directory, DataLocation.InferType(dir));
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void ReadRootOptionsLoadsRootSettingsAndMapConfiguration()
    {
        var wadConfiguration = new Configuration(sorted: true);
        wadConfiguration.InputConfiguration("""
            gameconfig = "Doom_Doom.cfg";
            strictpatches = 1;
            maps
            {
                MAP01
                {
                    defaultfloortexture = "FLOOR1";
                }
            }
            """);
        var options = new MapOptions();

        options.ReadRootOptions(wadConfiguration, "MAP01");

        Assert.Equal("Doom_Doom.cfg", options.ConfigFile);
        Assert.True(options.StrictPatches);
        Assert.Equal("MAP01", options.CurrentName);
        Assert.Equal("", options.PreviousName);
        Assert.False(options.LevelNameChanged);
        Assert.Equal("FLOOR1", options.MapConfiguration.ReadSetting("defaultfloortexture", ""));
    }

    [Fact]
    public void WriteRootOptionsStoresUdbCompatibleRootSettings()
    {
        var options = new MapOptions
        {
            ConfigFile = "Doom_Doom.cfg",
            StrictPatches = true,
            CurrentName = "MAP01",
            DefaultFloorTexture = "FLOOR1",
        };
        options.WriteDrawingOptions();
        var wadConfiguration = new Configuration(sorted: true);

        options.WriteRootOptions(wadConfiguration);

        Assert.Equal("Doom Builder Map Settings Configuration", wadConfiguration.ReadSetting("type", ""));
        Assert.Equal("Doom_Doom.cfg", wadConfiguration.ReadSetting("gameconfig", ""));
        Assert.Equal(1, wadConfiguration.ReadSetting("strictpatches", 0));
        var mapRoot = Assert.IsAssignableFrom<IDictionary>(wadConfiguration.ReadSetting("maps.MAP01", (IDictionary?)null));
        Assert.Equal("FLOOR1", mapRoot["defaultfloortexture"]);
    }

    [Fact]
    public void WriteSelectionGroupsStoresUdbCompatibleIndexLists()
    {
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[2].Groups = MapSet.GroupMask(0);
        map.Linedefs[0].Groups = MapSet.GroupMask(1);
        map.Sectors[0].Groups = MapSet.GroupMask(1);
        map.Things[0].Groups = MapSet.GroupMask(2);
        var options = new MapOptions();

        options.WriteSelectionGroups(map);

        var groups = options.MapConfiguration.ReadSetting(MapOptions.SelectionGroupsPath, (IDictionary?)null);
        Assert.NotNull(groups);
        var group0 = Assert.IsAssignableFrom<IDictionary>(groups![0]);
        var group1 = Assert.IsAssignableFrom<IDictionary>(groups[1]);
        var group2 = Assert.IsAssignableFrom<IDictionary>(groups[2]);
        Assert.Equal("0 2", group0["vertices"]);
        Assert.Equal("0", group1["linedefs"]);
        Assert.Equal("0", group1["sectors"]);
        Assert.Equal("0", group2["things"]);
    }

    [Fact]
    public void ReadSelectionGroupsRestoresMembershipByIndex()
    {
        var src = BuildMap();
        src.Vertices[1].Groups = MapSet.GroupMask(0);
        src.Linedefs[0].Groups = MapSet.GroupMask(3);
        src.Sectors[0].Groups = MapSet.GroupMask(3);
        src.Things[0].Groups = MapSet.GroupMask(4);
        var options = new MapOptions();
        options.WriteSelectionGroups(src);

        var dst = BuildMap();
        options.ReadSelectionGroups(dst);

        Assert.Equal(0, dst.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), dst.Vertices[1].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Linedefs[0].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Sectors[0].Groups);
        Assert.Equal(MapSet.GroupMask(4), dst.Things[0].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsReplacesPersistedGroupBits()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "1";
                }
            }
            """);
        var map = BuildMap();
        map.Vertices[0].Groups = MapSet.GroupMask(0);
        map.Vertices[1].Groups = MapSet.GroupMask(2);

        options.ReadSelectionGroups(map);

        Assert.Equal(0, map.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(0), map.Vertices[1].Groups);
    }

    [Fact]
    public void ReadSelectionGroupsIgnoresInvalidIndices()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            selectiongroups
            {
                0
                {
                    vertices = "-1 0 99 bad";
                    things = "5";
                }
            }
            """);
        var map = BuildMap();

        options.ReadSelectionGroups(map);

        Assert.Equal(MapSet.GroupMask(0), map.Vertices[0].Groups);
        Assert.Equal(0, map.Things[0].Groups);
    }

    [Fact]
    public void WriteTagLabelsStoresUdbCompatibleEntries()
    {
        var options = new MapOptions();
        options.TagLabels[12] = "Door controls";
        options.TagLabels[3] = "Secret lift";

        options.WriteTagLabels();

        var labels = options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null);
        Assert.NotNull(labels);
        var first = Assert.IsAssignableFrom<IDictionary>(labels!["taglabel1"]);
        var second = Assert.IsAssignableFrom<IDictionary>(labels["taglabel2"]);
        Assert.Equal(12, first["tag"]);
        Assert.Equal("Door controls", first["label"]);
        Assert.Equal(3, second["tag"]);
        Assert.Equal("Secret lift", second["label"]);
    }

    [Fact]
    public void ReadTagLabelsRestoresValidEntries()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 12;
                    label = "Door controls";
                }
                taglabel2
                {
                    tag = 3;
                    label = "Secret lift";
                }
            }
            """);

        options.ReadTagLabels();

        Assert.Equal("Door controls", options.TagLabels[12]);
        Assert.Equal("Secret lift", options.TagLabels[3]);
    }

    [Fact]
    public void ReadTagLabelsReplacesExistingLabelsAndIgnoresInvalidEntries()
    {
        var options = new MapOptions();
        options.TagLabels[99] = "Stale";
        options.MapConfiguration.InputConfiguration("""
            taglabels
            {
                taglabel1
                {
                    tag = 0;
                    label = "Ignored zero";
                }
                taglabel2
                {
                    tag = 8;
                    label = "";
                }
                taglabel3
                {
                    tag = 5;
                    label = "Valid";
                }
                broken = 12;
            }
            """);

        options.ReadTagLabels();

        Assert.Single(options.TagLabels);
        Assert.Equal("Valid", options.TagLabels[5]);
    }

    [Fact]
    public void WriteTagLabelsRemovesStaleConfigurationWhenEmpty()
    {
        var options = new MapOptions();
        options.TagLabels[1] = "Old";
        options.WriteTagLabels();
        options.TagLabels.Clear();

        options.WriteTagLabels();

        Assert.Null(options.MapConfiguration.ReadSetting(MapOptions.TagLabelsPath, (IDictionary?)null));
    }

    [Fact]
    public void WriteDrawingOptionsStoresUdbCompatibleScalarKeys()
    {
        var options = new MapOptions
        {
            DefaultFloorTexture = "FLOOR1",
            DefaultCeilingTexture = "CEIL1",
            DefaultTopTexture = "TOP",
            DefaultWallTexture = "MID",
            DefaultBottomTexture = "BOT",
            CustomBrightness = 144,
            CustomFloorHeight = -16,
            CustomCeilingHeight = 192,
            OverrideFloorTexture = true,
            OverrideCeilingTexture = true,
            OverrideTopTexture = true,
            OverrideMiddleTexture = true,
            OverrideBottomTexture = true,
            OverrideFloorHeight = true,
            OverrideCeilingHeight = true,
            OverrideBrightness = true,
            UseLongTextureNames = true,
            ViewPosition = new Vector2D(12.5, -4.25),
            ViewScale = 1.75,
            ScriptCompiler = "acc",
        };

        options.WriteDrawingOptions();

        Assert.Equal("FLOOR1", options.MapConfiguration.ReadSetting("defaultfloortexture", ""));
        Assert.Equal("CEIL1", options.MapConfiguration.ReadSetting("defaultceiltexture", ""));
        Assert.Equal("TOP", options.MapConfiguration.ReadSetting("defaulttoptexture", ""));
        Assert.Equal("MID", options.MapConfiguration.ReadSetting("defaultwalltexture", ""));
        Assert.Equal("BOT", options.MapConfiguration.ReadSetting("defaultbottomtexture", ""));
        Assert.Equal(144, options.MapConfiguration.ReadSetting("custombrightness", 0));
        Assert.Equal(-16, options.MapConfiguration.ReadSetting("customfloorheight", 0));
        Assert.Equal(192, options.MapConfiguration.ReadSetting("customceilheight", 0));
        Assert.True(options.MapConfiguration.ReadSetting("overridefloortexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overrideceiltexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridetoptexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridemiddletexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridebottomtexture", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridefloorheight", false));
        Assert.True(options.MapConfiguration.ReadSetting("overrideceilheight", false));
        Assert.True(options.MapConfiguration.ReadSetting("overridebrightness", false));
        Assert.True(options.MapConfiguration.ReadSetting("uselongtexturenames", false));
        Assert.Equal(12.5, options.MapConfiguration.ReadSetting("viewpositionx", 0.0));
        Assert.Equal(-4.25, options.MapConfiguration.ReadSetting("viewpositiony", 0.0));
        Assert.Equal(1.75, options.MapConfiguration.ReadSetting("viewscale", 0.0));
        Assert.Equal("acc", options.MapConfiguration.ReadSetting("scriptcompiler", ""));
    }

    [Fact]
    public void ReadDrawingOptionsRestoresValuesAndClampsBrightness()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            defaultfloortexture = "FLOOR1";
            defaultceiltexture = "CEIL1";
            defaulttoptexture = "TOP";
            defaultwalltexture = "MID";
            defaultbottomtexture = "BOT";
            custombrightness = 999;
            customfloorheight = -16;
            customceilheight = 192;
            overridefloortexture = true;
            overrideceiltexture = true;
            overridetoptexture = true;
            overridemiddletexture = true;
            overridebottomtexture = true;
            overridefloorheight = true;
            overrideceilheight = true;
            overridebrightness = true;
            uselongtexturenames = true;
            viewpositionx = 12.5;
            viewpositiony = -4.25;
            viewscale = 1.75;
            scriptcompiler = "acc";
            """);

        options.ReadDrawingOptions(longTextureNamesSupported: true);

        Assert.Equal("FLOOR1", options.DefaultFloorTexture);
        Assert.Equal("CEIL1", options.DefaultCeilingTexture);
        Assert.Equal("TOP", options.DefaultTopTexture);
        Assert.Equal("MID", options.DefaultWallTexture);
        Assert.Equal("BOT", options.DefaultBottomTexture);
        Assert.Equal(255, options.CustomBrightness);
        Assert.Equal(-16, options.CustomFloorHeight);
        Assert.Equal(192, options.CustomCeilingHeight);
        Assert.True(options.OverrideFloorTexture);
        Assert.True(options.OverrideCeilingTexture);
        Assert.True(options.OverrideTopTexture);
        Assert.True(options.OverrideMiddleTexture);
        Assert.True(options.OverrideBottomTexture);
        Assert.True(options.OverrideFloorHeight);
        Assert.True(options.OverrideCeilingHeight);
        Assert.True(options.OverrideBrightness);
        Assert.True(options.UseLongTextureNames);
        Assert.Equal(new Vector2D(12.5, -4.25), options.ViewPosition);
        Assert.Equal(1.75, options.ViewScale);
        Assert.Equal("acc", options.ScriptCompiler);
    }

    [Fact]
    public void ReadDrawingOptionsUsesDefaultsAndHonorsLongTextureSupport()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("uselongtexturenames = true; custombrightness = -5;");

        options.ReadDrawingOptions(longTextureNamesSupported: false);

        Assert.Equal("", options.DefaultFloorTexture);
        Assert.Equal(0, options.CustomBrightness);
        Assert.Equal(0, options.CustomFloorHeight);
        Assert.Equal(128, options.CustomCeilingHeight);
        Assert.False(options.UseLongTextureNames);
        Assert.True(double.IsNaN(options.ViewPosition.x));
        Assert.True(double.IsNaN(options.ViewPosition.y));
        Assert.True(double.IsNaN(options.ViewScale));
    }

    [Fact]
    public void WriteDrawingOptionsRemovesStaleOptionalRendererAndScriptSettings()
    {
        var options = new MapOptions
        {
            ViewPosition = new Vector2D(1, 2),
            ViewScale = 3,
            ScriptCompiler = "acc",
        };
        options.WriteDrawingOptions();
        options.ViewPosition = new Vector2D(double.NaN, double.NaN);
        options.ViewScale = double.NaN;
        options.ScriptCompiler = "";

        options.WriteDrawingOptions();

        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewpositionx", double.NaN)));
        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewpositiony", double.NaN)));
        Assert.True(double.IsNaN(options.MapConfiguration.ReadSetting("viewscale", double.NaN)));
        Assert.Equal("", options.MapConfiguration.ReadSetting("scriptcompiler", ""));
    }

    [Fact]
    public void WriteScriptDocumentSettingsStoresUdbCompatibleDocumentEntries()
    {
        var options = new MapOptions();
        var settings = new ScriptDocumentSettings
        {
            Filename = "SCRIPTS",
            Hash = 1234567890123,
            ResourceLocation = "maps/test.wad",
            TabType = ScriptDocumentTabType.Lump,
            ScriptType = ScriptType.Acs,
            CaretPosition = 42,
            FirstVisibleLine = 7,
            IsActiveTab = true,
        };
        settings.FoldLevels[1] = new HashSet<int> { 12, 13 };
        settings.FoldLevels[2] = new HashSet<int> { 21 };
        options.ScriptDocumentSettings[settings.Filename] = settings;

        options.WriteScriptDocumentSettings();

        var document = Assert.IsAssignableFrom<IDictionary>(
            options.MapConfiguration.ReadSetting("scriptdocuments.document0", (IDictionary?)null));
        Assert.Equal("SCRIPTS", document["filename"]);
        Assert.Equal(1234567890123L, document["hash"]);
        Assert.Equal("maps/test.wad", document["resource"]);
        Assert.Equal((int)ScriptDocumentTabType.Lump, document["tabtype"]);
        Assert.Equal((int)ScriptType.Acs, document["scripttype"]);
        Assert.Equal(42, document["caretposition"]);
        Assert.Equal(7, document["firstvisibleline"]);
        Assert.True((bool)document["activetab"]!);
        Assert.Equal("1:12,13;2:21", document["foldlevels"]);
    }

    [Fact]
    public void ReadScriptDocumentSettingsRestoresValidDocuments()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            scriptdocuments
            {
                document0
                {
                    filename = "SCRIPTS";
                    hash = 1234567890123;
                    resource = "maps/test.wad";
                    tabtype = 0;
                    scripttype = 1;
                    caretposition = 42;
                    firstvisibleline = 7;
                    activetab = true;
                    foldlevels = "1:12,13;2:21";
                }
            }
            """);

        options.ReadScriptDocumentSettings();

        var settings = options.ScriptDocumentSettings["SCRIPTS"];
        Assert.Equal(1234567890123L, settings.Hash);
        Assert.Equal("maps/test.wad", settings.ResourceLocation);
        Assert.Equal(ScriptDocumentTabType.Lump, settings.TabType);
        Assert.Equal(ScriptType.Acs, settings.ScriptType);
        Assert.Equal(42, settings.CaretPosition);
        Assert.Equal(7, settings.FirstVisibleLine);
        Assert.True(settings.IsActiveTab);
        Assert.Equal(new[] { 12, 13 }, settings.FoldLevels[1].OrderBy(v => v));
        Assert.Equal(new[] { 21 }, settings.FoldLevels[2].OrderBy(v => v));
    }

    [Fact]
    public void ReadScriptDocumentSettingsReplacesExistingAndSkipsInvalidDocuments()
    {
        var options = new MapOptions();
        options.ScriptDocumentSettings["OLD"] = new ScriptDocumentSettings { Filename = "OLD" };
        options.MapConfiguration.InputConfiguration("""
            scriptdocuments
            {
                document0
                {
                    hash = 99;
                }
                document1
                {
                    filename = "SCRIPTS";
                    foldlevels = "1:12,bad;2:21";
                }
            }
            """);

        options.ReadScriptDocumentSettings();

        Assert.Single(options.ScriptDocumentSettings);
        Assert.False(options.ScriptDocumentSettings.ContainsKey("OLD"));
        var settings = options.ScriptDocumentSettings["SCRIPTS"];
        Assert.False(settings.FoldLevels.ContainsKey(1));
        Assert.Equal(new[] { 21 }, settings.FoldLevels[2].OrderBy(v => v));
    }

    [Fact]
    public void WriteScriptDocumentSettingsRemovesStaleConfigurationWhenEmpty()
    {
        var options = new MapOptions();
        options.ScriptDocumentSettings["SCRIPTS"] = new ScriptDocumentSettings { Filename = "SCRIPTS" };
        options.WriteScriptDocumentSettings();
        options.ScriptDocumentSettings.Clear();

        options.WriteScriptDocumentSettings();

        Assert.Null(options.MapConfiguration.ReadSetting("scriptdocuments", (IDictionary?)null));
    }

    [Fact]
    public void ExternalCommandSettingsDefaultsMatchUdb()
    {
        var settings = new ExternalCommandSettings();

        Assert.Equal("", settings.WorkingDirectory);
        Assert.Equal("", settings.Commands);
        Assert.True(settings.AutoCloseOnSuccess);
        Assert.True(settings.ExitCodeIsError);
        Assert.True(settings.StdErrIsError);
    }

    [Fact]
    public void ExternalCommandSettingsWriteAndReadRoundTrip()
    {
        var configuration = new Configuration(sorted: true);
        var settings = new ExternalCommandSettings
        {
            WorkingDirectory = "/tmp/project",
            Commands = "make test",
            AutoCloseOnSuccess = false,
            ExitCodeIsError = false,
            StdErrIsError = false,
        };

        settings.WriteSettings(configuration, "testprecommand");
        var roundTrip = new ExternalCommandSettings(configuration, "testprecommand");

        Assert.Equal("/tmp/project", roundTrip.WorkingDirectory);
        Assert.Equal("make test", roundTrip.Commands);
        Assert.False(roundTrip.AutoCloseOnSuccess);
        Assert.False(roundTrip.ExitCodeIsError);
        Assert.False(roundTrip.StdErrIsError);
    }

    [Fact]
    public void ExternalCommandSettingsRemovesBlankCommandAndWorkingDirectory()
    {
        var configuration = new Configuration(sorted: true);
        configuration.WriteSetting("testprecommand.commands", "old");
        configuration.WriteSetting("testprecommand.workingdirectory", "/tmp");
        var settings = new ExternalCommandSettings
        {
            WorkingDirectory = " ",
            Commands = " ",
        };

        settings.WriteSettings(configuration, "testprecommand");

        Assert.Equal("", configuration.ReadSetting("testprecommand.commands", ""));
        Assert.Equal("", configuration.ReadSetting("testprecommand.workingdirectory", ""));
        Assert.True(configuration.ReadSetting("testprecommand.autocloseonsuccess", false));
        Assert.True(configuration.ReadSetting("testprecommand.exitcodeiserror", false));
        Assert.True(configuration.ReadSetting("testprecommand.stderriserror", false));
    }

    [Fact]
    public void MapOptionsWritesExternalCommandSections()
    {
        var options = new MapOptions
        {
            ReloadResourcePreCommand = new ExternalCommandSettings { Commands = "reload-pre" },
            ReloadResourcePostCommand = new ExternalCommandSettings { Commands = "reload-post" },
            TestPreCommand = new ExternalCommandSettings { Commands = "test-pre" },
            TestPostCommand = new ExternalCommandSettings { Commands = "test-post" },
        };

        options.WriteExternalCommandSettings();

        Assert.Equal("reload-pre", options.MapConfiguration.ReadSetting("reloadresourceprecommand.commands", ""));
        Assert.Equal("reload-post", options.MapConfiguration.ReadSetting("reloadresourcepostcommand.commands", ""));
        Assert.Equal("test-pre", options.MapConfiguration.ReadSetting("testprecommand.commands", ""));
        Assert.Equal("test-post", options.MapConfiguration.ReadSetting("testpostcommand.commands", ""));
    }

    [Fact]
    public void MapOptionsReadsExternalCommandSections()
    {
        var options = new MapOptions();
        options.MapConfiguration.InputConfiguration("""
            reloadresourceprecommand
            {
                commands = "reload-pre";
            }
            reloadresourcepostcommand
            {
                commands = "reload-post";
            }
            testprecommand
            {
                commands = "test-pre";
            }
            testpostcommand
            {
                commands = "test-post";
            }
            """);

        options.ReadExternalCommandSettings();

        Assert.Equal("reload-pre", options.ReloadResourcePreCommand.Commands);
        Assert.Equal("reload-post", options.ReloadResourcePostCommand.Commands);
        Assert.Equal("test-pre", options.TestPreCommand.Commands);
        Assert.Equal("test-post", options.TestPostCommand.Commands);
    }

    [Fact]
    public void MapOptionsRoundTripsFullExternalCommandSections()
    {
        var options = new MapOptions
        {
            ReloadResourcePreCommand = new ExternalCommandSettings
            {
                Commands = "reload-pre",
                WorkingDirectory = "/tmp/reload-pre",
                AutoCloseOnSuccess = false,
                ExitCodeIsError = true,
                StdErrIsError = false,
            },
            ReloadResourcePostCommand = new ExternalCommandSettings
            {
                Commands = "reload-post",
                WorkingDirectory = "/tmp/reload-post",
                AutoCloseOnSuccess = true,
                ExitCodeIsError = false,
                StdErrIsError = true,
            },
            TestPreCommand = new ExternalCommandSettings
            {
                Commands = "test-pre",
                WorkingDirectory = "/tmp/test-pre",
                AutoCloseOnSuccess = false,
                ExitCodeIsError = false,
                StdErrIsError = true,
            },
            TestPostCommand = new ExternalCommandSettings
            {
                Commands = "test-post",
                WorkingDirectory = "/tmp/test-post",
                AutoCloseOnSuccess = true,
                ExitCodeIsError = true,
                StdErrIsError = false,
            },
        };

        options.WriteExternalCommandSettings();
        var restored = new MapOptions(options.MapConfiguration);
        restored.ReadExternalCommandSettings();

        AssertCommand(restored.ReloadResourcePreCommand, "reload-pre", "/tmp/reload-pre", false, true, false);
        AssertCommand(restored.ReloadResourcePostCommand, "reload-post", "/tmp/reload-post", true, false, true);
        AssertCommand(restored.TestPreCommand, "test-pre", "/tmp/test-pre", false, false, true);
        AssertCommand(restored.TestPostCommand, "test-post", "/tmp/test-post", true, true, false);
    }

    [Fact]
    public void DataLocationListWritesUdbCompatibleResourceEntries()
    {
        var configuration = new Configuration(sorted: true);
        var locations = new DataLocationList
        {
            new(DataLocationType.Wad, "/tmp/base.wad", option1: true, option2: false, notForTesting: true),
            new(DataLocationType.Pk3, "/tmp/mod.pk3", option1: false, option2: true),
        };
        locations[0].RequiredArchives.AddRange(new[] { "doom.wad", "textures.pk3" });

        locations.WriteToConfig(configuration, "resources");

        var first = Assert.IsAssignableFrom<IDictionary>(configuration.ReadSetting("resources.resource0", (IDictionary?)null));
        var second = Assert.IsAssignableFrom<IDictionary>(configuration.ReadSetting("resources.resource1", (IDictionary?)null));
        Assert.Equal((int)DataLocationType.Wad, first["type"]);
        Assert.Equal("/tmp/base.wad", first["location"]);
        Assert.Equal(1, first["option1"]);
        Assert.Equal(0, first["option2"]);
        Assert.Equal(1, first["notfortesting"]);
        Assert.Equal("doom.wad,textures.pk3", first["requiredarchives"]);
        Assert.Equal((int)DataLocationType.Pk3, second["type"]);
        Assert.Equal(0, second["option1"]);
        Assert.Equal(1, second["option2"]);
    }

    [Fact]
    public void DataLocationListReadsResourceEntries()
    {
        var configuration = new Configuration(sorted: true);
        configuration.InputConfiguration("""
            resources
            {
                resource0
                {
                    type = 0;
                    location = "/tmp/base.wad";
                    option1 = 1;
                    option2 = 0;
                    notfortesting = 1;
                    requiredarchives = "doom.wad,textures.pk3";
                }
            }
            """);

        var locations = new DataLocationList(configuration, "resources");

        var location = Assert.Single(locations);
        Assert.Equal(DataLocationType.Wad, location.Type);
        Assert.Equal("/tmp/base.wad", location.Location);
        Assert.True(location.Option1);
        Assert.False(location.Option2);
        Assert.True(location.NotForTesting);
        Assert.Equal(new[] { "doom.wad", "textures.pk3" }, location.RequiredArchives);
    }

    [Fact]
    public void DataLocationListTrimsRequiredArchiveEntries()
    {
        var configuration = new Configuration(sorted: true);
        configuration.InputConfiguration("""
            resources
            {
                resource0
                {
                    type = 2;
                    location = "/tmp/mod.pk3";
                    requiredarchives = "doom.wad, textures.pk3, , extras.pk3 ";
                }
            }
            """);

        var locations = new DataLocationList(configuration, "resources");

        var location = Assert.Single(locations);
        Assert.Equal(new[] { "doom.wad", "textures.pk3", "extras.pk3" }, location.RequiredArchives);
    }

    [Fact]
    public void DataLocationRequiredArchivesTextTrimsCommaSeparatedEntries()
    {
        var location = new DataLocation(DataLocationType.Pk3, "/tmp/mod.pk3")
        {
            RequiredArchivesText = "doom.wad, textures.pk3, , extras.pk3 ",
        };

        Assert.Equal(new[] { "doom.wad", "textures.pk3", "extras.pk3" }, location.RequiredArchives);
        Assert.Equal("doom.wad, textures.pk3, extras.pk3", location.RequiredArchivesText);

        location.RequiredArchivesText = "";

        Assert.Empty(location.RequiredArchives);
    }

    [Fact]
    public void DataLocationListCombinedKeepsLaterDuplicates()
    {
        var first = new DataLocationList
        {
            new(DataLocationType.Wad, "/tmp/base.wad", option1: false),
            new(DataLocationType.Pk3, "/tmp/extra.pk3"),
        };
        var second = new DataLocationList
        {
            new(DataLocationType.Wad, "/tmp/base.wad", option1: true),
        };

        var combined = DataLocationList.Combined(first, second);

        Assert.Equal(2, combined.Count);
        Assert.True(combined.Single(location => location.Location == "/tmp/base.wad").Option1);
    }

    [Fact]
    public void DataLocationDisplayNameMatchesResourceType()
    {
        var directory = new DataLocation(DataLocationType.Directory, Path.Combine(Path.GetTempPath(), "resource-dir"));
        var wad = new DataLocation(DataLocationType.Wad, Path.Combine(Path.GetTempPath(), "base.wad"));
        var nestedWad = new DataLocation(DataLocationType.Wad, Path.Combine(Path.GetTempPath(), "outer.pk3"))
        {
            InitialLocation = "maps/nested.wad",
        };
        var pk3 = new DataLocation(DataLocationType.Pk3, Path.Combine(Path.GetTempPath(), "mod.pk3"));

        Assert.Equal("resource-dir", directory.GetDisplayName());
        Assert.Equal("base.wad", wad.GetDisplayName());
        Assert.Equal("maps/nested.wad", nestedWad.GetDisplayName());
        Assert.Equal("mod.pk3", pk3.GetDisplayName());
    }

    [Fact]
    public void DataLocationCopiesPreserveInitialLocation()
    {
        var source = new DataLocationList
        {
            new(DataLocationType.Wad, Path.Combine(Path.GetTempPath(), "outer.pk3"))
            {
                InitialLocation = "maps/nested.wad",
            },
        };

        var copy = new DataLocationList(source);
        source[0].InitialLocation = "changed.wad";

        Assert.Equal("maps/nested.wad", copy[0].InitialLocation);
        Assert.Equal("maps/nested.wad", copy[0].GetDisplayName());
    }

    [Fact]
    public void MapOptionsResourceHelpersReplaceByFullPathAndReturnCopies()
    {
        var options = new MapOptions();
        string path = Path.Combine(Path.GetTempPath(), "dbuilder-resource-test.wad");
        var first = new DataLocation(DataLocationType.Wad, path, option1: false);
        var second = new DataLocation(DataLocationType.Wad, path, option1: true);

        int firstIndex = options.AddResource(first);
        int secondIndex = options.AddResource(second);
        var resources = options.GetResources();
        resources[0].Option1 = false;

        Assert.Equal(0, firstIndex);
        Assert.Equal(0, secondIndex);
        Assert.Single(options.GetResources());
        Assert.True(options.GetResources()[0].Option1);
    }

    [Fact]
    public void MapOptionsReadsAndWritesResources()
    {
        var options = new MapOptions();
        options.AddResource(new DataLocation(DataLocationType.Directory, Path.GetTempPath(), option2: true));

        options.WriteResources();
        var restored = new MapOptions(options.MapConfiguration);
        restored.ReadResources();

        var location = Assert.Single(restored.GetResources());
        Assert.Equal(DataLocationType.Directory, location.Type);
        Assert.True(location.Option2);
    }

    [Fact]
    public void MapOptionsRootRoundTripPreservesPerMapResources()
    {
        var source = new MapOptions { CurrentName = "MAP01", ConfigFile = "GZDoom_DoomUDMF.cfg" };
        source.AddResource(new DataLocation(DataLocationType.Pk3, "/tmp/textures.pk3", option1: true, option2: true));
        source.WriteResources();
        var root = new Configuration(sorted: true);

        source.WriteRootOptions(root);

        var restored = new MapOptions();
        restored.ReadRootOptions(root, "MAP01");
        restored.ReadResources();
        var location = Assert.Single(restored.GetResources());
        Assert.Equal("GZDoom_DoomUDMF.cfg", restored.ConfigFile);
        Assert.Equal(DataLocationType.Pk3, location.Type);
        Assert.True(location.Option1);
        Assert.True(location.Option2);
    }

    [Fact]
    public void DataLocationIsValidChecksTypeSpecificPath()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            Assert.True(new DataLocation(DataLocationType.Wad, tempFile).IsValid());
            Assert.True(new DataLocation(DataLocationType.Directory, Path.GetTempPath()).IsValid());
            Assert.False(new DataLocation(DataLocationType.Pk3, tempFile + ".missing").IsValid());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PluginSettingsUseLowercasePluginPrefix()
    {
        var options = new MapOptions();

        Assert.True(options.WritePluginSetting("TagRange", "enabled", true));
        Assert.True(options.WritePluginSetting("TagRange", "count", 3));
        Assert.True(options.WritePluginSetting("TagRange", "label", "range"));

        Assert.True(options.MapConfiguration.ReadSetting("tagrange.enabled", false));
        Assert.Equal(3, options.ReadPluginSetting("TagRange", "count", 0));
        Assert.Equal("range", options.ReadPluginSetting("TagRange", "label", ""));
    }

    [Fact]
    public void DeletePluginSettingRemovesPluginValue()
    {
        var options = new MapOptions();
        options.WritePluginSetting("TagRange", "enabled", true);

        Assert.True(options.DeletePluginSetting("TagRange", "enabled"));

        Assert.False(options.ReadPluginSetting("TagRange", "enabled", false));
    }

    [Fact]
    public void ToStringReturnsCurrentName()
    {
        var options = new MapOptions { CurrentName = "MAP01" };

        Assert.Equal("MAP01", options.ToString());
    }

    [Fact]
    public void UniversalFieldTypeFallsBackToMapConfiguration()
    {
        var options = new MapOptions();

        options.SetUniversalFieldType("thing", "health", 1);

        Assert.Equal(1, options.GetUniversalFieldType("thing", "health", 0));
        Assert.Equal(0, options.GetUniversalFieldType("thing", "missing", 0));
        Assert.Equal(1, options.MapConfiguration.ReadSetting("fieldtypes.thing.health", 0));
    }

    [Fact]
    public void UniversalFieldTypePrefersGameConfiguration()
    {
        var options = new MapOptions();
        options.SetUniversalFieldType("thing", "health", 1);
        var gameConfiguration = new Configuration(sorted: true);
        gameConfiguration.InputConfiguration("""
            universalfields
            {
                thing
                {
                    health
                    {
                        type = 2;
                    }
                }
            }
            """);

        int fieldType = options.GetUniversalFieldType("thing", "health", 0, gameConfiguration);
        options.SetUniversalFieldType("thing", "health", 3, gameConfiguration);

        Assert.Equal(2, fieldType);
        Assert.Equal(1, options.MapConfiguration.ReadSetting("fieldtypes.thing.health", 0));
    }

    [Fact]
    public void ForgetUniversalFieldTypesRemovesOverrides()
    {
        var options = new MapOptions();
        options.SetUniversalFieldType("thing", "health", 1);

        options.ForgetUniversalFieldTypes();

        Assert.Equal(0, options.GetUniversalFieldType("thing", "health", 0));
    }

    private static MapSet BuildMap()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var v2 = map.AddVertex(new Vector2D(64, 64));
        var line = map.AddLinedef(v0, v1);
        map.AddLinedef(v1, v2);
        map.AddSidedef(line, true, sector);
        map.AddThing(new Vector2D(32, 16), 3001);
        map.BuildIndexes();
        return map;
    }

    private static void AssertCommand(
        ExternalCommandSettings settings,
        string commands,
        string workingDirectory,
        bool autoCloseOnSuccess,
        bool exitCodeIsError,
        bool stdErrIsError)
    {
        Assert.Equal(commands, settings.Commands);
        Assert.Equal(workingDirectory, settings.WorkingDirectory);
        Assert.Equal(autoCloseOnSuccess, settings.AutoCloseOnSuccess);
        Assert.Equal(exitCodeIsError, settings.ExitCodeIsError);
        Assert.Equal(stdErrIsError, settings.StdErrIsError);
    }
}
