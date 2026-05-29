// ABOUTME: Verifies editor command metadata used by shortcut help and future key binding persistence.
// ABOUTME: Guards stable command ids and default gestures as the action system is ported in slices.

using DBuilder.IO;

namespace DBuilder.Tests;

public class EditorCommandCatalogTests
{
    [Fact]
    public void CommandIdsAreUniqueAndStable()
    {
        var ids = EditorCommandCatalog.All.Select(command => command.Id).ToArray();

        Assert.DoesNotContain(ids, string.IsNullOrWhiteSpace);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("window.save", ids);
        Assert.Contains("map2d.toggle-3d", ids);
        Assert.Contains("map3d.toggle-2d", ids);
    }

    [Fact]
    public void CommandMetadataHasDisplayTextAndGestures()
    {
        Assert.All(EditorCommandCatalog.All, command =>
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Title));
            Assert.False(string.IsNullOrWhiteSpace(command.DefaultGesture));
        });
    }

    [Fact]
    public void DefaultShortcutsReferenceKnownCommands()
    {
        var commandIds = EditorCommandCatalog.All.Select(command => command.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(EditorCommandCatalog.DefaultShortcuts, shortcut => Assert.Contains(shortcut.CommandId, commandIds));
    }

    [Fact]
    public void ScopeLookupPreservesCatalogOrder()
    {
        var map2D = EditorCommandCatalog.ByScope(EditorCommandScope.Map2D);

        Assert.True(map2D.Count > 10);
        Assert.Equal("map2d.select", map2D[0].Id);
        Assert.Equal(EditorCommandScope.Map2D, map2D[^1].Scope);
    }

    [Fact]
    public void DefaultShortcutsResolveWindowAccelerators()
    {
        Assert.Equal("window.save", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "S", accelerator: true));
        Assert.Equal("window.duplicate", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "d", accelerator: true));
        Assert.Equal("window.delete", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "Delete"));
        Assert.Equal("window.delete", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "Back"));
        Assert.Equal("window.cancel-draw", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "Escape"));
    }

    [Fact]
    public void DefaultShortcutsRespectScopeAndModifiers()
    {
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "S", accelerator: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "S"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "S", accelerator: true, shift: true));
    }

    [Fact]
    public void DefaultShortcutsResolveMap2DCommands()
    {
        Assert.Equal("map2d.toggle-sector-fills", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "S"));
        Assert.Equal("map2d.draw-sector", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D"));
        Assert.Equal("map2d.draw-lines", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", shift: true));
        Assert.Equal("map2d.mode-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "NumPad1"));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add"));
        Assert.Equal("map2d.toggle-3d", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Tab"));
    }

    [Fact]
    public void DefaultShortcutsResolveMap3DToggle()
    {
        Assert.Equal("map3d.toggle-2d", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Tab"));
    }

    [Fact]
    public void DefaultShortcutsResolveDiscreteMap3DCommands()
    {
        Assert.Equal("map3d.walk-mode", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "G"));
        Assert.Equal("map3d.copy-texture", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C"));
        Assert.Equal("map3d.align-texture-y", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", shift: true));
        Assert.Equal("map3d.delete-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Back"));
        Assert.Equal("map3d.nudge-offset-left", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left", shift: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C", accelerator: true));
    }

    [Fact]
    public void EffectiveShortcutsReplaceDefaultBindingsForCommand()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
        });

        Assert.Null(EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "S", accelerator: true));
        Assert.Equal("window.save", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "F5"));
    }

    [Fact]
    public void EffectiveShortcutsIgnoreUnknownOrBlankOverrides()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("missing.command", EditorCommandScope.Window, "F5"),
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, ""),
        });

        Assert.Same(EditorCommandCatalog.DefaultShortcuts, bindings);
        Assert.Equal("window.save", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "S", accelerator: true));
    }

    [Fact]
    public void EffectiveShortcutsLetOverridesWinConflicts()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "Z", Accelerator: true),
        });

        Assert.Equal("window.save", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Z", accelerator: true));
    }

    [Fact]
    public void GestureTextUsesEffectiveBindings()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
        });

        Assert.Equal("F5", EditorCommandCatalog.GestureText("window.save", bindings));
        Assert.Equal("Ctrl/Cmd+Z", EditorCommandCatalog.GestureText("window.undo", bindings));
    }

    [Fact]
    public void GestureTextFormatsModifiersAndDisplayKeys()
    {
        var binding = new EditorShortcutBinding("map2d.grid-down", EditorCommandScope.Map2D, "OemOpenBrackets", Shift: true);

        Assert.Equal("Shift+[", EditorCommandCatalog.GestureText(binding));
    }

    [Fact]
    public void GestureTextShowsMissingCommandsAsUnset()
    {
        Assert.Equal("-", EditorCommandCatalog.GestureText("missing.command", EditorCommandCatalog.DefaultShortcuts));
    }
}
