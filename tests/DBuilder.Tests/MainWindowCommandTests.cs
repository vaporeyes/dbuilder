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
    [InlineData("window.reference-manual", "OnReferenceManual")]
    [InlineData("window.edit-mode-help", "OnEditModeHelp")]
    [InlineData("window.open-command-palette", "OnOpenCommandPalette")]
    [InlineData("window.opencommandpalette", "OnOpenCommandPalette")]
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
    [InlineData("window.gradientbrightness", "OnGradientBrightnessUdbAlias")]
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
    [InlineData("window.test-map-from-view", "OnTestMapFromView")]
    [InlineData("window.testmapfromview", "OnTestMapFromView")]
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

    [Fact]
    public void UdbGradientBrightnessAliasDispatchesByCurrentEditMode()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        int methodIndex = body.IndexOf("private void OnGradientBrightnessUdbAlias", StringComparison.Ordinal);
        int linedefsCheck = body.IndexOf("MapView.CurrentEditMode == MapControl.EditMode.Linedefs", methodIndex, StringComparison.Ordinal);
        int linedefHandler = body.IndexOf("OnGradientLinedefBrightness(sender, e);", methodIndex, StringComparison.Ordinal);
        int sectorHandler = body.IndexOf("OnGradientBrightness(sender, e);", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(linedefsCheck > methodIndex);
        Assert.True(linedefHandler > linedefsCheck);
        Assert.True(sectorHandler > linedefHandler);
    }

    [Fact]
    public void LoggedWorkflowFailuresUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus($\"{context}: {exception.Message}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("private void SetStatus(string text, StatusHistoryKind kind = StatusHistoryKind.Info)", body, StringComparison.Ordinal);
        Assert.Contains("_statusHistory.Add(text, kind);", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("window.classiccopyproperties", "OnCopyProperties")]
    [InlineData("window.classicpasteproperties", "OnPasteProperties")]
    [InlineData("window.classicpastepropertieswithoptions", "OnPastePropertiesWithOptions")]
    [InlineData("window.deleteitem", "OnDelete")]
    [InlineData("window.clearselection", "OnSelectNone")]
    [InlineData("window.selectsimilar", "OnSelectSimilar")]
    [InlineData("window.filterselectedthings", "OnFilterSelectedThings")]
    [InlineData("window.thingsfilterssetup", "OnThingFilter")]
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
    public void BlockmapExplorerCommandUsesUdbEngageChecks()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private void OnBlockmapExplorer", StringComparison.Ordinal);
        int rebuildIndex = body.IndexOf("_mapDirty ? RebuildCurrentMapBlockmapForExplorer() : ReadCurrentMapLump(\"BLOCKMAP\")", methodIndex, StringComparison.Ordinal);
        int dirtyFailureIndex = body.IndexOf("if (_mapDirty && bytes == null) return;", rebuildIndex, StringComparison.Ordinal);
        int decisionIndex = body.IndexOf("BlockmapExplorerModel.EngageDecision(bytes, blockmap)", dirtyFailureIndex, StringComparison.Ordinal);
        int cancelIndex = body.IndexOf("if (!decision.CanEngage)", decisionIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("SetStatus(decision.StatusText);", cancelIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private byte[]? RebuildCurrentMapBlockmapForExplorer()", statusIndex, StringComparison.Ordinal);
        int rebuildStatusIndex = body.IndexOf("BlockmapExplorerModel.DirtyMapRebuildStatusText()", helperIndex, StringComparison.Ordinal);
        int failureStatusIndex = body.IndexOf("BlockmapExplorerModel.NodeRebuildFailureStatusText()", rebuildStatusIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(rebuildIndex > methodIndex);
        Assert.True(dirtyFailureIndex > rebuildIndex);
        Assert.True(decisionIndex > dirtyFailureIndex);
        Assert.True(cancelIndex > decisionIndex);
        Assert.True(statusIndex > cancelIndex);
        Assert.True(helperIndex > statusIndex);
        Assert.True(rebuildStatusIndex > helperIndex);
        Assert.True(failureStatusIndex > rebuildStatusIndex);
    }

    [Fact]
    public void NodesViewerCommandUsesUdbEngageChecks()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private void OnNodesViewer", StringComparison.Ordinal);
        int readIndex = body.IndexOf("NodesViewerLumps lumps = ReadCurrentNodesViewerLumps();", methodIndex, StringComparison.Ordinal);
        int rebuildFlagIndex = body.IndexOf("bool rebuiltNodes = _mapDirty || !lumps.HasAnyNodes;", readIndex, StringComparison.Ordinal);
        int rebuildIndex = body.IndexOf("lumps = RebuildCurrentMapNodesForViewer();", rebuildFlagIndex, StringComparison.Ordinal);
        int rebuildFailureIndex = body.IndexOf("if (rebuiltNodes && !lumps.HasCompleteNodeSet)", rebuildIndex, StringComparison.Ordinal);
        int decisionIndex = body.IndexOf("NodesViewerModel.EngageDecision(", rebuildFailureIndex, StringComparison.Ordinal);
        int cancelIndex = body.IndexOf("if (!decision.CanEngage)", decisionIndex, StringComparison.Ordinal);
        int readFailureIndex = body.IndexOf("NodesViewerModel.ReadFailureStatusText()", cancelIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private NodesViewerLumps RebuildCurrentMapNodesForViewer()", readFailureIndex, StringComparison.Ordinal);
        int completeSetIndex = body.IndexOf("public bool HasCompleteNodeSet", helperIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(readIndex > methodIndex);
        Assert.True(rebuildFlagIndex > readIndex);
        Assert.True(rebuildIndex > rebuildFlagIndex);
        Assert.True(rebuildFailureIndex > rebuildIndex);
        Assert.True(decisionIndex > rebuildFailureIndex);
        Assert.True(cancelIndex > decisionIndex);
        Assert.True(readFailureIndex > cancelIndex);
        Assert.True(helperIndex > readFailureIndex);
        Assert.True(completeSetIndex > helperIndex);
    }

    [Fact]
    public void ObjTerrainImportActionExposesUdbSettingsBeforeParsing()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnImportObjTerrain", StringComparison.Ordinal);
        int dialogIndex = body.IndexOf("var dialog = new ObjTerrainImportDialog(_config?.VertexHeightSupport == true);", methodIndex, StringComparison.Ordinal);
        int showIndex = body.IndexOf("await dialog.ShowDialog<bool>(this)", dialogIndex, StringComparison.Ordinal);
        int parseIndex = body.IndexOf("ObjTerrainImporter.Parse(text, dialog.ResultScale, dialog.ResultUpAxis)", showIndex, StringComparison.Ordinal);
        int buildOptionsIndex = body.IndexOf("BuildObjTerrainImportOptions(dialog.ResultUseVertexHeights)", parseIndex, StringComparison.Ordinal);
        int optionsMethodIndex = body.IndexOf("private ObjTerrainImportOptions BuildObjTerrainImportOptions(bool useVertexHeights)", buildOptionsIndex, StringComparison.Ordinal);
        int useVertexHeightsIndex = body.IndexOf("UseVertexHeights: useVertexHeights", optionsMethodIndex, StringComparison.Ordinal);
        int createHeightThingsIndex = body.IndexOf("CreateVertexHeightThings: useVertexHeights && _mapFormat != MapFormat.Udmf", useVertexHeightsIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(dialogIndex > methodIndex);
        Assert.True(showIndex > dialogIndex);
        Assert.True(parseIndex > showIndex);
        Assert.True(buildOptionsIndex > parseIndex);
        Assert.True(optionsMethodIndex > buildOptionsIndex);
        Assert.True(useVertexHeightsIndex > optionsMethodIndex);
        Assert.True(createHeightThingsIndex > useVertexHeightsIndex);
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
        int buildIndex = body.IndexOf("BuildSectorHeightJitter(sector, _mapFormat == MapFormat.Udmf)", methodIndex, StringComparison.Ordinal);
        int floorIndex = body.IndexOf("BuilderEffects.ApplySectorFloorHeight(", buildIndex, StringComparison.Ordinal);
        int floorVertexHeightsIndex = body.IndexOf("dialog.ResultUseFloorVertexHeights", floorIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("BuilderEffects.ApplySectorCeilingHeight(", floorVertexHeightsIndex, StringComparison.Ordinal);
        int ceilingVertexHeightsIndex = body.IndexOf("dialog.ResultUseCeilingVertexHeights", ceilingIndex, StringComparison.Ordinal);
        int peggingIndex = body.IndexOf("BuilderEffects.ApplySectorPegging(", ceilingVertexHeightsIndex, StringComparison.Ordinal);
        int upperUnpeggedFlagIndex = body.IndexOf("_config?.UpperUnpeggedFlag", peggingIndex, StringComparison.Ordinal);
        int lowerUnpeggedFlagIndex = body.IndexOf("_config?.LowerUnpeggedFlag", upperUnpeggedFlagIndex, StringComparison.Ordinal);
        int upperUnpeggedResultIndex = body.IndexOf("dialog.ResultUpperUnpegged", lowerUnpeggedFlagIndex, StringComparison.Ordinal);
        int lowerUnpeggedResultIndex = body.IndexOf("dialog.ResultLowerUnpegged", upperUnpeggedResultIndex, StringComparison.Ordinal);
        int texturesIndex = body.IndexOf("BuilderEffects.ApplySectorHeightTextures(", lowerUnpeggedResultIndex, StringComparison.Ordinal);
        int upperTextureModeIndex = body.IndexOf("dialog.ResultUpperTextureMode", texturesIndex, StringComparison.Ordinal);
        int lowerTextureModeIndex = body.IndexOf("dialog.ResultLowerTextureMode", upperTextureModeIndex, StringComparison.Ordinal);
        int keepTexturesIndex = body.IndexOf("dialog.ResultKeepExistingSectorTextures", lowerTextureModeIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private static int JitterSectorSafeHeightDistance(Sector sector)", StringComparison.Ordinal);
        int formulaIndex = body.IndexOf("Math.Max(0, (sector.CeilHeight - sector.FloorHeight) / 2)", helperIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(buildIndex > methodIndex);
        Assert.True(floorIndex > buildIndex);
        Assert.True(floorVertexHeightsIndex > floorIndex);
        Assert.True(ceilingIndex > floorVertexHeightsIndex);
        Assert.True(ceilingVertexHeightsIndex > ceilingIndex);
        Assert.True(peggingIndex > ceilingVertexHeightsIndex);
        Assert.True(upperUnpeggedFlagIndex > peggingIndex);
        Assert.True(lowerUnpeggedFlagIndex > upperUnpeggedFlagIndex);
        Assert.True(upperUnpeggedResultIndex > lowerUnpeggedFlagIndex);
        Assert.True(lowerUnpeggedResultIndex > upperUnpeggedResultIndex);
        Assert.True(texturesIndex > lowerUnpeggedResultIndex);
        Assert.True(upperTextureModeIndex > texturesIndex);
        Assert.True(lowerTextureModeIndex > upperTextureModeIndex);
        Assert.True(keepTexturesIndex > lowerTextureModeIndex);
        Assert.True(helperIndex > ceilingIndex);
        Assert.True(formulaIndex > helperIndex);
    }

    [Fact]
    public void JitterActionUsesUdbVertexSafeDistanceClamp()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int buildIndex = body.IndexOf("BuildVertexJitter(vertices, _map)", methodIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private static List<VertexJitter> BuildVertexJitter(IReadOnlyCollection<Vertex> vertices, MapSet map)", StringComparison.Ordinal);
        int dictionaryIndex = body.IndexOf("new Dictionary<Vertex, int>(ReferenceEqualityComparer.Instance)", helperIndex, StringComparison.Ordinal);
        int initializationIndex = body.IndexOf("safeDistances[vertex] = 0;", dictionaryIndex, StringComparison.Ordinal);
        int lineScanIndex = body.IndexOf("foreach (Linedef line in map.Linedefs)", initializationIndex, StringComparison.Ordinal);
        int incidentIndex = body.IndexOf("if (vertex.Linedefs.Contains(line)) continue;", lineScanIndex, StringComparison.Ordinal);
        int safeDistanceToIndex = body.IndexOf("line.SafeDistanceToSq(vertex.Position, bounded: true)", incidentIndex, StringComparison.Ordinal);
        int nearestIndex = body.IndexOf("closestLine.NearestOnLine(vertex.Position)", safeDistanceToIndex, StringComparison.Ordinal);
        int startReduceIndex = body.IndexOf("ReduceVertexSafeDistance(safeDistances, closestLine.Start, distance);", nearestIndex, StringComparison.Ordinal);
        int endReduceIndex = body.IndexOf("ReduceVertexSafeDistance(safeDistances, closestLine.End, distance);", startReduceIndex, StringComparison.Ordinal);
        int setIndex = body.IndexOf("SetVertexSafeDistance(safeDistances, vertex, distance);", endReduceIndex, StringComparison.Ordinal);
        int clampIndex = body.IndexOf("SafeDistance: safeDistances[vertex] > 0 ? safeDistances[vertex] / 2 : 0", setIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(buildIndex > methodIndex);
        Assert.True(helperIndex > buildIndex);
        Assert.True(dictionaryIndex > helperIndex);
        Assert.True(initializationIndex > dictionaryIndex);
        Assert.True(lineScanIndex > initializationIndex);
        Assert.True(incidentIndex > lineScanIndex);
        Assert.True(safeDistanceToIndex > incidentIndex);
        Assert.True(nearestIndex > safeDistanceToIndex);
        Assert.True(startReduceIndex > nearestIndex);
        Assert.True(endReduceIndex > startReduceIndex);
        Assert.True(setIndex > endReduceIndex);
        Assert.True(clampIndex > setIndex);
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
    public void JitterActionUsesUdbThingSafeDistanceClamp()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int safeDistanceIndex = body.IndexOf("SafeDistance: JitterThingSafeDistance(thing, things)", methodIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private static int JitterThingSafeDistance(Thing thing, ICollection<Thing> things)", StringComparison.Ordinal);
        int nearestIndex = body.IndexOf("Thing? closest = MapSet.NearestThing(things, thing);", helperIndex, StringComparison.Ordinal);
        int fallbackIndex = body.IndexOf("? 512", nearestIndex, StringComparison.Ordinal);
        int distanceIndex = body.IndexOf("Vector2D.Distance(thing.Position, closest.Position)", fallbackIndex, StringComparison.Ordinal);
        int clampIndex = body.IndexOf("return distance > 0 ? distance / 2 : 0;", distanceIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(safeDistanceIndex > methodIndex);
        Assert.True(helperIndex > safeDistanceIndex);
        Assert.True(nearestIndex > helperIndex);
        Assert.True(fallbackIndex > nearestIndex);
        Assert.True(distanceIndex > fallbackIndex);
        Assert.True(clampIndex > distanceIndex);
    }

    [Fact]
    public void JitterActionAppliesThingPitchAndRollOnlyInUdmf()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        int methodIndex = body.IndexOf("private async void OnApplyJitter", StringComparison.Ordinal);
        int pitchFactorIndex = body.IndexOf("PitchFactor: RandomScaleFactor(dialog.ResultAllowNegativeThingPitch)", methodIndex, StringComparison.Ordinal);
        int rollFactorIndex = body.IndexOf("RollFactor: RandomScaleFactor(dialog.ResultAllowNegativeThingRoll)", pitchFactorIndex, StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_mapFormat == MapFormat.Udmf)", rollFactorIndex, StringComparison.Ordinal);
        int pitchIndex = body.IndexOf("BuilderEffects.ApplyThingPitch(thingJitter, dialog.ResultThingPitchAmount, dialog.ResultRelativeThingPitch)", guardIndex, StringComparison.Ordinal);
        int rollIndex = body.IndexOf("BuilderEffects.ApplyThingRoll(thingJitter, dialog.ResultThingRollAmount, dialog.ResultRelativeThingRoll)", pitchIndex, StringComparison.Ordinal);
        int factorMethodIndex = body.IndexOf("private static double RandomScaleFactor(bool allowNegative)", rollIndex, StringComparison.Ordinal);

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
        int scaleXFactorIndex = body.IndexOf("ScaleXFactor: RandomScaleFactor(dialog.ResultAllowNegativeThingScaleX)", methodIndex, StringComparison.Ordinal);
        int scaleYFactorIndex = body.IndexOf("ScaleYFactor: RandomScaleFactor(dialog.ResultAllowNegativeThingScaleY)", scaleXFactorIndex, StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_mapFormat == MapFormat.Udmf)", scaleYFactorIndex, StringComparison.Ordinal);
        int scaleIndex = body.IndexOf("BuilderEffects.ApplyThingScale(", guardIndex, StringComparison.Ordinal);
        int minXIndex = body.IndexOf("dialog.ResultThingScaleMinX", scaleIndex, StringComparison.Ordinal);
        int relativeIndex = body.IndexOf("dialog.ResultRelativeThingScale", minXIndex, StringComparison.Ordinal);
        int uniformIndex = body.IndexOf("dialog.ResultUniformThingScale", relativeIndex, StringComparison.Ordinal);
        int factorMethodIndex = body.IndexOf("private static double RandomScaleFactor(bool allowNegative)", uniformIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(scaleXFactorIndex > methodIndex);
        Assert.True(scaleYFactorIndex > scaleXFactorIndex);
        Assert.True(guardIndex > scaleYFactorIndex);
        Assert.True(scaleIndex > guardIndex);
        Assert.True(minXIndex > scaleIndex);
        Assert.True(relativeIndex > minXIndex);
        Assert.True(uniformIndex > relativeIndex);
        Assert.True(factorMethodIndex > uniformIndex);
    }

    [Fact]
    public void JitterDialogExposesUdbSectorOffsetModes()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("AddCombo(\"Floor offset mode\", FloorOffsetModeItems(), (int)ResultFloorOffsetMode)", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Ceiling offset mode\", CeilingOffsetModeItems(), (int)ResultCeilingOffsetMode)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use floor vertex heights\", ResultUseFloorVertexHeights)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use ceiling vertex heights\", ResultUseCeilingVertexHeights)", body, StringComparison.Ordinal);
        Assert.Contains("_useFloorVertexHeights.IsEnabled = vertexHeightsSupported;", body, StringComparison.Ordinal);
        Assert.Contains("_useCeilingVertexHeights.IsEnabled = vertexHeightsSupported;", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Upper Unpegged\", ResultUpperUnpegged)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Lower Unpegged\", ResultLowerUnpegged)", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Upper texture mode\", TextureModeItems(), (int)ResultUpperTextureMode)", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Lower texture mode\", TextureModeItems(), (int)ResultLowerTextureMode)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Upper texture\", ResultUpperTexture)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Lower texture\", ResultLowerTexture)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Don't change existing sidedef textures\", ResultKeepExistingSectorTextures)", body, StringComparison.Ordinal);
        Assert.Contains("ResultUpperUnpegged = _upperUnpegged.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultLowerUnpegged = _lowerUnpegged.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUpperTextureMode = (JitterSectorTextureMode)ComboNumber(_upperTextureMode, (int)ResultUpperTextureMode);", body, StringComparison.Ordinal);
        Assert.Contains("ResultLowerTextureMode = (JitterSectorTextureMode)ComboNumber(_lowerTextureMode, (int)ResultLowerTextureMode);", body, StringComparison.Ordinal);
        Assert.Contains("ResultKeepExistingSectorTextures = _keepExistingSectorTextures.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseAndLower, \"Raise and lower\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseOnly, \"Raise only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.LowerOnly, \"Lower only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.RaiseOnly, \"Lower only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterOffsetMode.LowerOnly, \"Raise only\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterSectorTextureMode.NoChange, \"No change\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterSectorTextureMode.SectorTexture, \"Use sector texture\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)JitterSectorTextureMode.CustomTexture, \"Use given texture\")", body, StringComparison.Ordinal);
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
        Assert.Contains("public bool ResultRelativeThingPitch { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultRelativeThingRoll { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultAllowNegativeThingPitch { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultAllowNegativeThingRoll { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing pitch amount\", ResultThingPitchAmount.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing roll amount\", ResultThingRollAmount.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Relative thing pitch\", ResultRelativeThingPitch)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Relative thing roll\", ResultRelativeThingRoll)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use negative pitch\", ResultAllowNegativeThingPitch)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use negative roll\", ResultAllowNegativeThingRoll)", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingPitchAmount = Math.Max(0, ParseInt(_thingPitchAmount, ResultThingPitchAmount));", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingRollAmount = Math.Max(0, ParseInt(_thingRollAmount, ResultThingRollAmount));", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeThingPitch = _relativeThingPitch.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeThingRoll = _relativeThingRoll.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultAllowNegativeThingPitch = _allowNegativeThingPitch.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultAllowNegativeThingRoll = _allowNegativeThingRoll.IsChecked == true;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterDialogExposesUdbThingScaleControls()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("public double ResultThingScaleMinX { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMaxX { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMinY { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultThingScaleMaxY { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultAllowNegativeThingScaleX { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultAllowNegativeThingScaleY { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale X min\", ResultThingScaleMinX.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale X max\", ResultThingScaleMaxX.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale Y min\", ResultThingScaleMinY.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Thing scale Y max\", ResultThingScaleMaxY.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Relative thing scale\", ResultRelativeThingScale)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Uniform thing scale\", ResultUniformThingScale)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use negative width scale\", ResultAllowNegativeThingScaleX)", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use negative height scale\", ResultAllowNegativeThingScaleY)", body, StringComparison.Ordinal);
        Assert.Contains("ResultThingScaleMinX = ParseDouble(_thingScaleMinX, ResultThingScaleMinX);", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeThingScale = _relativeThingScale.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUniformThingScale = _uniformThingScale.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultAllowNegativeThingScaleX = _allowNegativeThingScaleX.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("ResultAllowNegativeThingScaleY = _allowNegativeThingScaleY.IsChecked == true;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterDialogRemembersUdbCheckboxAndOffsetSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/JitterDialog.cs"));

        Assert.Contains("private static JitterOffsetMode s_floorOffsetMode = JitterOffsetMode.RaiseAndLower;", body, StringComparison.Ordinal);
        Assert.Contains("private static JitterOffsetMode s_ceilingOffsetMode = JitterOffsetMode.RaiseAndLower;", body, StringComparison.Ordinal);
        Assert.Contains("private static bool s_useFloorVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("private static bool s_useCeilingVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("private static bool s_relativeThingPitch;", body, StringComparison.Ordinal);
        Assert.Contains("private static bool s_allowNegativeThingScaleY;", body, StringComparison.Ordinal);
        Assert.Contains("ResultFloorOffsetMode = s_floorOffsetMode;", body, StringComparison.Ordinal);
        Assert.Contains("ResultCeilingOffsetMode = s_ceilingOffsetMode;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUseFloorVertexHeights = vertexHeightsSupported && s_useFloorVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUseCeilingVertexHeights = vertexHeightsSupported && s_useCeilingVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("ResultRelativeThingPitch = s_relativeThingPitch;", body, StringComparison.Ordinal);
        Assert.Contains("ResultAllowNegativeThingScaleY = s_allowNegativeThingScaleY;", body, StringComparison.Ordinal);
        Assert.Contains("s_floorOffsetMode = ResultFloorOffsetMode;", body, StringComparison.Ordinal);
        Assert.Contains("s_ceilingOffsetMode = ResultCeilingOffsetMode;", body, StringComparison.Ordinal);
        Assert.Contains("s_useFloorVertexHeights = ResultUseFloorVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("s_useCeilingVertexHeights = ResultUseCeilingVertexHeights;", body, StringComparison.Ordinal);
        Assert.Contains("s_relativeThingPitch = ResultRelativeThingPitch;", body, StringComparison.Ordinal);
        Assert.Contains("s_allowNegativeThingScaleY = ResultAllowNegativeThingScaleY;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalShadingDialogRemembersUdbSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/DirectionalShadingDialog.cs"));

        Assert.Contains("private static DirectionalShadingOptions s_options = new();", body, StringComparison.Ordinal);
        Assert.Contains("ResultOptions = s_options;", body, StringComparison.Ordinal);
        Assert.Contains("DirectionalShadingOptions defaults = ResultOptions;", body, StringComparison.Ordinal);
        Assert.Contains("s_options = ResultOptions;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjTerrainImportDialogExposesUdbSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ObjTerrainImportDialog.cs"));

        Assert.Contains("public ObjTerrainUpAxis ResultUpAxis { get; private set; } = ObjTerrainUpAxis.Y;", body, StringComparison.Ordinal);
        Assert.Contains("public double ResultScale { get; private set; } = 1.0;", body, StringComparison.Ordinal);
        Assert.Contains("public bool ResultUseVertexHeights { get; private set; }", body, StringComparison.Ordinal);
        Assert.Contains("ResultUseVertexHeights = vertexHeightsSupported;", body, StringComparison.Ordinal);
        Assert.Contains("AddCombo(\"Up axis\", UpAxisItems(), (int)ResultUpAxis)", body, StringComparison.Ordinal);
        Assert.Contains("AddField(\"Scale\", ResultScale.ToString(CultureInfo.InvariantCulture))", body, StringComparison.Ordinal);
        Assert.Contains("AddCheckBox(\"Use vertex heights\", ResultUseVertexHeights)", body, StringComparison.Ordinal);
        Assert.Contains("_useVertexHeights.IsEnabled = vertexHeightsSupported;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUpAxis = (ObjTerrainUpAxis)ComboNumber(_upAxis, (int)ResultUpAxis);", body, StringComparison.Ordinal);
        Assert.Contains("ResultScale = scale == 0.0 ? ResultScale : scale;", body, StringComparison.Ordinal);
        Assert.Contains("ResultUseVertexHeights = _vertexHeightsSupported && _useVertexHeights.IsChecked == true;", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)ObjTerrainUpAxis.Y, \"Y\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)ObjTerrainUpAxis.Z, \"Z\")", body, StringComparison.Ordinal);
        Assert.Contains("new CatalogItem((int)ObjTerrainUpAxis.X, \"X\")", body, StringComparison.Ordinal);
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
    public void RejectExplorerLaunchUsesUdbEngageDecision()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        int methodIndex = body.IndexOf("private void OnRejectViewer", StringComparison.Ordinal);
        int readIndex = body.IndexOf("byte[]? bytes = ReadCurrentMapLump(\"REJECT\");", methodIndex, StringComparison.Ordinal);
        int directWadIndex = body.IndexOf("WadMaps.ReadMapLump(wad, _mapMarker, \"REJECT\")", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(readIndex > methodIndex);
        Assert.Equal(-1, directWadIndex);
        Assert.Contains("RejectExplorerEngageDecision decision = RejectExplorerModel.EngageDecision(validation);", body, StringComparison.Ordinal);
        Assert.Contains("if (!decision.CanEngage)", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"{decision.Title}: {decision.Message}\", decision.IsWarning ? StatusHistoryKind.Warning : StatusHistoryKind.Info);", body, StringComparison.Ordinal);
        Assert.Contains("if (decision.IsWarning) SetStatus($\"{decision.Title}: {decision.Message}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
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

        Assert.Contains("bool canSave = hasMap && (_wadPath is null || FileSaveStamp.CanWriteSourcePath(_wadPath, _sourceWadStamp));", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canSave, SaveMenuItem, SaveButton);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void EditMenuCopyPasteAvailabilityReflectsUdbModeLevelRule()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool canUseCopyPaste = hasMap;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canUseCopyPaste, CutMenuItem, CopyMenuItem, PasteMenuItem, PasteSpecialMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelection,\n            DuplicateMenuItem, DeleteMenuItem, SelectNoneMenuItem,", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEnabled(hasSelection,\n            CutMenuItem, CopyMenuItem", body, StringComparison.Ordinal);
    }

    [Fact]
    public void EditMenuVisibilityReflectsLoadedMapLikeUdb()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("Header=\"_Edit\" x:Name=\"EditMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("EditMenuItem.IsVisible = hasMap;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuSeparatorsAreNormalizedAfterCommandAvailabilityLikeUdb()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("<Menu Grid.Row=\"0\" x:Name=\"MainMenu\">", xaml, StringComparison.Ordinal);
        Assert.Contains("UpdateCommandCheckedState();\n        UpdateMenuSeparators(MainMenu);", code, StringComparison.Ordinal);
        Assert.Contains("private static void UpdateMenuSeparators(ItemsControl items)", code, StringComparison.Ordinal);
        Assert.Contains("if (item is MenuItem child)\n                UpdateMenuSeparators(child);", code, StringComparison.Ordinal);
        Assert.Contains("separator.IsVisible = false;", code, StringComparison.Ordinal);
        Assert.Contains("pendingSeparator.IsVisible = true;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorPickerCommandAvailabilityReflectsMapFormatAndSelection()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("bool canEditSectorColor = ColorPickerModel.CanEditSectorColors(_mapFormat == MapFormat.Udmf) && hasSelectedSector;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canEditSectorColor, SectorColorMenuItem, SectorColorButton);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedInternalDynamicLight, DynamicLightColorMenuItem, DynamicLightColorButton);", body, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ToggleLightPanelMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleLightPanelMenuItem, \"Open Color Picker\", \"window.togglelightpannel\");", body, StringComparison.Ordinal);
        Assert.Contains("SectorColorMenuItem, DynamicLightColorMenuItem, ToggleLightPanelMenuItem, TagRangeMenuItem", body, StringComparison.Ordinal);
        Assert.Contains("private void OnToggleLightPanel(object? sender, RoutedEventArgs e)", body, StringComparison.Ordinal);
        Assert.Contains("ColorPickerModel.ToggleLightPanelDecision(ColorPickerToggleContext())", body, StringComparison.Ordinal);
        Assert.Contains("private ColorPickerToggleContext ColorPickerToggleContext()", body, StringComparison.Ordinal);
        Assert.Contains("private ColorPickerToggleMode ColorPickerModeForCurrentEditorState()", body, StringComparison.Ordinal);
        Assert.Contains("SelectedVisualThings: MapView.SelectedVisualThingsForActions().Count", body, StringComparison.Ordinal);
        Assert.Contains("SelectedVisualSectors: MapView.SelectedVisualSurfacesForActions().Count", body, StringComparison.Ordinal);
        Assert.Contains("OnDynamicLightColor(sender, e);", body, StringComparison.Ordinal);
        Assert.Contains("OnSectorColor(sender, e);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(ColorPickerModel.SectorColorsRequireUdmfWarning, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(ColorPickerModel.NoSelectedSectorsWarning, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(ColorPickerModel.NoDynamicLightsWarning, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(decision.WarningText, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TagRangeCommandAvailabilityReflectsTaggableSelection()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasTagRangeSelection = TagRangeModel.HasSelection(", body, StringComparison.Ordinal);
        Assert.Contains("(_map?.SelectedSectorsCount ?? 0) + (_map?.SelectedLinedefsCount ?? 0) + (_map?.SelectedThingsCount ?? 0));", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasTagRangeSelection, TagRangeMenuItem, TagRangeButton);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(TagRangeModel.NoSelectionWarning, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(TagRangeModel.EmptySelectionStatus(dlg.ResultTarget), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(TagRangeModel.OutOfTagsStatus(result.Tags.Count), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewCommandAvailabilityReflectsMapResourcesAndConfigState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasResources = _resources is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseCatalogs = hasMap && _config is not null;", body, StringComparison.Ordinal);
        Assert.Contains("bool canBrowseAny = hasResources || canBrowseCatalogs;", body, StringComparison.Ordinal);
        Assert.Contains("bool canFilterThingCategories = hasMap && _config is { Things.Count: > 0 };", body, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ThingFilterCategoryChoice> cats = ThingFilterWindow.CategoryChoices(_config);", body, StringComparison.Ordinal);
        Assert.Contains("GridSetupMenuItem, SmartGridTransformMenuItem, AlignGridToLinedefMenuItem, SetGridOriginToVertexMenuItem,", body, StringComparison.Ordinal);
        Assert.Contains("ResetGridTransformMenuItem, ToggleSnapToGridMenuItem, ToggleDynamicGridSizeMenuItem, GridSizeDownMenuItem, GridSizeUpMenuItem", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canFilterThingCategories, ThingFilterMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseAny, BrowsersMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasResources, BrowseWallTexturesMenuItem, BrowseFlatsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(canBrowseCatalogs, BrowseThingsMenuItem, BrowseLinedefActionsMenuItem, BrowseSectorEffectsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedSector && hasResources, BrowseFloorFlatsMenuItem, BrowseCeilingFlatsMenuItem);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterSelectedThingsAvailabilityReflectsThingsModeLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasSelectedThingInThingsMode = hasSelectedThing && MapView.CurrentEditMode == MapControl.EditMode.Things;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedThingInThingsMode, AlignThingsToWallMenuItem);\n        SetEnabled(hasSelectedThingInThingsMode, FilterSelectedThingsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("if (MapView.CurrentEditMode != MapControl.EditMode.Things)", body, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<int, int> selectedTypeCounts = ThingSelectionFilter.SelectedTypeCounts(_map);", body, StringComparison.Ordinal);
        Assert.Contains("var dlg = new FilterSelectedThingsDialog(selectedTypeCounts, _config);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Filter Selected Things is only available in Things mode.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("\"window.filter-selected-things\" or \"window.filterselectedthings\" => FilterSelectedThingsMenuItem", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AlignThingsToWallAvailabilityReflectsThingsModeLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("bool hasSelectedThingInThingsMode = hasSelectedThing && MapView.CurrentEditMode == MapControl.EditMode.Things;", body, StringComparison.Ordinal);
        Assert.Contains("SetEnabled(hasSelectedThingInThingsMode, AlignThingsToWallMenuItem);\n        SetEnabled(hasSelectedThingInThingsMode, FilterSelectedThingsMenuItem);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Align Things to Wall is only available in Things mode.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("\"window.align-things-to-wall\" => AlignThingsToWallMenuItem", body, StringComparison.Ordinal);
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
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("ApplyToolbarShortcutTooltips();", body, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenWadButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TestMapFromViewButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenWadButton, \"Open WAD\", \"window.open-map\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SaveButton, \"Save WAD\", \"window.save\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(OpenMapButton, \"Open Map\", \"window.open-map-in-current-wad\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(TestMapFromViewButton, \"Test Map from Current Position\", \"window.testmapfromview\");", body, StringComparison.Ordinal);
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

        Assert.Contains("if (!WadMaps.RenameMap(dst, _sourceMapMarker, marker, _config))", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Save blocked: target map {marker} is unavailable.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
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
        Assert.Contains("SetStatus(writeBlockStatus, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("System.IO.File.WriteAllBytes(outPath, bytes);", body, StringComparison.Ordinal);
        Assert.True(
            body.IndexOf("FileSaveStamp.ExistingPathWriteBlockStatus(outPath)", StringComparison.Ordinal)
            < body.IndexOf("System.IO.File.WriteAllBytes(outPath, bytes);", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveBackSourceBlocksUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Save blocked: the source WAD changed on disk. Reload the map or use Save WAD As.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Save blocked: the source WAD is read-only. Use Save WAD As or clear the read-only flag.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndMapOptionsGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Nothing to save.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No map loaded.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBlocksUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(selection.Warning ?? \"Image export failed.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Image export blocked: \" + string.Join(\" \", errors), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(message.Message, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(preflight.Warning ?? \"Object export failed.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Object export blocked: \" + string.Join(\" \", errors), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Object export blocked: load resources before exporting textures.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Object export failed: no geometry was generated.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"idStudio export blocked: \" + string.Join(\" \", errors), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"idStudio export blocked: load resources before exporting textures.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(preflight.Warning ?? \"OBJ export failed.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Wavefront export blocked: \" + string.Join(\" \", errors), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Wavefront export blocked: load resources before exporting textures.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Wavefront export failed: no geometry was generated.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentMapLoadFailuresUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus($\"File not found: {map.Path}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"No map found in {System.IO.Path.GetFileName(path)}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Recent map not found: {preferredMapName} in {System.IO.Path.GetFileName(path)}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Recent map not found: {RecentMapHeader(recentMap)}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenMapGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Open a WAD or PK3 first.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No maps in this PK3 match the active game configuration.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No maps in this WAD.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AutosaveRecoveryGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"No autosave snapshots found.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Autosave contains no recoverable map: {autosave.DisplayName}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Failed to recover autosave map: {autosave.DisplayName}\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceCatalogGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Open a WAD first.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No resources loaded for textures.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No resources loaded for flats.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Open a WAD map to reload resources.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select one or more sectors before applying flats.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No game configuration loaded.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PropertyEditGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"This action requires highlight or selection!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select exactly one vertex, linedef, sidedef, sector or thing to edit properties.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select exactly one linedef, sidedef, sector or thing to edit flags.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select exactly one vertex, linedef, sidedef, sector or thing to edit custom fields.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionAndIndexGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"This action requires a selection!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"No similar {MapView.CurrentEditMode.ToString().ToLowerInvariant()} found.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"This action requires a selection!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(error, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Index must be between 0 and \" + target.MaxIndex + \".\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ClipboardEditGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"No map loaded.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Nothing selected to cut.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(options.StatusMessage ?? PastePropertiesOptionsModel.NoCopiedPropertiesMessage, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Nothing selected to duplicate.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Nothing selected to delete.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TransformGradientAndGroupGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Select elements before adding them to a group.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Group {groupIndex + 1} is empty.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Flat alignment to linedefs is only available for UDMF maps.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select elements to transform first.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select at least 3 sectors first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Linedef brightness gradients are only available for UDMF maps.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select at least 3 linedefs first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PrefabGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Select something to save as a prefab.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Open a map first.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No previous prefab file available.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomapSlopeAndStairGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Select one or more sectors to toggle textured automap visibility.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Select one or more linedefs to toggle {label}.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select one or more sectors to slope-arch.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Selected sectors have no linedef bounds.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Selected sectors need horizontal span for slope arch.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select 2 or more sectors to build stairs.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void JitterAndDirectionalShadingGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Select some things, sectors or surfaces first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select some things, sectors, linedefs or vertices first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"This action is available only in UDMF map format!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select some floor or wall surfaces first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select some sectors or linedefs first!\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPanelGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"No Tag Explorer entries to export.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Tag Explorer entry no longer exists.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Select one or more {MapView.CurrentEditMode.ToString().ToLowerInvariant()} first.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Load a game config to filter thing categories.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SoundAndDoorGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Select one sector to trace sound propagation, or two sectors to find a sound leak path.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Sound can not travel between the two selected sectors.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("path == null ? StatusHistoryKind.Warning : StatusHistoryKind.Info", body, StringComparison.Ordinal);
        Assert.Contains("startMarker ? \"Select one sector to set the sound leak start.\" : \"Select one sector to set the sound leak end.\",\n                StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Select one or more sectors to make doors.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Choose a door texture before making a door.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeAndVisplaneGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"Nodes overlay needs the source WAD.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Nodes overlay unavailable: {structure.Status}.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Nodes Viewer needs a map marker.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(NodesViewerModel.NodeRebuildFailureStatusText(), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(decision.StatusText, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(NodesViewerModel.ReadFailureStatusText(), StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Visplane Explorer supports Doom and Hexen map formats.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus($\"Visplane Explorer unavailable: {preflight.Message}.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void EditMenuTooltipsRefreshFromEffectiveShortcutBindings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetShortcutToolTip(CopyMenuItem, \"Copy selection\", \"window.copy\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SelectSimilarMenuItem, \"Select Similar Map Elements\", \"window.select-similar\");", body, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SnapSelectionToGridMenuItem, \"Snap Selected Map Elements to Grid\", \"window.snap-selection-to-grid\");", body, StringComparison.Ordinal);
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
        string palette = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/CommandPaletteWindow.cs"));

        Assert.Contains("x:Name=\"ApplyJitterMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ApplyDirectionalShadingMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(UdbScriptDockerMenuItem, \"Scripts\", \"window.udbscripts\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SoundPropagationMenuItem, \"Sound Propagation\", \"window.sound-propagation-mode\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SetLeakFinderStartMenuItem, \"Set leak finder start sector\", \"window.setleakfinderstart\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(SetLeakFinderEndMenuItem, \"Set leak finder end sector\", \"window.setleakfinderend\");", code, StringComparison.Ordinal);
        Assert.Contains("SoundPropagationMenuItem, SetLeakFinderStartMenuItem, SetLeakFinderEndMenuItem, SoundEnvironmentsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(GradientFloorHeightsMenuItem, \"Gradient Floor Heights\", \"window.gradient-floor-heights\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyJitterMenuItem, \"Randomize\", \"window.applyjitter\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ApplyDirectionalShadingMenuItem, \"Apply Directional Shading\", \"window.applydirectionalshading\");", code, StringComparison.Ordinal);
        Assert.Contains("ApplyJitterMenuItem, ApplyDirectionalShadingMenuItem, ApplySlopeArchMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("ApplyJitterButton, ApplyDirectionalShadingButton, ApplySlopeArchButton", code, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CommandPaletteMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(CommandPaletteMenuItem, \"Open Command Palette\", \"window.open-command-palette\");", code, StringComparison.Ordinal);
        Assert.Contains("case \"window.open-command-palette\": OnOpenCommandPalette(this, new RoutedEventArgs()); return true;", code, StringComparison.Ordinal);
        Assert.Contains("case \"window.opencommandpalette\": OnOpenCommandPalette(this, new RoutedEventArgs()); return true;", code, StringComparison.Ordinal);
        Assert.Contains("new CommandPaletteWindow(", code, StringComparison.Ordinal);
        Assert.Contains("_recentCommandPaletteCommands", code, StringComparison.Ordinal);
        Assert.Contains("RunCommandFromPalette", code, StringComparison.Ordinal);
        Assert.Contains("CommandPaletteModel.BuildGroups", palette, StringComparison.Ordinal);
        Assert.Contains("Watermark = \"Search commands\"", palette, StringComparison.Ordinal);
        Assert.Contains("Text = \"No results found\"", palette, StringComparison.Ordinal);
        Assert.Contains("Key.Enter", palette, StringComparison.Ordinal);
        Assert.Contains("Key.Down", palette, StringComparison.Ordinal);
        Assert.Contains("Key.Up", palette, StringComparison.Ordinal);
        Assert.Contains("Key.PageDown", palette, StringComparison.Ordinal);
        Assert.Contains("Key.PageUp", palette, StringComparison.Ordinal);
        Assert.Contains("MoveSelection(CommandPaletteModel.MaxItems - 1, wrap: false)", palette, StringComparison.Ordinal);
        Assert.Contains("MoveSelection(-CommandPaletteModel.MaxItems + 1, wrap: false)", palette, StringComparison.Ordinal);
        Assert.Contains("SelectedUsableRow", palette, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ToggleAutomapSecretLineMenuItem, \"Toggle Selected Line Secret\", \"window.toggle-automap-secret-line\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportObjectMenuItem, \"Export Object OBJ\", \"window.export-object\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportImageMenuItem, \"Export Image PNG\", \"window.export-image\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportWavefrontMenuItem, \"Export Wavefront OBJ\", \"window.export-wavefront\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ExportIdStudioMenuItem, \"Export idStudio\", \"window.export-idstudio\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandPaletteUsabilityFollowsMenuAvailabilityWhenAvailable()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains(".Where(IsPaletteCommandUsable)", code, StringComparison.Ordinal);
        Assert.Contains("if (command.Scope != EditorCommandScope.Window) return false;", code, StringComparison.Ordinal);
        Assert.Contains("return PaletteCommandControl(command.Id) is not { } control || PaletteControlIsUsable(control);", code, StringComparison.Ordinal);
        Assert.Contains("private static bool PaletteControlIsUsable(Control control)", code, StringComparison.Ordinal);
        Assert.Contains("for (Control? current = control; current is not null; current = current.Parent as Control)", code, StringComparison.Ordinal);
        Assert.Contains("if (!current.IsEnabled || !current.IsVisible) return false;", code, StringComparison.Ordinal);
        Assert.Contains("if (IsSelectionGroupCommand(commandId)) return SelectionGroupsMenu;", code, StringComparison.Ordinal);
        Assert.Contains("if (IsUdbScriptPaletteCommand(commandId)) return UdbScriptDockerMenuItem;", code, StringComparison.Ordinal);
        Assert.Contains("if (commandId == \"window.cancel-draw\") return DrawMenuItem;", code, StringComparison.Ordinal);
        Assert.Contains("commandId.StartsWith(\"window.select-group-\", StringComparison.Ordinal)", code, StringComparison.Ordinal);
        Assert.Contains("commandId.StartsWith(\"window.assign-group-\", StringComparison.Ordinal)", code, StringComparison.Ordinal);
        Assert.Contains("commandId.StartsWith(\"window.clear-group-\", StringComparison.Ordinal)", code, StringComparison.Ordinal);
        Assert.Contains("commandId == \"window.udbscriptexecute\"", code, StringComparison.Ordinal);
        Assert.Contains("commandId.StartsWith(\"window.udbscriptexecuteslot\", StringComparison.Ordinal)", code, StringComparison.Ordinal);
        Assert.Contains("\"window.undo\" => UndoMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.redo\" => RedoMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.new-map\" => NewMapMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.open-map\" => OpenWadMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.open-map-in-current-wad\" => OpenMapMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.reload-map\" => ReloadMapMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.save\" or \"window.save-map\" => SaveMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.map-options\" => MapOptionsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.snap-selection-to-grid\" => SnapSelectionToGridMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.game-configurations\" => LoadGameConfigMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.exit\" => ExitMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.show-errors\" => ErrorLogMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.model-render-all\" => ModelRenderAllMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.next-model-render-mode\" => NextModelRenderModeMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-3d-floors\" => Toggle3DFloorsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-blockmap\" => ToggleBlockmapMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-info-panel\" => InfoPanelMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.delete\" or \"window.deleteitem\" => DeleteMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.properties\" => PropertiesMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.moveselectionup\" or \"window.moveselectiondown\" or \"window.moveselectionleft\" or \"window.moveselectionright\" => TransformSelectionMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.tag-range\" or \"window.rangetagselection\" => TagRangeMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.sector-color\" => SectorColorMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.dynamic-light-color\" => DynamicLightColorMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.togglelightpannel\" => ToggleLightPanelMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.browse-floor-flats\" => BrowseFloorFlatsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.blockmap-explorer\" or \"window.blockmapexplorermode\" => BlockmapExplorerMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.nodes-viewer\" or \"window.nodesviewermode\" => NodesViewerMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.sound-propagation-mode\" or \"window.soundpropagationmode\" => SoundPropagationMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.setleakfinderstart\" => SetLeakFinderStartMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.setleakfinderend\" => SetLeakFinderEndMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.sound-environment-mode\" or \"window.soundenvironmentmode\" => SoundEnvironmentsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.applyjitter\" => ApplyJitterMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.applydirectionalshading\" => ApplyDirectionalShadingMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.create-prefab\" => SavePrefabMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.insert-prefab-file\" => InsertPrefabMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.insert-previous-prefab\" => InsertPreviousPrefabMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.apply-slope-arch\" => ApplySlopeArchMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.gradient-floor-heights\" or \"window.gradientfloors\" => GradientFloorHeightsMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.gradient-linedef-brightness\" => GradientLinedefBrightnessMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.gradient-interpolation-linear\" => GradientInterpolationLinearMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-automap-secret-line\" => ToggleAutomapSecretLineMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-automap-textured-hidden-sector\" => ToggleAutomapTexturedHiddenSectorMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.toggle-auto-clear-sidedef-textures\" => AutoClearSidedefTexturesMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.undo-redo-panel\" => UndoRedoPanelMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.test-map-from-view\" or \"window.testmapfromview\" => TestMapFromViewMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.things-filters-setup\" or \"window.thingsfilterssetup\" => ThingFilterMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.open-command-palette\" or \"window.opencommandpalette\" => CommandPaletteMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.reference-manual\" => ReferenceManualMenuItem", code, StringComparison.Ordinal);
        Assert.Contains("\"window.edit-mode-help\" => EditModeHelpMenuItem", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TestMapPassesTemporaryWadToPreAndPostCommands()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("System.IO.File.WriteAllBytes(temp, bytes);", body, StringComparison.Ordinal);
        Assert.Contains("ExternalCommand.Run(preCommand, \"Before test map\", temp)", body, StringComparison.Ordinal);
        Assert.Contains("ExternalCommand.Run(postCommand, \"After test map\", temp)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TestMapGuardsUseWarningStatusKind()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("SetStatus(\"No map loaded to test.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"Set a source port in Settings (or DBUILDER_TESTPORT) to use Test Map.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(\"No IWAD for testing - set one in Settings/DBUILDER_TESTIWAD, or open/add an IWAD.\", StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(placement.Message, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(preResult.Message, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
        Assert.Contains("SetStatus(postResult.Message, StatusHistoryKind.Warning);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TestMapPassesAdditionalParametersToLaunchArguments()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("testMonsters: _settings.TestMonsters", body, StringComparison.Ordinal);
        Assert.Contains("skill: _settings.NormalizedTestSkill", body, StringComparison.Ordinal);
        Assert.Contains("additionalParameters: _settings.TestAdditionalParameters", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TestMapMenuExposesSkillSelectionEntries()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml"));

        Assert.Contains("x:Name=\"TestMapFromViewMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(TestMapFromViewMenuItem, \"Test map from current position\", \"window.testmapfromview\");", body, StringComparison.Ordinal);
        Assert.Contains("TestMapMenuItem, TestMapFromViewMenuItem, SoundPropagationMenuItem", body, StringComparison.Ordinal);
        Assert.Contains("TestMapButton, TestMapFromViewButton, BuildBridgeButton", body, StringComparison.Ordinal);
        Assert.Contains("private void RebuildTestMapMenu()", body, StringComparison.Ordinal);
        Assert.Contains("TestMapMenuModel.Build(skills, _settings.NormalizedTestSkill, _settings.TestMonsters)", body, StringComparison.Ordinal);
        Assert.Contains("ToggleType = MenuItemToggleType.CheckBox", body, StringComparison.Ordinal);
        Assert.Contains("IsChecked = entry.Checked", body, StringComparison.Ordinal);
        Assert.Contains("TestMapMenuItem.ItemsSource = items;", body, StringComparison.Ordinal);
        Assert.Contains("_settings.TestSkill = TestMapMenuModel.SelectedSkillFromEntry(entry);", body, StringComparison.Ordinal);
        Assert.Contains("_settings.TestMonsters = TestMapMenuModel.TestMonstersFromEntry(entry);", body, StringComparison.Ordinal);
        Assert.Contains("TestMap(testFromCurrentPosition: false);", body, StringComparison.Ordinal);
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
        Assert.Contains("x:Name=\"ReferenceManualMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EditModeHelpMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ReferenceManualMenuItem, \"Reference Manual\", \"window.reference-manual\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(EditModeHelpMenuItem, \"About This Editing Mode\", \"window.edit-mode-help\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(ShortcutsMenuItem, \"Shortcuts\", \"window.shortcuts\");", code, StringComparison.Ordinal);
        Assert.Contains("SetShortcutToolTip(AboutMenuItem, \"About\", \"window.about\");", code, StringComparison.Ordinal);
        Assert.Contains("case \"window.reference-manual\": OnReferenceManual(this, new RoutedEventArgs()); return true;", code, StringComparison.Ordinal);
        Assert.Contains("case \"window.edit-mode-help\": OnEditModeHelp(this, new RoutedEventArgs()); return true;", code, StringComparison.Ordinal);
        Assert.Contains("EditModeHelpMenuItem,", code, StringComparison.Ordinal);
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
