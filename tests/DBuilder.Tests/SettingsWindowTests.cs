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
}
