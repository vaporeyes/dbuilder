// ABOUTME: Tests the editor-facing UDBScript docker window API surface.
// ABOUTME: Keeps script browser events and selection state discoverable without starting a UI platform.

using DBuilder.Editor;
using System.Reflection;

namespace DBuilder.Tests;

public class UdbScriptDockerWindowTests
{
    [Fact]
    public void DockerWindowExposesExpectedEditorSurface()
    {
        Type type = typeof(UdbScriptDockerWindow);

        Assert.Equal("DBuilder.Editor.UdbScriptDockerWindow", type.FullName);
        Assert.NotNull(type.GetConstructor([
            typeof(DBuilder.IO.UdbScriptDirectory),
            typeof(IReadOnlyDictionary<int, DBuilder.IO.UdbScriptInfo?>),
            typeof(IReadOnlyDictionary<int, string>),
        ]));
        Assert.NotNull(type.GetEvent("RunRequested"));
        Assert.NotNull(type.GetEvent("EditRequested"));
        Assert.NotNull(type.GetEvent("OptionsRequested"));
        Assert.NotNull(type.GetEvent("ResetOptionsRequested"));
        Assert.NotNull(type.GetEvent("SlotAssignmentRequested"));
        Assert.NotNull(type.GetEvent("SlotClearedRequested"));
        Assert.NotNull(type.GetProperty("Nodes"));
        Assert.NotNull(type.GetProperty("CurrentSelection"));
        Assert.NotNull(type.GetMethod("ApplyCurrentScript", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetMethod("ApplySlotAssignments", BindingFlags.Instance | BindingFlags.Public));
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(type));
    }
}
