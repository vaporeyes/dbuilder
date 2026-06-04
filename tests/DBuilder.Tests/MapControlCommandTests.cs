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
}
