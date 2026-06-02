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
        Assert.Contains("window.properties", ids);
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

    [Theory]
    [InlineData("window.new-map", "New Map", "Menu")]
    [InlineData("window.open-map", "Open Map", "Menu")]
    [InlineData("window.open-map-in-current-wad", "Open Map in current WAD", "Ctrl/Cmd+Shift+O")]
    [InlineData("window.close-map", "Close Map", "Menu")]
    [InlineData("window.save-map", "Save Map", "Ctrl/Cmd+S")]
    [InlineData("window.save-map-as", "Save Map As", "Menu")]
    [InlineData("window.map-options", "Map Options", "Menu")]
    [InlineData("window.snap-selection-to-grid", "Snap Selected Map Elements to Grid", "Menu")]
    [InlineData("window.game-configurations", "Game Configurations", "Menu")]
    [InlineData("window.preferences", "Preferences", "Menu")]
    [InlineData("window.view-used-tags", "View Used Tags", "Menu")]
    [InlineData("window.view-thing-types", "View Thing Types", "Menu")]
    [InlineData("window.go-to-coordinates", "Go To Coordinates", "Ctrl/Cmd+Shift+G")]
    public void KeyOnlyWindowCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void ShowErrorsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.show-errors");

        Assert.NotNull(command);
        Assert.Equal("Show Errors and Warnings", command.Title);
        Assert.Equal("F11", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void OpenMapInCurrentWadShortcutMatchesUdbDefault()
    {
        Assert.Equal("window.open-map-in-current-wad", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Window,
            "O",
            accelerator: true,
            shift: true));
    }

    [Fact]
    public void CoreViewToolShortcutsMatchUdbDefaults()
    {
        Assert.Equal("window.go-to-coordinates", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Window,
            "G",
            accelerator: true,
            shift: true));
        Assert.Equal("window.show-errors", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "F11"));
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
    public void FilterSelectedThingsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.filter-selected-things");

        Assert.NotNull(command);
        Assert.Equal("Filter Selected Things", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void ChangeMapElementIndexCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.change-map-element-index");

        Assert.NotNull(command);
        Assert.Equal("Change Map Element Index", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.copy-properties", "Copy Properties")]
    [InlineData("window.paste-properties", "Paste Properties")]
    [InlineData("window.paste-properties-options", "Paste Properties With Options")]
    public void PastePropertiesCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void PasteSpecialCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.paste-special");

        Assert.NotNull(command);
        Assert.Equal("Paste Selection Special", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.create-prefab", "Create Prefab")]
    [InlineData("window.insert-prefab-file", "Insert Prefab File")]
    [InlineData("window.insert-previous-prefab", "Insert Previous Prefab")]
    public void PrefabCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.test-map", "Test Map")]
    [InlineData("window.things-filters-setup", "Configure Things Filters")]
    [InlineData("window.reload-resources", "Reload Resources")]
    public void KeyOnlyToolCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void GridSetupCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.grid-setup");

        Assert.NotNull(command);
        Assert.Equal("Grid and Backdrop Setup", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void DynamicGridCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.toggle-dynamic-grid-size");

        Assert.NotNull(command);
        Assert.Equal("Toggle Dynamic Grid Size", command.Title);
        Assert.Equal("Ctrl/Cmd+Alt+G", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal("map2d.toggle-dynamic-grid-size", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map2D,
            "G",
            accelerator: true,
            alt: true));
    }

    [Theory]
    [InlineData("map2d.align-grid-to-linedef", "Align Grid to Selected Linedef")]
    [InlineData("map2d.set-grid-origin-to-vertex", "Set Grid Origin to Selected Vertex")]
    [InlineData("map2d.reset-grid-transform", "Reset Grid Transform")]
    [InlineData("map2d.smart-grid-transform", "Smart Grid Transform")]
    public void GridTransformCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void PlaceThingsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.place-things");

        Assert.NotNull(command);
        Assert.Equal("Place Things", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.join-sectors", "Join Sectors")]
    [InlineData("map2d.merge-sectors", "Merge Sectors")]
    public void JoinMergeSectorCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.select-single-sided", "Select Single-sided")]
    [InlineData("map2d.select-double-sided", "Select Double-sided")]
    public void SelectSidednessCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void AlignLinedefsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.align-linedefs");

        Assert.NotNull(command);
        Assert.Equal("Align Linedefs", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void ApplyLightFogFlagCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.apply-lightfog-flag");

        Assert.NotNull(command);
        Assert.Equal("Apply 'lightfog' flag", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("select", "Select Group", "Menu")]
    [InlineData("assign", "Assign Group", "Menu")]
    [InlineData("clear", "Clear Group", "Ctrl/Cmd+Shift")]
    public void SelectionGroupCommandsMatchUdbActionSurface(string verb, string titlePrefix, string gesturePrefix)
    {
        for (int group = 1; group <= 10; group++)
        {
            var command = EditorCommandCatalog.Find($"window.{verb}-group-{group}");

            Assert.NotNull(command);
            Assert.Equal($"{titlePrefix} {group}", command.Title);
            Assert.Equal(EditorCommandScope.Window, command.Scope);
            Assert.True(command.AllowKeys);
            Assert.True(command.AllowMouse);
            Assert.False(command.AllowScroll);
            Assert.StartsWith(gesturePrefix, command.DefaultGesture, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("D1", "window.clear-group-1")]
    [InlineData("D2", "window.clear-group-2")]
    [InlineData("D3", "window.clear-group-3")]
    [InlineData("D4", "window.clear-group-4")]
    [InlineData("D5", "window.clear-group-5")]
    [InlineData("D6", "window.clear-group-6")]
    [InlineData("D7", "window.clear-group-7")]
    [InlineData("D8", "window.clear-group-8")]
    [InlineData("D9", "window.clear-group-9")]
    [InlineData("D0", "window.clear-group-10")]
    public void ClearSelectionGroupShortcutsMatchUdbDefaults(string key, string commandId)
    {
        Assert.Equal(commandId, EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Window,
            key,
            accelerator: true,
            shift: true));
    }

    [Fact]
    public void PropertiesCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.properties");

        Assert.NotNull(command);
        Assert.Equal("Properties", command.Title);
        Assert.Equal("Enter", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Contains(EditorCommandCatalog.DefaultShortcuts,
            shortcut => shortcut.CommandId == "window.properties"
                && shortcut.Scope == EditorCommandScope.Window
                && shortcut.Key == "Enter");
    }

    [Fact]
    public void ToggleCommentsCommandMatchesUdbActionSurface()
    {
        AssertKeyOnlyMap2DCommand("map2d.toggle-comments", "Toggle Comments");
    }

    [Theory]
    [InlineData("map2d.toggle-fixed-things-scale", "Toggle Fixed Things Scale")]
    [InlineData("map2d.toggle-always-show-vertices", "Toggle Always Show Vertices")]
    public void ViewToggleCommandsMatchUdbActionSurface(string commandId, string title)
        => AssertKeyOnlyMap2DCommand(commandId, title);

    [Theory]
    [InlineData("map2d.view-mode-wireframe", "View Wireframe")]
    [InlineData("map2d.view-mode-brightness", "View Brightness Levels")]
    [InlineData("map2d.view-mode-floors", "View Floor Textures")]
    [InlineData("map2d.view-mode-ceilings", "View Ceiling Textures")]
    [InlineData("map2d.next-view-mode", "Next View Mode")]
    [InlineData("map2d.previous-view-mode", "Previous View Mode")]
    public void ClassicViewModeCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.toggle-full-brightness", EditorCommandScope.Map2D)]
    [InlineData("map3d.toggle-full-brightness", EditorCommandScope.Map3D)]
    public void FullBrightnessCommandsMatchUdbActionSurface(string commandId, EditorCommandScope scope)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Toggle Full Brightness", command.Title);
        Assert.Equal("B", command.DefaultGesture);
        Assert.Equal(scope, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.toggle-highlight", EditorCommandScope.Map2D)]
    [InlineData("map3d.toggle-highlight", EditorCommandScope.Map3D)]
    public void HighlightCommandsMatchUdbActionSurface(string commandId, EditorCommandScope scope)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Toggle Highlight", command.Title);
        Assert.Equal("H", command.DefaultGesture);
        Assert.Equal(scope, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    private static void AssertKeyOnlyMap2DCommand(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void UsdfDialogEditorCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.usdf-dialog-editor");

        Assert.NotNull(command);
        Assert.Equal("Dialog Editor", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("opendialogeditor", UsdfDialogEditorModel.Action.Id);
    }

    [Fact]
    public void WavefrontExportCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.export-wavefront");

        Assert.NotNull(command);
        Assert.Equal("Export Wavefront OBJ", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void ImageExportCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.export-image");

        Assert.NotNull(command);
        Assert.Equal("Export Image PNG", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void ObjectExportCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.export-object");

        Assert.NotNull(command);
        Assert.Equal("Export Object OBJ", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void UndoRedoPanelCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.undo-redo-panel");

        Assert.NotNull(command);
        Assert.Equal("Undo / Redo Panel", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
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
    public void VisplaneExplorerModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.mode-visplane-explorer");

        Assert.NotNull(command);
        Assert.Equal("Visplane Explorer Mode", command.Title);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal("visplaneexplorermode", VisplaneExplorerInterfaceModel.ModeDescriptor.SwitchAction);
    }

    [Fact]
    public void StairSectorBuilderCommandsMatchUdbActionSurface()
    {
        var mode = EditorCommandCatalog.Find("map2d.mode-stair-sector-builder");
        var outline = EditorCommandCatalog.Find("map2d.select-sectors-outline");

        Assert.NotNull(mode);
        Assert.Equal("Stair Sector Builder Mode", mode.Title);
        Assert.Equal(EditorCommandScope.Map2D, mode.Scope);
        Assert.True(mode.AllowKeys);
        Assert.True(mode.AllowMouse);
        Assert.True(mode.AllowScroll);

        Assert.NotNull(outline);
        Assert.Equal("Select Sectors Outline", outline.Title);
        Assert.Equal(EditorCommandScope.Map2D, outline.Scope);
        Assert.True(outline.AllowKeys);
        Assert.True(outline.AllowMouse);
        Assert.True(outline.AllowScroll);
    }

    [Fact]
    public void ThreeDFloorModeCommandsMatchUdbActionSurface()
    {
        var floor = EditorCommandCatalog.Find("map2d.mode-3d-floor");
        var slope = EditorCommandCatalog.Find("map2d.mode-3d-slope");
        var drawSlopes = EditorCommandCatalog.Find("map2d.mode-draw-slopes");

        Assert.NotNull(floor);
        Assert.Equal("3D Floor Mode", floor.Title);
        Assert.Equal(EditorCommandScope.Map2D, floor.Scope);
        Assert.True(floor.AllowScroll);
        Assert.Equal("threedfloorhelpermode", ThreeDFloors.ModeDescriptor.SwitchAction);

        Assert.NotNull(slope);
        Assert.Equal("Slope Mode", slope.Title);
        Assert.Equal("threedslopemode", ThreeDFloors.SlopeModeDescriptor.SwitchAction);

        Assert.NotNull(drawSlopes);
        Assert.Equal("Draw Slopes Mode", drawSlopes.Title);
        Assert.Equal("drawslopesmode", ThreeDFloors.DrawSlopesModeDescriptor.SwitchAction);
    }

    [Fact]
    public void ThreeDFloorActionCommandsMatchUdbActionsConfig()
    {
        var expected = new Dictionary<string, string>
        {
            ["map2d.3dfloor.draw-slope-point"] = "drawslopepoint",
            ["map2d.3dfloor.draw-floor-slope"] = "drawfloorslope",
            ["map2d.3dfloor.draw-ceiling-slope"] = "drawceilingslope",
            ["map2d.3dfloor.draw-floor-and-ceiling-slope"] = "drawfloorandceilingslope",
            ["map2d.3dfloor.finish-slope-draw"] = "finishslopedraw",
            ["map2d.3dfloor.flip-slope"] = "threedflipslope",
            ["map2d.3dfloor.cycle-highlight-up"] = "cyclehighlighted3dfloorup",
            ["map2d.3dfloor.cycle-highlight-down"] = "cyclehighlighted3dfloordown",
            ["map2d.3dfloor.relocate-control-sectors"] = "relocate3dfloorcontrolsectors",
            ["map2d.3dfloor.select-control-sector"] = "select3dfloorcontrolsector",
            ["map2d.3dfloor.duplicate-geometry"] = "duplicate3dfloorgeometry",
        };

        foreach ((string commandId, string udbActionId) in expected)
        {
            var command = EditorCommandCatalog.Find(commandId);
            ThreeDFloorActionDescriptor action = ThreeDFloors.ActionDescriptors.Single(action => action.Id == udbActionId);

            Assert.NotNull(command);
            Assert.Equal(action.Title, command.Title);
            Assert.Equal(EditorCommandScope.Map2D, command.Scope);
            Assert.Equal(action.AllowKeys, command.AllowKeys);
            Assert.Equal(action.AllowMouse, command.AllowMouse);
            Assert.Equal(action.AllowScroll, command.AllowScroll);
        }
    }

    [Fact]
    public void VisualModeCommandsMatchUdbActionSurface()
    {
        var expected = new Dictionary<string, string>
        {
            ["map3d.toggle-visual-sidedef-slope-picking"] = "Toggle Visual Sidedef Slope Picking",
            ["map3d.toggle-visual-vertex-slope-picking"] = "Toggle Visual Vertex Slope Picking",
            ["map3d.toggle-visual-vertex-slope-adjacent-selection"] = "Toggle Adjacent Visual Vertex Slope Selection",
            ["map3d.reset-slope"] = "Reset Plane Slope",
            ["map3d.move-camera-to-cursor"] = "Move Camera to Cursor",
        };

        foreach ((string commandId, string title) in expected)
        {
            var command = EditorCommandCatalog.Find(commandId);

            Assert.NotNull(command);
            Assert.Equal(title, command.Title);
            Assert.Equal("Menu", command.DefaultGesture);
            Assert.Equal(EditorCommandScope.Map3D, command.Scope);
            Assert.True(command.AllowKeys);
            Assert.True(command.AllowMouse);
            Assert.False(command.AllowScroll);
            Assert.False(command.Repeat);
        }
    }

    [Fact]
    public void VisualThingMovementCommandsMatchUdbActionSurface()
    {
        var expected = new Dictionary<string, (string Title, bool AllowScroll, bool Repeat)>
        {
            ["map3d.move-thing-left"] = ("Move Thing Left", true, true),
            ["map3d.move-thing-right"] = ("Move Thing Right", true, true),
            ["map3d.move-thing-forward"] = ("Move Thing Forward", true, true),
            ["map3d.move-thing-backward"] = ("Move Thing Backward", true, true),
            ["map3d.place-thing-at-cursor"] = ("Move Thing to Cursor Location", false, false),
            ["map3d.rotate-thing-clockwise"] = ("Rotate Thing Clockwise", true, true),
            ["map3d.rotate-thing-counterclockwise"] = ("Rotate Thing Counter-clockwise", true, true),
            ["map3d.pitch-thing-clockwise"] = ("Pitch Thing Clockwise", true, true),
            ["map3d.pitch-thing-counterclockwise"] = ("Pitch Thing Counter-clockwise", true, true),
            ["map3d.roll-thing-clockwise"] = ("Roll Thing Clockwise", true, true),
            ["map3d.roll-thing-counterclockwise"] = ("Roll Thing Counter-clockwise", true, true),
        };

        foreach ((string commandId, (string title, bool allowScroll, bool repeat)) in expected)
        {
            var command = EditorCommandCatalog.Find(commandId);

            Assert.NotNull(command);
            Assert.Equal(title, command.Title);
            Assert.Equal(EditorCommandScope.Map3D, command.Scope);
            Assert.True(command.AllowKeys);
            Assert.True(command.AllowMouse);
            Assert.Equal(allowScroll, command.AllowScroll);
            Assert.Equal(repeat, command.Repeat);
        }
    }

    [Fact]
    public void VisualCameraThingCommandsMatchUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.apply-camera-rotation-to-things");

        Assert.NotNull(command);
        Assert.Equal("Apply Camera Rotation To Things", command.Title);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);

        var lookThrough = EditorCommandCatalog.Find("map3d.look-through-thing");

        Assert.NotNull(lookThrough);
        Assert.Equal("Look Through Selection", lookThrough.Title);
        Assert.Equal(EditorCommandScope.Map3D, lookThrough.Scope);
        Assert.True(lookThrough.AllowKeys);
        Assert.False(lookThrough.AllowMouse);
        Assert.True(lookThrough.AllowScroll);
        Assert.False(lookThrough.Repeat);

        var align = EditorCommandCatalog.Find("map3d.align-things-to-wall");

        Assert.NotNull(align);
        Assert.Equal("Align Things to Nearest Linedef", align.Title);
        Assert.Equal(EditorCommandScope.Map3D, align.Scope);
        Assert.True(align.AllowKeys);
        Assert.True(align.AllowMouse);
        Assert.True(align.AllowScroll);
        Assert.False(align.Repeat);

        var showThings = EditorCommandCatalog.Find("map3d.show-visual-things");

        Assert.NotNull(showThings);
        Assert.Equal("Show Things", showThings.Title);
        Assert.Equal(EditorCommandScope.Map3D, showThings.Scope);
        Assert.True(showThings.AllowKeys);
        Assert.True(showThings.AllowMouse);
        Assert.True(showThings.AllowScroll);
        Assert.False(showThings.Repeat);
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
        Assert.Equal("map2d.toggle-full-brightness", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "B"));
        Assert.Equal("map2d.toggle-highlight", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "H"));
        Assert.Equal("map2d.draw-sector", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D"));
        Assert.Equal("map2d.draw-lines", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", shift: true));
        Assert.Equal("map2d.mode-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "NumPad1"));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.LeftButton));
        Assert.Equal("map2d.split-line", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.RightButton));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add"));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollUp));
        Assert.Equal("map2d.zoom-out", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollDown));
        Assert.Equal("map2d.point-thing-to-cursor", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "L", shift: true));
        Assert.Equal("map2d.point-thing-to-cursor", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "L", accelerator: true, shift: true));
        Assert.Equal("map2d.toggle-3d", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Tab"));
    }

    [Fact]
    public void SplitLinedefsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.split-line");

        Assert.NotNull(command);
        Assert.Equal("Split Linedefs", command.Title);
        Assert.Equal("Right-click", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.flip", "Flip Linedefs", "F")]
    [InlineData("map2d.flip-sidedefs", "Flip Sidedefs", "Shift+F")]
    public void FlipLinedefCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.lower-floor-8", "Lower Floor by 8 mp")]
    [InlineData("map2d.raise-floor-8", "Raise Floor by 8 mp")]
    [InlineData("map2d.lower-ceiling-8", "Lower Ceiling by 8 mp")]
    [InlineData("map2d.raise-ceiling-8", "Raise Ceiling by 8 mp")]
    [InlineData("map2d.raise-brightness-8", "Increase Brightness by 8")]
    [InlineData("map2d.lower-brightness-8", "Decrease Brightness by 8")]
    public void SectorHeightCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
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
        Assert.Equal("map3d.toggle-full-brightness", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "B"));
        Assert.Equal("map3d.toggle-highlight", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "H"));
        Assert.Equal("map3d.lower-brightness-8", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "["));
        Assert.Equal("map3d.raise-brightness-8", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "]"));
        Assert.Equal("map3d.copy-texture", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C"));
        Assert.Equal("map3d.flood-fill-texture", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true));
        Assert.Equal("map3d.paste-properties", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true, alt: true));
        Assert.Equal("map3d.paste-properties-options", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true, shift: true));
        Assert.Equal("map3d.look-through-thing", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Y"));
        Assert.Equal("map3d.align-things-to-wall", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true, shift: true));
        Assert.Equal("map3d.scale-up", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad9"));
        Assert.Equal("map3d.scale-down", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad7"));
        Assert.Equal("map3d.scale-up-x", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad6"));
        Assert.Equal("map3d.scale-down-x", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad4"));
        Assert.Equal("map3d.scale-up-y", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad8"));
        Assert.Equal("map3d.scale-down-y", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad5"));
        Assert.Equal("map3d.visual-auto-align", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true));
        Assert.Equal("map3d.align-texture-y", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", shift: true));
        Assert.Equal("map3d.reset-local-offsets", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "R", accelerator: true, shift: true));
        Assert.Equal("map3d.delete-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Back"));
        Assert.Equal("map3d.toggle-slope", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "S", alt: true));
        Assert.Equal("map3d.select-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.LeftButton));
        Assert.Equal("map3d.nudge-offset-left", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left", shift: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C", accelerator: true));
    }

    [Theory]
    [InlineData("map3d.lower-sector-1", "Lower Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.raise-sector-1", "Raise Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.lower-sector-8", "Lower Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.raise-sector-8", "Raise Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.lower-sector-128", "Lower Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.raise-sector-128", "Raise Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.lower-map-element-by-grid-size", "Lower Floor/Ceiling/Thing by grid size")]
    [InlineData("map3d.raise-map-element-by-grid-size", "Raise Floor/Ceiling/Thing by grid size")]
    public void VisualHeightStepCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.raise-brightness-8", "Increase Brightness by 8")]
    [InlineData("map3d.lower-brightness-8", "Decrease Brightness by 8")]
    public void VisualBrightnessStepCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.scale-up", "Increase Scale", "NumPad9")]
    [InlineData("map3d.scale-down", "Decrease Scale", "NumPad7")]
    [InlineData("map3d.scale-up-x", "Increase Horizontal Scale", "NumPad6")]
    [InlineData("map3d.scale-down-x", "Decrease Horizontal Scale", "NumPad4")]
    [InlineData("map3d.scale-up-y", "Increase Vertical Scale", "NumPad8")]
    [InlineData("map3d.scale-down-y", "Decrease Vertical Scale", "NumPad5")]
    public void VisualScaleCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.move-texture-left-1", "Move Texture Left by 1")]
    [InlineData("map3d.move-texture-right-1", "Move Texture Right by 1")]
    [InlineData("map3d.move-texture-up-1", "Move Texture Up by 1")]
    [InlineData("map3d.move-texture-down-1", "Move Texture Down by 1")]
    [InlineData("map3d.move-texture-left-8", "Move Texture Left by 8")]
    [InlineData("map3d.move-texture-right-8", "Move Texture Right by 8")]
    [InlineData("map3d.move-texture-up-8", "Move Texture Up by 8")]
    [InlineData("map3d.move-texture-down-8", "Move Texture Down by 8")]
    [InlineData("map3d.move-texture-left-grid", "Move Texture Left by Grid Size")]
    [InlineData("map3d.move-texture-right-grid", "Move Texture Right by Grid Size")]
    [InlineData("map3d.move-texture-up-grid", "Move Texture Up by Grid Size")]
    [InlineData("map3d.move-texture-down-grid", "Move Texture Down by Grid Size")]
    public void VisualTextureOffsetStepCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.reset-offsets", "Reset Texture Offsets", "O")]
    [InlineData("map3d.reset-local-offsets", "Reset Local Texture Offsets (UDMF)", "Ctrl/Cmd+Shift+R")]
    public void VisualTextureResetCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualTextureFloodFillCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.flood-fill-texture");

        Assert.NotNull(command);
        Assert.Equal("Paste Texture Flood-Fill", command.Title);
        Assert.Equal("Ctrl/Cmd+V", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.toggle-upper-unpegged", "Toggle Upper Unpegged")]
    [InlineData("map3d.toggle-lower-unpegged", "Toggle Lower Unpegged")]
    public void VisualUnpeggedCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualToggleSlopeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-slope");

        Assert.NotNull(command);
        Assert.Equal("Toggle Slope", command.Title);
        Assert.Equal("Alt+S", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualAlphaBasedTextureHighlightingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-alpha-based-texture-highlighting");

        Assert.NotNull(command);
        Assert.Equal("Toggle Alpha-based Texture Highlighting", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.copy-offsets", "Copy Offsets")]
    [InlineData("map3d.paste-offsets", "Paste Offsets")]
    public void VisualTextureOffsetClipboardCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.copy-properties", "Copy Properties", "Menu")]
    [InlineData("map3d.paste-properties", "Paste Properties", "Ctrl/Cmd+Alt+V")]
    [InlineData("map3d.paste-properties-options", "Paste Properties Special", "Ctrl/Cmd+Shift+V")]
    public void VisualPastePropertiesCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualFitTexturesCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.fit-textures");

        Assert.NotNull(command);
        Assert.Equal("Fit Textures", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.visual-auto-align", "Auto-align Textures X and Y", "Ctrl+A")]
    [InlineData("map3d.visual-auto-align-x", "Auto-align Textures X", "Menu")]
    [InlineData("map3d.visual-auto-align-y", "Auto-align Textures Y", "Menu")]
    [InlineData("map3d.visual-auto-align-to-selection", "Auto-align Textures to Selection (X and Y)", "Menu")]
    [InlineData("map3d.visual-auto-align-to-selection-x", "Auto-align Textures to Selection (X)", "Menu")]
    [InlineData("map3d.visual-auto-align-to-selection-y", "Auto-align Textures to Selection (Y)", "Menu")]
    public void VisualAutoAlignCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void RepeatableCommandMetadataMatchesAdjustmentActions()
    {
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.zoom-in"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.grid-up"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.lower-floor-8"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.raise-brightness-8"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.raise-sector-128"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.move-texture-left-grid"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.lower-brightness-8"));
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
