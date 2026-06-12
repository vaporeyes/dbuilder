// ABOUTME: Tests editor settings dialog API surface without starting the Avalonia platform.
// ABOUTME: Keeps persisted settings fields discoverable from the modal Settings window.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public class SettingsWindowTests
{
    [Fact]
    public void SettingsWindowExposesUdbScriptExternalEditorResult()
    {
        Type type = typeof(SettingsWindow);

        Assert.Equal("DBuilder.Editor.SettingsWindow", type.FullName);
        Assert.NotNull(type.GetConstructor([typeof(DBuilder.IO.Settings)]));
        Assert.NotNull(type.GetField("UdbScriptExternalEditor", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void SettingsWindowExposesTestAdditionalParameters()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("TestAdditionalParameters", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddField(\"Test additional parameters\", s.TestAdditionalParameters ?? \"\")", body, StringComparison.Ordinal);
        Assert.Contains("TestAdditionalParameters = NullIfBlank(_testAdditionalParameters.Text);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.TestAdditionalParameters = dlg.TestAdditionalParameters;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowDescribesShortcutOverrideSyntax()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));

        Assert.NotNull(type.GetField("ShortcutOverrides", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddField(\"Shortcut overrides\", EditorCommandCatalog.OverrideText(s.ShortcutOverrides))", body, StringComparison.Ordinal);
        Assert.Contains("ShortcutOverrideWatermark", body, StringComparison.Ordinal);
        Assert.Contains("command.id=Shortcut", body, StringComparison.Ordinal);
        Assert.Contains("use None or Unassigned to clear", body, StringComparison.Ordinal);
        Assert.Contains("_shortcutOverrides.AcceptsReturn = true;", body, StringComparison.Ordinal);
        Assert.Contains("_shortcutOverrides.MinHeight = 72;", body, StringComparison.Ordinal);
        Assert.Contains("_shortcutOverrides.TextWrapping = Avalonia.Media.TextWrapping.Wrap;", body, StringComparison.Ordinal);
        Assert.Contains("ShortcutOverrides = EditorCommandCatalog.ParseOverrideText(_shortcutOverrides.Text);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesTestSkillAndMonsters()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("TestSkill", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("TestMonsters", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddField(\"Test skill\", Settings.TestSkillText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Test with monsters\", s.TestMonsters)", body, StringComparison.Ordinal);
        Assert.Contains("TestSkill = Settings.AcceptTestSkillText(_testSkill.Text);", body, StringComparison.Ordinal);
        Assert.Contains("TestMonsters = _testMonsters.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.TestSkill = dlg.TestSkill;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.TestMonsters = dlg.TestMonsters;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesAutosavePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("Autosave", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("AutosaveCount", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("AutosaveIntervalMinutes", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Enable autosave\", s.Autosave)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Autosave count\", Settings.AutosaveCountText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Autosave interval\", Settings.AutosaveIntervalText(s))", body, StringComparison.Ordinal);
        Assert.Contains("Autosave = _autosave.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("AutosaveCount = Settings.AcceptAutosaveCountText(_autosaveCount.Text);", body, StringComparison.Ordinal);
        Assert.Contains("AutosaveIntervalMinutes = Settings.AcceptAutosaveIntervalText(_autosaveInterval.Text);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.Autosave = dlg.Autosave;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.AutosaveCount = dlg.AutosaveCount;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.AutosaveIntervalMinutes = dlg.AutosaveIntervalMinutes;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ApplyAutosaveSettings();", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesSectorCreationDefaults()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DefaultSectorFloorHeightSetting", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("DefaultSectorCeilingHeightSetting", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("DefaultSectorBrightnessSetting", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddField(\"Default floor height\", Settings.DefaultSectorFloorHeightText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Default ceiling height\", Settings.DefaultSectorCeilingHeightText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Default brightness\", Settings.DefaultSectorBrightnessText(s))", body, StringComparison.Ordinal);
        Assert.Contains("DefaultSectorFloorHeightSetting = Settings.AcceptSectorHeightText(_defaultSectorFloorHeight.Text);", body, StringComparison.Ordinal);
        Assert.Contains("DefaultSectorCeilingHeightSetting = Settings.AcceptSectorHeightText(_defaultSectorCeilingHeight.Text);", body, StringComparison.Ordinal);
        Assert.Contains("DefaultSectorBrightnessSetting = Settings.AcceptSectorBrightnessText(_defaultSectorBrightness.Text);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DefaultSectorFloorHeightSetting = dlg.DefaultSectorFloorHeightSetting;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.DefaultSectorCeilingHeightSetting = dlg.DefaultSectorCeilingHeightSetting;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.DefaultSectorBrightnessSetting = dlg.DefaultSectorBrightnessSetting;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ApplySectorDefaultSettings();", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesAlphaBasedTextureHighlightingPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("AlphaBasedTextureHighlighting", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Alpha-based texture highlighting\", s.AlphaBasedTextureHighlighting)", body, StringComparison.Ordinal);
        Assert.Contains("AlphaBasedTextureHighlighting = _alphaBasedTextureHighlighting.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.AlphaBasedTextureHighlighting = dlg.AlphaBasedTextureHighlighting;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetAlphaBasedTextureHighlighting(_settings.AlphaBasedTextureHighlighting);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesUseHighlightPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("UseHighlight", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Use highlight\", s.UseHighlight)", body, StringComparison.Ordinal);
        Assert.Contains("UseHighlight = _useHighlight.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.UseHighlight = dlg.UseHighlight;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetUseHighlight(_settings.UseHighlight);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDynamicGridSizePreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DynamicGridSize", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Dynamic grid size\", s.DynamicGridSize)", body, StringComparison.Ordinal);
        Assert.Contains("DynamicGridSize = _dynamicGridSize.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DynamicGridSize = dlg.DynamicGridSize;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DynamicGridSizeEnabled = _settings.DynamicGridSize;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawLineModePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawLineSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw lines continuously\", s.NormalizedDrawLineSettings.ContinuousDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Auto-close drawn lines\", s.NormalizedDrawLineSettings.AutoCloseDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("ShowGuidelines: _drawLineShowGuidelines", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawLineSettings = dlg.DrawLineSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DrawLineSettings = _settings.NormalizedDrawLineSettings;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawRectangleModePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawRectangleSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw rectangles continuously\", s.NormalizedDrawRectangleSettings.ContinuousDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Draw rectangles radially\", s.NormalizedDrawRectangleSettings.RadialDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Place things at rectangle vertices\", s.NormalizedDrawRectangleSettings.PlaceThingsAtVertices)", body, StringComparison.Ordinal);
        Assert.Contains("Subdivisions: _drawRectangleSubdivisions", body, StringComparison.Ordinal);
        Assert.Contains("BevelWidth: _drawRectangleBevelWidth", body, StringComparison.Ordinal);
        Assert.Contains("ShowGuidelines: _drawRectangleShowGuidelines", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawRectangleSettings = dlg.DrawRectangleSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DrawRectangleSettings = _settings.NormalizedDrawRectangleSettings;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawEllipseModePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawEllipseSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw ellipses continuously\", s.NormalizedDrawEllipseSettings.ContinuousDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Draw ellipses radially\", s.NormalizedDrawEllipseSettings.RadialDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Place things at ellipse vertices\", s.NormalizedDrawEllipseSettings.PlaceThingsAtVertices)", body, StringComparison.Ordinal);
        Assert.Contains("Subdivisions: _drawEllipseSubdivisions", body, StringComparison.Ordinal);
        Assert.Contains("BevelWidth: _drawEllipseBevelWidth", body, StringComparison.Ordinal);
        Assert.Contains("Angle: _drawEllipseAngle", body, StringComparison.Ordinal);
        Assert.Contains("ShowGuidelines: _drawEllipseShowGuidelines", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawEllipseSettings = dlg.DrawEllipseSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DrawEllipseSettings = _settings.NormalizedDrawEllipseSettings;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawCurveModePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawCurveSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw curves continuously\", s.NormalizedDrawCurveSettings.ContinuousDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Auto-close drawn curves\", s.NormalizedDrawCurveSettings.AutoCloseDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Place things at curve vertices\", s.NormalizedDrawCurveSettings.PlaceThingsAtVertices)", body, StringComparison.Ordinal);
        Assert.Contains("SegmentLength: _drawCurveSegmentLength", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawCurveSettings = dlg.DrawCurveSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DrawCurveSettings = _settings.NormalizedDrawCurveSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.CurveLinedefsSettings = _settings.NormalizedCurveLinedefsSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.MergeGeometryMode = _settings.NormalizedMergeGeometryMode;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawGridModePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawGridSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("_drawGridSettings = s.NormalizedDrawGridSettings;", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Draw grids continuously\", s.NormalizedDrawGridSettings.ContinuousDrawing)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Triangulate drawn grids\", s.NormalizedDrawGridSettings.Triangulate)", body, StringComparison.Ordinal);
        Assert.Contains("DrawGridSettings = _drawGridSettings with", body, StringComparison.Ordinal);
        Assert.Contains("ContinuousDrawing = _drawGridContinuousDrawing.IsChecked == true", body, StringComparison.Ordinal);
        Assert.Contains("Triangulate = _drawGridTriangulate.IsChecked == true", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawGridSettings = dlg.DrawGridSettings;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.DrawGridSettings = _settings.NormalizedDrawGridSettings;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesShowVisualVerticesPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("ShowVisualVertices", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Show visual vertices\", s.ShowVisualVertices)", body, StringComparison.Ordinal);
        Assert.Contains("ShowVisualVertices = _showVisualVertices.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ShowVisualVertices = dlg.ShowVisualVertices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetShowVisualVertices(_settings.ShowVisualVertices);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesShowEventLinesPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("ShowEventLines", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Show event lines\", s.ShowEventLines)", body, StringComparison.Ordinal);
        Assert.Contains("ShowEventLines = _showEventLines.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ShowEventLines = dlg.ShowEventLines;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetShowEventLines(_settings.ShowEventLines);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawSkyPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawSky", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw sky\", s.DrawSky)", body, StringComparison.Ordinal);
        Assert.Contains("DrawSky = _drawSky.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawSky = dlg.DrawSky;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetDrawSky(_settings.DrawSky);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesDrawFogPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("DrawFog", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Draw fog\", s.DrawFog)", body, StringComparison.Ordinal);
        Assert.Contains("DrawFog = _drawFog.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.DrawFog = dlg.DrawFog;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetDrawFog(_settings.DrawFog);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesToastPreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("ToastsEnabled", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ToastAnchor", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ToastDurationMilliseconds", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ToastActionSettings", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Show toasts\", s.ToastsEnabled)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Toast duration\", ToastPreferences.DurationSecondsText(s.NormalizedToastDurationMilliseconds))", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Toast position\", ToastAnchorItems(), (int)s.NormalizedToastAnchor)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Disabled toasts\", ToastPreferences.DisabledActionsText(s.ToastActionSettings))", body, StringComparison.Ordinal);
        Assert.Contains("ToastsEnabled = _toastsEnabled.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ToastAnchor = (ToastAnchor)ComboNumber(_toastAnchor, (int)ToastPreferences.DefaultAnchor);", body, StringComparison.Ordinal);
        Assert.Contains("ToastDurationMilliseconds = ToastPreferences.AcceptDurationSecondsText(_toastDuration.Text);", body, StringComparison.Ordinal);
        Assert.Contains("ToastActionSettings = ToastPreferences.ParseDisabledActionsText(_toastDisabledActions.Text);", body, StringComparison.Ordinal);
        Assert.Contains("_toastDisabledActions.Watermark = \"disabled toast ids: \" + ToastPreferences.KnownActionNamesText();", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ToastsEnabled = dlg.ToastsEnabled;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.ToastAnchor = dlg.ToastAnchor;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.ToastDurationMilliseconds = dlg.ToastDurationMilliseconds;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.ToastActionSettings = dlg.ToastActionSettings;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesClassicRenderingPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("ClassicRendering", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Classic rendering\", s.ClassicRendering)", body, StringComparison.Ordinal);
        Assert.Contains("ClassicRendering = _classicRendering.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ClassicRendering = dlg.ClassicRendering;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetClassicRendering(_settings.ClassicRendering);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesRenderQualityPreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("QualityDisplay", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ClassicBilinear", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("VisualBilinear", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("BlackBrowsers", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("FlatShadeVertices", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("MarkExtraFloors", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ImageBrightness", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("DoubleSidedAlpha", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("VisualFov", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("ViewDistance", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("MoveSpeed", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("MouseSpeed", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddField(\"Image brightness\", Settings.ImageBrightnessText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Double-sided alpha\", Settings.DoubleSidedAlphaText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Visual FOV\", Settings.VisualFovText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"View distance\", Settings.ViewDistanceText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Move speed\", Settings.MoveSpeedText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Mouse speed\", Settings.MouseSpeedText(s))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"High quality rendering\", s.QualityDisplay)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Bilinear filtering in classic modes\", s.ClassicBilinear)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Bilinear filtering in visual modes\", s.VisualBilinear)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Black background in image browser\", s.BlackBrowsers)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Flat shade vertices\", s.FlatShadeVertices)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Mark 3D floors in classic modes\", s.MarkExtraFloors)", body, StringComparison.Ordinal);
        Assert.Contains("ImageBrightness = Settings.AcceptImageBrightnessText(_imageBrightness.Text);", body, StringComparison.Ordinal);
        Assert.Contains("DoubleSidedAlpha = Settings.AcceptDoubleSidedAlphaText(_doubleSidedAlpha.Text);", body, StringComparison.Ordinal);
        Assert.Contains("VisualFov = Settings.AcceptVisualFovText(_visualFov.Text);", body, StringComparison.Ordinal);
        Assert.Contains("ViewDistance = Settings.AcceptViewDistanceText(_viewDistance.Text);", body, StringComparison.Ordinal);
        Assert.Contains("MoveSpeed = Settings.AcceptMoveSpeedText(_moveSpeed.Text);", body, StringComparison.Ordinal);
        Assert.Contains("MouseSpeed = Settings.AcceptMouseSpeedText(_mouseSpeed.Text);", body, StringComparison.Ordinal);
        Assert.Contains("QualityDisplay = _qualityDisplay.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ClassicBilinear = _classicBilinear.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("VisualBilinear = _visualBilinear.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("BlackBrowsers = _blackBrowsers.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("FlatShadeVertices = _flatShadeVertices.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("MarkExtraFloors = _markExtraFloors.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ImageBrightness = dlg.ImageBrightness;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.DoubleSidedAlpha = dlg.DoubleSidedAlpha;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.VisualFov = dlg.VisualFov;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.ViewDistance = dlg.ViewDistance;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.MoveSpeed = dlg.MoveSpeed;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.MouseSpeed = dlg.MouseSpeed;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.QualityDisplay = dlg.QualityDisplay;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.ClassicBilinear = dlg.ClassicBilinear;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.VisualBilinear = dlg.VisualBilinear;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.BlackBrowsers = dlg.BlackBrowsers;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.FlatShadeVertices = dlg.FlatShadeVertices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.MarkExtraFloors = dlg.MarkExtraFloors;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesEnhancedRenderingEffectsPreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("EnhancedRenderingEffects", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Enhanced rendering effects\", s.EnhancedRenderingEffects)", body, StringComparison.Ordinal);
        Assert.Contains("EnhancedRenderingEffects = _enhancedRenderingEffects.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.EnhancedRenderingEffects = dlg.EnhancedRenderingEffects;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetEnhancedRenderingEffects(_settings.EnhancedRenderingEffects);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesModelRenderModePreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("ModelRenderMode", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCombo(\"Model render mode\", ModelRenderModeItems(), (int)s.NormalizedModelRenderMode)", body, StringComparison.Ordinal);
        Assert.Contains("ModelRenderMode = ComboNumber(_modelRenderMode, (int)ThingModelRenderMode.All);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.ModelRenderMode = dlg.ModelRenderMode;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetModelRenderMode(_settings.NormalizedModelRenderMode);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesLightRenderModePreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("LightRenderMode", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCombo(\"Light render mode\", LightRenderModeItems(), (int)s.NormalizedLightRenderMode)", body, StringComparison.Ordinal);
        Assert.Contains("LightRenderMode = ComboNumber(_lightRenderMode, (int)ThingLightRenderMode.All);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.LightRenderMode = dlg.LightRenderMode;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetLightRenderMode(_settings.NormalizedLightRenderMode);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesMergeGeometryModePreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("MergeGeometryMode", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCombo(\"Merge geometry mode\", MergeGeometryModeItems(), (int)s.NormalizedMergeGeometryMode)", body, StringComparison.Ordinal);
        Assert.Contains("MergeGeometryMode = (MergeGeometryMode)ComboNumber(_mergeGeometryMode, (int)MergeGeometryMode.Replace);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.MergeGeometryMode = dlg.MergeGeometryMode;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.MergeGeometryMode = _settings.NormalizedMergeGeometryMode;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_map.StitchSelectedGeometry(_settings.NormalizedMergeGeometryMode, 0.5)", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesGeometryTogglePreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("AutoMerge", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("SplitJoinedSectors", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Snap to geometry\", s.AutoMerge)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Split joined sectors\", s.SplitJoinedSectors)", body, StringComparison.Ordinal);
        Assert.Contains("AutoMerge = _autoMerge.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("SplitJoinedSectors = _splitJoinedSectors.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.AutoMerge = dlg.AutoMerge;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.SplitJoinedSectors = dlg.SplitJoinedSectors;", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesAdjacentVisualVertexSlopeHandlePreference()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("SelectAdjacentVisualVertexSlopeHandles", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Select adjacent visual vertex slope handles\", s.SelectAdjacentVisualVertexSlopeHandles)", body, StringComparison.Ordinal);
        Assert.Contains("SelectAdjacentVisualVertexSlopeHandles = _selectAdjacentVisualVertexSlopeHandles.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.SelectAdjacentVisualVertexSlopeHandles = dlg.SelectAdjacentVisualVertexSlopeHandles;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetSelectAdjacentVisualVertexSlopeHandles(_settings.SelectAdjacentVisualVertexSlopeHandles);", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowExposesClassicThingRenderPreferences()
    {
        Type type = typeof(SettingsWindow);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/SettingsWindow.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.NotNull(type.GetField("FixedThingsScale", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetField("AlwaysShowVertices", BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("AddCheckBox(\"Fixed things scale\", s.FixedThingsScale)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Always show vertices\", s.AlwaysShowVertices)", body, StringComparison.Ordinal);
        Assert.Contains("FixedThingsScale = _fixedThingsScale.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("AlwaysShowVertices = _alwaysShowVertices.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.FixedThingsScale = dlg.FixedThingsScale;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_settings.AlwaysShowVertices = dlg.AlwaysShowVertices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetFixedThingsScale(_settings.FixedThingsScale);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("MapView.SetAlwaysShowVertices(_settings.AlwaysShowVertices);", mainWindow, StringComparison.Ordinal);
    }
}
