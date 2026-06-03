// ABOUTME: Tests MainWindow command dispatch metadata without constructing the Avalonia window.
// ABOUTME: Keeps stable window command ids wired to editor handlers as menu actions move into the catalog.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class MainWindowCommandTests
{
    [Theory]
    [InlineData("window.shortcuts", "OnShortcuts")]
    [InlineData("window.about", "OnAbout")]
    [InlineData("window.select-all", "OnSelectAll")]
    [InlineData("window.invert-selection", "OnInvertSelection")]
    [InlineData("window.select-none", "OnSelectNone")]
    [InlineData("window.flags", "OnFlags")]
    [InlineData("window.custom-fields", "OnCustomFields")]
    [InlineData("window.tags", "OnTagList")]
    [InlineData("window.stitch-geometry", "OnStitch")]
    [InlineData("window.join-sectors", "OnJoinSectors")]
    [InlineData("window.merge-sectors", "OnMergeSectors")]
    [InlineData("window.flip-selection-horizontal", "OnFlipH")]
    [InlineData("window.flip-selection-vertical", "OnFlipV")]
    [InlineData("window.rotate-selection-cw", "OnRotateCW")]
    [InlineData("window.rotate-selection-ccw", "OnRotateCCW")]
    [InlineData("window.scale-selection-up", "OnScaleUp")]
    [InlineData("window.scale-selection-down", "OnScaleDown")]
    [InlineData("window.align-floor-to-front", "OnAlignFloorToFront")]
    [InlineData("window.align-floor-to-back", "OnAlignFloorToBack")]
    [InlineData("window.align-ceiling-to-front", "OnAlignCeilingToFront")]
    [InlineData("window.align-ceiling-to-back", "OnAlignCeilingToBack")]
    [InlineData("window.align-things-to-wall", "OnAlignThingsToWall")]
    [InlineData("window.find-replace", "OnFindReplace")]
    [InlineData("window.status-history", "OnStatusHistory")]
    [InlineData("window.browse-wall-textures", "OnBrowseWallTextures")]
    [InlineData("window.browse-flats", "OnBrowseFlats")]
    [InlineData("window.browse-floor-flats", "OnBrowseFloorFlats")]
    [InlineData("window.browse-ceiling-flats", "OnBrowseCeilingFlats")]
    [InlineData("window.browse-things", "OnBrowseThingsCatalog")]
    [InlineData("window.browse-linedef-actions", "OnBrowseActionsCatalog")]
    [InlineData("window.browse-sector-effects", "OnBrowseEffectsCatalog")]
    [InlineData("window.model-render-none", "OnModelRenderNone")]
    [InlineData("window.model-render-selection", "OnModelRenderSelection")]
    [InlineData("window.model-render-active-filter", "OnModelRenderActiveFilter")]
    [InlineData("window.model-render-all", "OnModelRenderAll")]
    [InlineData("window.next-model-render-mode", "OnNextModelRenderMode")]
    [InlineData("window.toggle-3d-floors", "OnToggle3DFloors")]
    [InlineData("window.toggle-blockmap", "OnToggleBlockmap")]
    [InlineData("window.toggle-nodes", "OnToggleNodes")]
    [InlineData("window.gradient-floor-heights", "OnGradientFloorHeights")]
    [InlineData("window.gradient-ceiling-heights", "OnGradientCeilingHeights")]
    [InlineData("window.gradient-sector-brightness", "OnGradientBrightness")]
    [InlineData("window.gradient-floor-light", "OnGradientFloorLight")]
    [InlineData("window.gradient-ceiling-light", "OnGradientCeilingLight")]
    [InlineData("window.gradient-light-color", "OnGradientLightColor")]
    [InlineData("window.gradient-fade-color", "OnGradientFadeColor")]
    [InlineData("window.gradient-light-and-fade-colors", "OnGradientLightAndFadeColor")]
    [InlineData("window.gradient-linedef-brightness", "OnGradientLinedefBrightness")]
    [InlineData("window.gradient-interpolation-linear", "OnGradientInterpolationLinear")]
    [InlineData("window.gradient-interpolation-ease-in-out-sine", "OnGradientInterpolationEaseInOutSine")]
    [InlineData("window.gradient-interpolation-ease-in-sine", "OnGradientInterpolationEaseInSine")]
    [InlineData("window.gradient-interpolation-ease-out-sine", "OnGradientInterpolationEaseOutSine")]
    [InlineData("window.apply-slope-arch", "OnApplySlopeArch")]
    [InlineData("window.apply-slopes", "OnApplySlopes")]
    [InlineData("window.toggle-automap-secret-line", "OnToggleAutomapSecretLine")]
    [InlineData("window.toggle-automap-hidden-line", "OnToggleAutomapHiddenLine")]
    [InlineData("window.toggle-automap-textured-hidden-sector", "OnToggleAutomapTexturedHiddenSector")]
    [InlineData("window.sector-color", "OnSectorColor")]
    [InlineData("window.dynamic-light-color", "OnDynamicLightColor")]
    [InlineData("window.import-obj-terrain", "OnImportObjTerrain")]
    [InlineData("window.export-idstudio", "OnExportIdStudio")]
    [InlineData("window.check-map", "OnCheckMap")]
    [InlineData("window.clean-up-geometry", "OnCleanUpGeometry")]
    [InlineData("window.build-bridge", "OnBuildBridge")]
    [InlineData("window.build-stairs", "OnBuildStairs")]
    [InlineData("window.usdf-conversations", "OnUsdfConversations")]
    public void MenuCommandsAreRoutedThroughWindowCommandDispatch(string commandId, string handlerName)
    {
        Type type = typeof(MainWindow);
        MethodInfo? dispatcher = type.GetMethod("RunWindowCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        Assert.Contains($"case \"{commandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"{handlerName}(this, new RoutedEventArgs())", body, StringComparison.Ordinal);
    }
}
