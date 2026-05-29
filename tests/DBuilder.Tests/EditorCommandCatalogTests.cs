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
}
