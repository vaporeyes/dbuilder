// ABOUTME: Tests MapControl command dispatch metadata without constructing the Avalonia control.
// ABOUTME: Keeps stable 2D and 3D command ids wired to map editing handlers.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class MapControlCommandTests
{
    [Theory]
    [InlineData("STARTAN3", 1, "applied texture STARTAN3 to 1 surface")]
    [InlineData("BRICK1", 2, "applied texture BRICK1 to 2 surfaces")]
    public void TextureApplied3DStatusTextFormatsSingularAndPluralSurfaceCounts(string textureName, int surfaceCount, string expected)
        => Assert.Equal(expected, MapControl.TextureApplied3DStatusText(textureName, surfaceCount));

    [Theory]
    [InlineData(1, "1 surface selected")]
    [InlineData(2, "2 surfaces selected")]
    public void SurfaceSelection3DStatusTextFormatsSingularAndPluralSurfaceCounts(int surfaceCount, string expected)
        => Assert.Equal(expected, MapControl.SurfaceSelection3DStatusText(surfaceCount));

    [Theory]
    [InlineData(0, "Thing visibility is now OFF.")]
    [InlineData(1, "Thing visibility is now SPRITE ONLY.")]
    [InlineData(2, "Thing visibility is now ON.")]
    [InlineData(3, "Thing visibility is now ON.")]
    public void VisualThingVisibilityStatusTextMatchesUdbStates(int state, string expected)
        => Assert.Equal(expected, MapControl.VisualThingVisibilityStatusText(state));

    [Theory]
    [InlineData("Copied", 1, "Copied 1 thing.")]
    [InlineData("Copied", 2, "Copied 2 things.")]
    [InlineData("Cut", 1, "Cut 1 thing.")]
    [InlineData("Pasted", 3, "Pasted 3 things.")]
    public void VisualThingSelectionStatusTextMatchesUdbCopyPasteText(string verb, int count, string expected)
        => Assert.Equal(expected, MapControl.VisualThingSelectionStatusText(verb, count));

    [Fact]
    public void VisualThingInsertedStatusTextMatchesUdb()
        => Assert.Equal("Inserted a new thing.", MapControl.VisualThingInsertedStatusText());

    [Theory]
    [InlineData("map2d.mode-automap", "ToggleAutomapMode")]
    [InlineData("map2d.split-linedefs", "SplitLinedefs")]
    [InlineData("map2d.fit-selected-textures", "FitSelectedTextures")]
    [InlineData("map2d.3dfloor.select-control-sector", "SelectThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.relocate-control-sectors", "RelocateThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.duplicate-geometry", "DuplicateThreeDFloorGeometry")]
    [InlineData("map2d.select-sectors-outline", "SelectSectorsOutline")]
    public void MapCommandsAreRoutedThroughMapCommandDispatch(string commandId, string handlerName)
    {
        Type type = typeof(MapControl);
        MethodInfo? dispatcher = type.GetMethod("RunMapCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(
            handlerName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        Assert.Contains($"case \"{commandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"{handlerName}();", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RelocateThreeDFloorControlSectorsUsesInjectedAreaSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("ThreeDFloors.RelocateManagedControlSectors(_map, ThreeDFloorControlSectorAreaSettings)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ThreeDFloors.RelocateManagedControlSectors(_map, new ThreeDFloorControlSectorAreaSettings())", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualScale3DCommandsAreLimitedToUdmf()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ChangeVisualScale3D(int incrementX, int incrementY)", StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (_mapFormat != MapFormat.Udmf) return;", methodIndex, StringComparison.Ordinal);
        int flatScaleIndex = body.IndexOf("VisualScaleAdjustment.AdjustFlat", methodIndex, StringComparison.Ordinal);
        int thingScaleIndex = body.IndexOf("VisualScaleAdjustment.AdjustThing", methodIndex, StringComparison.Ordinal);
        int wallScaleIndex = body.IndexOf("VisualScaleAdjustment.AdjustWall", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(guardIndex > methodIndex);
        Assert.True(flatScaleIndex > guardIndex);
        Assert.True(thingScaleIndex > guardIndex);
        Assert.True(wallScaleIndex > guardIndex);
    }

    [Fact]
    public void VisualRotation3DCommandsUseVisualTargetRotation()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int clockwiseCase = body.IndexOf("case \"map3d.rotate-clockwise\":", StringComparison.Ordinal);
        int counterclockwiseCase = body.IndexOf("case \"map3d.rotate-counterclockwise\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf("private bool RotateVisualTargets3D", StringComparison.Ordinal);

        Assert.True(clockwiseCase >= 0);
        Assert.True(counterclockwiseCase >= 0);
        Assert.True(handlerIndex >= 0);
        Assert.Contains("RotateVisualTargets3D(_gameConfig?.DoomThingRotationAngles == true ? 45 : 5, 5);", body, StringComparison.Ordinal);
        Assert.Contains("RotateVisualTargets3D(_gameConfig?.DoomThingRotationAngles == true ? -45 : -5, -5);", body, StringComparison.Ordinal);
        Assert.Contains("VisualFlatRotation.Rotate(targets, textureAngleIncrement, _mapFormat == MapFormat.Udmf)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyCameraRotation3DUsesUdbEmptySelectionWarning()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool ApplyCameraRotationToSelectedThings3D()", StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Can't apply camera rotation to things: no things selected.", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(warningIndex > methodIndex);
    }

    [Fact]
    public void ApplyCameraRotation3DUsesUdbSuccessStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool ApplyCameraRotationToSelectedThings3D()", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Applied camera rotation and pitch to {things.Count} thing", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusIndex > methodIndex);
    }

    [Fact]
    public void PlaceThingAtCursor3DUsesUdbInvalidHitWarning()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool PlaceThingTargetsAtCursor3D()", StringComparison.Ordinal);
        int missingTargetIndex = body.IndexOf("if (_target3D is not { } target)", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Cannot place Thing here", methodIndex, StringComparison.Ordinal);
        int emptySelectionIndex = body.IndexOf("if (things.Count == 0) return false;", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(missingTargetIndex > methodIndex);
        Assert.True(warningIndex > missingTargetIndex);
        Assert.True(emptySelectionIndex > warningIndex);
    }

    [Fact]
    public void ShowVisualThings3DUsesUdbStatusText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void CycleVisualThings3D()", StringComparison.Ordinal);
        int stateIndex = body.IndexOf("int state = CycleVisualThings();", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingVisibilityStatusText(state));", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(stateIndex > methodIndex);
        Assert.True(statusIndex > stateIndex);
    }

    [Fact]
    public void VisualThingClipboardCommandsUseUdbStatusesAndWarnings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int copyIndex = body.IndexOf("private bool CopyVisualThingSelection3D()", StringComparison.Ordinal);
        int copyStatusIndex = body.IndexOf("VisualThingSelectionStatusText(\"Copied\", things.Count)", copyIndex, StringComparison.Ordinal);
        int cutIndex = body.IndexOf("private bool CutVisualThingSelection3D()", StringComparison.Ordinal);
        int cutStatusIndex = body.IndexOf("VisualThingSelectionStatusText(\"Cut\", things.Count)", cutIndex, StringComparison.Ordinal);
        int pasteIndex = body.IndexOf("private bool PasteVisualThingSelection3D()", StringComparison.Ordinal);
        int cannotPasteIndex = body.IndexOf("Cannot paste here!", pasteIndex, StringComparison.Ordinal);
        int pasteStatusIndex = body.IndexOf("VisualThingSelectionStatusText(\"Pasted\", pasted.Count)", pasteIndex, StringComparison.Ordinal);

        Assert.True(copyIndex >= 0);
        Assert.True(copyStatusIndex > copyIndex);
        Assert.True(cutIndex >= 0);
        Assert.True(cutStatusIndex > cutIndex);
        Assert.True(pasteIndex >= 0);
        Assert.True(cannotPasteIndex > pasteIndex);
        Assert.True(pasteStatusIndex > cannotPasteIndex);
    }

    [Fact]
    public void InsertThingAtTarget3DUsesUdbStatusesAndWarnings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool InsertThingAtTarget3D()", StringComparison.Ordinal);
        int missingTargetIndex = body.IndexOf("if (_target3D is not { } target)", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Cannot insert thing here!", missingTargetIndex, StringComparison.Ordinal);
        int insertIndex = body.IndexOf("InsertThingAt(new Vec2D(target.Point.x, target.Point.y), snap: false, height: target.Point.z);", warningIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingInsertedStatusText());", insertIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(missingTargetIndex > methodIndex);
        Assert.True(warningIndex > missingTargetIndex);
        Assert.True(insertIndex > warningIndex);
        Assert.True(statusIndex > insertIndex);
    }

    [Fact]
    public void VisualTextureOffset3DCommandsUseFlatOffsetTargets()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void NudgeTargetOffset3D(int deltaX, int deltaY)", StringComparison.Ordinal);
        int flatTargetsIndex = body.IndexOf("FlatTextureOffsetTargets3D()", methodIndex, StringComparison.Ordinal);
        int flatOffsetIndex = body.IndexOf("VisualFlatOffset.Nudge", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(flatTargetsIndex > methodIndex);
        Assert.True(flatOffsetIndex > flatTargetsIndex);
        Assert.Contains("if (_mapFormat == MapFormat.Udmf)", body, StringComparison.Ordinal);
        Assert.Contains("VisualSidedefTextureOffsets.Nudge(side, part, deltaX, deltaY, localOffsets, textureWidth, textureHeight)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualTextureOffsetClipboardUsesLocalSidedefOffsetsWhenConfigured()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int copyIndex = body.IndexOf("private void CopyTextureOffsets3D()", StringComparison.Ordinal);
        int pasteIndex = body.IndexOf("private void PasteTextureOffsets3D()", StringComparison.Ordinal);

        Assert.True(copyIndex >= 0);
        Assert.True(pasteIndex >= 0);
        Assert.Contains("_mapFormat == MapFormat.Udmf && _gameConfig?.UseLocalSidedefTextureOffsets == true", body, StringComparison.Ordinal);
        Assert.Contains("VisualSidedefTextureOffsets.Copy(target.Side, target.Part, localOffsets)", body, StringComparison.Ordinal);
        Assert.Contains("localOffsets ? TextureOffsetPartTargets3D() : new System.Collections.Generic.List<(Sidedef Side, SidedefPart Part)>()", body, StringComparison.Ordinal);
        Assert.Contains("VisualSidedefTextureOffsets.Paste(side, part, offsets, localOffsets)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualFitTextures3DUsesUdbSelectionWarningBeforeResourceGuard()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void FitSelectedVisualTextures3D()", StringComparison.Ordinal);
        int targetsIndex = body.IndexOf("var targets = SelectedWallTextureParts3D();", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Fit Textures action requires selected sidedefs.", targetsIndex, StringComparison.Ordinal);
        int resourcesIndex = body.IndexOf("if (_resources == null)", warningIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetsIndex > methodIndex);
        Assert.True(warningIndex > targetsIndex);
        Assert.True(resourcesIndex > warningIndex);
    }

    [Fact]
    public void VisualTextureReset3DUsesMapFormatAwareSidedefReset()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ResetVisualTexture3D(bool local)", StringComparison.Ordinal);
        int resetIndex = body.IndexOf("VisualTextureReset.ResetSidedefForCommand", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(resetIndex > methodIndex);
        Assert.Contains("VisualTextureReset.ResetSidedefForCommand(side, hit.Part, local: true, isUdmf: _mapFormat == MapFormat.Udmf)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualBrightnessStep3DOnlyChangesFlatSurfaces()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AdjustTargetBrightness3D(int delta)", StringComparison.Ordinal);
        int filterIndex = body.IndexOf("if (h.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling)) continue;", methodIndex, StringComparison.Ordinal);
        int sectorWriteIndex = body.IndexOf("s.Brightness = Math.Clamp(s.Brightness + delta, 0, 255);", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(filterIndex > methodIndex);
        Assert.True(sectorWriteIndex > filterIndex);
    }

    [Fact]
    public void VisualAutoAlign3DTriesUdmfFlatTargets()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AutoAlignTarget3D(bool alignX, bool alignY)", StringComparison.Ordinal);
        int flatHelperIndex = body.IndexOf("if (AutoAlignFlatTargets3D(alignX, alignY)) return;", methodIndex, StringComparison.Ordinal);
        int wallMessageIndex = body.IndexOf("aim at a wall or UDMF flat to align textures", methodIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private bool AutoAlignFlatTargets3D(bool alignX, bool alignY)", StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(flatHelperIndex > methodIndex);
        Assert.True(wallMessageIndex > flatHelperIndex);
        Assert.True(helperIndex > methodIndex);
        Assert.Contains("SectorFlatAlignment.AlignToClosestLine", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedThings3DUsesHighlightedThingFallback()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private System.Collections.Generic.List<Thing> SelectedThings3D()", StringComparison.Ordinal);
        int loopIndex = body.IndexOf("foreach (VisualHit hit in _sel3D)", methodIndex, StringComparison.Ordinal);
        int fallbackIndex = body.IndexOf("if (result.Count == 0 && _target3D?.Thing is { } target)", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(loopIndex > methodIndex);
        Assert.True(fallbackIndex > loopIndex);
    }

    [Fact]
    public void LookThroughSelection3DUsesUdbSelectionWarning()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool LookThroughSelectedThing3D()", StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Look Through Selection action requires 1 selected Thing!", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(warningIndex > methodIndex);
    }

    [Fact]
    public void AlignThingsToWall3DUsesUdbEmptySelectionWarning()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool AlignSelectedVisualThingsToWall3D()", StringComparison.Ordinal);
        int emptySelectionIndex = body.IndexOf("if (things.Count == 0)", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("This action requires selected Things!", emptySelectionIndex, StringComparison.Ordinal);
        int gameConfigIndex = body.IndexOf("if (_gameConfig == null)", warningIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(emptySelectionIndex > methodIndex);
        Assert.True(warningIndex > emptySelectionIndex);
        Assert.True(gameConfigIndex > warningIndex);
    }

    [Fact]
    public void VisualUnpeggedToggleUsesHighlightedWallState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ToggleUnpegged3D(bool upper)", StringComparison.Ordinal);
        int targetIndex = body.IndexOf("Sidedef? targetSide = TargetSidedef3D();", methodIndex, StringComparison.Ordinal);
        int targetsIndex = body.IndexOf("var targets = WallLineTargets3D();", targetIndex, StringComparison.Ordinal);
        int fallbackIndex = body.IndexOf("if (targets.Count == 0) targets.Add(targetSide.Line);", targetsIndex, StringComparison.Ordinal);
        int nextIndex = body.IndexOf("bool next = !IsLineFlagSet3D(targetSide.Line, flag);", fallbackIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetIndex > methodIndex);
        Assert.True(targetsIndex > targetIndex);
        Assert.True(fallbackIndex > targetsIndex);
        Assert.True(nextIndex > fallbackIndex);
    }

    [Fact]
    public void VisualSlopeCommandsUseHighlightedSurfaceFallback()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int toggleIndex = body.IndexOf("private void ToggleSlope3D()", StringComparison.Ordinal);
        int toggleTargetsIndex = body.IndexOf("foreach (VisualHit hit in EditTargets3D())", toggleIndex, StringComparison.Ordinal);
        int resetIndex = body.IndexOf("private void ResetSlope3D()", StringComparison.Ordinal);
        int resetTargetsIndex = body.IndexOf("foreach (VisualHit hit in EditTargets3D())", resetIndex, StringComparison.Ordinal);

        Assert.True(toggleIndex >= 0);
        Assert.True(toggleTargetsIndex > toggleIndex);
        Assert.True(resetIndex >= 0);
        Assert.True(resetTargetsIndex > resetIndex);
    }

    [Fact]
    public void VisualTexturePasteTargetsOnlySurfacesLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int applyIndex = body.IndexOf("private void ApplyTextureToTarget(string tex)", StringComparison.Ordinal);
        int targetsIndex = body.IndexOf("var targets = TextureApplyTargets3D();", applyIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private System.Collections.Generic.List<VisualHit> TextureApplyTargets3D()", StringComparison.Ordinal);
        int filterIndex = body.IndexOf("hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall", helperIndex, StringComparison.Ordinal);

        Assert.True(applyIndex >= 0);
        Assert.True(targetsIndex > applyIndex);
        Assert.True(helperIndex > applyIndex);
        Assert.True(filterIndex > helperIndex);
    }

    [Fact]
    public void VisualDeleteClearsSurfaceTexturesAndDeletesThingsLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void DeleteVisualTargets3D()", StringComparison.Ordinal);
        int targetsIndex = body.IndexOf("var targets = EditTargets3D();", methodIndex, StringComparison.Ordinal);
        int floorIndex = body.IndexOf("floor.SetFloorTexture(\"-\");", methodIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("ceiling.SetCeilTexture(\"-\");", methodIndex, StringComparison.Ordinal);
        int wallIndex = body.IndexOf("side.SetTexture(hit.Part, \"-\");", methodIndex, StringComparison.Ordinal);
        int thingIndex = body.IndexOf("_map.RemoveThing(thing);", methodIndex, StringComparison.Ordinal);
        int dispatchIndex = body.IndexOf("case \"map3d.delete-target\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf("DeleteVisualTargets3D();", dispatchIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetsIndex > methodIndex);
        Assert.True(floorIndex > targetsIndex);
        Assert.True(ceilingIndex > targetsIndex);
        Assert.True(wallIndex > targetsIndex);
        Assert.True(thingIndex > targetsIndex);
        Assert.True(dispatchIndex >= 0);
        Assert.True(handlerIndex > dispatchIndex);
    }

    [Fact]
    public void AdjacentVertexSlopeSelectionUsesDedicatedUdbGuard()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("public bool ToggleVisualVertexSlopeAdjacentSelection()", StringComparison.Ordinal);
        int guardIndex = body.IndexOf("if (!CanToggleAdjacentVisualVertexSlopeSelection())", methodIndex, StringComparison.Ordinal);
        int helperIndex = body.IndexOf("private bool CanToggleAdjacentVisualVertexSlopeSelection()", StringComparison.Ordinal);
        int policyIndex = body.IndexOf("VisualSlopePickingPolicy.CanToggleAdjacentVertexSelection", helperIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(guardIndex > methodIndex);
        Assert.True(helperIndex > methodIndex);
        Assert.True(policyIndex > helperIndex);
    }
}
