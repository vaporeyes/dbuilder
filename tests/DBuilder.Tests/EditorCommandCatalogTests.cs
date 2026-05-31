// ABOUTME: Verifies editor command metadata used by shortcut help and future key binding persistence.
// ABOUTME: Guards stable command ids and default gestures as the action system is ported in slices.

using DBuilder.IO;
using DBuilder.Map;

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
        Assert.Contains("window.select-similar", ids);
        Assert.Contains("window.toggle-auto-clear-sidedef-textures", ids);
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
            Assert.True(command.AllowKeys || command.AllowMouse || command.AllowScroll);
        });
    }

    [Fact]
    public void CommandMetadataExposesUdbStyleShortcutOptions()
    {
        var zoomIn = EditorCommandCatalog.Find("map2d.zoom-in");
        var select = EditorCommandCatalog.Find("map2d.select");
        var save = EditorCommandCatalog.Find("window.save");
        var targetHeight = EditorCommandCatalog.Find("map3d.target-height");

        Assert.NotNull(zoomIn);
        Assert.True(zoomIn.AllowKeys);
        Assert.True(zoomIn.AllowMouse);
        Assert.True(zoomIn.AllowScroll);
        Assert.True(zoomIn.Repeat);
        Assert.False(zoomIn.DisregardShift);
        Assert.False(zoomIn.DisregardAccelerator);
        Assert.False(zoomIn.DisregardAlt);

        Assert.NotNull(select);
        Assert.True(select.AllowMouse);
        Assert.False(select.AllowScroll);
        Assert.True(select.DisregardShift);
        Assert.True(select.DisregardAccelerator);
        Assert.True(select.DisregardAlt);

        Assert.NotNull(save);
        Assert.True(save.AllowKeys);
        Assert.True(save.AllowMouse);
        Assert.False(save.AllowScroll);
        Assert.False(save.Repeat);

        Assert.NotNull(targetHeight);
        Assert.True(targetHeight.AllowScroll);
        Assert.False(targetHeight.Repeat);
    }

    [Fact]
    public void AutoClearSidedefTexturesCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.toggle-auto-clear-sidedef-textures");

        Assert.NotNull(command);
        Assert.Equal("Auto Clear Sidedef Textures", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void SelectSimilarCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.select-similar");

        Assert.NotNull(command);
        Assert.Equal("Select Similar Map Elements", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void WadAuthorModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.mode-wadauthor");

        Assert.NotNull(command);
        Assert.Equal("WadAuthor Mode", command.Title);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("wadauthormode", WadAuthorModeModel.ModeDescriptor.SwitchAction);
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
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add", shift: true));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.LeftButton, accelerator: true, shift: true, alt: true));
        Assert.Equal("map3d.select-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.LeftButton, accelerator: true, shift: true, alt: true));
    }

    [Fact]
    public void ShortcutResolutionRespectsCommandInputKindOptions()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, EditorPointerInput.ScrollUp),
            new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, EditorPointerInput.ScrollDown),
            new EditorShortcutBinding("map2d.select", EditorCommandScope.Map2D, "F5"),
        });

        Assert.Null(EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, EditorPointerInput.ScrollUp));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, EditorPointerInput.ScrollDown));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, "F5"));
    }

    [Fact]
    public void DefaultShortcutsResolveMap2DCommands()
    {
        Assert.Equal("map2d.toggle-sector-fills", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "S"));
        Assert.Equal("map2d.draw-sector", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D"));
        Assert.Equal("map2d.draw-lines", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", shift: true));
        Assert.Equal("map2d.mode-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "NumPad1"));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.LeftButton));
        Assert.Equal("map2d.split-line", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.RightButton));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add"));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollUp));
        Assert.Equal("map2d.zoom-out", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollDown));
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
        Assert.Equal("map3d.select-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.LeftButton));
        Assert.Equal("map3d.nudge-offset-left", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left", shift: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C", accelerator: true));
    }

    [Fact]
    public void RepeatableCommandMetadataMatchesAdjustmentActions()
    {
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.zoom-in"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.grid-up"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.brightness-down"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.nudge-offset-left"));

        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.toggle-3d"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-sector"));
        Assert.False(EditorCommandCatalog.IsRepeatable("window.save"));
    }

    [Fact]
    public void ShortcutPressKeysUseNormalizedKeyNames()
    {
        Assert.Equal(
            EditorCommandCatalog.ShortcutPressKey(EditorCommandScope.Window, "Escape"),
            EditorCommandCatalog.ShortcutPressKey(EditorCommandScope.Window, "Esc"));
        Assert.Equal(
            EditorCommandCatalog.ShortcutReleasePrefix(EditorCommandScope.Map2D, "OemPlus"),
            EditorCommandCatalog.ShortcutReleasePrefix(EditorCommandScope.Map2D, "+"));
    }

    [Fact]
    public void WheelInputNormalizesToUdbScrollKeys()
    {
        Assert.Equal(EditorPointerInput.ScrollUp, EditorPointerInput.WheelKey(0, 1));
        Assert.Equal(EditorPointerInput.ScrollDown, EditorPointerInput.WheelKey(0, -1));
        Assert.Equal(EditorPointerInput.ScrollRight, EditorPointerInput.WheelKey(2, 1));
        Assert.Equal(EditorPointerInput.ScrollLeft, EditorPointerInput.WheelKey(-2, 1));
        Assert.Null(EditorPointerInput.WheelKey(0, 0));
    }

    [Fact]
    public void MouseButtonsNormalizeToUdbButtonKeys()
    {
        Assert.Equal(EditorPointerInput.LeftButton, EditorPointerInput.ButtonKey(EditorPointerButton.Left));
        Assert.Equal(EditorPointerInput.MiddleButton, EditorPointerInput.ButtonKey(EditorPointerButton.Middle));
        Assert.Equal(EditorPointerInput.RightButton, EditorPointerInput.ButtonKey(EditorPointerButton.Right));
        Assert.Equal(EditorPointerInput.ExtendedButton1, EditorPointerInput.ButtonKey(EditorPointerButton.XButton1));
        Assert.Equal(EditorPointerInput.ExtendedButton2, EditorPointerInput.ButtonKey(EditorPointerButton.XButton2));
        Assert.Null(EditorPointerInput.ButtonKey(EditorPointerButton.None));
        Assert.True(EditorPointerInput.IsButtonKey(EditorPointerInput.LeftButton));
        Assert.False(EditorPointerInput.IsButtonKey(EditorPointerInput.ScrollUp));
        Assert.True(EditorPointerInput.IsScrollKey(EditorPointerInput.ScrollUp));
        Assert.False(EditorPointerInput.IsScrollKey(EditorPointerInput.LeftButton));
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
    public void GestureTextFormatsUdbStylePunctuationAndNumpadKeys()
    {
        Assert.Equal("~", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemTilde")));
        Assert.Equal(";", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemSemicolon")));
        Assert.Equal("'", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemQuotes")));
        Assert.Equal(",", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemComma")));
        Assert.Equal(".", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemPeriod")));
        Assert.Equal("?", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemQuestion")));
        Assert.Equal("\\", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "OemBackslash")));
        Assert.Equal("NumPad+", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "Add")));
        Assert.Equal("NumPad-", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.zoom-out", EditorCommandScope.Map2D, "Subtract")));
        Assert.Equal("NumPad.", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "Decimal")));
        Assert.Equal("NumPad*", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "Multiply")));
        Assert.Equal("NumPad/", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "Divide")));
    }

    [Fact]
    public void GestureTextFormatsUdbStyleMouseButtonKeys()
    {
        Assert.Equal("LButton", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.select", EditorCommandScope.Map2D, EditorPointerInput.LeftButton)));
        Assert.Equal("MButton", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.pan", EditorCommandScope.Map2D, EditorPointerInput.MiddleButton)));
        Assert.Equal("RButton", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.split-line", EditorCommandScope.Map2D, EditorPointerInput.RightButton)));
        Assert.Equal("XButton1", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, EditorPointerInput.ExtendedButton1)));
        Assert.Equal("XButton2", EditorCommandCatalog.GestureText(new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, EditorPointerInput.ExtendedButton2)));
    }

    [Fact]
    public void GestureTextFormatsSpecialKeys()
    {
        Assert.Equal("Esc", EditorCommandCatalog.GestureText(new EditorShortcutBinding("window.cancel-draw", EditorCommandScope.Window, "Escape")));
        Assert.Equal("Backspace", EditorCommandCatalog.GestureText(new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Back")));
    }

    [Fact]
    public void GestureTextShowsMissingCommandsAsUnset()
    {
        Assert.Equal("-", EditorCommandCatalog.GestureText("missing.command", EditorCommandCatalog.DefaultShortcuts));
    }

    [Fact]
    public void CommandHintCombinesEffectiveGestureAndTitle()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map2d.draw-sector", EditorCommandScope.Map2D, "F6"),
        });

        Assert.Equal("F6 Draw sector", EditorCommandCatalog.CommandHint("map2d.draw-sector", bindings));
        Assert.Equal("1 / NumPad1 Vertices mode", EditorCommandCatalog.CommandHint("map2d.mode-vertices", bindings));
    }

    [Fact]
    public void CommandHintFallsBackForUnknownCommands()
    {
        Assert.Equal("missing.command", EditorCommandCatalog.CommandHint("missing.command", EditorCommandCatalog.DefaultShortcuts));
    }

    [Fact]
    public void CommandHintsJoinsMultipleCommands()
    {
        string hints = EditorCommandCatalog.CommandHints(
            EditorCommandCatalog.DefaultShortcuts,
            "map2d.draw-sector",
            "map2d.insert");

        Assert.Equal("D Draw sector; I / Insert Insert vertex or thing", hints);
    }

    [Fact]
    public void ParseOverrideTextReadsCommandGestures()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("window.save=F5; map2d.fit=Shift+R; map3d.brightness-down=[; window.cancel-draw=Esc");

        Assert.Contains(overrides, b => b.CommandId == "window.save" && b.Key == "F5");
        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "R" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemOpenBrackets");
        Assert.Contains(overrides, b => b.CommandId == "window.cancel-draw" && b.Key == "Escape");
    }

    [Fact]
    public void ParseOverrideTextReadsUdbStylePunctuationAndNumpadKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.fit=~; map2d.zoom-in=NumPad+; map2d.zoom-out=NumPad-; map3d.brightness-up=]; map3d.brightness-down=+");

        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "OemTilde");
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-in" && b.Key == "Add");
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-out" && b.Key == "Subtract");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-up" && b.Key == "OemCloseBrackets");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemPlus");
    }

    [Fact]
    public void ParseOverrideTextReadsUdbStyleMouseButtonKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.select=LButton; map2d.pan=Alt+MButton; map2d.split-line=Ctrl+Shift+RButton; map3d.select-target=XButton1");

        Assert.Contains(overrides, b => b.CommandId == "map2d.select" && b.Key == EditorPointerInput.LeftButton);
        Assert.Contains(overrides, b => b.CommandId == "map2d.pan" && b.Key == EditorPointerInput.MiddleButton && b.Alt);
        Assert.Contains(overrides, b => b.CommandId == "map2d.split-line" && b.Key == EditorPointerInput.RightButton && b.Accelerator && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.select-target" && b.Key == EditorPointerInput.ExtendedButton1);
    }

    [Fact]
    public void SpecialKeyAliasesResolveToAvaloniaKeyNames()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.cancel-draw", EditorCommandScope.Window, "Esc"),
            new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Backspace"),
        });

        Assert.Equal("window.cancel-draw", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Escape"));
        Assert.Equal("window.delete", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Back"));
    }

    [Fact]
    public void ParseOverrideTextSkipsInvalidEntries()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("missing.command=F5; window.save=; malformed");

        Assert.Empty(overrides);
    }

    [Fact]
    public void OverrideTextRoundTripsParseableGestures()
    {
        var overrides = new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
            new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "R", Shift: true),
        };

        var parsed = EditorCommandCatalog.ParseOverrideText(EditorCommandCatalog.OverrideText(overrides));

        Assert.Equal(overrides, parsed);
    }
}
