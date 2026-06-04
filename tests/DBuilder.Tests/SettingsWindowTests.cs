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
        Assert.Contains("_map.StitchSelectedGeometry(_settings.NormalizedMergeGeometryMode, 0.5)", mainWindow, StringComparison.Ordinal);
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
}
