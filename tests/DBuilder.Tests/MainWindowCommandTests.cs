// ABOUTME: Tests MainWindow command dispatch metadata without constructing the Avalonia window.
// ABOUTME: Keeps stable window command ids wired to editor handlers as menu actions move into the catalog.

using System.Reflection;
using DBuilder.Editor;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class MainWindowCommandTests
{
    [Theory]
    [InlineData("window.recover-autosave", "OnRecoverAutosave")]
    [InlineData("window.reload-map", "OnReloadMap")]
    [InlineData("window.add-resource", "OnAddResource")]
    [InlineData("window.add-resource-directory", "OnAddResourceDirectory")]
    [InlineData("window.save-as-format", "OnSaveAsFormat")]
    [InlineData("window.exit", "OnExit")]
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
    [InlineData("window.moveselectionup", "OnMoveSelectionUp")]
    [InlineData("window.moveselectiondown", "OnMoveSelectionDown")]
    [InlineData("window.moveselectionleft", "OnMoveSelectionLeft")]
    [InlineData("window.moveselectionright", "OnMoveSelectionRight")]
    [InlineData("window.scale-selection-up", "OnScaleUp")]
    [InlineData("window.scale-selection-down", "OnScaleDown")]
    [InlineData("window.align-floor-to-front", "OnAlignFloorToFront")]
    [InlineData("window.align-floor-to-back", "OnAlignFloorToBack")]
    [InlineData("window.align-ceiling-to-front", "OnAlignCeilingToFront")]
    [InlineData("window.align-ceiling-to-back", "OnAlignCeilingToBack")]
    [InlineData("window.align-things-to-wall", "OnAlignThingsToWall")]
    [InlineData("window.find-replace", "OnFindReplace")]
    [InlineData("window.findmode", "OnFindReplace")]
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
    [InlineData("window.gradientfloors", "OnGradientFloorHeights")]
    [InlineData("window.gradient-ceiling-heights", "OnGradientCeilingHeights")]
    [InlineData("window.gradientceilings", "OnGradientCeilingHeights")]
    [InlineData("window.gradient-sector-brightness", "OnGradientBrightness")]
    [InlineData("window.gradientbrightness", "OnGradientBrightness")]
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
    [InlineData("window.togglelightpannel", "OnToggleLightPanel")]
    [InlineData("window.import-obj-terrain", "OnImportObjTerrain")]
    [InlineData("window.importobjasterrain", "OnImportObjTerrain")]
    [InlineData("window.export-object", "OnExportObject")]
    [InlineData("window.export-image", "OnExportImage")]
    [InlineData("window.exporttoimage", "OnExportImage")]
    [InlineData("window.export-wavefront", "OnExportWavefront")]
    [InlineData("window.exporttoobj", "OnExportWavefront")]
    [InlineData("window.export-idstudio", "OnExportIdStudio")]
    [InlineData("window.exporttoidstudio", "OnExportIdStudio")]
    [InlineData("window.check-map", "OnCheckMap")]
    [InlineData("window.errorcheckmode", "OnCheckMap")]
    [InlineData("window.clean-up-geometry", "OnCleanUpGeometry")]
    [InlineData("window.build-bridge", "OnBuildBridge")]
    [InlineData("window.makedoor", "OnMakeDoor")]
    [InlineData("window.build-stairs", "OnBuildStairs")]
    [InlineData("window.usdf-conversations", "OnUsdfConversations")]
    [InlineData("window.usdf-dialog-editor", "OnUsdfConversations")]
    [InlineData("window.opendialogeditor", "OnUsdfConversations")]
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

    [Theory]
    [InlineData("window.classiccopyproperties", "OnCopyProperties")]
    [InlineData("window.classicpasteproperties", "OnPasteProperties")]
    [InlineData("window.classicpastepropertieswithoptions", "OnPastePropertiesWithOptions")]
    [InlineData("window.deleteitem", "OnDelete")]
    [InlineData("window.clearselection", "OnSelectNone")]
    [InlineData("window.selectsimilar", "OnSelectSimilar")]
    [InlineData("window.filterselectedthings", "OnFilterSelectedThings")]
    [InlineData("window.changemapelementindex", "OnChangeMapElementIndex")]
    [InlineData("window.flipselectionh", "OnFlipH")]
    [InlineData("window.flipselectionv", "OnFlipV")]
    [InlineData("window.rotateclockwise", "OnRotateCW")]
    [InlineData("window.rotatecounterclockwise", "OnRotateCCW")]
    [InlineData("window.moveselectionup", "OnMoveSelectionUp")]
    [InlineData("window.moveselectiondown", "OnMoveSelectionDown")]
    [InlineData("window.moveselectionleft", "OnMoveSelectionLeft")]
    [InlineData("window.moveselectionright", "OnMoveSelectionRight")]
    [InlineData("window.alignfloortofront", "OnAlignFloorToFront")]
    [InlineData("window.alignfloortoback", "OnAlignFloorToBack")]
    [InlineData("window.alignceilingtofront", "OnAlignCeilingToFront")]
    [InlineData("window.alignceilingtoback", "OnAlignCeilingToBack")]
    [InlineData("window.rangetagselection", "OnTagRange")]
    [InlineData("window.blockmapexplorermode", "OnBlockmapExplorer")]
    [InlineData("window.rejectexplorermode", "OnRejectViewer")]
    [InlineData("window.rejectexplorercolorconfiguration", "OnRejectExplorerColors")]
    [InlineData("window.nodesviewermode", "OnNodesViewer")]
    [InlineData("window.soundpropagationmode", "OnSoundPropagation")]
    [InlineData("window.soundenvironmentmode", "OnSoundEnvironments")]
    [InlineData("window.soundpropagationcolorconfiguration", "OnSoundPropagationColors")]
    [InlineData("window.setleakfinderstart", "OnSetLeakFinderStart")]
    [InlineData("window.setleakfinderend", "OnSetLeakFinderEnd")]
    [InlineData("window.applyjitter", "OnApplyJitter")]
    [InlineData("window.applydirectionalshading", "OnApplyDirectionalShading")]
    public void UdbClassicWindowActionAliasesAreDispatched(string commandId, string handlerName)
    {
        Type type = typeof(MainWindow);
        MethodInfo? dispatcher = type.GetMethod("RunWindowCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf($"{handlerName}(this, new RoutedEventArgs())", commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Fact]
    public void JitterActionUsesVisualSelectionPriorityIn3DMode()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int visualModeIndex = body.IndexOf("if (MapView.In3DMode)", methodIndex, StringComparison.Ordinal);
        int visualThingsIndex = body.IndexOf("MapView.SelectedVisualThingsForActions()", visualModeIndex, StringComparison.Ordinal);
        int visualSurfacesIndex = body.IndexOf("MapView.SelectedVisualSurfacesForActions()", visualThingsIndex, StringComparison.Ordinal);
        int regularThingsModeIndex = body.IndexOf("MapView.CurrentEditMode == MapControl.EditMode.Things", visualSurfacesIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(visualModeIndex > methodIndex);
        Assert.True(visualThingsIndex > visualModeIndex);
        Assert.True(visualSurfacesIndex > visualThingsIndex);
        Assert.True(regularThingsModeIndex > visualSurfacesIndex);
    }

    [Fact]
    public void JitterActionAppliesSelectedSectorOffsetModes()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int floorIndex = body.IndexOf("BuilderEffects.ApplySectorFloorHeight(sectorJitter, dialog.ResultFloorAmount, dialog.ResultFloorOffsetMode)", methodIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("BuilderEffects.ApplySectorCeilingHeight(sectorJitter, dialog.ResultCeilingAmount, dialog.ResultCeilingOffsetMode)", floorIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(floorIndex > methodIndex);
        Assert.True(ceilingIndex > floorIndex);
    }

    [Fact]
    public void JitterActionSnapsThingAnglesWhenGameConfigUsesDoomAngles()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int rotationIndex = body.IndexOf("BuilderEffects.ApplyThingRotation(", methodIndex, StringComparison.Ordinal);
        int configIndex = body.IndexOf("_config?.DoomThingRotationAngles == true", rotationIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(rotationIndex > methodIndex);
        Assert.True(configIndex > rotationIndex);
    }

    [Fact]
    public void JitterActionAppliesThingHeightWhenGameConfigSupportsThingHeight()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int factorIndex = body.IndexOf("HeightFactor: RandomFactor()", methodIndex, StringComparison.Ordinal);
        int sectorHeightIndex = body.IndexOf("SectorHeight: JitterThingSectorHeight(thing)", factorIndex, StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_config?.HasThingHeight == true)", sectorHeightIndex, StringComparison.Ordinal);
        int applyIndex = body.IndexOf("BuilderEffects.ApplyThingHeight(thingJitter, dialog.ResultThingHeightAmount)", guardIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private int JitterThingSectorHeight(Thing thing)", StringComparison.Ordinal);
        int thingTypeHeightIndex = body.IndexOf("_config.GetThing(thing.Type)?.Height ?? 0", helperIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(factorIndex > methodIndex);
        Assert.True(sectorHeightIndex > factorIndex);
        Assert.True(guardIndex > sectorHeightIndex);
        Assert.True(applyIndex > guardIndex);
        Assert.True(helperIndex > applyIndex);
        Assert.True(thingTypeHeightIndex > helperIndex);
    }

    [Fact]
    public void JitterActionAppliesThingPitchAndRollOnlyInUdmf()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int pitchFactorIndex = body.IndexOf("PitchFactor: RandomPositiveFactor()", methodIndex, StringComparison.Ordinal);
        int rollFactorIndex = body.IndexOf("RollFactor: RandomPositiveFactor()", pitchFactorIndex, StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_mapFormat == MapFormat.Udmf)", rollFactorIndex, StringComparison.Ordinal);
        int pitchIndex = body.IndexOf("BuilderEffects.ApplyThingPitch(thingJitter, dialog.ResultThingPitchAmount, relative: false)", guardIndex, StringComparison.Ordinal);
        int rollIndex = body.IndexOf("BuilderEffects.ApplyThingRoll(thingJitter, dialog.ResultThingRollAmount, relative: false)", pitchIndex, StringComparison.Ordinal);
        int factorMethodIndex = body.IndexOf("private static double RandomPositiveFactor()", rollIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(pitchFactorIndex > methodIndex);
        Assert.True(rollFactorIndex > pitchFactorIndex);
        Assert.True(guardIndex > rollFactorIndex);
        Assert.True(pitchIndex > guardIndex);
        Assert.True(rollIndex > pitchIndex);
        Assert.True(factorMethodIndex > rollIndex);
    }

    [Fact]
    public void JitterActionAppliesThingScaleOnlyInUdmf()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int scaleXFactorIndex = body.IndexOf("ScaleXFactor: RandomPositiveFactor()", methodIndex, StringComparison.Ordinal);
        int scaleYFactorIndex = body.IndexOf("ScaleYFactor: RandomPositiveFactor()", scaleXFactorIndex, StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_mapFormat == MapFormat.Udmf)", scaleYFactorIndex, StringComparison.Ordinal);
        int scaleIndex = body.IndexOf("BuilderEffects.ApplyThingScale(", guardIndex, StringComparison.Ordinal);
        int minXIndex = body.IndexOf("dialog.ResultThingScaleMinX", scaleIndex, StringComparison.Ordinal);
        int relativeIndex = body.IndexOf("dialog.ResultRelativeThingScale", minXIndex, StringComparison.Ordinal);
        int uniformIndex = body.IndexOf("dialog.ResultUniformThingScale", relativeIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(scaleXFactorIndex > methodIndex);
        Assert.True(scaleYFactorIndex > scaleXFactorIndex);
        Assert.True(guardIndex > scaleYFactorIndex);
        Assert.True(scaleIndex > guardIndex);
        Assert.True(minXIndex > scaleIndex);
        Assert.True(relativeIndex > minXIndex);
        Assert.True(uniformIndex > relativeIndex);
    }

    [Fact]
    public void JitterDialogExposesUdbSectorOffsetModes()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("AddCombo(\"Floor offset mode\", FloorOffsetModeItems(), (int)ResultFloorOffsetMode)", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Ceiling offset mode\", CeilingOffsetModeItems(), (int)ResultCeilingOffsetMode)", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseAndLower, \"Raise and lower\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseOnly, \"Raise only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.LowerOnly, \"Lower only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseOnly, \"Lower only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.LowerOnly, \"Raise only\")", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterDialogExposesUdbThingHeightAmount()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("public int ResultThingHeightAmount { get; private set; } = 16;", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing height amount\", ResultThingHeightAmount.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingHeightAmount = Math.Max(0, ParseInt(_thingHeightAmount, ResultThingHeightAmount));", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterDialogExposesUdbThingPitchAndRollAmounts()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("public int ResultThingPitchAmount { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("public int ResultThingRollAmount { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing pitch amount\", ResultThingPitchAmount.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing roll amount\", ResultThingRollAmount.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingPitchAmount = Math.Max(0, ParseInt(_thingPitchAmount, ResultThingPitchAmount));", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingRollAmount = Math.Max(0, ParseInt(_thingRollAmount, ResultThingRollAmount));", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterDialogExposesUdbThingScaleControls()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("public double ResultThingScaleMinX { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMaxX { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMinY { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMaxY { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale X min\", ResultThingScaleMinX.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale X max\", ResultThingScaleMaxX.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale Y min\", ResultThingScaleMinY.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale Y max\", ResultThingScaleMaxY.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Relative thing scale\", ResultRelativeThingScale)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Uniform thing scale\", ResultUniformThingScale)", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingScaleMinX = Math.Max(0.0, ParseDouble(_thingScaleMinX, ResultThingScaleMinX));", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeThingScale = _relativeThingScale.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUniformThingScale = _uniformThingScale.IsChecked == true;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalShadingActionUsesVisualFloorAndWallSelectionsIn3DMode()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyDirectionalShading", StringComparison.Ordinal);
        int visualModeIndex = body.IndexOf("if (MapView.In3DMode)", methodIndex, StringComparison.Ordinal);
        int visualSurfacesIndex = body.IndexOf("MapView.SelectedVisualSurfacesForActions()", visualModeIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("AddDirectionalShadingVisualSurface(hit, sectors, sides)", visualSurfacesIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Select some floor or wall surfaces first!", helperIndex, StringComparison.Ordinal);
        int linedefsModeIndex = body.IndexOf("MapView.CurrentEditMode == MapControl.EditMode.Linedefs", warningIndex, StringComparison.Ordinal);
        int helperMethodIndex = body.IndexOf("private static void AddDirectionalShadingVisualSurface", StringComparison.Ordinal);
        int floorIndex = body.IndexOf("hit.Kind == VisualHitKind.Floor", helperMethodIndex, StringComparison.Ordinal);
        int wallIndex = body.IndexOf("hit.Kind == VisualHitKind.Wall", floorIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("VisualHitKind.Ceiling", helperMethodIndex, body.IndexOf("// Traces Doom-style", helperMethodIndex, StringComparison.Ordinal) - helperMethodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(visualModeIndex > methodIndex);
        Assert.True(visualSurfacesIndex > visualModeIndex);
        Assert.True(helperIndex > visualSurfacesIndex);
        Assert.True(warningIndex > helperIndex);
        Assert.True(linedefsModeIndex > warningIndex);
        Assert.True(helperMethodIndex > methodIndex);
        Assert.True(floorIndex > helperMethodIndex);
        Assert.True(wallIndex > floorIndex);
        Assert.Equal(-1, ceilingIndex);
    }

    [Fact]
    public void RejectExplorerColorConfigurationUsesSharedDialogHandler()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("case \"window.rejectexplorercolorconfiguration\": OnRejectExplorerColors", body, StringComparison.Ordinal);
        Assert.Contains("await ConfigureRejectExplorerColorsAsync(win, reject, target);", body, StringComparison.Ordinal);
        Assert.Contains("private async Task ConfigureRejectExplorerColorsAsync(Window owner, RejectTable? reject, int? target)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionGroupCommandsAreRoutedThroughDynamicWindowCommandDispatch()
    {
        Type type = typeof(MainWindow);
        MethodInfo? dispatcher = type.GetMethod("RunWindowCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? groupDispatcher = type.GetMethod("RunSelectionGroupCommand", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dispatcher);
        Assert.NotNull(groupDispatcher);

        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        Assert.Contains("return RunUdbScriptCommand(commandId) || RunSelectionGroupCommand(commandId);", body, StringComparison.Ordinal);
        Assert.Contains("const string selectPrefix = \"window.select-group-\";", body, StringComparison.Ordinal);
        Assert.Contains("const string assignPrefix = \"window.assign-group-\";", body, StringComparison.Ordinal);
        Assert.Contains("const string clearPrefix = \"window.clear-group-\";", body, StringComparison.Ordinal);
        Assert.Contains("SelectGroup(selectGroup);", body, StringComparison.Ordinal);
        Assert.Contains("AddSelectionToGroup(assignGroup);", body, StringComparison.Ordinal);
        Assert.Contains("ClearGroup(clearGroup);", body, StringComparison.Ordinal);
        Assert.Contains("groupNumber is < 1 or > MapOptions.SelectionGroupCount", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisplaneExplorerModeReportsQueuedReadyStatusWithPersistedSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("scan.QueuePoints(", body, StringComparison.Ordinal);
        Assert.Contains("VisplaneExplorerInterfaceModel.ReadyStatus(", body, StringComparison.Ordinal);
        Assert.Contains("string readyStatus =", body, StringComparison.Ordinal);
        Assert.Contains("scan.Progress(queued.Count).FormatStatus()", body, StringComparison.Ordinal);
        Assert.Contains("_settings.VisplaneExplorerSettings.SelectedStat", body, StringComparison.Ordinal);
        Assert.Contains("_settings.VisplaneExplorerSettings", body, StringComparison.Ordinal);
    }

    [Fact]
    public void WadAuthorModeReportsModelToggleStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("MapView.ToggleWadAuthorMode()", body, StringComparison.Ordinal);
        Assert.Contains("WadAuthorModeModel.ModeToggleStatusText(enabled, MapView.CurrentEditMode.ToString())", body, StringComparison.Ordinal);
    }

    [Fact]
    public void UndoRedoPanelBeginRowUsesCurrentMapLabel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("UndoRedoPanelModel.Build(UndoRedoPanelBeginDescription(), _undo)", body, StringComparison.Ordinal);
        Assert.Contains("private string UndoRedoPanelBeginDescription()", body, StringComparison.Ordinal);
        Assert.Contains("return $\"{System.IO.Path.GetFileName(_wadPath)} ({marker})\";", body, StringComparison.Ordinal);
        Assert.Contains("return $\"{System.IO.Path.GetFileName(_pk3Path)} ({_pk3MapArchivePath}:{marker})\";", body, StringComparison.Ordinal);
        Assert.Contains("return marker;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void WadAuthorPopupUsesModelEditDescriptions()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("EditBegun?.Invoke(WadAuthorModeModel.EditDescription(action));", body, StringComparison.Ordinal);
        Assert.Contains("WadAuthorLinedefPopupResult propertiesResult = WadAuthorModeModel.ExecuteLinedefPopupAction(_map, line, action, splitPosition);", body, StringComparison.Ordinal);
        Assert.Contains("propertiesResult.Status == WadAuthorModeModel.EditPropertiesStatus", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicLightDialogUsesRelativeLimitsWhenRelativeModeIsSelected()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/DynamicLightDialog.cs"));

        Assert.Contains("_relativeMode.IsCheckedChanged += (_, _) => RefreshRelativeModeFields();", body, StringComparison.Ordinal);
        Assert.Contains("_primaryRadius.Text = FormatInt(relativeMode ? 0 : _state.PrimaryRadius);", body, StringComparison.Ordinal);
        Assert.Contains("_secondaryRadius.Text = FormatInt(relativeMode ? 0 : _state.SecondaryRadius)", body, StringComparison.Ordinal);
        Assert.Contains("_interval.Text = FormatInt(relativeMode ? 0 : _state.Interval)", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeMode = _relativeMode.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.DynamicLightRadiusLimits(relativeMode: true)", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.DynamicLightIntervalLimits(relativeMode: true)", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.ClampDynamicLightSliderValue(\n            radiusLimits,", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.ClampDynamicLightSliderValue(intervalLimits,", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveCommandAvailabilityReflectsWritableSourceArchive()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canSave = hasMap && (_wadPath is null || FileSaveStamp.CanWriteExistingPath(_wadPath));", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canSave, SaveMenuItem, SaveButton);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorPickerCommandAvailabilityReflectsMapFormatAndSelection()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canEditSectorColor = ColorPickerModel.CanEditSectorColors(_mapFormat == MapFormat.Udmf) && hasSelectedSector;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canEditSectorColor, SectorColorMenuItem, SectorColorButton);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedInternalDynamicLight, DynamicLightColorMenuItem, DynamicLightColorButton);", body, StringComparison.Ordinal);
        Assert.Contains("private void OnToggleLightPanel(object? sender, RoutedEventArgs e)", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.HasInternalDynamicLightSelection(_map.GetSelectedThings())", body, StringComparison.Ordinal);
        Assert.Contains("OnDynamicLightColor(sender, e);", body, StringComparison.Ordinal);
        Assert.Contains("OnSectorColor(sender, e);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TagRangeCommandAvailabilityReflectsTaggableSelection()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasTagRangeSelection = TagRangeModel.HasSelection(", body, StringComparison.Ordinal);
        Assert.Contains("(_map?.SelectedSectorsCount ?? 0) + (_map?.SelectedLinedefsCount ?? 0) + (_map?.SelectedThingsCount ?? 0));", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasTagRangeSelection, TagRangeMenuItem, TagRangeButton);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(TagRangeModel.NoSelectionWarning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewCommandAvailabilityReflectsMapResourcesAndConfigState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasResources = _resources is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseCatalogs = hasMap && _config is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseAny = hasResources || canBrowseCatalogs;", body, StringComparison.Ordinal);
        Assert.Contains("bool canFilterThingCategories = hasMap && _config is { Things.Count: > 0 };", body, StringComparison.Ordinal);
        Assert.Contains("GridSetupMenuItem, SmartGridTransformMenuItem, AlignGridToLinedefMenuItem, SetGridOriginToVertexMenuItem,", body, StringComparison.Ordinal);
        Assert.Contains("ResetGridTransformMenuItem, ToggleSnapToGridMenuItem, ToggleDynamicGridSizeMenuItem, GridSizeDownMenuItem, GridSizeUpMenuItem", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canFilterThingCategories, ThingFilterMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseAny, BrowsersMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasResources, BrowseWallTexturesMenuItem, BrowseFlatsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseCatalogs, BrowseThingsMenuItem, BrowseLinedefActionsMenuItem, BrowseSectorEffectsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedSector && hasResources, BrowseFloorFlatsMenuItem, BrowseCeilingFlatsMenuItem);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Draw\" x:Name=\"DrawMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DrawMenuItem,\n            MakeSectorAtCursorMenuItem, DrawSectorMenuItem, DrawLinesMenuItem, DrawCurveMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowsersMenuAvailabilityReflectsChildAvailability()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Browsers\" x:Name=\"BrowsersMenuItem\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckMapUsesPersistedUdbCheckerSelection()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("var checkerSelection = _settings.MapErrorCheckerSelection();", code, StringComparison.Ordinal);
        Assert.Contains("MapAnalysis.Check(map, BuildCheckContext(), checkerSelection.EnabledDescriptors())", code, StringComparison.Ordinal);
        Assert.Contains("enabled => MapAnalysis.Check(map, BuildCheckContext(), enabled),", code, StringComparison.Ordinal);
        Assert.Contains("fix => ApplyMapCheckFix(map, fix));", code, StringComparison.Ordinal);
        Assert.Contains("win.IssuesChanged += count => SetStatus(MapIssueListModel.AnalysisStatusText(count));", code, StringComparison.Ordinal);
        Assert.Contains("_settings.ApplyMapErrorCheckerSelection(checkerSelection);", code, StringComparison.Ordinal);
        Assert.Contains("SaveSettings();", code, StringComparison.Ordinal);
        Assert.Contains("_undo?.CreateUndo(\"Fix map analysis issue\");", code, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Applied fix: {fix.Label}\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MapCheckWindowExposesUdbCheckerSelectionRows()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapCheckWindow.cs"));

        Assert.Contains("MapErrorCheckerSelectionModel? checkerSelection = null", code, StringComparison.Ordinal);
        Assert.Contains("Func<IReadOnlyList<MapErrorCheckerDescriptor>, IReadOnlyList<MapIssue>>? runChecks = null", code, StringComparison.Ordinal);
        Assert.Contains("Func<MapIssueFix, bool>? applyFix = null", code, StringComparison.Ordinal);
        Assert.Contains("CheckerSelectionPanel(checkerSelection, runChecks)", code, StringComparison.Ordinal);
        Assert.Contains("new Expander", code, StringComparison.Ordinal);
        Assert.Contains("new CheckBox", code, StringComparison.Ordinal);
        Assert.Contains("Content = \"Run Checks\"", code, StringComparison.Ordinal);
        Assert.Contains("_model.ReplaceIssues(issues);", code, StringComparison.Ordinal);
        Assert.Contains("selection.SetChecked(row.SettingsKey, check.IsChecked == true)", code, StringComparison.Ordinal);
        Assert.Contains("ApplySelectedFix(index)", code, StringComparison.Ordinal);
        Assert.Contains("_applyFix(issue.Fixes[index])", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var similarIssue in SelectedIssues())", code, StringComparison.Ordinal);
        Assert.Contains("if (similarIssue.Kind != issue.Kind || index >= similarIssue.Fixes.Count) continue;", code, StringComparison.Ordinal);
        Assert.Contains("if (!_applyFix(similarIssue.Fixes[index])) break;", code, StringComparison.Ordinal);
        Assert.Contains("UpdateActionButtons();", code, StringComparison.Ordinal);
        Assert.Contains("_showAll.IsEnabled = hasHidden;", code, StringComparison.Ordinal);
        Assert.Contains("_copySelected.IsEnabled = hasSelection;", code, StringComparison.Ordinal);
        Assert.Contains("e.Key == Key.A && HasCopyModifier(e.KeyModifiers)", code, StringComparison.Ordinal);
        Assert.Contains("SelectAllVisibleResults();", code, StringComparison.Ordinal);
        Assert.Contains("_model.AllVisibleIssues().ToHashSet();", code, StringComparison.Ordinal);
        Assert.Contains("UpdateSelectionInfo();", code, StringComparison.Ordinal);
        Assert.Contains("All results are hidden. Use Show All to restore them.", code, StringComparison.Ordinal);
        Assert.Contains("Select a result to view details. Hold Ctrl to select several results. Hold Shift to select a range.", code, StringComparison.Ordinal);
        Assert.Contains("\" Fixes: \" + string.Join(\", \", issue.Fixes.Take(3).Select(fix => fix.Label)) + \".\"", code, StringComparison.Ordinal);
        Assert.Contains("UpdateWindowTitle();", code, StringComparison.Ordinal);
        Assert.Contains("MapIssueListModel.WindowTitleText(", code, StringComparison.Ordinal);
        Assert.Contains("_model.AllIssues.Count == 0", code, StringComparison.Ordinal);
        Assert.Contains("Text = MapIssueListModel.NoErrorsResultText", code, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = false", code, StringComparison.Ordinal);
        Assert.Contains("MapIssueListModel.HaveSameFixSignature(selected)", code, StringComparison.Ordinal);
        Assert.Contains("Several types of map analysis results are selected. To display fixes, make sure that only a single result type is selected.", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomapMenuAvailabilityReflectsChildAvailability()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Automap\" x:Name=\"AutomapMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("bool hasSelectedAutomapTarget = hasSelectedLinedef || hasSelectedSector;", code, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedAutomapTarget, AutomapMenuItem);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SectorHeightsMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"Sector _Heights\" x:Name=\"SectorHeightsMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SectorHeightsMenuItem,\n            LowerFloor8MenuItem, RaiseFloor8MenuItem, LowerCeiling8MenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void AlignTexturesParentAvailabilityReflectsEitherChildGroup()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canAlignTextures = hasSelectedLinedefWithFront || hasSelectedUdmfLinedef;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canAlignTextures, AlignTexturesMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedLinedefWithFront, AlignHorizontalMenuItem, AlignVerticalMenuItem, FitSelectedTexturesMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedUdmfLinedef,\n            AlignFloorToFrontMenuItem, AlignFloorToBackMenuItem, AlignCeilingToFrontMenuItem, AlignCeilingToBackMenuItem);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionGroupsMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"Selection _Groups\" x:Name=\"SelectionGroupsMenu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectAllMenuItem, InvertSelectionMenuItem, SelectionGroupsMenu,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GridMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Grid\" x:Name=\"GridMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ThingFilterMenuItem, GridMenuItem, GridSetupMenuItem, SmartGridTransformMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModeMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_View Mode\" x:Name=\"ViewModeMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleHighlightMenuItem, ViewModeMenuItem, ViewModeWireframeMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelRenderingMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Model Rendering\" x:Name=\"ModelRenderingMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ModelRenderingMenuItem, ModelRenderNoneMenuItem, ModelRenderSelectionMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void EditModeMenuAvailabilityReflectsMapState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Edit Mode\" x:Name=\"EditModeMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LowerBrightness8MenuItem, EditModeMenuItem, VerticesModeMenuItem,", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolbarTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ApplyToolbarShortcutTooltips();", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveButton, \"Save WAD\", \"window.save\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenMapButton, \"Open Map\", \"window.open-map-in-current-wad\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(VerticesModeButton, \"Vertices Mode\", \"map2d.mode-vertices\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyJitterButton, \"Randomize\", \"window.applyjitter\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyDirectionalShadingButton, \"Apply Directional Shading\", \"window.applydirectionalshading\");", body, StringComparison.Ordinal);
        Assert.Contains("EditorCommandCatalog.CommandToolTip(label, commandId, _shortcutBindings)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AddResourceUsesSharedRequiredArchiveDefaultsModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ConfigResourceDefaultsModel.ApplyRequiredArchiveDefaults(_config, resource);", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredArchiveDetector.Detect(_config, resource)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredArchiveDetector.RequiresTestExclusion(_config, requiredArchives)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveBackBlocksMapRenameTargetConflicts()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("if (!WadMaps.RenameMap(dst, _sourceMapMarker, marker))", body, StringComparison.Ordinal);
        Assert.Contains("Save blocked: target map {marker} already exists.", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ApplyMenuShortcutTooltips();", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveMenuItem, \"Save WAD\", \"window.save\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(VerticesModeMenuItem, \"Vertices\", \"map2d.mode-vertices\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(DrawSectorMenuItem, \"Draw Sector\", \"map2d.draw-sector\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FileMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"NewMapMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(NewMapMenuItem, \"New Map\", \"window.new-map\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenMapMenuItem, \"Open Map\", \"window.open-map-in-current-wad\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveAsMenuItem, \"Save WAD As\", \"window.save-map-as\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveAsFormatMenuItem, \"Save As Format\", \"window.save-as-format\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SettingsMenuItem, \"Preferences\", \"window.preferences\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAsPreflightsReadOnlyExistingTargets()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("FileSaveStamp.ExistingPathWriteBlockStatus(outPath)", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(writeBlockStatus);", body, StringComparison.Ordinal);
        Assert.Contains("System.IO.File.WriteAllBytes(outPath, bytes);", body, StringComparison.Ordinal);
        Assert.True(
            body.IndexOf("FileSaveStamp.ExistingPathWriteBlockStatus(outPath)", StringComparison.Ordinal)
            < body.IndexOf("System.IO.File.WriteAllBytes(outPath, bytes);", StringComparison.Ordinal));
    }

    [Fact]
    public void EditMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(CopyMenuItem, \"Copy selection\", \"window.copy\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SelectSimilarMenuItem, \"Select Similar Map Elements\", \"window.select-similar\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(LowerFloor8MenuItem, \"Lower Floor by 8 mp\", \"map2d.lower-floor-8\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(FitSelectedTexturesMenuItem, \"Fit Selected Textures\", \"map2d.fit-selected-textures\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyLightFogFlagMenuItem, \"Apply 'lightfog' Flag\", \"map2d.apply-lightfog-flag\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(InsertPrefabMenuItem, \"Insert Prefab File\", \"window.insert-prefab-file\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(GoToCoordinatesMenuItem, \"Go To Coordinates\", \"window.go-to-coordinates\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(InfoPanelMenuItem, \"Toggle Info Panel\", \"window.toggle-info-panel\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleSectorFillsMenuItem, \"Show Sector Fills\", \"map2d.toggle-sector-fills\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ModelRenderAllMenuItem, \"Model Rendering All\", \"window.model-render-all\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleDynamicGridSizeMenuItem, \"Dynamic Grid Size\", \"map2d.toggle-dynamic-grid-size\");", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RemainingViewMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"StatusHistoryMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BrowseWallTexturesMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GridSetupMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(StatusHistoryMenuItem, \"Status History\", \"window.status-history\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ErrorLogMenuItem, \"Show Errors and Warnings\", \"window.show-errors\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(BrowseThingsMenuItem, \"Browse Things\", \"window.browse-things\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ThingFilterMenuItem, \"Configure Things Filters\", \"window.things-filters-setup\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(FilterSelectedThingsMenuItem, \"Filter Selected Things\", \"window.filter-selected-things\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GridSetupMenuItem, \"Grid and Backdrop Setup\", \"window.grid-setup\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleSnapToGridMenuItem, \"Toggle grid snap\", \"map2d.toggle-grid-snap\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GridSizeUpMenuItem, \"Increase grid size\", \"map2d.grid-up\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolsMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"ApplyJitterMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ApplyDirectionalShadingMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(UdbScriptDockerMenuItem, \"Scripts\", \"window.udbscripts\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SoundPropagationMenuItem, \"Sound Propagation\", \"window.sound-propagation-mode\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GradientFloorHeightsMenuItem, \"Gradient Floor Heights\", \"window.gradient-floor-heights\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyJitterMenuItem, \"Randomize\", \"window.applyjitter\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyDirectionalShadingMenuItem, \"Apply Directional Shading\", \"window.applydirectionalshading\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleAutomapSecretLineMenuItem, \"Toggle Selected Line Secret\", \"window.toggle-automap-secret-line\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportObjectMenuItem, \"Export Object OBJ\", \"window.export-object\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportImageMenuItem, \"Export Image PNG\", \"window.export-image\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportWavefrontMenuItem, \"Export Wavefront OBJ\", \"window.export-wavefront\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportIdStudioMenuItem, \"Export idStudio\", \"window.export-idstudio\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SoundPropagationColorUpdatesRefreshActiveSoundEnvironmentOverlay()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("RefreshSoundEnvironmentColors();", body, StringComparison.Ordinal);
        Assert.Contains("_soundEnvironments?.SetModel(_soundEnvironmentModel);", body, StringComparison.Ordinal);
        Assert.Contains("MapView.SetSectorOverlayColors(_soundEnvironmentModel.SectorOverlayColors(_map.Sectors, _settings.SoundPropagationColors), 128);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"ShortcutsMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ShortcutsMenuItem, \"Shortcuts\", \"window.shortcuts\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(AboutMenuItem, \"About\", \"window.about\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void InfoPanelCheckedStateRefreshesFromPanelVisibility()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetChecked(InfoPanelMenuItem, InfoPanel.IsVisible);", body, StringComparison.Ordinal);
        Assert.Contains("case \"window.toggle-info-panel\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolbarModeAndDrawButtonsRefreshActiveState()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Button.toolbarButton.active", xaml, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(VerticesModeButton, verticesMode);", code, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(Toggle3DModeButton, MapView.In3DMode);", code, StringComparison.Ordinal);
        Assert.Contains("SetActiveClass(DrawSectorButton, drawSector);", code, StringComparison.Ordinal);
        Assert.Contains("control.Classes.Set(\"active\", active)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfMenuLabelMatchesDialogEditorToolSurface()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Equal("Dialog Editor...", UsdfDialogEditorModel.MenuItem.Text);
        Assert.Contains("Header=\"USDF _Dialog Editor...\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfWindowUsesDialogEditorDefaultDimensions()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/UsdfConversationWindow.cs"));

        Assert.Contains(": this(result, UsdfDialogEditorModel.DefaultWindowState, applyPosition: false)", body, StringComparison.Ordinal);
        Assert.Contains("Width = Math.Max(1, windowState.SizeWidth);", body, StringComparison.Ordinal);
        Assert.Contains("Height = Math.Max(1, windowState.SizeHeight);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfWindowAppliesDialogEditorWindowStateModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/UsdfConversationWindow.cs"));

        Assert.Contains("public UsdfConversationWindow(UsdfParseResult result, UsdfDialogEditorWindowState windowState)", body, StringComparison.Ordinal);
        Assert.Contains("WindowState = (Avalonia.Controls.WindowState)windowState.WindowState;", body, StringComparison.Ordinal);
        Assert.Contains("Position = new PixelPoint(windowState.PositionX, windowState.PositionY);", body, StringComparison.Ordinal);
        Assert.Contains("CurrentWindowState()", body, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfWindowStatePersistsThroughEditorSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("_settings.UsdfDialogEditorSettings", body, StringComparison.Ordinal);
        Assert.Contains("UsdfDialogEditorModel.ReadWindowState(", body, StringComparison.Ordinal);
        Assert.Contains("UsdfDialogEditorModel", body, StringComparison.Ordinal);
        Assert.Contains(".WriteWindowState(window.CurrentWindowState())", body, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(_usdfConversations, window)", body, StringComparison.Ordinal);
        Assert.Contains("SaveSettings();", body, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdfWindowUsesDialogEditorTreeModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/UsdfConversationWindow.cs"));

        Assert.Contains("private readonly TreeView _tree = new();", body, StringComparison.Ordinal);
        Assert.Contains("UsdfDialogEditorModel.BuildTree(result)", body, StringComparison.Ordinal);
        Assert.Contains("UsdfDialogEditorModel.TreeWidth", body, StringComparison.Ordinal);
        Assert.Contains("Tag = node.ImageKey", body, StringComparison.Ordinal);
    }
}
