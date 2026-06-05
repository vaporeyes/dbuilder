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
        Assert.Contains("window.make-door", ids);
        Assert.Contains("window.tag-range", ids);
        Assert.Contains("window.tag-explorer", ids);
        Assert.Contains("window.comments-panel", ids);
        Assert.Contains("window.blockmap-explorer", ids);
        Assert.Contains("window.reject-explorer", ids);
        Assert.Contains("window.nodes-viewer", ids);
        Assert.Contains("window.sound-propagation-mode", ids);
        Assert.Contains("window.sound-environment-mode", ids);
        Assert.Contains("window.sound-propagation-colors", ids);
        Assert.Contains("map2d.mode-image-example", ids);
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
            Assert.False(string.IsNullOrWhiteSpace(command.CategoryTitle));
        });
    }

    [Theory]
    [InlineData("window.open-map", "File")]
    [InlineData("window.newmap", "File")]
    [InlineData("window.openmap", "File")]
    [InlineData("window.openmapincurrentwad", "File")]
    [InlineData("window.closemap", "File")]
    [InlineData("window.savemap", "File")]
    [InlineData("window.savemapas", "File")]
    [InlineData("window.savemapinto", "File")]
    [InlineData("window.undo", "Edit")]
    [InlineData("window.mapoptions", "Edit")]
    [InlineData("window.toggleautoclearsidetextures", "Edit")]
    [InlineData("window.open-command-palette", "Tools")]
    [InlineData("window.reloadresources", "Tools")]
    [InlineData("window.testmap", "Tools")]
    [InlineData("window.viewusedtags", "Tools")]
    [InlineData("window.viewthingtypes", "Tools")]
    [InlineData("window.gridsetup", "Tools")]
    [InlineData("window.toggle-info-panel", "View")]
    [InlineData("window.toggleinfopanel", "View")]
    [InlineData("window.togglebrightness", "View")]
    [InlineData("window.create-prefab", "Prefabs")]
    [InlineData("window.createprefab", "Prefabs")]
    [InlineData("window.insertprefabfile", "Prefabs")]
    [InlineData("window.insertpreviousprefab", "Prefabs")]
    [InlineData("window.select-group-1", "Selecting")]
    [InlineData("window.selectgroup1", "Selecting")]
    [InlineData("window.assigngroup1", "Selecting")]
    [InlineData("window.cleargroup1", "Selecting")]
    [InlineData("window.udbscripts", "Scripting")]
    [InlineData("window.model-render-all", "Rendering")]
    [InlineData("map2d.mode-vertices", "Modes")]
    [InlineData("map2d.zoom-in", "Classic Modes")]
    [InlineData("map3d.move-forward", "Visual Modes")]
    public void CommandMetadataExposesUdbStyleCategoryTitles(string commandId, string categoryTitle)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(categoryTitle, command.CategoryTitle);
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
        var udbAlias = EditorCommandCatalog.Find("window.toggleautoclearsidetextures");

        Assert.NotNull(command);
        Assert.Equal("Auto Clear Sidedef Textures", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.CategoryTitle, udbAlias.CategoryTitle);
    }

    [Theory]
    [InlineData("window.toggleautomerge", "Snap to Geometry", "Toggles snapping to the nearest vertex or linedef for map elements that are being dragged.")]
    [InlineData("window.togglejoinedsectorssplitting", "Split Joined Sectors", "When enabled, joined sectors adjacent to drawn lines will be split.")]
    public void GeometryToggleCommandsMatchUdbActionSurface(string commandId, string title, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal(description, command.Description);
    }

    [Fact]
    public void MakeDoorCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.make-door");
        var udbAlias = EditorCommandCatalog.Find("window.makedoor");

        Assert.NotNull(command);
        Assert.Equal("Make Door", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal("imageexamplemode", ImageExampleModeModel.ModeDescriptor.SwitchAction);
    }

    [Fact]
    public void TagRangeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.tag-range");

        Assert.NotNull(command);
        Assert.Equal(TagRangeModel.ToolWindowTitle, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void BlockmapExplorerCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.blockmap-explorer");

        Assert.NotNull(command);
        Assert.Equal("Blockmap Explorer mode", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void RejectExplorerCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.reject-explorer");

        Assert.NotNull(command);
        Assert.Equal("Reject Explorer mode", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void NodesViewerCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.nodes-viewer");

        Assert.NotNull(command);
        Assert.Equal("Nodes Viewer Mode", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void SoundPropagationModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.sound-propagation-mode");

        Assert.NotNull(command);
        Assert.Equal("Sound propagation mode", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void SoundPropagationColorCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.sound-propagation-colors");

        Assert.NotNull(command);
        Assert.Equal(SoundPropagationColorSettings.ColorConfigurationAction.Title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("window.setleakfinderstart", "Set leak finder start sector", "Shift+S")]
    [InlineData("window.setleakfinderend", "Set leak finder end sector", "Shift+E")]
    public void SoundLeakFinderCommandsMatchUdbActionSurface(string commandId, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("S", "window.setleakfinderstart")]
    [InlineData("E", "window.setleakfinderend")]
    public void SoundLeakFinderShortcutsMatchUdbDefaults(string key, string commandId)
        => Assert.Equal(commandId, EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, key, shift: true));

    [Fact]
    public void JitterCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.applyjitter");

        Assert.NotNull(command);
        Assert.Equal("Randomize", command.Title);
        Assert.Equal("Ctrl/Cmd+J", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal("window.applyjitter", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "J", accelerator: true));
    }

    [Fact]
    public void DirectionalShadingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.applydirectionalshading");

        Assert.NotNull(command);
        Assert.Equal("Apply Directional Shading", command.Title);
        Assert.Equal("Ctrl/Cmd+L", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal("window.applydirectionalshading", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "L", accelerator: true));
    }

    [Fact]
    public void SoundEnvironmentModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.sound-environment-mode");

        Assert.NotNull(command);
        Assert.Equal(SoundEnvironmentModeModel.ModeAction.Title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void TagExplorerCommandMatchesUdbDockerSurface()
    {
        var command = EditorCommandCatalog.Find("window.tag-explorer");

        Assert.NotNull(command);
        Assert.Equal(TagExplorerModel.DockerTitle, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void CommentsPanelCommandMatchesUdbDockerSurface()
    {
        var command = EditorCommandCatalog.Find("window.comments-panel");

        Assert.NotNull(command);
        Assert.Equal(CommentsPanelModel.DockerTitle, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("window.new-map", "New Map", "Menu")]
    [InlineData("window.newmap", "New Map", "Menu")]
    [InlineData("window.open-map", "Open Map", "Menu")]
    [InlineData("window.openmap", "Open Map", "Menu")]
    [InlineData("window.recover-autosave", "Recover Autosave", "Menu")]
    [InlineData("window.open-map-in-current-wad", "Open Map in current WAD", "Ctrl/Cmd+Shift+O")]
    [InlineData("window.openmapincurrentwad", "Open Map in current WAD", "Ctrl/Cmd+Shift+O")]
    [InlineData("window.reload-map", "Reload Map", "Menu")]
    [InlineData("window.close-map", "Close Map", "Menu")]
    [InlineData("window.closemap", "Close Map", "Menu")]
    [InlineData("window.add-resource", "Add Resource", "Menu")]
    [InlineData("window.add-resource-directory", "Add Resource Directory", "Menu")]
    [InlineData("window.save-map", "Save Map", "Ctrl/Cmd+S")]
    [InlineData("window.savemap", "Save Map", "Ctrl/Cmd+S")]
    [InlineData("window.save-map-as", "Save Map As", "Menu")]
    [InlineData("window.savemapas", "Save Map As", "Menu")]
    [InlineData("window.save-as-format", "Save As Format", "Menu")]
    [InlineData("window.savemapinto", "Save Map Into", "Menu")]
    [InlineData("window.map-options", "Map Options", "Menu")]
    [InlineData("window.mapoptions", "Map Options", "Menu")]
    [InlineData("window.snap-selection-to-grid", "Snap Selected Map Elements to Grid", "Menu")]
    [InlineData("window.snapvertstogrid", "Snap Selected Map Elements to Grid", "Menu")]
    [InlineData("window.game-configurations", "Game Configurations", "Menu")]
    [InlineData("window.configuration", "Game Configurations", "Menu")]
    [InlineData("window.preferences", "Preferences", "Menu")]
    [InlineData("window.exit", "Exit", "Menu")]
    [InlineData("window.reference-manual", "Reference Manual", "Menu")]
    [InlineData("window.edit-mode-help", "About This Editing Mode", "Menu")]
    [InlineData("window.shortcuts", "Shortcuts", "Menu")]
    [InlineData("window.about", "About", "Menu")]
    [InlineData("window.view-used-tags", "View Used Tags", "Menu")]
    [InlineData("window.viewusedtags", "View Used Tags", "Menu")]
    [InlineData("window.view-thing-types", "View Thing Types", "Menu")]
    [InlineData("window.viewthingtypes", "View Thing Types", "Menu")]
    [InlineData("window.center-on-coordinates", "Go To Coordinates", "Ctrl/Cmd+Shift+G")]
    [InlineData("window.centeroncoordinates", "Go To Coordinates", "Ctrl/Cmd+Shift+G")]
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

    [Theory]
    [InlineData("window.open-command-palette")]
    [InlineData("window.opencommandpalette")]
    public void CommandPaletteActionMatchesUdbActionSurface(string commandId)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Open Command Palette", command.Title);
        Assert.Equal("Opens the command palette.", command.Description);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Fact]
    public void ShowErrorsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.show-errors");
        var udbAlias = EditorCommandCatalog.Find("window.showerrors");

        Assert.NotNull(command);
        Assert.Equal("Show Errors and Warnings", command.Title);
        Assert.Equal("F11", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void StatusHistoryCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.status-history");

        Assert.NotNull(command);
        Assert.Equal("Status History", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.browse-wall-textures", "Browse Textures")]
    [InlineData("window.browse-flats", "Browse Flats")]
    [InlineData("window.browse-floor-flats", "Set Selected Floor Flats")]
    [InlineData("window.browse-ceiling-flats", "Set Selected Ceiling Flats")]
    [InlineData("window.browse-things", "Browse Things")]
    [InlineData("window.browse-linedef-actions", "Browse Linedef Actions")]
    [InlineData("window.browse-sector-effects", "Browse Sector Effects")]
    public void BrowserCommandsMatchUdbActionSurface(string commandId, string title)
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
    [InlineData("window.model-render-none", "No Model Rendering")]
    [InlineData("window.model-render-selection", "Model Rendering Selection Only")]
    [InlineData("window.model-render-active-filter", "Model Rendering Active Things Filter Only")]
    [InlineData("window.model-render-all", "Model Rendering All")]
    [InlineData("window.next-model-render-mode", "Next Model Rendering Mode")]
    public void ModelRenderMenuCommandsMatchUdbActionSurface(string commandId, string title)
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
    [InlineData("window.gradient-floor-heights", "Make Floors Gradient", "Creates a floor heights gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradientfloors", "Make Floors Gradient", "Creates a floor heights gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradient-ceiling-heights", "Make Ceilings Gradient", "Creates a ceiling heights gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradientceilings", "Make Ceilings Gradient", "Creates a ceiling heights gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradient-sector-brightness", "Make Brightness Gradient", "Creates a brightness gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradientbrightness", "Make Brightness Gradient", "Creates a brightness gradient over all selected sectors from the first to the last selected sector.")]
    [InlineData("window.gradient-floor-light", "Gradient Floor Light")]
    [InlineData("window.gradient-ceiling-light", "Gradient Ceiling Light")]
    [InlineData("window.gradient-light-color", "Gradient Light Color")]
    [InlineData("window.gradient-fade-color", "Gradient Fade Color")]
    [InlineData("window.gradient-light-and-fade-colors", "Gradient Light and Fade Colors")]
    [InlineData("window.gradient-linedef-brightness", "Gradient Linedef Brightness")]
    [InlineData("window.gradient-interpolation-linear", "Gradient Interpolation Linear")]
    [InlineData("window.gradient-interpolation-ease-in-out-sine", "Gradient Interpolation Ease In/Out Sine")]
    [InlineData("window.gradient-interpolation-ease-in-sine", "Gradient Interpolation Ease In Sine")]
    [InlineData("window.gradient-interpolation-ease-out-sine", "Gradient Interpolation Ease Out Sine")]
    public void GradientMenuCommandsMatchUdbActionSurface(string commandId, string title, string? description = null)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        if (description != null) Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.apply-slope-arch", "Apply Slope Arch")]
    [InlineData("window.apply-slopes", "Apply Slopes")]
    public void SlopeUtilityCommandsMatchUdbActionSurface(string commandId, string title)
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

    [Theory]
    [InlineData("window.toggle-automap-secret-line", "Toggle Selected Line Secret")]
    [InlineData("window.toggle-automap-hidden-line", "Toggle Selected Line Hidden")]
    [InlineData("window.toggle-automap-textured-hidden-sector", "Toggle Selected Sector Textured Hidden")]
    public void AutomapSelectionCommandsMatchUdbActionSurface(string commandId, string title)
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

    [Theory]
    [InlineData("window.sector-color", "Sector Color")]
    [InlineData("window.dynamic-light-color", "Dynamic Light Color")]
    public void ColorDialogCommandsMatchUdbActionSurface(string commandId, string title)
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
    public void ColorPickerPanelCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.togglelightpannel");

        Assert.NotNull(command);
        Assert.Equal("Open Color Picker", command.Title);
        Assert.Equal("K", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal("window.togglelightpannel", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "K"));
    }

    [Theory]
    [InlineData("window.import-obj-terrain", "Import Wavefront .obj as terrain", "Creates sectors from given model (UDMF only).", false, false)]
    [InlineData("window.importobjasterrain", "Import Wavefront .obj as terrain", "Creates sectors from given model (UDMF only).", false, false)]
    [InlineData("window.export-object", "Export Object OBJ", null, true, true)]
    [InlineData("window.export-image", "Export to image", "Exports selected sectors (or the whole map if no sectors selected) to an image", false, false)]
    [InlineData("window.exporttoimage", "Export to image", "Exports selected sectors (or the whole map if no sectors selected) to an image", false, false)]
    [InlineData("window.export-wavefront", "Export to Wavefront .obj", "Exports selected sectors (or the whole map if no sectors selected) to Wavefront .obj", false, false)]
    [InlineData("window.exporttoobj", "Export to Wavefront .obj", "Exports selected sectors (or the whole map if no sectors selected) to Wavefront .obj", false, false)]
    [InlineData("window.export-idstudio", "Export to idStudio .map", "Exports level to a set of .map files useable by idStudio", false, false)]
    [InlineData("window.exporttoidstudio", "Export to idStudio .map", "Exports level to a set of .map files useable by idStudio", false, false)]
    public void ImportExportToolCommandsMatchUdbActionSurface(string commandId, string title, string? description, bool allowMouse, bool allowScroll)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        if (description != null) Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.Equal(allowMouse, command.AllowMouse);
        Assert.Equal(allowScroll, command.AllowScroll);
    }

    [Theory]
    [InlineData("window.savescreenshot", "Save Screenshot", "F12", "Saves a screenshot of editor's window into 'Screenshots' folder.")]
    [InlineData("window.saveeditareascreenshot", "Save Screenshot (active window)", "Ctrl/Cmd+F12", "Saves a screenshot of currently active window, or editing area if no windows are open into 'Screenshots' folder.")]
    public void ScreenshotCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal(description, command.Description);
    }

    [Fact]
    public void SaveMapIntoAliasMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.savemapinto");

        Assert.NotNull(command);
        Assert.Equal("Save Map Into", command.Title);
        Assert.Equal("Saves the current map without any other resources into an existing or new WAD file.", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.check-map", "Check Map")]
    [InlineData("window.clean-up-geometry", "Clean Up Geometry")]
    public void MapMaintenanceCommandsMatchUdbActionSurface(string commandId, string title)
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

    [Theory]
    [InlineData("window.build-bridge", "Build Bridge")]
    [InlineData("window.build-stairs", "Build Stairs")]
    public void ConstructionToolCommandsMatchUdbActionSurface(string commandId, string title)
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
    public void UsdfConversationsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.usdf-conversations");

        Assert.NotNull(command);
        Assert.Equal("USDF Conversations", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.toggle-3d-floors", "Show 3D Floors")]
    [InlineData("window.toggle-blockmap", "Show Blockmap")]
    [InlineData("window.toggle-nodes", "Show Nodes")]
    public void ViewOverlayToggleCommandsMatchUdbActionSurface(string commandId, string title)
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

    [Fact]
    public void ToggleInfoPanelCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.toggle-info-panel");
        var udbAlias = EditorCommandCatalog.Find("window.toggleinfopanel");

        Assert.NotNull(command);
        Assert.Equal("Toggle Info Panel", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
        Assert.Equal(command.CategoryTitle, udbAlias.CategoryTitle);
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
        Assert.Equal("window.center-on-coordinates", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Window,
            "G",
            accelerator: true,
            shift: true));
        Assert.Equal("window.show-errors", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "F11"));
    }

    [Fact]
    public void GoToCoordinatesLegacyAliasKeepsUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.center-on-coordinates");
        var alias = EditorCommandCatalog.Find("window.go-to-coordinates");

        Assert.NotNull(command);
        Assert.NotNull(alias);
        Assert.Equal(command.Title, alias.Title);
        Assert.Equal(command.DefaultGesture, alias.DefaultGesture);
        Assert.Equal(command.Scope, alias.Scope);
    }

    [Fact]
    public void SelectSimilarCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.select-similar");
        var udbAlias = EditorCommandCatalog.Find("window.selectsimilar");

        Assert.NotNull(command);
        Assert.Equal("Select Similar Map Elements", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void FilterSelectedThingsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.filter-selected-things");
        var udbAlias = EditorCommandCatalog.Find("window.filterselectedthings");

        Assert.NotNull(command);
        Assert.Equal("Filter Selected Things", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void ThingsFiltersSetupCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.things-filters-setup");
        var udbAlias = EditorCommandCatalog.Find("window.thingsfilterssetup");

        Assert.NotNull(command);
        Assert.Equal("Configure Things Filters", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void LinedefColorsSetupCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.linedefcolorssetup");

        Assert.NotNull(command);
        Assert.Equal(LinedefColorPresetModel.ConfigureActionTitle, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal(LinedefColorPresetModel.ConfigureActionDescription, command.Description);
    }

    [Fact]
    public void ChangeMapElementIndexCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.change-map-element-index");
        var udbAlias = EditorCommandCatalog.Find("window.changemapelementindex");

        Assert.NotNull(command);
        Assert.Equal("Change Map Element Index", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("window.copy-properties", "Copy Properties", "Menu")]
    [InlineData("window.classiccopyproperties", "Copy Properties", "Ctrl/Cmd+Shift+C")]
    [InlineData("window.paste-properties", "Paste Properties", "Menu")]
    [InlineData("window.classicpasteproperties", "Paste Properties", "Ctrl/Cmd+Alt+V")]
    [InlineData("window.paste-properties-options", "Paste Properties With Options", "Menu")]
    [InlineData("window.classicpastepropertieswithoptions", "Paste Properties Special", "Ctrl/Cmd+Shift+V")]
    public void PastePropertiesCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        if (commandId == "window.classicpastepropertieswithoptions")
            Assert.Equal("Pastes the copied properties onto the highlighted or selected objects allowing you to choose the properties to paste.", command.Description);
    }

    [Theory]
    [InlineData("window.paste-special")]
    [InlineData("window.pasteselectionspecial")]
    public void PasteSpecialCommandMatchesUdbActionSurface(string commandId)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Paste Selection Special", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.delete")]
    [InlineData("window.deleteitem")]
    public void DeleteSelectionCommandsMatchUdbActionSurface(string commandId)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Delete Item", command.Title);
        Assert.Equal("Delete", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.select-all", "Select all")]
    [InlineData("window.invert-selection", "Invert selection")]
    [InlineData("window.select-none", "Clear Selection")]
    [InlineData("window.clearselection", "Clear Selection")]
    public void WindowSelectionCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.Equal(commandId is "window.select-none" or "window.clearselection", command.AllowScroll);
    }

    [Theory]
    [InlineData("window.flags", "Flags")]
    [InlineData("window.custom-fields", "Custom Fields")]
    [InlineData("window.tags", "Tags")]
    public void WindowPropertyDialogCommandsMatchUdbActionSurface(string commandId, string title)
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
    [InlineData("window.stitch-geometry", "Stitch geometry", null)]
    [InlineData("window.geomergeclassic", "Merge Dragged Vertices Only", null)]
    [InlineData("window.geomerge", "Merge Dragged Geometry", null)]
    [InlineData("window.georeplace", "Replace with Dragged Geometry", null)]
    [InlineData("window.join-sectors", "Join Sectors", "Joins two or more selected sectors together and keeps all linedefs.")]
    [InlineData("window.merge-sectors", "Merge Sectors", "Joins two or more selected sectors together and removes the shared linedefs.")]
    public void WindowGeometryEditCommandsMatchUdbActionSurface(string commandId, string title, string? description = null)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        if (description != null) Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.flip-selection-horizontal", "Flip Selection Horizontally", "Flips the selection in Edit Selection mode horizontally.")]
    [InlineData("window.flipselectionh", "Flip Selection Horizontally", "Flips the selection in Edit Selection mode horizontally.")]
    [InlineData("window.flip-selection-vertical", "Flip Selection Vertically", "Flips the selection in Edit Selection mode vertically.")]
    [InlineData("window.flipselectionv", "Flip Selection Vertically", "Flips the selection in Edit Selection mode vertically.")]
    [InlineData("window.rotate-selection-cw", "Rotate Clockwise", "Rotates selected or highlighted things clockwise. Also rotates floor/ceiling textures in UDMF map format, and rotates the selection in Edit Selection mode.")]
    [InlineData("window.rotateclockwise", "Rotate Clockwise", "Rotates selected or highlighted things clockwise. Also rotates floor/ceiling textures in UDMF map format, and rotates the selection in Edit Selection mode.")]
    [InlineData("window.rotate-selection-ccw", "Rotate Counterclockwise", "Rotates selected or highlighted things counterclockwise. Also rotates floor/ceiling textures in UDMF map format, and rotates the selection in Edit Selection mode.")]
    [InlineData("window.rotatecounterclockwise", "Rotate Counterclockwise", "Rotates selected or highlighted things counterclockwise. Also rotates floor/ceiling textures in UDMF map format, and rotates the selection in Edit Selection mode.")]
    [InlineData("window.moveselectionup", "Move Selection Up by Grid Size")]
    [InlineData("window.moveselectiondown", "Move Selection Down by Grid Size")]
    [InlineData("window.moveselectionleft", "Move Selection Left by Grid Size")]
    [InlineData("window.moveselectionright", "Move Selection Right by Grid Size")]
    [InlineData("window.scale-selection-up", "Scale Up")]
    [InlineData("window.scale-selection-down", "Scale Down")]
    public void WindowTransformSelectionCommandsMatchUdbActionSurface(string commandId, string title, string? description = null)
    {
        var command = EditorCommandCatalog.Find(commandId);
        bool isRotateClockwise = commandId.Contains("rotate-selection-cw", StringComparison.Ordinal)
            || commandId.Contains("rotateclockwise", StringComparison.Ordinal);
        bool isRotateCounterclockwise = commandId.Contains("rotate-selection-ccw", StringComparison.Ordinal)
            || commandId.Contains("rotatecounterclockwise", StringComparison.Ordinal);
        bool isRotate = isRotateClockwise || isRotateCounterclockwise;

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        if (description != null) Assert.Equal(description, command.Description);
        Assert.Equal(
            isRotateClockwise ? "Ctrl/Cmd+Shift+ScrollUp" :
            isRotateCounterclockwise ? "Ctrl/Cmd+Shift+ScrollDown" :
            "Menu",
            command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.Equal(!isRotate, command.AllowMouse);
        Assert.Equal(
            commandId.StartsWith("window.moveselection", StringComparison.Ordinal)
                || commandId.StartsWith("window.flip-selection", StringComparison.Ordinal)
                || commandId.StartsWith("window.flipselection", StringComparison.Ordinal)
                || isRotate,
            command.AllowScroll);
        Assert.Equal(
            commandId.StartsWith("window.moveselection", StringComparison.Ordinal) || isRotate,
            command.Repeat);
    }

    [Theory]
    [InlineData("window.align-floor-to-front", "Align Floor Texture to Front Side", "Aligns floor textures to front sides of selected linedefs.")]
    [InlineData("window.alignfloortofront", "Align Floor Texture to Front Side", "Aligns floor textures to front sides of selected linedefs.")]
    [InlineData("window.align-floor-to-back", "Align Floor Texture to Back Side", "Aligns floor textures to back sides of selected linedefs.")]
    [InlineData("window.alignfloortoback", "Align Floor Texture to Back Side", "Aligns floor textures to back sides of selected linedefs.")]
    [InlineData("window.align-ceiling-to-front", "Align Ceiling Texture to Front Side", "Aligns ceiling textures to front sides of selected linedefs.")]
    [InlineData("window.alignceilingtofront", "Align Ceiling Texture to Front Side", "Aligns ceiling textures to front sides of selected linedefs.")]
    [InlineData("window.align-ceiling-to-back", "Align Ceiling Texture to Back Side", "Aligns ceiling textures to back sides of selected linedefs.")]
    [InlineData("window.alignceilingtoback", "Align Ceiling Texture to Back Side", "Aligns ceiling textures to back sides of selected linedefs.")]
    public void WindowFlatAlignmentCommandsMatchUdbActionSurface(string commandId, string title, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("window.align-things-to-wall", "Align Things to Wall")]
    [InlineData("window.find-replace", "Find and Replace")]
    public void WindowEditUtilityCommandsMatchUdbActionSurface(string commandId, string title)
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
    [InlineData("window.create-prefab", "Create Prefab")]
    [InlineData("window.createprefab", "Create Prefab")]
    [InlineData("window.insert-prefab-file", "Insert Prefab File")]
    [InlineData("window.insertprefabfile", "Insert Prefab File")]
    [InlineData("window.insert-previous-prefab", "Insert Previous Prefab")]
    [InlineData("window.insertpreviousprefab", "Insert Previous Prefab")]
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
    [InlineData("window.testmap", "Test Map")]
    [InlineData("window.things-filters-setup", "Configure Things Filters")]
    [InlineData("window.reload-resources", "Reload Resources")]
    [InlineData("window.reloadresources", "Reload Resources")]
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

    [Theory]
    [InlineData("window.gzreloadmodeldef", "Reload MODELDEF/VOXELDEF", "Ctrl/Cmd+F5", "Reloads MODELDEF and VOXELDEF. Useful when resource files have been changed outside of Doom Builder.")]
    [InlineData("window.gzreloadgldefs", "Reload GLDEFS", "Ctrl/Cmd+F6", "Reloads GLDEFS. Useful when resource files have been changed outside of Doom Builder.")]
    public void GzReloadCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal(description, command.Description);
    }

    [Theory]
    [InlineData("window.test-map-from-view")]
    [InlineData("window.testmapfromview")]
    public void TestMapFromViewCommandMatchesUdbActionSurface(string commandId)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Test map from current position", command.Title);
        Assert.Equal("Ctrl/Cmd+F9", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal(
            "Starts the game and loads this map for playing. Player start is placed either at cursor position (in 2D-Modes) or at camera position (in Visual Modes).",
            command.Description);
    }

    [Fact]
    public void TestMapFromViewShortcutMatchesUdbDefault()
        => Assert.Equal("window.testmapfromview", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "F9", accelerator: true));

    [Fact]
    public void GridSetupCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.grid-setup");
        var udbAlias = EditorCommandCatalog.Find("window.gridsetup");

        Assert.NotNull(command);
        Assert.Equal("Grid and Backdrop Setup", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
        Assert.Equal(command.CategoryTitle, udbAlias.CategoryTitle);
    }

    [Fact]
    public void DynamicGridCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.toggle-dynamic-grid-size");
        var udbAlias = EditorCommandCatalog.Find("map2d.toggledynamicgrid");

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
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
        Assert.Equal(command.AllowMouse, udbAlias.AllowMouse);
    }

    [Theory]
    [InlineData("map2d.toggle-grid-snap", "Snap to Grid", "G", false, "Toggles snapping to the grid for things and vertices that are being dragged.")]
    [InlineData("map2d.togglesnap", "Snap to Grid", "G", false, "Toggles snapping to the grid for things and vertices that are being dragged.")]
    [InlineData("map2d.grid-down", "Decrease grid size", "[", true, null)]
    [InlineData("map2d.grid-up", "Increase grid size", "]", true, null)]
    public void GridSizeAndSnapCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture, bool repeat, string? description = null)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        if (description != null) Assert.Equal(description, command.Description);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.Equal(description == null, command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal(repeat, command.Repeat);
    }

    [Theory]
    [InlineData("map2d.griddec", "Grid Increase", "]", "Increases the grid size, decreasing the grid density.")]
    [InlineData("map2d.gridinc", "Grid Decrease", "[", "Decreases the grid size, increasing the grid density.")]
    public void GridSizeUdbAliasesMatchSwappedUdbActionSurface(string commandId, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.toggle-grid-rendering")]
    [InlineData("map2d.togglegrid")]
    public void GridRenderingCommandMatchesUdbActionSurface(string commandId)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal("Toggle Grid", command.Title);
        Assert.Equal("Toggles grid rendering in classic modes.", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.zoom-in", "Zoom In", "+", "Zooms in on the map at the current mouse location.")]
    [InlineData("map2d.zoomin", "Zoom In", "+", "Zooms in on the map at the current mouse location.")]
    [InlineData("map2d.zoom-out", "Zoom Out", "-", "Zooms out on the map from the current mouse location.")]
    [InlineData("map2d.zoomout", "Zoom Out", "-", "Zooms out on the map from the current mouse location.")]
    public void ZoomCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Fact]
    public void CenterInScreenCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.centerinscreen");

        Assert.NotNull(command);
        Assert.Equal("Fit To Screen", command.Title);
        Assert.Equal("R", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void PanViewCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.pan_view");

        Assert.NotNull(command);
        Assert.Equal("Pan View", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal("Pans the map in the direction of the mouse while held down.", command.Description);
        Assert.Equal("Classic Modes", command.CategoryTitle);
    }

    [Theory]
    [InlineData("map2d.scrollwest", "Scroll West")]
    [InlineData("map2d.scrolleast", "Scroll East")]
    [InlineData("map2d.scrollnorth", "Scroll North")]
    [InlineData("map2d.scrollsouth", "Scroll South")]
    public void ScrollCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.align-grid-to-linedef", "Align Grid to Selected Linedef")]
    [InlineData("map2d.aligngridtolinedef", "Align Grid to Selected Linedef")]
    [InlineData("map2d.set-grid-origin-to-vertex", "Set Grid Origin to Selected Vertex")]
    [InlineData("map2d.setgridorigintovertex", "Set Grid Origin to Selected Vertex")]
    [InlineData("map2d.reset-grid-transform", "Reset Grid Transform")]
    [InlineData("map2d.resetgrid", "Reset Grid Transform")]
    [InlineData("map2d.smart-grid-transform", "Smart Grid Transform")]
    [InlineData("map2d.smartgridtransform", "Smart Grid Transform")]
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
        var udbAlias = EditorCommandCatalog.Find("map2d.placethings");

        Assert.NotNull(command);
        Assert.Equal("Place Things", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void PlaceVisualStartCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.placevisualstart");

        Assert.NotNull(command);
        Assert.Equal("Place Visual Mode Camera", command.Title);
        Assert.Equal("Ctrl/Cmd+W", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("map2d.placevisualstart", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "W", accelerator: true));
    }

    [Fact]
    public void SynchronizedThingEditingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.syncedthingedit");

        Assert.NotNull(command);
        Assert.Equal("Synchronized Things Editing", command.Title);
        Assert.Equal("Shift+T", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.Equal("map2d.syncedthingedit", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "T", shift: true));
    }

    [Fact]
    public void InsertItemAliasMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.insert");
        var udbAlias = EditorCommandCatalog.Find("map2d.insertitem");

        Assert.NotNull(command);
        Assert.Equal("Insert vertex or thing", command.Title);
        Assert.Equal("I", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.NotNull(udbAlias);
        Assert.Equal("Insert Item", udbAlias.Title);
        Assert.Equal("I", udbAlias.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, udbAlias.Scope);
        Assert.True(udbAlias.AllowKeys);
        Assert.True(udbAlias.AllowMouse);
        Assert.True(udbAlias.AllowScroll);
        Assert.Equal("Creates a new item depending on the editing mode you are in.", udbAlias.Description);
    }

    [Theory]
    [InlineData("map2d.point-thing-to-cursor", "map2d.thinglookatcursor", "Point Thing to Cursor", "Shift+L")]
    public void ClassicThingActionAliasesMatchUdbActionSurface(string id, string udbId, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);
        var udbAlias = EditorCommandCatalog.Find(udbId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void ClassicThingAlignToWallAliasMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.thingaligntowall");

        Assert.NotNull(command);
        Assert.Equal("Align Things to Nearest Linedef", command.Title);
        Assert.Equal("Ctrl/Cmd+Shift+A", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.join-sectors", "Join Sectors", "Joins two or more selected sectors together and keeps all linedefs.")]
    [InlineData("map2d.joinsectors", "Join Sectors", "Joins two or more selected sectors together and keeps all linedefs.")]
    [InlineData("map2d.merge-sectors", "Merge Sectors", "Joins two or more selected sectors together and removes the shared linedefs.")]
    [InlineData("map2d.mergesectors", "Merge Sectors", "Joins two or more selected sectors together and removes the shared linedefs.")]
    public void JoinMergeSectorCommandsMatchUdbActionSurface(string commandId, string title, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.select-single-sided", "Select Single-sided", "Menu", "This keeps only the single-sided lines in your selection selected.")]
    [InlineData("map2d.selectsinglesided", "Select Single-sided", "Shift+Q", "This keeps only the single-sided lines in your selection selected.")]
    [InlineData("map2d.select-double-sided", "Select Double-sided", "Menu", "This keeps only the double-sided lines in your selection selected.")]
    [InlineData("map2d.selectdoublesided", "Select Double-sided", "Shift+R", "This keeps only the double-sided lines in your selection selected.")]
    public void SelectSidednessCommandsMatchUdbActionSurface(string commandId, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);

        Assert.Equal("map2d.selectsinglesided", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Q", shift: true));
        Assert.Equal("map2d.selectdoublesided", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "R", shift: true));
    }

    [Theory]
    [InlineData("map2d.align-linedefs", "Align Linedefs", "This aligns the selected linedefs, so their front (or back) point towards (or away from) the same sector.")]
    [InlineData("map2d.alignlinedefs", "Align Linedefs", "This aligns the selected linedefs, so their front (or back) point towards (or away from) the same sector.")]
    [InlineData("map2d.split-linedefs", "Split Linedefs", "Splits the selected linedefs in the middle, or splits the highlighted linedef at the mouse position.")]
    [InlineData("map2d.splitlinedefs", "Split Linedefs", "Splits the selected linedefs in the middle, or splits the highlighted linedef at the mouse position.")]
    public void LinedefEditCommandsMatchUdbActionSurface(string commandId, string title, string description)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
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
        var udbAlias = EditorCommandCatalog.Find("map2d.applylightfogflag");

        Assert.NotNull(command);
        Assert.Equal("Apply 'lightfog' flag", command.Title);
        Assert.Equal("This applies 'lightfog' flag to all sidedefs within current selection (UDMF only).", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("select", "Select Group", "Menu")]
    [InlineData("assign", "Assign Group", "Menu")]
    [InlineData("clear", "Clear Group", "Ctrl/Cmd+Shift")]
    public void SelectionGroupCommandsMatchUdbActionSurface(string verb, string titlePrefix, string gesturePrefix)
    {
        string rawVerb = verb.Replace("-", "", StringComparison.Ordinal);
        for (int group = 1; group <= 10; group++)
        {
            var command = EditorCommandCatalog.Find($"window.{verb}-group-{group}");
            var udbAlias = EditorCommandCatalog.Find($"window.{rawVerb}group{group}");

            Assert.NotNull(command);
            Assert.Equal($"{titlePrefix} {group}", command.Title);
            Assert.Equal(EditorCommandScope.Window, command.Scope);
            Assert.True(command.AllowKeys);
            Assert.True(command.AllowMouse);
            Assert.False(command.AllowScroll);
            Assert.StartsWith(gesturePrefix, command.DefaultGesture, StringComparison.Ordinal);
            Assert.NotNull(udbAlias);
            Assert.Equal(command.Title, udbAlias.Title);
            Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
            Assert.Equal(command.CategoryTitle, udbAlias.CategoryTitle);
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
        AssertKeyOnlyMap2DCommand("map2d.togglecomments", "Toggle Comments");
    }

    [Theory]
    [InlineData("map2d.toggle-fixed-things-scale", "Toggle Fixed Things Scale")]
    [InlineData("map2d.togglefixedthingsscale", "Toggle Fixed Things Scale")]
    [InlineData("map2d.toggle-always-show-vertices", "Toggle Always Show Vertices")]
    [InlineData("map2d.togglealwaysshowvertices", "Toggle Always Show Vertices")]
    public void ViewToggleCommandsMatchUdbActionSurface(string commandId, string title)
        => AssertKeyOnlyMap2DCommand(commandId, title);

    [Theory]
    [InlineData("map2d.view-mode-wireframe", "View Wireframe")]
    [InlineData("map2d.viewmodenormal", "View Wireframe")]
    [InlineData("map2d.view-mode-brightness", "View Brightness Levels")]
    [InlineData("map2d.viewmodebrightness", "View Brightness Levels")]
    [InlineData("map2d.view-mode-floors", "View Floor Textures")]
    [InlineData("map2d.viewmodefloors", "View Floor Textures")]
    [InlineData("map2d.view-mode-ceilings", "View Ceiling Textures")]
    [InlineData("map2d.viewmodeceilings", "View Ceiling Textures")]
    [InlineData("map2d.next-view-mode", "Next View Mode")]
    [InlineData("map2d.nextviewmode", "Next View Mode")]
    [InlineData("map2d.previous-view-mode", "Previous View Mode")]
    [InlineData("map2d.previousviewmode", "Previous View Mode")]
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
    [InlineData("map2d.flooralignmode", "Floor Align Mode")]
    [InlineData("map2d.ceilingalignmode", "Ceiling Align Mode")]
    public void FlatAlignModeCommandsMatchUdbActionSurface(string commandId, string title)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("window.togglebrightness", EditorCommandScope.Window)]
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
    [InlineData("map2d.verticesmode", "Vertices Mode", "V")]
    [InlineData("map2d.linedefsmode", "Linedefs Mode", "L")]
    [InlineData("map2d.sectorsmode", "Sectors Mode", "S")]
    [InlineData("map2d.thingsmode", "Things Mode", "T")]
    public void ClassicModeAliasesMatchUdbActionSurface(string id, string title, string gesture)
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
    [InlineData("map2d.toggle-highlight", EditorCommandScope.Map2D)]
    [InlineData("map2d.togglehighlight", EditorCommandScope.Map2D)]
    [InlineData("map3d.toggle-highlight", EditorCommandScope.Map3D)]
    [InlineData("map3d.togglehighlight", EditorCommandScope.Map3D)]
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
        var udbAlias = EditorCommandCatalog.Find("window.opendialogeditor");

        Assert.NotNull(command);
        Assert.Equal("Dialog Editor", command.Title);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("opendialogeditor", UsdfDialogEditorModel.Action.Id);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Scope, udbAlias.Scope);
        Assert.True(udbAlias.AllowScroll);
    }

    [Fact]
    public void UdbScriptActionsMatchUdbActionSurface()
    {
        Assert.Equal("udbscript", UdbScriptActions.CategoryId);
        Assert.Equal("Scripting", UdbScriptActions.CategoryTitle);
        Assert.Equal(30, UdbScriptActions.ScriptSlotCount);
        Assert.Equal(31, UdbScriptActions.All.Count);

        var scripts = UdbScriptActions.Scripts;
        Assert.Equal("udbscripts", scripts.Id);
        Assert.Equal("Scripts", scripts.Title);
        Assert.Equal("Opens the script browser", scripts.Description);
        Assert.Equal("udbscript", scripts.Category);
        Assert.True(scripts.AllowKeys);
        Assert.True(scripts.AllowMouse);
        Assert.True(scripts.AllowScroll);

        var execute = UdbScriptActions.Execute;
        Assert.Equal("udbscriptexecute", execute.Id);
        Assert.Equal("Execute Script", execute.Title);
        Assert.Equal("Executes a script", execute.Description);
        Assert.Equal("udbscript", execute.Category);
        Assert.True(execute.AllowKeys);
        Assert.True(execute.AllowMouse);
        Assert.True(execute.AllowScroll);

        var firstSlot = Assert.Single(UdbScriptActions.Slots, action => action.Id == "udbscriptexecuteslot1");
        Assert.Equal("Execute Script Slot 1", firstSlot.Title);
        Assert.Equal("execute script in slot 1", firstSlot.Description);

        var lastSlot = Assert.Single(UdbScriptActions.Slots, action => action.Id == "udbscriptexecuteslot30");
        Assert.Equal("Execute Script Slot 30", lastSlot.Title);
        Assert.Equal("execute script in slot 30", lastSlot.Description);
    }

    [Theory]
    [InlineData("window.udbscripts", "Scripts")]
    [InlineData("window.udbscriptexecute", "Execute Script")]
    [InlineData("window.udbscriptexecuteslot1", "Execute Script Slot 1")]
    [InlineData("window.udbscriptexecuteslot30", "Execute Script Slot 30")]
    public void UdbScriptCommandsMatchUdbActionSurface(string commandId, string title)
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
    public void OpenScriptEditorCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("window.openscripteditor");

        Assert.NotNull(command);
        Assert.Equal("Script Editor", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.Equal(
            "This opens the script editor that allows you to edit any scripts in your map or any script files.",
            command.Description);
    }

    [Fact]
    public void UdbScriptExecutionPlansMatchUdbActionDispatch()
    {
        var current = new UdbScriptInfo("Current", "Description", 1, "/scripts/current.js", "hash-current", null, Array.Empty<UdbScriptOption>());
        var slotted = new UdbScriptInfo("Slotted", "Description", 1, "/scripts/slotted.js", "hash-slotted", null, Array.Empty<UdbScriptOption>());

        UdbScriptExecutionPlan currentPlan = UdbScriptActions.ExecuteCurrentPlan(current);

        Assert.True(currentPlan.ShouldRun);
        Assert.Equal(current, currentPlan.Script);
        Assert.Equal(0, currentPlan.Slot);
        Assert.False(UdbScriptActions.ExecuteCurrentPlan(null).ShouldRun);

        Assert.Equal(12, UdbScriptActions.SlotFromActionName("udbscript_udbscriptexecuteslot12"));
        Assert.Equal(0, UdbScriptActions.SlotFromActionName("udbscript_udbscriptexecuteslot"));

        UdbScriptExecutionPlan slotPlan = UdbScriptActions.ExecuteSlotPlan(
            "udbscript_udbscriptexecuteslot12",
            new Dictionary<int, UdbScriptInfo?> { [12] = slotted });

        Assert.True(slotPlan.ShouldRun);
        Assert.Equal(slotted, slotPlan.Script);
        Assert.Equal(12, slotPlan.Slot);

        UdbScriptExecutionPlan emptySlot = UdbScriptActions.ExecuteSlotPlan(
            "udbscript_udbscriptexecuteslot12",
            new Dictionary<int, UdbScriptInfo?>());

        Assert.False(emptySlot.ShouldRun);
        Assert.Null(emptySlot.Script);
        Assert.Equal(12, emptySlot.Slot);
    }

    [Fact]
    public void WavefrontExportCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.export-wavefront");

        Assert.NotNull(command);
        Assert.Equal("Export to Wavefront .obj", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Fact]
    public void ImageExportCommandIsWindowMenuAction()
    {
        var command = EditorCommandCatalog.Find("window.export-image");

        Assert.NotNull(command);
        Assert.Equal("Export to image", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
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
    public void AutomapModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.mode-automap");

        Assert.NotNull(command);
        Assert.Equal("Automap Mode", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("automapmode", AutomapModeModel.ModeDescriptor.SwitchAction);
    }

    [Fact]
    public void EditSelectionModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.editselectionmode");

        Assert.NotNull(command);
        Assert.Equal("Edit Selection Mode", command.Title);
        Assert.Equal("Allows rotating, resizing and moving a selection.", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void ImageExampleModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.mode-image-example");

        Assert.NotNull(command);
        Assert.Equal("Image Example", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
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

    [Theory]
    [InlineData("map2d.automapmode", "Automap mode", EditorCommandScope.Map2D, true)]
    [InlineData("map2d.imageexamplemode", "Image Example", EditorCommandScope.Map2D, true)]
    [InlineData("map2d.wadauthormode", "WadAuthor Mode", EditorCommandScope.Map2D, true)]
    [InlineData("map2d.visplaneexplorermode", "Visplane Explorer Mode", EditorCommandScope.Map2D, false)]
    [InlineData("map2d.stairsectorbuildermode", "Stair Sector Builder Mode", EditorCommandScope.Map2D, true)]
    [InlineData("map2d.selectsectorsoutline", "Select Sectors Outline", EditorCommandScope.Map2D, true)]
    [InlineData("window.rangetagselection", "Tag Range", EditorCommandScope.Window, true)]
    [InlineData("window.blockmapexplorermode", "Blockmap Explorer mode", EditorCommandScope.Window, true)]
    [InlineData("window.rejectexplorermode", "Reject Explorer mode", EditorCommandScope.Window, true)]
    [InlineData("window.rejectexplorercolorconfiguration", "Configure colors", EditorCommandScope.Window, true)]
    [InlineData("window.nodesviewermode", "Nodes Viewer Mode", EditorCommandScope.Window, false)]
    [InlineData("window.soundpropagationmode", "Sound propagation mode", EditorCommandScope.Window, true)]
    [InlineData("window.soundenvironmentmode", "Sound environment mode", EditorCommandScope.Window, true)]
    [InlineData("window.soundpropagationcolorconfiguration", "Configure colors", EditorCommandScope.Window, true)]
    public void BundledPluginActionAliasesMatchUdbActionSurface(
        string commandId,
        string title,
        EditorCommandScope scope,
        bool allowScroll)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(scope, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.Equal(allowScroll, command.AllowScroll);
        Assert.False(command.Repeat);
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
        var floorAlias = EditorCommandCatalog.Find("map2d.threedfloorhelpermode");
        var slope = EditorCommandCatalog.Find("map2d.mode-3d-slope");
        var slopeAlias = EditorCommandCatalog.Find("map2d.threedslopemode");
        var drawSlopes = EditorCommandCatalog.Find("map2d.mode-draw-slopes");
        var drawSlopesAlias = EditorCommandCatalog.Find("map2d.drawslopesmode");

        Assert.NotNull(floor);
        Assert.Equal("3D Floor Mode", floor.Title);
        Assert.Equal(EditorCommandScope.Map2D, floor.Scope);
        Assert.True(floor.AllowScroll);
        Assert.Equal("threedfloorhelpermode", ThreeDFloors.ModeDescriptor.SwitchAction);
        Assert.NotNull(floorAlias);
        Assert.Equal("3D floor editing mode", floorAlias.Title);

        Assert.NotNull(slope);
        Assert.Equal("Slope Mode", slope.Title);
        Assert.Equal("threedslopemode", ThreeDFloors.SlopeModeDescriptor.SwitchAction);
        Assert.NotNull(slopeAlias);
        Assert.Equal("Slope mode", slopeAlias.Title);

        Assert.NotNull(drawSlopes);
        Assert.Equal("Draw Slopes Mode", drawSlopes.Title);
        Assert.Equal("drawslopesmode", ThreeDFloors.DrawSlopesModeDescriptor.SwitchAction);
        Assert.NotNull(drawSlopesAlias);
        Assert.Equal("Draw slope mode", drawSlopesAlias.Title);
    }

    [Fact]
    public void ThreeDFloorActionCommandsMatchUdbActionsConfig()
    {
        var expected = new Dictionary<string, string>
        {
            ["map2d.3dfloor.draw-slope-point"] = "drawslopepoint",
            ["map2d.drawslopepoint"] = "drawslopepoint",
            ["map2d.3dfloor.draw-floor-slope"] = "drawfloorslope",
            ["map2d.drawfloorslope"] = "drawfloorslope",
            ["map2d.3dfloor.draw-ceiling-slope"] = "drawceilingslope",
            ["map2d.drawceilingslope"] = "drawceilingslope",
            ["map2d.3dfloor.draw-floor-and-ceiling-slope"] = "drawfloorandceilingslope",
            ["map2d.drawfloorandceilingslope"] = "drawfloorandceilingslope",
            ["map2d.3dfloor.finish-slope-draw"] = "finishslopedraw",
            ["map2d.finishslopedraw"] = "finishslopedraw",
            ["map2d.3dfloor.flip-slope"] = "threedflipslope",
            ["map2d.threedflipslope"] = "threedflipslope",
            ["map2d.3dfloor.cycle-highlight-up"] = "cyclehighlighted3dfloorup",
            ["map2d.cyclehighlighted3dfloorup"] = "cyclehighlighted3dfloorup",
            ["map2d.3dfloor.cycle-highlight-down"] = "cyclehighlighted3dfloordown",
            ["map2d.cyclehighlighted3dfloordown"] = "cyclehighlighted3dfloordown",
            ["map2d.3dfloor.relocate-control-sectors"] = "relocate3dfloorcontrolsectors",
            ["map2d.relocate3dfloorcontrolsectors"] = "relocate3dfloorcontrolsectors",
            ["map2d.3dfloor.select-control-sector"] = "select3dfloorcontrolsector",
            ["map2d.select3dfloorcontrolsector"] = "select3dfloorcontrolsector",
            ["map2d.3dfloor.duplicate-geometry"] = "duplicate3dfloorgeometry",
            ["map2d.duplicate3dfloorgeometry"] = "duplicate3dfloorgeometry",
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
            ["map3d.togglevisualslopepicking"] = "Toggle Visual Sidedef Slope Picking",
            ["map3d.toggle-visual-vertex-slope-picking"] = "Toggle Visual Vertex Slope Picking",
            ["map3d.togglevisualvertexslopepicking"] = "Toggle Visual Vertex Slope Picking",
            ["map3d.toggle-visual-vertex-slope-adjacent-selection"] = "Toggle Adjacent Visual Vertex Slope Selection",
            ["map3d.togglevisualvertexslopeadjacentselection"] = "Toggle Adjacent Visual Vertex Slope Selection",
            ["map3d.reset-slope"] = "Reset Plane Slope",
            ["map3d.resetslope"] = "Reset Plane Slope",
            ["map3d.slope-between-handles"] = "Slope Between Handles",
            ["map3d.slopebetweenhandles"] = "Slope Between Handles",
            ["map3d.arch-between-handles"] = "Arch Between Slope Handles",
            ["map3d.archbetweenhandles"] = "Arch Between Slope Handles",
            ["map3d.move-camera-to-cursor"] = "Move Camera to Cursor",
            ["map3d.movecameratocursor"] = "Move Camera to Cursor",
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
            ["map3d.movethingleft"] = ("Move Thing Left", true, true),
            ["map3d.move-thing-right"] = ("Move Thing Right", true, true),
            ["map3d.movethingright"] = ("Move Thing Right", true, true),
            ["map3d.move-thing-forward"] = ("Move Thing Forward", true, true),
            ["map3d.movethingfwd"] = ("Move Thing Forward", true, true),
            ["map3d.move-thing-backward"] = ("Move Thing Backward", true, true),
            ["map3d.movethingback"] = ("Move Thing Backward", true, true),
            ["map3d.insert-item"] = ("Insert Item", true, false),
            ["map3d.insertitem"] = ("Insert Item", true, false),
            ["map3d.copy-selection"] = ("Copy Selection", false, false),
            ["map3d.copyselection"] = ("Copy Selection", false, false),
            ["map3d.cut-selection"] = ("Cut Selection", false, false),
            ["map3d.cutselection"] = ("Cut Selection", false, false),
            ["map3d.paste-selection"] = ("Paste Selection", false, false),
            ["map3d.pasteselection"] = ("Paste Selection", false, false),
            ["map3d.place-thing-at-cursor"] = ("Move Thing to Cursor Location", false, false),
            ["map3d.placethingatcursor"] = ("Move Thing to Cursor Location", false, false),
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

        Assert.Equal("Menu", EditorCommandCatalog.Find("map3d.place-thing-at-cursor")?.DefaultGesture);
        Assert.Equal("Ctrl/Cmd+M", EditorCommandCatalog.Find("map3d.placethingatcursor")?.DefaultGesture);
    }

    [Theory]
    [InlineData("map3d.rotate-clockwise", "map3d.rotate-thing-clockwise", "Rotate Clockwise", "Ctrl/Cmd+Shift+ScrollUp")]
    [InlineData("map3d.rotateclockwise", "map3d.rotate-thing-clockwise", "Rotate Clockwise", "Ctrl/Cmd+Shift+ScrollUp")]
    [InlineData("map3d.rotate-counterclockwise", "map3d.rotate-thing-counterclockwise", "Rotate Counterclockwise", "Ctrl/Cmd+Shift+ScrollDown")]
    [InlineData("map3d.rotatecounterclockwise", "map3d.rotate-thing-counterclockwise", "Rotate Counterclockwise", "Ctrl/Cmd+Shift+ScrollDown")]
    [InlineData("map3d.pitch-clockwise", "map3d.pitch-thing-clockwise", "Change Pitch Clockwise", "Ctrl/Cmd+Alt+ScrollUp")]
    [InlineData("map3d.pitchclockwise", "map3d.pitch-thing-clockwise", "Change Pitch Clockwise", "Ctrl/Cmd+Alt+ScrollUp")]
    [InlineData("map3d.pitch-counterclockwise", "map3d.pitch-thing-counterclockwise", "Change Pitch Counterclockwise", "Ctrl/Cmd+Alt+ScrollDown")]
    [InlineData("map3d.pitchcounterclockwise", "map3d.pitch-thing-counterclockwise", "Change Pitch Counterclockwise", "Ctrl/Cmd+Alt+ScrollDown")]
    [InlineData("map3d.roll-clockwise", "map3d.roll-thing-clockwise", "Change Roll Clockwise", "Alt+ScrollUp")]
    [InlineData("map3d.rollclockwise", "map3d.roll-thing-clockwise", "Change Roll Clockwise", "Alt+ScrollUp")]
    [InlineData("map3d.roll-counterclockwise", "map3d.roll-thing-counterclockwise", "Change Roll Counterclockwise", "Alt+ScrollDown")]
    [InlineData("map3d.rollcounterclockwise", "map3d.roll-thing-counterclockwise", "Change Roll Counterclockwise", "Alt+ScrollDown")]
    public void VisualRotationAliasesMatchUdbActionSurface(string id, string legacyId, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);
        var legacyAlias = EditorCommandCatalog.Find(legacyId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(EditorCommandScope.Map3D, legacyAlias.Scope);
    }

    [Fact]
    public void VisualCameraThingCommandsMatchUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.apply-camera-rotation");
        var commandAlias = EditorCommandCatalog.Find("map3d.apply-camera-rotation-to-things");
        var udbCommandAlias = EditorCommandCatalog.Find("map3d.applycamerarotationtothings");

        Assert.NotNull(command);
        Assert.Equal("Apply Camera Rotation To Things", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(commandAlias);
        Assert.Equal(command.Title, commandAlias.Title);
        Assert.NotNull(udbCommandAlias);
        Assert.Equal(command.Title, udbCommandAlias.Title);

        var lookThrough = EditorCommandCatalog.Find("map3d.look-through-selection");
        var lookThroughAlias = EditorCommandCatalog.Find("map3d.look-through-thing");
        var udbLookThroughAlias = EditorCommandCatalog.Find("map3d.lookthroughthing");

        Assert.NotNull(lookThrough);
        Assert.Equal("Look Through Selection", lookThrough.Title);
        Assert.Equal("Y", lookThrough.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, lookThrough.Scope);
        Assert.True(lookThrough.AllowKeys);
        Assert.False(lookThrough.AllowMouse);
        Assert.True(lookThrough.AllowScroll);
        Assert.False(lookThrough.Repeat);

        Assert.NotNull(lookThroughAlias);
        Assert.Equal(lookThrough.Title, lookThroughAlias.Title);
        Assert.Equal(lookThrough.DefaultGesture, lookThroughAlias.DefaultGesture);
        Assert.NotNull(udbLookThroughAlias);
        Assert.Equal(lookThrough.Title, udbLookThroughAlias.Title);
        Assert.Equal(lookThrough.DefaultGesture, udbLookThroughAlias.DefaultGesture);

        var align = EditorCommandCatalog.Find("map3d.thing-align-to-wall");
        var alignAlias = EditorCommandCatalog.Find("map3d.align-things-to-wall");
        var udbAlignAlias = EditorCommandCatalog.Find("map3d.thingaligntowall");

        Assert.NotNull(align);
        Assert.Equal("Align Things to Nearest Linedef", align.Title);
        Assert.Equal("Ctrl/Cmd+Shift+A", align.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, align.Scope);
        Assert.True(align.AllowKeys);
        Assert.True(align.AllowMouse);
        Assert.True(align.AllowScroll);
        Assert.False(align.Repeat);

        Assert.NotNull(alignAlias);
        Assert.Equal(align.Title, alignAlias.Title);
        Assert.Equal(align.DefaultGesture, alignAlias.DefaultGesture);
        Assert.NotNull(udbAlignAlias);
        Assert.Equal(align.Title, udbAlignAlias.Title);
        Assert.Equal(align.DefaultGesture, udbAlignAlias.DefaultGesture);

        var showThings = EditorCommandCatalog.Find("map3d.show-visual-things");
        var udbShowThings = EditorCommandCatalog.Find("map3d.showvisualthings");

        Assert.NotNull(showThings);
        Assert.Equal("Show Things", showThings.Title);
        Assert.Equal(EditorCommandScope.Map3D, showThings.Scope);
        Assert.True(showThings.AllowKeys);
        Assert.True(showThings.AllowMouse);
        Assert.True(showThings.AllowScroll);
        Assert.False(showThings.Repeat);
        Assert.NotNull(udbShowThings);
        Assert.Equal(showThings.Title, udbShowThings.Title);
        Assert.Equal(showThings.DefaultGesture, udbShowThings.DefaultGesture);
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
        Assert.Equal("window.classiccopyproperties", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "C", accelerator: true, shift: true));
        Assert.Equal("window.classicpasteproperties", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "V", accelerator: true, alt: true));
        Assert.Equal("window.classicpastepropertieswithoptions", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "V", accelerator: true, shift: true));
    }

    [Fact]
    public void DefaultShortcutsRespectScopeAndModifiers()
    {
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "S", accelerator: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "S"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, "S", accelerator: true, shift: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add", shift: true));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.LeftButton, accelerator: true, shift: true, alt: true));
        Assert.Equal("map3d.visualselect", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.LeftButton, accelerator: true, shift: true, alt: true));
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
    public void ShortcutResolutionNormalizesSpecialKeyAliases()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "Space"),
            new EditorShortcutBinding("map3d.select-texture", EditorCommandScope.Map3D, "OemBackslash"),
        });

        Assert.Equal("map2d.fit", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, "Spacebar"));
        Assert.Equal("map2d.fit", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, "SpaceKey"));
        Assert.Equal("map3d.select-texture", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map3D, "OemPipe"));
    }

    [Fact]
    public void DefaultShortcutsResolveMap2DCommands()
    {
        Assert.Equal("map2d.toggle-sector-fills", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "S"));
        Assert.Equal("map2d.toggle-full-brightness", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "B"));
        Assert.Equal("map2d.togglehighlight", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "H"));
        Assert.Equal("map2d.draw-sector", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D"));
        Assert.Equal("map2d.draw-lines", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", shift: true));
        Assert.Equal("map2d.draw-rectangle", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", accelerator: true, shift: true));
        Assert.Equal("map2d.draw-ellipse", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", shift: true, alt: true));
        Assert.Equal("map2d.draw-curve", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "D", accelerator: true, alt: true));
        Assert.Equal("map2d.curvelinesmode", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "C", shift: true));
        Assert.Equal("map2d.bridge-mode", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "B", accelerator: true));
        Assert.Equal("map2d.increasesubdivlevel", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, accelerator: true));
        Assert.Equal("map2d.decreasesubdivlevel", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, accelerator: true));
        Assert.Equal("map2d.increasebevel", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, accelerator: true, shift: true));
        Assert.Equal("map2d.decreasebevel", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, accelerator: true, shift: true));
        Assert.Equal("map2d.removefirstpoint", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Back", accelerator: true));
        Assert.Equal("map2d.mode-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "NumPad1"));
        Assert.Equal("map2d.mode-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Num1"));
        Assert.Equal("map2d.select", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.LeftButton));
        Assert.Equal("map2d.split-line", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.RightButton));
        Assert.Equal("map2d.fliplinedefs", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "F"));
        Assert.Equal("map2d.flipsidedefs", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "F", shift: true));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Add"));
        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollUp));
        Assert.Equal("map2d.zoom-out", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, EditorPointerInput.ScrollDown));
        Assert.Equal("map2d.thinglookatcursor", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "L", shift: true));
        Assert.Equal("map2d.thinglookatcursor", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "L", accelerator: true, shift: true));
        Assert.Equal("map2d.togglesnap", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "G"));
        Assert.Equal("map2d.toggle-3d", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Tab"));
    }

    [Fact]
    public void UdbFlipAliasesOwnDefaultShortcutResolution()
    {
        Assert.Equal("F", EditorCommandCatalog.GestureText("map2d.fliplinedefs", EditorCommandCatalog.DefaultShortcuts));
        Assert.Equal("Shift+F", EditorCommandCatalog.GestureText("map2d.flipsidedefs", EditorCommandCatalog.DefaultShortcuts));
        Assert.Equal("map2d.fliplinedefs", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "F"));
        Assert.Equal("map2d.flipsidedefs", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "F", shift: true));
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

    [Fact]
    public void DissolveItemCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.dissolveitem");

        Assert.NotNull(command);
        Assert.Equal("Dissolve Item", command.Title);
        Assert.Equal("Ctrl/Cmd+Delete", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal("Deletes the highlighted or selected items in classic modes, trying to preserve the rest of the map geometry intact.", command.Description);
        Assert.Equal("map2d.dissolveitem", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Delete", accelerator: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Back"));
    }

    [Fact]
    public void MakeSectorModeAliasMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.makesectormode");

        Assert.NotNull(command);
        Assert.Equal("Make Sector Mode", command.Title);
        Assert.Equal("Switches to the Make Sector editing mode. This mode allows creating and/or fixing split sectors by clicking within a closed region.", command.Description);
        Assert.Equal("M", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.draw-rectangle", "Start Rectangle Drawing", "Ctrl/Cmd+Shift+D", "Starts drawing rectangle. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    [InlineData("map2d.drawlinesmode", "Start Drawing", "Ctrl/Cmd+D", "Starts drawing lines. See the Drawing category for actions available during drawing mode.")]
    [InlineData("map2d.drawrectanglemode", "Start Rectangle Drawing", "Ctrl/Cmd+Shift+D", "Starts drawing rectangle. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    [InlineData("map2d.draw-ellipse", "Start Ellipse Drawing", "Alt+Shift+D", "Starts drawing ellipse. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    [InlineData("map2d.drawellipsemode", "Start Ellipse Drawing", "Alt+Shift+D", "Starts drawing ellipse. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    [InlineData("map2d.draw-curve", "Start Curve Drawing", "Ctrl/Cmd+Alt+D", "Starts drawing a curve. Increase/Decrease Subdivision Level actions are available in this mode.")]
    [InlineData("map2d.drawcurvemode", "Start Curve Drawing", "Ctrl/Cmd+Alt+D", "Starts drawing a curve. Increase/Decrease Subdivision Level actions are available in this mode.")]
    [InlineData("map2d.curvelinesmode", "Curve Linedefs", "Shift+C", "Curves the selected linedefs with a given number of vertices and distance from the line.")]
    [InlineData("map2d.draw-grid", "Start Grid Drawing", "Menu", "Starts drawing a grid. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    [InlineData("map2d.drawgridmode", "Start Grid Drawing", "Menu", "Starts drawing a grid. Increase/Decrease Subdivision Level and Increase/Decrease Corners Bevel actions are available in this mode.")]
    public void ShapeDrawCommandsMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void BridgeModeCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.bridge-mode");
        var alias = EditorCommandCatalog.Find("map2d.bridgemode");

        Assert.NotNull(command);
        Assert.Equal("Bridge Mode", command.Title);
        Assert.Equal("Ctrl/Cmd+B", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(alias);
        Assert.Equal(command.Title, alias.Title);
        Assert.Equal(command.DefaultGesture, alias.DefaultGesture);
        Assert.Equal(command.Scope, alias.Scope);
        Assert.True(alias.AllowKeys);
        Assert.False(alias.AllowMouse);
        Assert.False(alias.AllowScroll);
    }

    [Fact]
    public void VisualModeAliasMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.gzdbvisualmode");

        Assert.NotNull(command);
        Assert.Equal("Visual Mode", command.Title);
        Assert.Equal("Q", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.True(command.DisregardAccelerator);
        Assert.Equal("map2d.gzdbvisualmode", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "Q"));
    }

    [Theory]
    [InlineData("window.findmode", "Find and Replace Mode", "F3", "Finds vertices, linedefs, sectors or things with a specific property, selects them and optionally replaces them with a given setting.")]
    [InlineData("window.errorcheckmode", "Map Analysis Mode", "F4", "")]
    public void WindowModeAliasesMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Window, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal(id, EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Window, gesture));
    }

    [Theory]
    [InlineData("map2d.increase-subdivision-level", "Increase Subdivision Level", "Ctrl/Cmd+ScrollUp", "Increases subdivision level in Rectangle and Ellipse Drawing Modes.")]
    [InlineData("map2d.increasesubdivlevel", "Increase Subdivision Level", "Ctrl/Cmd+ScrollUp", "Increases subdivision level in Rectangle and Ellipse Drawing Modes.")]
    [InlineData("map2d.decrease-subdivision-level", "Decrease Subdivision Level", "Ctrl/Cmd+ScrollDown", "Decreases subdivision level in Rectangle and Ellipse Drawing Modes.")]
    [InlineData("map2d.decreasesubdivlevel", "Decrease Subdivision Level", "Ctrl/Cmd+ScrollDown", "Decreases subdivision level in Rectangle and Ellipse Drawing Modes.")]
    [InlineData("map2d.increase-bevel", "Increase Corners Bevel", "Ctrl/Cmd+Shift+ScrollUp", "Increase corners bevel in Rectangle Drawing Modes. Bevel can be negative.")]
    [InlineData("map2d.increasebevel", "Increase Corners Bevel", "Ctrl/Cmd+Shift+ScrollUp", "Increase corners bevel in Rectangle Drawing Modes. Bevel can be negative.")]
    [InlineData("map2d.decrease-bevel", "Decrease Corners Bevel", "Ctrl/Cmd+Shift+ScrollDown", "Decreases corners bevel in Rectangle Drawing Modes. Bevel can be negative.")]
    [InlineData("map2d.decreasebevel", "Decrease Corners Bevel", "Ctrl/Cmd+Shift+ScrollDown", "Decreases corners bevel in Rectangle Drawing Modes. Bevel can be negative.")]
    public void DrawAdjustmentCommandsMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.draw-point", "Draw Vertex", "Menu", true, true, true)]
    [InlineData("map2d.drawpoint", "Draw Vertex", "Menu", true, true, true)]
    [InlineData("map2d.remove-draw-point", "Remove Last Vertex", "Menu", false, false, false)]
    [InlineData("map2d.removepoint", "Remove Last Vertex", "Menu", false, false, false)]
    [InlineData("map2d.remove-first-draw-point", "Remove First Vertex", "Ctrl/Cmd+Backspace", false, false, false)]
    [InlineData("map2d.removefirstpoint", "Remove First Vertex", "Ctrl/Cmd+Backspace", false, false, false)]
    [InlineData("map2d.finish-draw", "Finish Drawing", "Enter", false, false, false)]
    [InlineData("map2d.finishdraw", "Finish Drawing", "Enter", false, false, false)]
    [InlineData("map2d.acceptmode", "Accept Action", "Enter", false, false, false)]
    [InlineData("map2d.cancelmode", "Cancel Action", "Esc", false, false, false)]
    public void DrawSessionCommandsMatchUdbActionSurface(
        string id,
        string title,
        string gesture,
        bool disregardShift,
        bool disregardAccelerator,
        bool disregardAlt)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.Equal(disregardShift, command.DisregardShift);
        Assert.Equal(disregardAccelerator, command.DisregardAccelerator);
        Assert.Equal(disregardAlt, command.DisregardAlt);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.flip", "Flip Linedefs", "F", "This flips the selected linedefs around and keeps sidedefs on the correct side.")]
    [InlineData("map2d.fliplinedefs", "Flip Linedefs", "F", "This flips the selected linedefs around and keeps sidedefs on the correct side.")]
    [InlineData("map2d.flip-sidedefs", "Flip Sidedefs", "Shift+F", "This flips the sidedefs on the selected linedefs around, keeping the line in the same direction.")]
    [InlineData("map2d.flipsidedefs", "Flip Sidedefs", "Shift+F", "This flips the sidedefs on the selected linedefs around, keeping the line in the same direction.")]
    public void FlipLinedefCommandsMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.align-textures-x", "Align textures X", "A")]
    [InlineData("map2d.align-textures-y", "Align textures Y", "Shift+A")]
    [InlineData("map2d.fit-selected-textures", "Fit Selected Textures", "Menu")]
    public void Map2DTextureCommandsMatchUdbActionSurface(string id, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
    }

    [Theory]
    [InlineData("map2d.lower-floor-8", "Lower Floor by 8 mp", "Menu", "Lowers the highlighted or selected floor heights by 8 mp.")]
    [InlineData("map2d.lowerfloor8", "Lower Floor by 8 mp", "Ctrl/Cmd+Alt+ScrollDown", "Lowers the highlighted or selected floor heights by 8 mp.")]
    [InlineData("map2d.raise-floor-8", "Raise Floor by 8 mp", "Menu", "Raises the highlighted or selected floor heights by 8 mp.")]
    [InlineData("map2d.raisefloor8", "Raise Floor by 8 mp", "Ctrl/Cmd+Alt+ScrollUp", "Raises the highlighted or selected floor heights by 8 mp.")]
    [InlineData("map2d.lower-ceiling-8", "Lower Ceiling by 8 mp", "Menu", "Lowers the highlighted or selected ceiling heights by 8 mp.")]
    [InlineData("map2d.lowerceiling8", "Lower Ceiling by 8 mp", "Alt+Shift+ScrollDown", "Lowers the highlighted or selected ceiling heights by 8 mp.")]
    [InlineData("map2d.raise-ceiling-8", "Raise Ceiling by 8 mp", "Menu", "Raises the highlighted or selected ceiling heights by 8 mp.")]
    [InlineData("map2d.raiseceiling8", "Raise Ceiling by 8 mp", "Alt+Shift+ScrollUp", "Raises the highlighted or selected ceiling heights by 8 mp.")]
    [InlineData("map2d.raise-brightness-8", "Increase Brightness by 8", "Menu", "Increases the targeted or selected sector brightness level by 8.")]
    [InlineData("map2d.raisebrightness8", "Increase Brightness by 8", "Menu", "Increases the targeted or selected sector brightness level by 8.")]
    [InlineData("map2d.lower-brightness-8", "Decrease Brightness by 8", "Menu", "Decreases the targeted or selected sector brightness level by 8.")]
    [InlineData("map2d.lowerbrightness8", "Decrease Brightness by 8", "Menu", "Decreases the targeted or selected sector brightness level by 8.")]
    public void SectorHeightCommandsMatchUdbActionSurface(string id, string title, string defaultGesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(defaultGesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Fact]
    public void UdbSectorHeightCommandsOwnDefaultShortcutResolution()
    {
        Assert.Equal("map2d.raisefloor8", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, accelerator: true, alt: true));
        Assert.Equal("map2d.lowerfloor8", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, accelerator: true, alt: true));
        Assert.Equal("map2d.raiseceiling8", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map2D, EditorPointerInput.ScrollUp, shift: true, alt: true));
        Assert.Equal("map2d.lowerceiling8", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map2D, EditorPointerInput.ScrollDown, shift: true, alt: true));
    }

    [Fact]
    public void DefaultShortcutsResolveMap3DToggle()
    {
        Assert.Equal("map3d.toggle-2d", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Tab"));
    }

    [Fact]
    public void DefaultShortcutsResolveDiscreteMap3DCommands()
    {
        Assert.Equal("map3d.togglegravity", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "G"));
        Assert.Equal("map3d.insertitem", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "I"));
        Assert.Equal("map3d.insertitem", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Insert"));
        Assert.Equal("map3d.copy-selection", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C", accelerator: true));
        Assert.Equal("map3d.cut-selection", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "X", accelerator: true));
        Assert.Equal("map3d.paste-selection", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true));
        Assert.Equal("map3d.placethingatcursor", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "M", accelerator: true));
        Assert.Equal("map3d.toggle-full-brightness", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "B"));
        Assert.Equal("map3d.togglehighlight", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "H"));
        Assert.Equal("map3d.lowerbrightness8", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "["));
        Assert.Equal("map3d.raisebrightness8", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "]"));
        Assert.Equal("map3d.texturecopy", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C"));
        Assert.Equal("map3d.texturepaste", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V"));
        Assert.Equal("map3d.paste-properties", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true, alt: true));
        Assert.Equal("map3d.paste-properties-options", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", accelerator: true, shift: true));
        Assert.Equal("map3d.lookthroughthing", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Y"));
        Assert.Equal("map3d.thingaligntowall", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true, shift: true));
        Assert.Equal("map3d.select-texture", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "T"));
        Assert.Equal("map3d.scaleup", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad9"));
        Assert.Equal("map3d.scaledown", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad7"));
        Assert.Equal("map3d.scaleupx", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad6"));
        Assert.Equal("map3d.scaledownx", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad4"));
        Assert.Equal("map3d.scaleupy", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad8"));
        Assert.Equal("map3d.scaledowny", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "NumPad5"));
        Assert.Equal("map3d.rotateclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, accelerator: true, shift: true));
        Assert.Equal("map3d.rotatecounterclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, accelerator: true, shift: true));
        Assert.Equal("map3d.pitchclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, accelerator: true, alt: true));
        Assert.Equal("map3d.pitchcounterclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, accelerator: true, alt: true));
        Assert.Equal("map3d.rollclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollUp, alt: true));
        Assert.Equal("map3d.rollcounterclockwise", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.ScrollDown, alt: true));
        Assert.Equal("map3d.visualautoalign", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true));
        Assert.Equal("map3d.align-texture-y", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", shift: true));
        Assert.Equal("map3d.resettexture", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "O"));
        Assert.Equal("map3d.resettextureudmf", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "R", accelerator: true, shift: true));
        Assert.Equal("map3d.delete-target", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Back"));
        Assert.Equal("map3d.toggle-slope", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "S", alt: true));
        Assert.Equal("map3d.visualselect", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, EditorPointerInput.LeftButton));
        Assert.Equal("map3d.visualedit", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Enter"));
        Assert.Equal("map3d.clearselection", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Escape"));
        Assert.Equal("map3d.nudge-offset-left", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left", shift: true));
        Assert.Equal("map3d.raisesectortonearest", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "PageUp"));
        Assert.Equal("map3d.raisesectortonearest", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "PageUp", accelerator: true));
        Assert.Equal("map3d.lowersectortonearest", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "PageDown"));
        Assert.Equal("map3d.lowersectortonearest", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "PageDown", accelerator: true));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "Left"));
        Assert.Null(EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "C", accelerator: true, shift: true));
    }

    [Theory]
    [InlineData("map3d.lower-sector-1", "Lower Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.lowersector1", "Lower Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.raise-sector-1", "Raise Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.raisesector1", "Raise Floor/Ceiling/Thing by 1 mp")]
    [InlineData("map3d.lower-sector-8", "Lower Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.lowersector8", "Lower Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.raise-sector-8", "Raise Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.raisesector8", "Raise Floor/Ceiling/Thing by 8 mp")]
    [InlineData("map3d.lower-sector-128", "Lower Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.lowersector128", "Lower Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.raise-sector-128", "Raise Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.raisesector128", "Raise Floor/Ceiling/Thing by 128 mp")]
    [InlineData("map3d.lower-map-element-by-grid-size", "Lower Floor/Ceiling/Thing by grid size")]
    [InlineData("map3d.lowermapelementbygridsize", "Lower Floor/Ceiling/Thing by grid size")]
    [InlineData("map3d.raise-map-element-by-grid-size", "Raise Floor/Ceiling/Thing by grid size")]
    [InlineData("map3d.raisemapelementbygridsize", "Raise Floor/Ceiling/Thing by grid size")]
    [InlineData("map3d.lower-sector-to-nearest", "Lower Floor/Ceiling/Thing to adjacent Sector/Thing")]
    [InlineData("map3d.lowersectortonearest", "Lower Floor/Ceiling/Thing to adjacent Sector/Thing")]
    [InlineData("map3d.raise-sector-to-nearest", "Raise Floor/Ceiling/Thing to adjacent Sector/Thing")]
    [InlineData("map3d.raisesectortonearest", "Raise Floor/Ceiling/Thing to adjacent Sector/Thing")]
    public void VisualHeightStepCommandsMatchUdbActionSurface(string id, string title)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(id switch
        {
            "map3d.lower-sector-to-nearest" => "PageDown",
            "map3d.lowersectortonearest" => "PageDown",
            "map3d.raise-sector-to-nearest" => "PageUp",
            "map3d.raisesectortonearest" => "PageUp",
            _ => "Menu",
        }, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        if (id.EndsWith("-to-nearest", StringComparison.Ordinal) || id.EndsWith("tonearest", StringComparison.Ordinal))
            Assert.False(command.Repeat);
        else
            Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.raise-brightness-8", "Increase Brightness by 8")]
    [InlineData("map3d.raisebrightness8", "Increase Brightness by 8")]
    [InlineData("map3d.lower-brightness-8", "Decrease Brightness by 8")]
    [InlineData("map3d.lowerbrightness8", "Decrease Brightness by 8")]
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

    [Fact]
    public void VisualMatchBrightnessCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.match-brightness");
        var udbAlias = EditorCommandCatalog.Find("map3d.matchbrightness");

        Assert.NotNull(command);
        Assert.Equal("Match Brightness", command.Title);
        Assert.Equal("Makes the brightness of selected surfaces the same as the brightness of highlighted surface (UDMF only).", command.Description);
        Assert.Equal("Ctrl/Cmd+M", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.False(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualToggleGravityCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-gravity");
        var legacyAlias = EditorCommandCatalog.Find("map3d.walk-mode");
        var udbAlias = EditorCommandCatalog.Find("map3d.togglegravity");

        Assert.NotNull(command);
        Assert.Equal("Toggle Gravity", command.Title);
        Assert.Equal("G", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(EditorCommandScope.Map3D, legacyAlias.Scope);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("map3d.visual-select", "map3d.select-target", "map3d.visualselect", "Select", "Click", false)]
    [InlineData("map3d.visual-edit", "map3d.edit-properties", "map3d.visualedit", "Edit", "Enter", false)]
    [InlineData("map3d.clear-selection", "map3d.clear-target", "map3d.clearselection", "Clear Selection", "Esc", true)]
    public void VisualBaseCommandAliasesMatchUdbActionSurface(string id, string legacyId, string udbId, string title, string gesture, bool allowScroll)
    {
        var command = EditorCommandCatalog.Find(id);
        var legacyAlias = EditorCommandCatalog.Find(legacyId);
        var udbAlias = EditorCommandCatalog.Find(udbId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.Equal(allowScroll, command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(EditorCommandScope.Map3D, legacyAlias.Scope);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("map3d.move-forward", "Move Forward", "Menu")]
    [InlineData("map3d.moveforward", "Move Forward", "Menu")]
    [InlineData("map3d.move-backward", "Move Backward", "Menu")]
    [InlineData("map3d.movebackward", "Move Backward", "Menu")]
    [InlineData("map3d.move-left", "Move Left (strafe)", "Menu")]
    [InlineData("map3d.moveleft", "Move Left (strafe)", "Menu")]
    [InlineData("map3d.move-right", "Move Right (strafe)", "Menu")]
    [InlineData("map3d.moveright", "Move Right (strafe)", "Menu")]
    [InlineData("map3d.move-up", "Move Up", "Menu")]
    [InlineData("map3d.moveup", "Move Up", "Menu")]
    [InlineData("map3d.move-down", "Move Down", "Menu")]
    [InlineData("map3d.movedown", "Move Down", "Menu")]
    public void VisualCameraMovementCommandsMatchUdbActionSurface(string commandId, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualOrbitCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.orbit");

        Assert.NotNull(command);
        Assert.Equal("Orbit", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualPaintSelectCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.visual-paint-select");
        var udbAlias = EditorCommandCatalog.Find("map3d.visualpaintselect");

        Assert.NotNull(command);
        Assert.Equal("Paint Selection", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.True(command.DisregardAccelerator);
        Assert.True(command.DisregardAlt);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void ClassicPaintSelectCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map2d.classicpaintselect");

        Assert.NotNull(command);
        Assert.Equal("Paint Selection", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.True(command.DisregardAccelerator);
        Assert.True(command.DisregardAlt);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map2d.classicselect", "Select", "Click")]
    [InlineData("map2d.classicedit", "Edit", "Menu")]
    public void ClassicSelectAndEditCommandsMatchUdbActionSurface(string commandId, string title, string gesture)
    {
        var command = EditorCommandCatalog.Find(commandId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.True(command.DisregardShift);
        Assert.True(command.DisregardAccelerator);
        Assert.True(command.DisregardAlt);
        Assert.False(command.Repeat);
    }

    [Fact]
    public void VisualDeleteCommandMatchesUdbDeleteItemAction()
    {
        var command = EditorCommandCatalog.Find("map3d.delete-target");
        var udbAlias = EditorCommandCatalog.Find("map3d.deleteitem");

        Assert.NotNull(command);
        Assert.Equal("Delete Item", command.Title);
        Assert.Equal("Delete", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("map3d.scale-up", "Increase Scale", "NumPad9")]
    [InlineData("map3d.scaleup", "Increase Scale", "NumPad9")]
    [InlineData("map3d.scale-down", "Decrease Scale", "NumPad7")]
    [InlineData("map3d.scaledown", "Decrease Scale", "NumPad7")]
    [InlineData("map3d.scale-up-x", "Increase Horizontal Scale", "NumPad6")]
    [InlineData("map3d.scaleupx", "Increase Horizontal Scale", "NumPad6")]
    [InlineData("map3d.scale-down-x", "Decrease Horizontal Scale", "NumPad4")]
    [InlineData("map3d.scaledownx", "Decrease Horizontal Scale", "NumPad4")]
    [InlineData("map3d.scale-up-y", "Increase Vertical Scale", "NumPad8")]
    [InlineData("map3d.scaleupy", "Increase Vertical Scale", "NumPad8")]
    [InlineData("map3d.scale-down-y", "Decrease Vertical Scale", "NumPad5")]
    [InlineData("map3d.scaledowny", "Decrease Vertical Scale", "NumPad5")]
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
    [InlineData("map3d.move-texture-left-1", "Move Texture Left by 1", "Moves the offset of the targeted or selected textures to the left by 1 pixel.")]
    [InlineData("map3d.movetextureleft", "Move Texture Left by 1", "Moves the offset of the targeted or selected textures to the left by 1 pixel.")]
    [InlineData("map3d.move-texture-right-1", "Move Texture Right by 1", "Moves the offset of the targeted or selected textures to the right by 1 pixel.")]
    [InlineData("map3d.movetextureright", "Move Texture Right by 1", "Moves the offset of the targeted or selected textures to the right by 1 pixel.")]
    [InlineData("map3d.move-texture-up-1", "Move Texture Up by 1", "Moves the offset of the targeted or selected textures up by 1 pixel.")]
    [InlineData("map3d.movetextureup", "Move Texture Up by 1", "Moves the offset of the targeted or selected textures up by 1 pixel.")]
    [InlineData("map3d.move-texture-down-1", "Move Texture Down by 1", "Moves the offset of the targeted or selected textures down by 1 pixel.")]
    [InlineData("map3d.movetexturedown", "Move Texture Down by 1", "Moves the offset of the targeted or selected textures down by 1 pixel.")]
    [InlineData("map3d.move-texture-left-8", "Move Texture Left by 8", "Moves the offset of the targeted or selected textures to the left by 8 pixels.")]
    [InlineData("map3d.movetextureleft8", "Move Texture Left by 8", "Moves the offset of the targeted or selected textures to the left by 8 pixels.")]
    [InlineData("map3d.move-texture-right-8", "Move Texture Right by 8", "Moves the offset of the targeted or selected textures to the right by 8 pixels.")]
    [InlineData("map3d.movetextureright8", "Move Texture Right by 8", "Moves the offset of the targeted or selected textures to the right by 8 pixels.")]
    [InlineData("map3d.move-texture-up-8", "Move Texture Up by 8", "Moves the offset of the targeted or selected textures up by 8 pixels.")]
    [InlineData("map3d.movetextureup8", "Move Texture Up by 8", "Moves the offset of the targeted or selected textures up by 8 pixels.")]
    [InlineData("map3d.move-texture-down-8", "Move Texture Down by 8", "Moves the offset of the targeted or selected textures down by 8 pixels.")]
    [InlineData("map3d.movetexturedown8", "Move Texture Down by 8", "Moves the offset of the targeted or selected textures down by 8 pixels.")]
    [InlineData("map3d.move-texture-left-grid", "Move Texture Left by Grid Size", "Moves the offset of the targeted or selected textures to the left by current grid size.")]
    [InlineData("map3d.movetextureleftgs", "Move Texture Left by Grid Size", "Moves the offset of the targeted or selected textures to the left by current grid size.")]
    [InlineData("map3d.move-texture-right-grid", "Move Texture Right by Grid Size", "Moves the offset of the targeted or selected textures to the right by current grid size.")]
    [InlineData("map3d.movetexturerightgs", "Move Texture Right by Grid Size", "Moves the offset of the targeted or selected textures to the right by current grid size.")]
    [InlineData("map3d.move-texture-up-grid", "Move Texture Up by Grid Size", "Moves the offset of the targeted or selected textures up by current grid size.")]
    [InlineData("map3d.movetextureupgs", "Move Texture Up by Grid Size", "Moves the offset of the targeted or selected textures up by current grid size.")]
    [InlineData("map3d.move-texture-down-grid", "Move Texture Down by Grid Size", "Moves the offset of the targeted or selected textures down by current grid size.")]
    [InlineData("map3d.movetexturedowngs", "Move Texture Down by Grid Size", "Moves the offset of the targeted or selected textures down by current grid size.")]
    public void VisualTextureOffsetStepCommandsMatchUdbActionSurface(string id, string title, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.True(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.reset-offsets", "Reset Texture Offsets", "O", "Resets the texture offsets of targeted or selected sidedefs (all map formats) and floors/ceilings (UDMF only). Also resets scale of targeted or selected things (UDMF only)")]
    [InlineData("map3d.resettexture", "Reset Texture Offsets", "O", "Resets the texture offsets of targeted or selected sidedefs (all map formats) and floors/ceilings (UDMF only). Also resets scale of targeted or selected things (UDMF only)")]
    [InlineData("map3d.reset-local-offsets", "Reset Local Texture Offsets (UDMF)", "Ctrl/Cmd+Shift+R", "Resets upper/middle/lower texture offsets, scale and brightness of targeted or selected sidedefs. Resets texture offsets, rotation, scale and brightness of targeted or selected floors/ceilings. Resets scale, pitch and roll of targeted or selected things.")]
    [InlineData("map3d.resettextureudmf", "Reset Local Texture Offsets (UDMF)", "Ctrl/Cmd+Shift+R", "Resets upper/middle/lower texture offsets, scale and brightness of targeted or selected sidedefs. Resets texture offsets, rotation, scale and brightness of targeted or selected floors/ceilings. Resets scale, pitch and roll of targeted or selected things.")]
    public void VisualTextureResetCommandsMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.texture-copy-offsets", "map3d.copy-offsets", "map3d.texturecopyoffsets", "Copy Offsets", "Copies the targeted texture offsets for pasting.")]
    [InlineData("map3d.texture-paste-offsets", "map3d.paste-offsets", "map3d.texturepasteoffsets", "Paste Offsets", "Pastes the copied texture offsets onto the targeted or selected walls.")]
    public void VisualTextureOffsetClipboardAliasesMatchUdbActionSurface(string id, string legacyId, string udbId, string title, string description)
    {
        var command = EditorCommandCatalog.Find(id);
        var legacyAlias = EditorCommandCatalog.Find(legacyId);
        var udbAlias = EditorCommandCatalog.Find(udbId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(command.Title, legacyAlias.Title);
        Assert.Equal(command.Description, legacyAlias.Description);
        Assert.Equal(command.DefaultGesture, legacyAlias.DefaultGesture);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualTextureSelectCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.select-texture");
        var udbAlias = EditorCommandCatalog.Find("map3d.textureselect");
        var legacyAlias = EditorCommandCatalog.Find("map3d.browse-texture");

        Assert.NotNull(command);
        Assert.Equal("Select Texture", command.Title);
        Assert.Equal("Opens the texture browser to select a texture for the targeted or selected walls, floors or ceilings.", command.Description);
        Assert.Equal("T", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(command.Title, legacyAlias.Title);
        Assert.Equal(command.Description, legacyAlias.Description);
        Assert.Equal(command.AllowScroll, legacyAlias.AllowScroll);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Theory]
    [InlineData("map3d.texture-copy", "map3d.copy-texture", "map3d.texturecopy", "Copy Texture", "C", "Copies the targeted texture or flat for pasting.")]
    [InlineData("map3d.texture-paste", "map3d.apply-texture", "map3d.texturepaste", "Paste Texture", "V", "Pastes the copied texture onto the targeted or selected walls, floors or ceilings.")]
    public void VisualTextureClipboardCommandsMatchUdbActionSurface(string id, string legacyId, string udbId, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);
        var legacyAlias = EditorCommandCatalog.Find(legacyId);
        var udbAlias = EditorCommandCatalog.Find(udbId);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(legacyAlias);
        Assert.Equal(command.Title, legacyAlias.Title);
        Assert.Equal(command.Description, legacyAlias.Description);
        Assert.Equal(command.DefaultGesture, legacyAlias.DefaultGesture);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualTextureFloodFillCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.flood-fill-texture");
        var udbAlias = EditorCommandCatalog.Find("map3d.floodfilltextures");

        Assert.NotNull(command);
        Assert.Equal("Paste Texture Flood-Fill", command.Title);
        Assert.Equal("This allows you to flood-fill all adjacent textures or flats that are identical to the original with the copied texture.", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal("Shift+MButton", udbAlias.DefaultGesture);
        Assert.Equal("map3d.floodfilltextures", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map3D,
            EditorPointerInput.MiddleButton,
            shift: true));
    }

    [Theory]
    [InlineData("map3d.toggle-upper-unpegged", "Toggle Upper Unpegged", "Toggles the Upper Unpegged setting on the selected or targeted linedef.")]
    [InlineData("map3d.toggleupperunpegged", "Toggle Upper Unpegged", "Toggles the Upper Unpegged setting on the selected or targeted linedef.")]
    [InlineData("map3d.toggle-lower-unpegged", "Toggle Lower Unpegged", "Toggles the Lower Unpegged setting on the selected or targeted linedef.")]
    [InlineData("map3d.togglelowerunpegged", "Toggle Lower Unpegged", "Toggles the Lower Unpegged setting on the selected or targeted linedef.")]
    public void VisualUnpeggedCommandsMatchUdbActionSurface(string id, string title, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
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
        var udbAlias = EditorCommandCatalog.Find("map3d.toggleslope");

        Assert.NotNull(command);
        Assert.Equal("Toggle Slope", command.Title);
        Assert.Equal("Alt+S", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualAlphaBasedTextureHighlightingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-alpha-based-texture-highlighting");
        var udbAlias = EditorCommandCatalog.Find("map3d.alphabasedtexturehighlighting");

        Assert.NotNull(command);
        Assert.Equal("Toggle Alpha-based Texture Highlighting", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualModelsRenderingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-models-rendering");
        var udbAlias = EditorCommandCatalog.Find("map3d.gztogglemodels");

        Assert.NotNull(command);
        Assert.Equal("Toggle models rendering", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualModelRenderingLegacyCommandRemainsAvailable()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-model-rendering");

        Assert.NotNull(command);
        Assert.Equal("Toggle Model Rendering Mode", command.Title);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
    }

    [Fact]
    public void VisualDynamicLightsRenderingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-dynamic-lights-rendering");
        var udbAlias = EditorCommandCatalog.Find("map3d.gztogglelights");

        Assert.NotNull(command);
        Assert.Equal("Toggle dynamic lights rendering", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualEnhancedRenderingEffectsCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-enhanced-rendering-effects");
        var udbAlias = EditorCommandCatalog.Find("map3d.gztoggleenhancedrendering");

        Assert.NotNull(command);
        Assert.Equal("Toggle Enhanced Rendering Effects", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.DefaultGesture, udbAlias.DefaultGesture);
    }

    [Fact]
    public void VisualClassicRenderingCommandMatchesUdbActionSurface()
    {
        var command = EditorCommandCatalog.Find("map3d.toggle-classic-rendering");

        Assert.NotNull(command);
        Assert.Equal("Toggle classic rendering", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.toggle-fog-rendering", "Toggle fog rendering", "Menu", true)]
    [InlineData("map3d.toggle-sky-rendering", "Toggle sky rendering", "Menu", true)]
    [InlineData("map3d.toggle-event-lines", "Toggle Event lines", "Menu", false)]
    [InlineData("map3d.toggle-visual-vertices", "Toggle Visual Vertices", "Alt+V", false)]
    public void VisualGzDoomToggleCommandsMatchUdbActionSurface(string id, string title, string gesture, bool allowMouse)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.Equal(allowMouse, command.AllowMouse);
        Assert.False(command.AllowScroll);
        Assert.False(command.Repeat);
    }

    [Theory]
    [InlineData("map3d.toggle-models-rendering", "map3d.gztogglemodels")]
    [InlineData("map3d.toggle-dynamic-lights-rendering", "map3d.toggledynamiclightsrendering")]
    [InlineData("map3d.toggle-dynamic-lights-rendering", "map3d.gztogglelights")]
    [InlineData("map3d.toggle-classic-rendering", "map3d.toggleclassicrendering")]
    [InlineData("map3d.toggle-fog-rendering", "map3d.togglefogrendering")]
    [InlineData("map3d.toggle-fog-rendering", "map3d.gztogglefog")]
    [InlineData("map3d.toggle-sky-rendering", "map3d.toggleskyrendering")]
    [InlineData("map3d.toggle-sky-rendering", "map3d.gztogglesky")]
    [InlineData("map3d.toggle-event-lines", "map3d.toggleeventlines")]
    [InlineData("map3d.toggle-event-lines", "map3d.gztoggleeventlines")]
    [InlineData("map3d.toggle-visual-vertices", "map3d.togglevisualvertices")]
    [InlineData("map3d.toggle-visual-vertices", "map3d.gztogglevisualvertices")]
    public void VisualRenderingToggleAliasesShareCanonicalMetadata(string canonicalId, string aliasId)
    {
        var canonical = EditorCommandCatalog.Find(canonicalId);
        var alias = EditorCommandCatalog.Find(aliasId);

        Assert.NotNull(canonical);
        Assert.NotNull(alias);
        Assert.Equal(canonical.Title, alias.Title);
        Assert.Equal(canonical.DefaultGesture, alias.DefaultGesture);
        Assert.Equal(canonical.Scope, alias.Scope);
        Assert.Equal(canonical.AllowKeys, alias.AllowKeys);
        Assert.Equal(canonical.AllowMouse, alias.AllowMouse);
        Assert.Equal(canonical.AllowScroll, alias.AllowScroll);
        Assert.Equal(canonical.Repeat, alias.Repeat);
    }

    [Fact]
    public void EventLineToggleExistsInClassicMapScopeWithoutStealingInsertShortcut()
    {
        var command = EditorCommandCatalog.Find("map2d.toggle-event-lines");

        Assert.NotNull(command);
        Assert.Equal("Toggle Event lines", command.Title);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map2D, command.Scope);
        Assert.False(command.AllowMouse);
        Assert.Equal("map2d.insertitem", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map2D, "I"));
        Assert.Equal("map3d.insertitem", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "I"));
    }

    [Fact]
    public void VisualVerticesToggleUsesUdbAltVShortcut()
    {
        Assert.Equal("map3d.toggle-visual-vertices", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "V", alt: true));
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
    [InlineData("map3d.copyproperties", "Copy Properties", "Menu")]
    [InlineData("map3d.paste-properties", "Paste Properties", "Ctrl/Cmd+Alt+V")]
    [InlineData("map3d.pasteproperties", "Paste Properties", "Ctrl/Cmd+Alt+V")]
    [InlineData("map3d.paste-properties-options", "Paste Properties Special", "Ctrl/Cmd+Shift+V")]
    [InlineData("map3d.pastepropertieswithoptions", "Paste Properties Special", "Ctrl/Cmd+Shift+V")]
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
        var udbAlias = EditorCommandCatalog.Find("map3d.visualfittextures");

        Assert.NotNull(command);
        Assert.Equal("Fit Textures", command.Title);
        Assert.Equal("Scales textures to match the size of selected surfaces.", command.Description);
        Assert.Equal("Menu", command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.NotNull(udbAlias);
        Assert.Equal(command.Title, udbAlias.Title);
        Assert.Equal(command.Description, udbAlias.Description);
        Assert.Equal("Ctrl/Cmd+Alt+A", udbAlias.DefaultGesture);
        Assert.Equal("map3d.visualfittextures", EditorCommandCatalog.ResolveShortcut(
            EditorCommandScope.Map3D,
            "A",
            accelerator: true,
            alt: true));
    }

    [Theory]
    [InlineData("map3d.align-texture-x", "Align texture X", "A")]
    [InlineData("map3d.align-texture-y", "Align texture Y", "Shift+A")]
    public void VisualTextureAlignCommandsMatchLocalActionSurface(string id, string title, string gesture)
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

        Assert.Equal("map3d.visualautoalign", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true));
    }

    [Theory]
    [InlineData("map3d.visual-auto-align", "Auto-align Textures X and Y", "Ctrl/Cmd+A", "Automatically aligns the neighbouring textures X and Y offsets until another texture is encountered.")]
    [InlineData("map3d.visualautoalign", "Auto-align Textures X and Y", "Ctrl/Cmd+A", "Automatically aligns the neighbouring textures X and Y offsets until another texture is encountered.")]
    [InlineData("map3d.visual-auto-align-x", "Auto-align Textures X", "Menu", "Automatically aligns the neighbouring textures X offsets until another texture is encountered.")]
    [InlineData("map3d.visualautoalignx", "Auto-align Textures X", "Menu", "Automatically aligns the neighbouring textures X offsets until another texture is encountered.")]
    [InlineData("map3d.visual-auto-align-y", "Auto-align Textures Y", "Menu", "Automatically aligns the neighbouring textures Y offsets until another texture is encountered. The Y alignment only takes the ceiling height for each sidedef into account.")]
    [InlineData("map3d.visualautoaligny", "Auto-align Textures Y", "Menu", "Automatically aligns the neighbouring textures Y offsets until another texture is encountered. The Y alignment only takes the ceiling height for each sidedef into account.")]
    [InlineData("map3d.visual-auto-align-to-selection", "Auto-align Textures to Selection (X and Y)", "Menu", "Automatically aligns the neighbouring textures X and Y offsets to selected sidedefs until another texture is encountered.")]
    [InlineData("map3d.visualautoaligntoselection", "Auto-align Textures to Selection (X and Y)", "Menu", "Automatically aligns the neighbouring textures X and Y offsets to selected sidedefs until another texture is encountered.")]
    [InlineData("map3d.visual-auto-align-to-selection-x", "Auto-align Textures to Selection (X)", "Menu", "Automatically aligns the neighbouring textures X offsets to selected sidedefs until another texture is encountered.")]
    [InlineData("map3d.visualautoaligntoselectionx", "Auto-align Textures to Selection (X)", "Menu", "Automatically aligns the neighbouring textures X offsets to selected sidedefs until another texture is encountered.")]
    [InlineData("map3d.visual-auto-align-to-selection-y", "Auto-align Textures to Selection (Y)", "Menu", "Automatically aligns the neighbouring textures Y offsets to selected sidedefs until another texture is encountered.")]
    [InlineData("map3d.visualautoaligntoselectiony", "Auto-align Textures to Selection (Y)", "Menu", "Automatically aligns the neighbouring textures Y offsets to selected sidedefs until another texture is encountered.")]
    public void VisualAutoAlignCommandsMatchUdbActionSurface(string id, string title, string gesture, string description)
    {
        var command = EditorCommandCatalog.Find(id);

        Assert.NotNull(command);
        Assert.Equal(title, command.Title);
        Assert.Equal(description, command.Description);
        Assert.Equal(gesture, command.DefaultGesture);
        Assert.Equal(EditorCommandScope.Map3D, command.Scope);
        Assert.True(command.AllowKeys);
        Assert.True(command.AllowMouse);
        Assert.True(command.AllowScroll);
        Assert.False(command.Repeat);

        Assert.Equal("map3d.visualautoalign", EditorCommandCatalog.ResolveShortcut(EditorCommandScope.Map3D, "A", accelerator: true));
    }

    [Fact]
    public void RepeatableCommandMetadataMatchesAdjustmentActions()
    {
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.zoom-in"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.zoomin"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.zoomout"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.grid-up"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.lower-floor-8"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map2d.raise-brightness-8"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.raise-sector-128"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.move-texture-left-grid"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.lower-brightness-8"));
        Assert.True(EditorCommandCatalog.IsRepeatable("map3d.nudge-offset-left"));
        Assert.True(EditorCommandCatalog.IsRepeatable("window.moveselectionup"));
        Assert.True(EditorCommandCatalog.IsRepeatable("window.moveselectiondown"));
        Assert.True(EditorCommandCatalog.IsRepeatable("window.moveselectionleft"));
        Assert.True(EditorCommandCatalog.IsRepeatable("window.moveselectionright"));

        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.toggle-3d"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-sector"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-rectangle"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-ellipse"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-curve"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-grid"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.increase-subdivision-level"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.decrease-subdivision-level"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.increase-bevel"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.decrease-bevel"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.draw-point"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.remove-draw-point"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.remove-first-draw-point"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.finish-draw"));
        Assert.False(EditorCommandCatalog.IsRepeatable("map2d.bridge-mode"));
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
        Assert.Equal(
            EditorCommandCatalog.ShortcutPressKey(EditorCommandScope.Map2D, "D", accelerator: true, shift: true, alt: true),
            EditorCommandCatalog.ShortcutPressKey(EditorCommandScope.Map2D, "D"));
    }

    [Fact]
    public void WheelInputNormalizesToUdbScrollKeys()
    {
        Assert.Equal(EditorPointerInput.ScrollUp, EditorPointerInput.WheelKey(0, 1));
        Assert.Equal(EditorPointerInput.ScrollDown, EditorPointerInput.WheelKey(0, -1));
        Assert.Equal(EditorPointerInput.ScrollRight, EditorPointerInput.WheelKey(2, 1));
        Assert.Equal(EditorPointerInput.ScrollLeft, EditorPointerInput.WheelKey(-2, 1));
        Assert.Null(EditorPointerInput.WheelKey(0, 0));
        Assert.Equal([EditorPointerInput.ScrollRight, EditorPointerInput.ScrollUp], EditorPointerInput.WheelKeys(2, 1));
        Assert.Equal([EditorPointerInput.ScrollUp, EditorPointerInput.ScrollRight], EditorPointerInput.WheelKeys(1, 2));
        Assert.Equal([EditorPointerInput.ScrollDown], EditorPointerInput.WheelKeys(0, -1));
        Assert.Empty(EditorPointerInput.WheelKeys(0, 0));
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
        });

        Assert.Same(EditorCommandCatalog.DefaultShortcuts, bindings);
        Assert.Equal("window.save", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "S", accelerator: true));
    }

    [Fact]
    public void EffectiveShortcutsCanClearDefaultBindings()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, ""),
        });

        Assert.Null(EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "S", accelerator: true));
        Assert.Equal("-", EditorCommandCatalog.GestureText("window.save", bindings));
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
    public void GestureTextFormatsAllTopRowDigitKeys()
    {
        Assert.Equal("0", EditorCommandCatalog.GestureText(new EditorShortcutBinding("window.clear-group-10", EditorCommandScope.Window, "D0")));
        Assert.Equal("5", EditorCommandCatalog.GestureText(new EditorShortcutBinding("window.clear-group-5", EditorCommandScope.Window, "D5")));
        Assert.Equal("9", EditorCommandCatalog.GestureText(new EditorShortcutBinding("window.clear-group-9", EditorCommandScope.Window, "D9")));
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
            "map2d.insertitem");

        Assert.Equal("D Draw sector; I / Insert Insert Item", hints);
    }

    [Fact]
    public void ExpandHintTemplateReplacesUdbStyleActionTokensWithShortcuts()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map2d.draw-sector", EditorCommandScope.Map2D, "F6"),
        });

        string hint = EditorCommandCatalog.ExpandHintTemplate(
            "Press <k>map2d.draw-sector</k> or <k>map2d.insertitem</k>.",
            bindings);

        Assert.Equal("Press F6 or I / Insert.", hint);
    }

    [Fact]
    public void ExpandHintTemplateLeavesUnknownAndUnclosedTokensUnchanged()
    {
        string hint = EditorCommandCatalog.ExpandHintTemplate(
            "Press <k>missing.command</k> then <k>map2d.draw-sector.",
            EditorCommandCatalog.DefaultShortcuts);

        Assert.Equal("Press <k>missing.command</k> then <k>map2d.draw-sector.", hint);
    }

    [Fact]
    public void CommandToolTipAppendsEffectiveGestureWhenBound()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
        });

        Assert.Equal("Save WAD (F5)", EditorCommandCatalog.CommandToolTip("Save WAD", "window.save", bindings));
        Assert.Equal("Open Map", EditorCommandCatalog.CommandToolTip("Open Map", "window.open-map", bindings));
    }

    [Fact]
    public void ParseOverrideTextReadsCommandGestures()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("window.save=F5; map2d.fit=Shift+R; map3d.brightness-down=[; window.cancel-draw=Esc; window.copy=Control+C");

        Assert.Contains(overrides, b => b.CommandId == "window.save" && b.Key == "F5");
        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "R" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemOpenBrackets");
        Assert.Contains(overrides, b => b.CommandId == "window.cancel-draw" && b.Key == "Escape");
        Assert.Contains(overrides, b => b.CommandId == "window.copy" && b.Key == "C" && b.Accelerator);
    }

    [Fact]
    public void ParseOverrideTextReadsLineAndCommaSeparatedCommandGestures()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("""
            window.save=F5
            map2d.fit=Shift+R, map3d.brightness-down=[
            """);

        Assert.Contains(overrides, b => b.CommandId == "window.save" && b.Key == "F5");
        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "R" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemOpenBrackets");
    }

    [Fact]
    public void ParseOverrideTextReadsUdbScriptActionNames()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("""
            udbscriptexecute=Ctrl+F8, udbscript_udbscriptexecuteslot12=Shift+F8
            udbscript__udbscriptexecuteslot30=Alt+F8
            """);

        Assert.Contains(overrides, b => b.CommandId == "window.udbscriptexecute" && b.Key == "F8" && b.Accelerator);
        Assert.Contains(overrides, b => b.CommandId == "window.udbscriptexecuteslot12" && b.Key == "F8" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.udbscriptexecuteslot30" && b.Key == "F8" && b.Alt);
    }

    [Fact]
    public void ParseOverrideTextKeepsCommaShortcutKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("map2d.fit=,");

        var binding = Assert.Single(overrides);
        Assert.Equal("map2d.fit", binding.CommandId);
        Assert.Equal("OemComma", binding.Key);
    }

    [Fact]
    public void ParseOverrideTextReadsUdbStylePunctuationAndNumpadKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.fit=~; map2d.zoom-in=NumPadPlus; map2d.zoom-out=NumPadMinus; map3d.scale-up=NumPadDecimal; map3d.scale-down=NumPadMultiply; map3d.select-texture=NumPadDivide; map3d.brightness-up=]; map3d.brightness-down=+; window.status-history=Oem1");

        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "OemTilde");
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-in" && b.Key == "Add");
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-out" && b.Key == "Subtract");
        Assert.Contains(overrides, b => b.CommandId == "map3d.scale-up" && b.Key == "Decimal");
        Assert.Contains(overrides, b => b.CommandId == "map3d.scale-down" && b.Key == "Multiply");
        Assert.Contains(overrides, b => b.CommandId == "map3d.select-texture" && b.Key == "Divide");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-up" && b.Key == "OemCloseBrackets");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemPlus");
        Assert.Contains(overrides, b => b.CommandId == "window.status-history" && b.Key == "OemSemicolon");
    }

    [Fact]
    public void ParseOverrideTextReadsWinFormsOemKeyNames()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.fit=Oem3; map3d.brightness-down=Oemplus; map3d.brightness-up=Oemcomma; window.tags=Oem7; window.status-history=Oem5; map2d.grid-down=Oem4; map2d.grid-up=Oem6; map3d.select-texture=Oem2; map3d.texture-copy=OemPipe; map2d.zoom-out=OemMinus");

        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "OemTilde");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemPlus");
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-up" && b.Key == "OemComma");
        Assert.Contains(overrides, b => b.CommandId == "window.tags" && b.Key == "OemQuotes");
        Assert.Contains(overrides, b => b.CommandId == "window.status-history" && b.Key == "OemBackslash");
        Assert.Contains(overrides, b => b.CommandId == "map2d.grid-down" && b.Key == "OemOpenBrackets");
        Assert.Contains(overrides, b => b.CommandId == "map2d.grid-up" && b.Key == "OemCloseBrackets");
        Assert.Contains(overrides, b => b.CommandId == "map3d.select-texture" && b.Key == "OemQuestion");
        Assert.Contains(overrides, b => b.CommandId == "map3d.texture-copy" && b.Key == "OemBackslash");
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-out" && b.Key == "OemMinus");
    }

    [Fact]
    public void ParseOverrideTextReadsSpacebarAliases()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.fit=Spacebar; map2d.toggle-3d=SpaceKey");

        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "Space");
        Assert.Contains(overrides, b => b.CommandId == "map2d.toggle-3d" && b.Key == "Space");
    }

    [Fact]
    public void ParseOverrideTextReadsShiftedUdbStylePunctuationKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "window.tags=Shift+\"; window.status-history=Shift+|; map3d.brightness-up=Shift+}; map3d.brightness-down=Shift+{; map2d.zoom-out=Shift+_; map2d.fit=Shift+<; map2d.grid-up=Shift+>; map3d.select-texture=Shift+/");

        Assert.Contains(overrides, b => b.CommandId == "window.tags" && b.Key == "OemQuotes" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.status-history" && b.Key == "OemBackslash" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-up" && b.Key == "OemCloseBrackets" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.brightness-down" && b.Key == "OemOpenBrackets" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-out" && b.Key == "OemMinus" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == "OemComma" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map2d.grid-up" && b.Key == "OemPeriod" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map3d.select-texture" && b.Key == "OemQuestion" && b.Shift);
    }

    [Fact]
    public void ParseOverrideTextReadsAllTopRowDigitKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "window.clear-group-10=0; window.clear-group-5=5; window.clear-group-9=9");

        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-10" && b.Key == "D0");
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-5" && b.Key == "D5");
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-9" && b.Key == "D9");
    }

    [Fact]
    public void ParseOverrideTextReadsUdbStyleNumpadDigitAliases()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.mode-vertices=Num1; map3d.scale-down-y=Num5; map3d.scale-up=Num9");

        Assert.Contains(overrides, b => b.CommandId == "map2d.mode-vertices" && b.Key == "NumPad1");
        Assert.Contains(overrides, b => b.CommandId == "map3d.scale-down-y" && b.Key == "NumPad5");
        Assert.Contains(overrides, b => b.CommandId == "map3d.scale-up" && b.Key == "NumPad9");
    }

    [Fact]
    public void ParseOverrideTextReadsWinFormsModifierKeyAliases()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "window.save=ControlKey+S; map2d.draw-lines=ShiftKey+D; map2d.draw-ellipse=Menu+D; map2d.draw-curve=LControlKey+RMenu+D");

        Assert.Contains(overrides, b => b.CommandId == "window.save" && b.Key == "S" && b.Accelerator);
        Assert.Contains(overrides, b => b.CommandId == "map2d.draw-lines" && b.Key == "D" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "map2d.draw-ellipse" && b.Key == "D" && b.Alt);
        Assert.Contains(overrides, b => b.CommandId == "map2d.draw-curve" && b.Key == "D" && b.Accelerator && b.Alt);
    }

    [Fact]
    public void ParseOverrideTextReadsShiftedTopRowDigitKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "window.clear-group-1=Shift+!; window.clear-group-2=Shift+@; window.clear-group-3=Shift+#; window.clear-group-4=Shift+$; window.clear-group-5=Shift+%; window.clear-group-6=Shift+^; window.clear-group-7=Shift+&; window.clear-group-8=Shift+*; window.clear-group-9=Shift+(; window.clear-group-10=Shift+)");

        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-1" && b.Key == "D1" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-2" && b.Key == "D2" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-3" && b.Key == "D3" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-4" && b.Key == "D4" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-5" && b.Key == "D5" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-6" && b.Key == "D6" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-7" && b.Key == "D7" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-8" && b.Key == "D8" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-9" && b.Key == "D9" && b.Shift);
        Assert.Contains(overrides, b => b.CommandId == "window.clear-group-10" && b.Key == "D0" && b.Shift);
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
    public void ParseOverrideTextReadsUdbInternalMouseScrollKeys()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "map2d.zoom-in=MScrollUp; map2d.zoom-out=MScrollDown; map2d.fit=Ctrl+MScrollLeft; map3d.roll-clockwise=Alt+MScrollRight");

        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-in" && b.Key == EditorPointerInput.ScrollUp);
        Assert.Contains(overrides, b => b.CommandId == "map2d.zoom-out" && b.Key == EditorPointerInput.ScrollDown);
        Assert.Contains(overrides, b => b.CommandId == "map2d.fit" && b.Key == EditorPointerInput.ScrollLeft && b.Accelerator);
        Assert.Contains(overrides, b => b.CommandId == "map3d.roll-clockwise" && b.Key == EditorPointerInput.ScrollRight && b.Alt);
    }

    [Fact]
    public void ParseOverrideTextReadsClearedShortcutOverrides()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText(
            "window.save=None; window.copy=Unbound; window.paste=-; window.delete=");

        Assert.Contains(overrides, b => b.CommandId == "window.save" && b.Key == "");
        Assert.Contains(overrides, b => b.CommandId == "window.copy" && b.Key == "");
        Assert.Contains(overrides, b => b.CommandId == "window.paste" && b.Key == "");
        Assert.Contains(overrides, b => b.CommandId == "window.delete" && b.Key == "");
    }

    [Fact]
    public void UdbInternalMouseScrollAliasesResolveToStableScrollKeys()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map2d.zoom-in", EditorCommandScope.Map2D, "MScrollUp"),
        });

        Assert.Equal("map2d.zoom-in", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, EditorPointerInput.ScrollUp));
    }

    [Fact]
    public void SpecialKeyAliasesResolveToAvaloniaKeyNames()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.cancel-draw", EditorCommandScope.Window, "Esc"),
            new EditorShortcutBinding("window.properties", EditorCommandScope.Window, "Return"),
            new EditorShortcutBinding("map2d.insert", EditorCommandScope.Map2D, "Ins"),
            new EditorShortcutBinding("window.delete", EditorCommandScope.Window, "Bksp"),
            new EditorShortcutBinding("map3d.raise-sector-to-nearest", EditorCommandScope.Map3D, "Prior"),
            new EditorShortcutBinding("map3d.lower-sector-to-nearest", EditorCommandScope.Map3D, "Next"),
            new EditorShortcutBinding("window.toggle-info-panel", EditorCommandScope.Window, "Capital"),
        });

        Assert.Equal("window.cancel-draw", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Escape"));
        Assert.Equal("window.properties", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Enter"));
        Assert.Equal("map2d.insert", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map2D, "Insert"));
        Assert.Equal("window.delete", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "Back"));
        Assert.Equal("map3d.raise-sector-to-nearest", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map3D, "PageUp"));
        Assert.Equal("map3d.lower-sector-to-nearest", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map3D, "PageDown"));
        Assert.Equal("window.toggle-info-panel", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "CapsLock"));
    }

    [Fact]
    public void PageKeyAbbreviationsResolveToAvaloniaKeyNames()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("map3d.raise-sector-to-nearest", EditorCommandScope.Map3D, "PgUp"),
            new EditorShortcutBinding("map3d.lower-sector-to-nearest", EditorCommandScope.Map3D, "PgDn"),
        });

        Assert.Equal("map3d.raise-sector-to-nearest", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map3D, "PageUp"));
        Assert.Equal("map3d.lower-sector-to-nearest", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Map3D, "PageDown"));
    }

    [Fact]
    public void TopRowDigitAliasesResolveToAvaloniaKeyNames()
    {
        var bindings = EditorCommandCatalog.EffectiveShortcuts(new[]
        {
            new EditorShortcutBinding("window.clear-group-10", EditorCommandScope.Window, "0", Accelerator: true, Shift: true),
            new EditorShortcutBinding("window.clear-group-5", EditorCommandScope.Window, "5", Accelerator: true, Shift: true),
        });

        Assert.Equal("window.clear-group-10", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "D0", accelerator: true, shift: true));
        Assert.Equal("window.clear-group-5", EditorCommandCatalog.ResolveShortcut(bindings, EditorCommandScope.Window, "D5", accelerator: true, shift: true));
    }

    [Fact]
    public void ParseOverrideTextSkipsInvalidEntries()
    {
        var overrides = EditorCommandCatalog.ParseOverrideText("missing.command=F5; window.save=Ctrl; malformed");

        Assert.Empty(overrides);
    }

    [Fact]
    public void OverrideTextRoundTripsParseableGestures()
    {
        var overrides = new[]
        {
            new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
            new EditorShortcutBinding("map2d.fit", EditorCommandScope.Map2D, "R", Shift: true),
            new EditorShortcutBinding("window.copy", EditorCommandScope.Window, ""),
        };

        var parsed = EditorCommandCatalog.ParseOverrideText(EditorCommandCatalog.OverrideText(overrides));

        Assert.Equal(overrides, parsed);
    }
}
