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
}
