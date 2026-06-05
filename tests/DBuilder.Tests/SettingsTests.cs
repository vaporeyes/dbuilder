// ABOUTME: Tests Settings persistence (JSON round-trip, resilient load) and the recent-files list semantics.
// ABOUTME: Uses a temp file path so it never touches the real user settings.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SettingsTests
{
    [Fact]
    public void AddRecentMovesToFrontAndDedupes()
    {
        var s = new Settings();
        s.AddRecent("/a.wad");
        s.AddRecent("/b.wad");
        s.AddRecent("/a.wad"); // re-adding moves it to front, no duplicate
        Assert.Equal(new[] { "/a.wad", "/b.wad" }, s.RecentFiles);
    }

    [Fact]
    public void AddRecentIsCaseInsensitiveForDedup()
    {
        var s = new Settings();
        s.AddRecent("/Maps/E1M1.wad");
        s.AddRecent("/maps/e1m1.WAD");
        Assert.Single(s.RecentFiles);
    }

    [Fact]
    public void AddRecentCapsAtMax()
    {
        var s = new Settings();
        for (int i = 0; i < Settings.DefaultMaxRecentFiles + 5; i++) s.AddRecent($"/wad{i}.wad");
        Assert.Equal(Settings.DefaultMaxRecentFiles, s.RecentFiles.Count);
        Assert.Equal($"/wad{Settings.DefaultMaxRecentFiles + 4}.wad", s.RecentFiles[0]); // most recent first
    }

    [Fact]
    public void AddRecentMapMovesToFrontAndDedupes()
    {
        var s = new Settings();
        s.AddRecentMap("/maps/a.wad", "MAP01");
        s.AddRecentMap("/maps/a.wad", "MAP02");
        s.AddRecentMap("/MAPS/A.WAD", "map01");

        Assert.Equal(2, s.RecentMaps.Count);
        Assert.Equal("/MAPS/A.WAD", s.RecentMaps[0].Path);
        Assert.Equal("map01", s.RecentMaps[0].MapName);
        Assert.Equal("MAP02", s.RecentMaps[1].MapName);
    }

    [Fact]
    public void AddRecentMapSeparatesPk3ArchiveEntries()
    {
        var s = new Settings();
        s.AddRecentMap("/mods/a.pk3", "MAP01", "maps/a.wad");
        s.AddRecentMap("/mods/a.pk3", "MAP01", "maps/b.wad");

        Assert.Equal(2, s.RecentMaps.Count);
        Assert.Equal("maps/b.wad", s.RecentMaps[0].ArchivePath);
        Assert.Equal("maps/a.wad", s.RecentMaps[1].ArchivePath);
    }

    [Fact]
    public void AddRecentMapCapsAtMax()
    {
        var s = new Settings();
        for (int i = 0; i < Settings.DefaultMaxRecentFiles + 5; i++) s.AddRecentMap("/maps/a.wad", $"MAP{i:00}");
        Assert.Equal(Settings.DefaultMaxRecentFiles, s.RecentMaps.Count);
        Assert.Equal($"MAP{Settings.DefaultMaxRecentFiles + 4:00}", s.RecentMaps[0].MapName);
    }

    [Fact]
    public void RecentListsUseConfiguredUdbLimit()
    {
        var s = new Settings { MaxRecentFiles = 12 };

        for (int i = 0; i < 20; i++)
        {
            s.AddRecent($"/wad{i}.wad");
            s.AddRecentMap("/maps/a.wad", $"MAP{i:00}");
        }

        Assert.Equal(12, s.NormalizedMaxRecentFiles);
        Assert.Equal(12, s.RecentFiles.Count);
        Assert.Equal(12, s.RecentMaps.Count);
        Assert.Equal("/wad19.wad", s.RecentFiles[0]);
        Assert.Equal("MAP19", s.RecentMaps[0].MapName);
    }

    [Fact]
    public void MaxRecentFilesClampsToUdbPreferenceRange()
    {
        Assert.Equal(Settings.DefaultMaxRecentFiles, new Settings().NormalizedMaxRecentFiles);
        Assert.Equal(Settings.MinMaxRecentFiles, new Settings { MaxRecentFiles = 1 }.NormalizedMaxRecentFiles);
        Assert.Equal(Settings.MaxMaxRecentFiles, new Settings { MaxRecentFiles = 50 }.NormalizedMaxRecentFiles);
    }

    [Fact]
    public void VisualAlphaBasedTextureHighlightingDefaultsEnabledLikeUdb()
    {
        Assert.True(new Settings().AlphaBasedTextureHighlighting);
    }

    [Fact]
    public void ModelRenderModeDefaultsToAllLikeUdb()
    {
        Assert.Equal(ThingModelRenderMode.All, new Settings().NormalizedModelRenderMode);
    }

    [Fact]
    public void EnhancedRenderingEffectsDefaultsEnabledLikeUdb()
    {
        Assert.True(new Settings().EnhancedRenderingEffects);
    }

    [Fact]
    public void ClassicRenderingDefaultsDisabledLikeUdb()
    {
        Assert.False(new Settings().ClassicRendering);
    }

    [Fact]
    public void GzVisualToggleDefaultsMatchUdb()
    {
        var settings = new Settings();

        Assert.False(settings.DrawFog);
        Assert.True(settings.DrawSky);
        Assert.True(settings.ShowEventLines);
        Assert.True(settings.ShowVisualVertices);
    }

    [Fact]
    public void AdjacentVisualVertexSlopeSelectionDefaultsDisabled()
    {
        Assert.False(new Settings().SelectAdjacentVisualVertexSlopeHandles);
    }

    [Fact]
    public void EnabledMapErrorCheckersUseUdbDefaults()
    {
        var settings = new Settings();

        var enabled = settings.EnabledMapErrorCheckers();

        Assert.Contains(enabled, checker => checker.DisplayName == "Check missing textures");
        Assert.DoesNotContain(enabled, checker => checker.DisplayName == "Check texture alignment");
        Assert.DoesNotContain(enabled, checker => checker.DisplayName == "Check very short linedefs");
    }

    [Fact]
    public void EnabledMapErrorCheckersApplyPersistedOverrides()
    {
        var settings = new Settings();
        var textureAlignment = MapAnalysis.CheckerDescriptors.Single(checker => checker.DisplayName == "Check texture alignment");
        var stuckThings = MapAnalysis.CheckerDescriptors.Single(checker => checker.DisplayName == "Check stuck things");

        settings.SetMapErrorCheckerEnabled(textureAlignment, enabled: true);
        settings.SetMapErrorCheckerEnabled(stuckThings, enabled: false);
        var enabled = settings.EnabledMapErrorCheckers();

        Assert.Contains(textureAlignment, enabled);
        Assert.DoesNotContain(stuckThings, enabled);
        Assert.True(settings.MapErrorCheckSettings["errorchecks.checktexturealignment"]);
        Assert.False(settings.MapErrorCheckSettings["errorchecks.checkstuckthings"]);
    }

    [Fact]
    public void MapErrorCheckerSelectionRoundTripsThroughSettings()
    {
        var settings = new Settings();

        var selection = settings.MapErrorCheckerSelection();
        selection.SetChecked("errorchecks.checktexturealignment", true);
        selection.SetChecked("errorchecks.checkmissingtextures", false);
        settings.ApplyMapErrorCheckerSelection(selection);

        Assert.True(settings.MapErrorCheckSettings["errorchecks.checktexturealignment"]);
        Assert.False(settings.MapErrorCheckSettings["errorchecks.checkmissingtextures"]);
        Assert.Contains(settings.EnabledMapErrorCheckers(), checker => checker.SettingsKey == "errorchecks.checktexturealignment");
        Assert.DoesNotContain(settings.EnabledMapErrorCheckers(), checker => checker.SettingsKey == "errorchecks.checkmissingtextures");
    }

    [Fact]
    public void ExistingRecentFilesSkipsMissingPathsLikeUdbMenu()
    {
        var s = new Settings
        {
            RecentFiles = new() { "/missing.wad", "/present.wad" },
        };

        Assert.Equal(new[] { "/present.wad" }, s.ExistingRecentFiles(path => path.Contains("present")));
    }

    [Fact]
    public void ExistingRecentFilesCapsVisibleRowsToConfiguredUdbLimit()
    {
        var s = new Settings
        {
            MaxRecentFiles = Settings.MinMaxRecentFiles,
            RecentFiles = Enumerable.Range(0, Settings.MinMaxRecentFiles + 3)
                .Select(i => $"/wad{i}.wad")
                .ToList(),
        };

        Assert.Equal(
            Enumerable.Range(0, Settings.MinMaxRecentFiles).Select(i => $"/wad{i}.wad"),
            s.ExistingRecentFiles(_ => true));
    }

    [Fact]
    public void ExistingRecentFilesCapsAfterSkippingMissingRows()
    {
        var s = new Settings
        {
            MaxRecentFiles = Settings.MinMaxRecentFiles,
            RecentFiles = Enumerable.Range(0, Settings.MinMaxRecentFiles + 2)
                .Select(i => $"/wad{i}.wad")
                .Prepend("/missing.wad")
                .ToList(),
        };

        Assert.Equal(
            Enumerable.Range(0, Settings.MinMaxRecentFiles).Select(i => $"/wad{i}.wad"),
            s.ExistingRecentFiles(path => path != "/missing.wad"));
    }

    [Fact]
    public void ExistingRecentMapsSkipsMissingArchivePaths()
    {
        var s = new Settings
        {
            RecentMaps = new()
            {
                new RecentMapReference { Path = "/missing.pk3", MapName = "MAP01", ArchivePath = "maps/a.wad" },
                new RecentMapReference { Path = "/present.pk3", MapName = "MAP02", ArchivePath = "maps/b.wad" },
            },
        };

        var map = Assert.Single(s.ExistingRecentMaps(path => path.Contains("present")));
        Assert.Equal("MAP02", map.MapName);
        Assert.Equal("maps/b.wad", map.ArchivePath);
    }

    [Fact]
    public void ExistingRecentMapsCapsVisibleRowsToConfiguredUdbLimit()
    {
        var s = new Settings
        {
            MaxRecentFiles = Settings.MinMaxRecentFiles,
            RecentMaps = Enumerable.Range(0, Settings.MinMaxRecentFiles + 3)
                .Select(i => new RecentMapReference { Path = $"/maps/{i}.wad", MapName = $"MAP{i:00}" })
                .ToList(),
        };

        IReadOnlyList<RecentMapReference> maps = s.ExistingRecentMaps(_ => true);

        Assert.Equal(Settings.MinMaxRecentFiles, maps.Count);
        Assert.Equal("MAP00", maps[0].MapName);
        Assert.Equal($"MAP{Settings.MinMaxRecentFiles - 1:00}", maps[^1].MapName);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_settings_{System.Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings
            {
                ConfigDir = "/cfg",
                LastUsedConfigName = "Hexen_HexenHexen.cfg",
                LastUsedMapFolder = "/maps",
                TestPort = "/gz",
                TestIwad = "/iwad.wad",
                TestAdditionalParameters = "+set test_value 1",
                UdbScriptExternalEditor = "/tools/editor.exe",
                UdbScriptSettings = new Dictionary<string, object?>
                {
                    ["scriptslots.slot3"] = "/scripts/slotted.js",
                    ["directoryexpand.hash-child"] = false,
                },
                UsdfDialogEditorSettings = new Dictionary<string, object?>
                {
                    [UsdfDialogEditorModel.PositionXKey] = 44,
                    [UsdfDialogEditorModel.PositionYKey] = 55,
                    [UsdfDialogEditorModel.SizeWidthKey] = 777,
                    [UsdfDialogEditorModel.SizeHeightKey] = 555,
                    [UsdfDialogEditorModel.WindowStateKey] = UsdfDialogEditorModel.NormalWindowState,
                },
                MapErrorCheckSettings = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["errorchecks.checktexturealignment"] = true,
                    ["errorchecks.checkstuckthings"] = false,
                },
                MaxRecentFiles = 12,
                AutoClearSidedefTextures = false,
                AlphaBasedTextureHighlighting = false,
                SelectAdjacentVisualVertexSlopeHandles = true,
                DynamicGridSize = false,
                DefaultViewMode = 3,
                ModelRenderMode = (int)ThingModelRenderMode.Selection,
                LightRenderMode = (int)ThingLightRenderMode.Animated,
                EnhancedRenderingEffects = false,
                ClassicRendering = true,
                DrawFog = true,
                DrawSky = false,
                ShowEventLines = false,
                ShowVisualVertices = false,
                StatusHistoryLimit = 250,
                MergeGeometryMode = MergeGeometryMode.Merge,
                PasteOptions = new PasteOptions
                {
                    ChangeTags = PasteTagMode.Renumber,
                    RemoveActions = true,
                },
                DrawLineSettings = new DrawLineModeSettings(ContinuousDrawing: true, AutoCloseDrawing: true),
                DrawRectangleSettings = new DrawRectangleModeSettings(Subdivisions: 6, BevelWidth: 12),
                DrawEllipseSettings = new DrawEllipseModeSettings(Subdivisions: 10, BevelWidth: -8, Angle: 45),
                DrawCurveSettings = new DrawCurveModeSettings(SegmentLength: 96, ContinuousDrawing: true),
                DrawGridSettings = new DrawGridModeSettings(HorizontalSlices: 5, VerticalSlices: 7, Triangulate: true),
                EditSelectionSettings = new EditSelectionModeSettings(
                    UsePrecisePosition: false,
                    HeightAdjustMode: EditSelectionHeightAdjustMode.AdjustBoth),
                AutomapSettings = new AutomapModeSettings(
                    ShowHiddenLines: true,
                    ShowSecretSectors: true,
                    ShowLocks: false,
                    ShowTextures: false,
                    ColorPreset: AutomapColorPreset.Strife),
                ThreeDFloorControlSectorAreaSettings = new ThreeDFloorControlSectorAreaSettings(
                    UseCustomTagRange: true,
                    FirstTag: 2000,
                    LastTag: 2050,
                    OuterLeft: -2048,
                    OuterRight: -1024,
                    OuterTop: 1024,
                    OuterBottom: 512),
                MakeDoorSettings = new MakeDoorModeSettings(
                    HasValues: true,
                    DoorTexture: " BIGDOOR2 ",
                    TrackTexture: "DOORTRAK",
                    CeilingTexture: "CEIL5_1",
                    FloorTexture: "STEP1",
                    ResetOffsets: false,
                    ApplyActionSpecials: false,
                    ApplyTag: true),
                TagRangeSettings = new TagRangeStoredOptions(Step: 4, Relative: true),
                TagExplorerSettings = new TagExplorerPersistedSettings(
                    TagExplorerDisplayMode.Actions,
                    TagExplorerSortMode.ByAction,
                    CommentsOnly: true,
                    CenterOnSelected: true,
                    SelectOnClick: true),
                RejectExplorerColors = new RejectExplorerColorSettings(
                    Default: unchecked((int)0xFF010203),
                    Highlight: unchecked((int)0xFF040506),
                    Bidirectional: unchecked((int)0xFF070809),
                    UnidirectionalFrom: unchecked((int)0xFF0A0B0C),
                    UnidirectionalTo: unchecked((int)0xFF0D0E0F)),
                SoundPropagationColors = new SoundPropagationColorSettings(
                    HighlightColor: 0xFF101112u,
                    Level1Color: 0xFF131415u,
                    Level2Color: 0xFF161718u,
                    NoSoundColor: 0xFF191A1Bu,
                    BlockSoundColor: 0xFF1C1D1Eu,
                    DistinctDomainColors: new[] { 0xFF202122u }),
                StairBuilderPrefabs = new()
                {
                    new StairBuilderPrefab
                    {
                        Name = "[Default]",
                        FloorStep = 12,
                        ApplyFloorHeight = true,
                        CeilingStep = 24,
                        ApplyCeilingHeight = true,
                    },
                },
                VisplaneExplorerSettings = new VisplaneExplorerInterfaceSettings(
                    OpenDoors: true,
                    ShowHeatmap: true,
                    SelectedStat: VisplaneExplorerStat.Openings,
                    ViewHeight: 56,
                    ViewHeightCustom: 72),
                WindowX = 120,
                WindowY = 80,
                WindowWidth = 1280,
                WindowHeight = 900,
                ShortcutOverrides = new()
                {
                    new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
                },
            };
            s.SetResourcesForConfiguration("Doom_Doom2Doom.cfg", new[]
            {
                new DataLocation(DataLocationType.Pk3, "/textures.pk3", option1: true, option2: true)
                {
                    RequiredArchives = new List<string> { "doom2.wad" },
                },
            });
            s.AddRecent("/x.wad");
            s.AddRecentMap("/x.wad", "MAP01");
            Assert.True(s.Save(path));

            var loaded = Settings.Load(path);
            Assert.Equal("/cfg", loaded.ConfigDir);
            Assert.Equal("Hexen_HexenHexen.cfg", loaded.LastUsedConfigName);
            Assert.Equal("/maps", loaded.LastUsedMapFolder);
            Assert.Equal("/gz", loaded.TestPort);
            Assert.Equal("/iwad.wad", loaded.TestIwad);
            Assert.Equal("+set test_value 1", loaded.TestAdditionalParameters);
            Assert.Equal("/tools/editor.exe", loaded.UdbScriptExternalEditor);
            Assert.Equal("/scripts/slotted.js", loaded.UdbScriptSettings["scriptslots.slot3"]?.ToString());
            Assert.Equal("False", loaded.UdbScriptSettings["directoryexpand.hash-child"]?.ToString());
            Assert.Equal("44", loaded.UsdfDialogEditorSettings[UsdfDialogEditorModel.PositionXKey]?.ToString());
            Assert.Equal("55", loaded.UsdfDialogEditorSettings[UsdfDialogEditorModel.PositionYKey]?.ToString());
            Assert.Equal("777", loaded.UsdfDialogEditorSettings[UsdfDialogEditorModel.SizeWidthKey]?.ToString());
            Assert.Equal("555", loaded.UsdfDialogEditorSettings[UsdfDialogEditorModel.SizeHeightKey]?.ToString());
            Assert.Equal(
                UsdfDialogEditorModel.NormalWindowState.ToString(),
                loaded.UsdfDialogEditorSettings[UsdfDialogEditorModel.WindowStateKey]?.ToString());
            Assert.True(loaded.MapErrorCheckSettings["errorchecks.checktexturealignment"]);
            Assert.False(loaded.MapErrorCheckSettings["errorchecks.checkstuckthings"]);
            Assert.Equal(12, loaded.MaxRecentFiles);
            Assert.Equal(12, loaded.NormalizedMaxRecentFiles);
            Assert.False(loaded.AutoClearSidedefTextures);
            Assert.False(loaded.AlphaBasedTextureHighlighting);
            Assert.True(loaded.SelectAdjacentVisualVertexSlopeHandles);
            Assert.False(loaded.DynamicGridSize);
            Assert.Equal(3, loaded.DefaultViewMode);
            Assert.Equal(3, loaded.NormalizedDefaultViewMode);
            Assert.Equal((int)ThingModelRenderMode.Selection, loaded.ModelRenderMode);
            Assert.Equal(ThingModelRenderMode.Selection, loaded.NormalizedModelRenderMode);
            Assert.Equal((int)ThingLightRenderMode.Animated, loaded.LightRenderMode);
            Assert.Equal(ThingLightRenderMode.Animated, loaded.NormalizedLightRenderMode);
            Assert.False(loaded.EnhancedRenderingEffects);
            Assert.True(loaded.ClassicRendering);
            Assert.True(loaded.DrawFog);
            Assert.False(loaded.DrawSky);
            Assert.False(loaded.ShowEventLines);
            Assert.False(loaded.ShowVisualVertices);
            Assert.Equal(250, loaded.StatusHistoryLimit);
            Assert.Equal(250, loaded.NormalizedStatusHistoryLimit);
            Assert.Equal(MergeGeometryMode.Merge, loaded.MergeGeometryMode);
            Assert.Equal(MergeGeometryMode.Merge, loaded.NormalizedMergeGeometryMode);
            Assert.Equal(PasteTagMode.Renumber, loaded.PasteOptions.ChangeTags);
            Assert.True(loaded.PasteOptions.RemoveActions);
            Assert.Equal(PasteTagMode.Renumber, loaded.NormalizedPasteOptions.ChangeTags);
            Assert.True(loaded.NormalizedPasteOptions.RemoveActions);
            Assert.True(loaded.DrawLineSettings.ContinuousDrawing);
            Assert.True(loaded.DrawLineSettings.AutoCloseDrawing);
            Assert.Equal(6, loaded.DrawRectangleSettings.Subdivisions);
            Assert.Equal(12, loaded.DrawRectangleSettings.BevelWidth);
            Assert.Equal(6, loaded.NormalizedDrawRectangleSettings.Subdivisions);
            Assert.Equal(10, loaded.DrawEllipseSettings.Subdivisions);
            Assert.Equal(-8, loaded.DrawEllipseSettings.BevelWidth);
            Assert.Equal(45, loaded.DrawEllipseSettings.Angle);
            Assert.Equal(10, loaded.NormalizedDrawEllipseSettings.Subdivisions);
            Assert.Equal(96, loaded.DrawCurveSettings.SegmentLength);
            Assert.Equal(96, loaded.NormalizedDrawCurveSettings.SegmentLength);
            Assert.Equal(5, loaded.DrawGridSettings.HorizontalSlices);
            Assert.Equal(7, loaded.DrawGridSettings.VerticalSlices);
            Assert.True(loaded.DrawGridSettings.Triangulate);
            Assert.Equal(5, loaded.NormalizedDrawGridSettings.HorizontalSlices);
            Assert.False(loaded.EditSelectionSettings.UsePrecisePosition);
            Assert.Equal(EditSelectionHeightAdjustMode.AdjustBoth, loaded.EditSelectionSettings.HeightAdjustMode);
            Assert.Equal(EditSelectionHeightAdjustMode.AdjustBoth, loaded.NormalizedEditSelectionSettings.HeightAdjustMode);
            Assert.True(loaded.AutomapSettings.ShowHiddenLines);
            Assert.True(loaded.AutomapSettings.ShowSecretSectors);
            Assert.False(loaded.AutomapSettings.ShowLocks);
            Assert.False(loaded.AutomapSettings.ShowTextures);
            Assert.Equal(AutomapColorPreset.Strife, loaded.AutomapSettings.ColorPreset);
            Assert.Equal(AutomapColorPreset.Strife, loaded.NormalizedAutomapSettings.ColorPreset);
            Assert.True(loaded.NormalizedThreeDFloorControlSectorAreaSettings.UseCustomTagRange);
            Assert.Equal(2000, loaded.NormalizedThreeDFloorControlSectorAreaSettings.FirstTag);
            Assert.Equal(2050, loaded.NormalizedThreeDFloorControlSectorAreaSettings.LastTag);
            Assert.Equal(-2048, loaded.NormalizedThreeDFloorControlSectorAreaSettings.OuterLeft);
            Assert.Equal(-1024, loaded.NormalizedThreeDFloorControlSectorAreaSettings.OuterRight);
            Assert.Equal(1024, loaded.NormalizedThreeDFloorControlSectorAreaSettings.OuterTop);
            Assert.Equal(512, loaded.NormalizedThreeDFloorControlSectorAreaSettings.OuterBottom);
            Assert.True(loaded.MakeDoorSettings.HasValues);
            Assert.Equal("BIGDOOR2", loaded.NormalizedMakeDoorSettings.DoorTexture);
            Assert.Equal("DOORTRAK", loaded.NormalizedMakeDoorSettings.TrackTexture);
            Assert.Equal("CEIL5_1", loaded.NormalizedMakeDoorSettings.CeilingTexture);
            Assert.Equal("STEP1", loaded.NormalizedMakeDoorSettings.FloorTexture);
            Assert.False(loaded.NormalizedMakeDoorSettings.ResetOffsets);
            Assert.False(loaded.NormalizedMakeDoorSettings.ApplyActionSpecials);
            Assert.True(loaded.NormalizedMakeDoorSettings.ApplyTag);
            Assert.Equal(4, loaded.NormalizedTagRangeSettings.Step);
            Assert.True(loaded.NormalizedTagRangeSettings.Relative);
            Assert.Equal(TagExplorerDisplayMode.Actions, loaded.TagExplorerSettings.DisplayMode);
            Assert.Equal(TagExplorerSortMode.ByAction, loaded.TagExplorerSettings.SortMode);
            Assert.True(loaded.TagExplorerSettings.CommentsOnly);
            Assert.True(loaded.TagExplorerSettings.CenterOnSelected);
            Assert.True(loaded.TagExplorerSettings.SelectOnClick);
            Assert.Equal(unchecked((int)0xFF010203), loaded.RejectExplorerColors.Default);
            Assert.Equal(unchecked((int)0xFF040506), loaded.RejectExplorerColors.Highlight);
            Assert.Equal(unchecked((int)0xFF070809), loaded.RejectExplorerColors.Bidirectional);
            Assert.Equal(unchecked((int)0xFF0A0B0C), loaded.RejectExplorerColors.UnidirectionalFrom);
            Assert.Equal(unchecked((int)0xFF0D0E0F), loaded.RejectExplorerColors.UnidirectionalTo);
            Assert.Equal(0xFF101112u, loaded.SoundPropagationColors.HighlightColor);
            Assert.Equal(0xFF131415u, loaded.SoundPropagationColors.Level1Color);
            Assert.Equal(0xFF161718u, loaded.SoundPropagationColors.Level2Color);
            Assert.Equal(0xFF191A1Bu, loaded.SoundPropagationColors.NoSoundColor);
            Assert.Equal(0xFF1C1D1Eu, loaded.SoundPropagationColors.BlockSoundColor);
            Assert.Equal(new[] { 0xFF202122u }, loaded.SoundPropagationColors.DistinctDomainColors);
            StairBuilderPrefab stairPrefab = Assert.Single(loaded.StairBuilderPrefabs);
            Assert.Equal("[Default]", stairPrefab.Name);
            Assert.True(stairPrefab.ApplyFloorHeight);
            Assert.Equal(12, stairPrefab.FloorStep);
            Assert.True(stairPrefab.ApplyCeilingHeight);
            Assert.Equal(24, stairPrefab.CeilingStep);
            Assert.True(loaded.VisplaneExplorerSettings.OpenDoors);
            Assert.True(loaded.VisplaneExplorerSettings.ShowHeatmap);
            Assert.Equal(VisplaneExplorerStat.Openings, loaded.VisplaneExplorerSettings.SelectedStat);
            Assert.Equal(56, loaded.VisplaneExplorerSettings.ViewHeight);
            Assert.Equal(72, loaded.VisplaneExplorerSettings.ViewHeightCustom);
            Assert.Equal(120, loaded.WindowX);
            Assert.Equal(80, loaded.WindowY);
            Assert.Equal(1280, loaded.WindowWidth);
            Assert.Equal(900, loaded.WindowHeight);
            Assert.Contains("/x.wad", loaded.RecentFiles);
            Assert.Contains(loaded.RecentMaps, m => m.Path == "/x.wad" && m.MapName == "MAP01");
            Assert.Contains(loaded.ShortcutOverrides, b => b.CommandId == "window.save" && b.Key == "F5");
            var configResource = Assert.Single(loaded.ResourcesForConfiguration("Doom_Doom2Doom"));
            Assert.Equal(DataLocationType.Pk3, configResource.Type);
            Assert.Equal("/textures.pk3", configResource.Location);
            Assert.True(configResource.Option1);
            Assert.True(configResource.Option2);
            Assert.Equal(new[] { "doom2.wad" }, configResource.RequiredArchives);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ConfigurationResourcesUseLowercaseFilenameStemKeys()
    {
        var settings = new Settings();
        var location = new DataLocation(DataLocationType.Wad, "/iwad.wad");

        settings.SetResourcesForConfiguration("/Configs/Doom_Doom2Doom.cfg", new[] { location });

        Assert.Equal("doom_doom2doom", Settings.ConfigurationResourceKey("/Configs/Doom_Doom2Doom.cfg"));
        Assert.Single(settings.ResourcesForConfiguration("Doom_Doom2Doom"));
        Assert.Single(settings.ResourcesForConfiguration("doom_doom2doom.cfg"));

        location.Location = "/changed.wad";
        Assert.Equal("/iwad.wad", Assert.Single(settings.ResourcesForConfiguration("Doom_Doom2Doom")).Location);

        settings.SetResourcesForConfiguration("Doom_Doom2Doom", Array.Empty<DataLocation>());
        Assert.Empty(settings.ResourcesForConfiguration("Doom_Doom2Doom"));
    }

    [Fact]
    public void StartupConfigPrefersExistingEnvironmentPath()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/env/Hexen.cfg",
            "/configs/Doom_DoomDoom.cfg",
        };

        string? path = Settings.ResolveStartupConfigPath(
            "/env/Hexen.cfg",
            "/configs",
            "Doom_Doom2Doom.cfg",
            existing.Contains);

        Assert.Equal("/env/Hexen.cfg", path);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    public void DefaultViewModeUsesUdbRange(int? value, int expected)
    {
        var settings = new Settings { DefaultViewMode = value };

        Assert.Equal(expected, settings.NormalizedDefaultViewMode);
    }

    [Theory]
    [InlineData(null, ThingModelRenderMode.All)]
    [InlineData(-1, ThingModelRenderMode.All)]
    [InlineData(0, ThingModelRenderMode.None)]
    [InlineData(1, ThingModelRenderMode.Selection)]
    [InlineData(2, ThingModelRenderMode.ActiveThingsFilter)]
    [InlineData(3, ThingModelRenderMode.All)]
    [InlineData(4, ThingModelRenderMode.All)]
    public void ModelRenderModeUsesUdbRange(int? value, ThingModelRenderMode expected)
    {
        var settings = new Settings { ModelRenderMode = value };

        Assert.Equal(expected, settings.NormalizedModelRenderMode);
    }

    [Theory]
    [InlineData(null, ThingLightRenderMode.All)]
    [InlineData(-1, ThingLightRenderMode.All)]
    [InlineData(0, ThingLightRenderMode.None)]
    [InlineData(1, ThingLightRenderMode.All)]
    [InlineData(2, ThingLightRenderMode.Animated)]
    [InlineData(3, ThingLightRenderMode.All)]
    public void LightRenderModeUsesUdbRange(int? value, ThingLightRenderMode expected)
    {
        var settings = new Settings { LightRenderMode = value };

        Assert.Equal(expected, settings.NormalizedLightRenderMode);
    }

    [Fact]
    public void StartupConfigFallsBackToLastUsedConfigName()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/configs", "Doom_Doom2Doom.cfg"),
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            "/missing/Hexen.cfg",
            "/configs",
            "Doom_Doom2Doom.cfg",
            existing.Contains);

        Assert.Equal(Path.Combine("/configs", "Doom_Doom2Doom.cfg"), path);
    }

    [Fact]
    public void StartupConfigAcceptsRootedLastUsedConfigPath()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/external/Custom.cfg",
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            null,
            "/configs",
            "/external/Custom.cfg",
            existing.Contains);

        Assert.Equal("/external/Custom.cfg", path);
    }

    [Fact]
    public void StartupConfigFallsBackToDoomDefaultWhenLastUsedIsMissing()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            null,
            "/configs",
            "Missing.cfg",
            existing.Contains);

        Assert.Equal(Path.Combine("/configs", "Doom_DoomDoom.cfg"), path);
    }

    [Fact]
    public void RememberMapFolderForPathStoresContainingFolder()
    {
        var s = new Settings();
        string path = Path.Combine("/maps", "doom2.wad");

        s.RememberMapFolderForPath(path);

        Assert.Equal("/maps", s.LastUsedMapFolder);
    }

    [Fact]
    public void RememberMapFolderForPathIgnoresMissingFoldersWhenChecked()
    {
        var s = new Settings { LastUsedMapFolder = "/old" };
        string path = Path.Combine("/missing", "doom2.wad");

        s.RememberMapFolderForPath(path, _ => false);

        Assert.Equal("/old", s.LastUsedMapFolder);
    }

    [Fact]
    public void ExistingMapFolderReturnsOnlyExistingFolders()
    {
        Assert.Equal("/maps", Settings.ExistingMapFolder(" /maps ", path => path == "/maps"));
        Assert.Null(Settings.ExistingMapFolder("/missing", _ => false));
        Assert.Null(Settings.ExistingMapFolder("", _ => true));
    }

    [Fact]
    public void LoadMissingFileReturnsDefaults()
    {
        var s = Settings.Load(Path.Combine(Path.GetTempPath(), "definitely_missing_dbuilder_settings.json"));
        Assert.NotNull(s);
        Assert.Empty(s.RecentFiles);
        Assert.Empty(s.ShortcutOverrides);
        Assert.Null(s.ConfigDir);
        Assert.Equal(MergeGeometryMode.Replace, s.NormalizedMergeGeometryMode);
        Assert.Equal(PasteTagMode.Keep, s.NormalizedPasteOptions.ChangeTags);
        Assert.False(s.NormalizedPasteOptions.RemoveActions);
        Assert.Equal(new DrawLineModeSettings(), s.NormalizedDrawLineSettings);
        Assert.Equal(new DrawRectangleModeSettings(), s.NormalizedDrawRectangleSettings);
        Assert.Equal(new DrawEllipseModeSettings(), s.NormalizedDrawEllipseSettings);
        Assert.Equal(new DrawCurveModeSettings(), s.NormalizedDrawCurveSettings);
        Assert.Equal(new DrawGridModeSettings(), s.NormalizedDrawGridSettings);
        Assert.Equal(new EditSelectionModeSettings(), s.NormalizedEditSelectionSettings);
        Assert.Equal(new AutomapModeSettings(), s.NormalizedAutomapSettings);
        Assert.Equal(new ThreeDFloorControlSectorAreaSettings(), s.NormalizedThreeDFloorControlSectorAreaSettings);
        Assert.Equal(new MakeDoorModeSettings(), s.NormalizedMakeDoorSettings);
        Assert.Equal(new TagRangeStoredOptions(), s.NormalizedTagRangeSettings);
    }

    [Fact]
    public void InvalidMergeGeometryModeFallsBackToReplace()
    {
        var s = new Settings { MergeGeometryMode = (MergeGeometryMode)99 };

        Assert.Equal(MergeGeometryMode.Replace, s.NormalizedMergeGeometryMode);
    }

    [Fact]
    public void InvalidPasteTagModeFallsBackToDefaults()
    {
        var s = new Settings
        {
            PasteOptions = new PasteOptions
            {
                ChangeTags = (PasteTagMode)99,
                RemoveActions = true,
            },
        };

        Assert.Equal(PasteTagMode.Keep, s.NormalizedPasteOptions.ChangeTags);
        Assert.False(s.NormalizedPasteOptions.RemoveActions);
    }

    [Fact]
    public void InvalidAutomapColorPresetFallsBackToDoom()
    {
        var s = new Settings
        {
            AutomapSettings = new AutomapModeSettings(ColorPreset: (AutomapColorPreset)99),
        };

        Assert.Equal(AutomapColorPreset.Doom, s.NormalizedAutomapSettings.ColorPreset);
    }

    [Fact]
    public void InvalidTagRangeStepFallsBackToOne()
    {
        var s = new Settings
        {
            TagRangeSettings = new TagRangeStoredOptions(Step: 0, Relative: true),
        };

        Assert.Equal(1, s.NormalizedTagRangeSettings.Step);
        Assert.True(s.NormalizedTagRangeSettings.Relative);
    }

    [Fact]
    public void LoadCorruptFileReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_corrupt_{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not valid json ");
        try { Assert.Empty(Settings.Load(path).RecentFiles); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NormalizedStatusHistoryLimitClampsToSupportedRange()
    {
        Assert.Equal(Settings.DefaultStatusHistoryLimit, new Settings().NormalizedStatusHistoryLimit);
        Assert.Equal(Settings.MinStatusHistoryLimit, new Settings { StatusHistoryLimit = 1 }.NormalizedStatusHistoryLimit);
        Assert.Equal(Settings.MaxStatusHistoryLimit, new Settings { StatusHistoryLimit = 5000 }.NormalizedStatusHistoryLimit);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("nope", null)]
    [InlineData("1", Settings.MinMaxRecentFiles)]
    [InlineData("12", 12)]
    [InlineData("50", Settings.MaxMaxRecentFiles)]
    public void AcceptMaxRecentFilesTextClampsSettingsDialogInput(string text, int? expected)
        => Assert.Equal(expected, Settings.AcceptMaxRecentFilesText(text));

    [Theory]
    [InlineData("", null)]
    [InlineData("nope", null)]
    [InlineData("1", Settings.MinStatusHistoryLimit)]
    [InlineData("250", 250)]
    [InlineData("5000", Settings.MaxStatusHistoryLimit)]
    public void AcceptStatusHistoryLimitTextClampsSettingsDialogInput(string text, int? expected)
        => Assert.Equal(expected, Settings.AcceptStatusHistoryLimitText(text));

    [Fact]
    public void NumericPreferenceTextShowsNormalizedSettingsDialogValues()
    {
        Assert.Equal("8", Settings.MaxRecentFilesText(new Settings()));
        Assert.Equal("25", Settings.MaxRecentFilesText(new Settings { MaxRecentFiles = 50 }));
        Assert.Equal("100", Settings.StatusHistoryLimitText(new Settings()));
        Assert.Equal("10", Settings.StatusHistoryLimitText(new Settings { StatusHistoryLimit = 1 }));
    }
}
