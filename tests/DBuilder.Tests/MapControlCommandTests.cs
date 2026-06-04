// ABOUTME: Tests MapControl command dispatch metadata without constructing the Avalonia control.
// ABOUTME: Keeps stable 2D and 3D command ids wired to map editing handlers.

using System.Reflection;
using DBuilder.Editor;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class MapControlCommandTests
{
    [Theory]
    [InlineData("FLOOR0_1", VisualHitKind.Floor, "Pasted flat \"FLOOR0_1\" on floor.")]
    [InlineData("CEIL1_1", VisualHitKind.Ceiling, "Pasted flat \"CEIL1_1\" on ceiling.")]
    [InlineData("STARTAN3", VisualHitKind.Wall, "Pasted texture \"STARTAN3\".")]
    public void TexturePasted3DStatusTextMatchesUdbTargetKind(string textureName, VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.TexturePasted3DStatusText(textureName, kind));

    [Theory]
    [InlineData("FLOOR0_1", true, "Copied flat \"FLOOR0_1\".")]
    [InlineData("STARTAN3", false, "Copied texture \"STARTAN3\".")]
    public void TextureCopied3DStatusTextMatchesUdbTargetKind(string textureName, bool flat, string expected)
        => Assert.Equal(expected, MapControl.TextureCopied3DStatusText(textureName, flat));

    [Fact]
    public void TextureOffsetStatusTextMatchesUdb()
    {
        Assert.Equal("Copied texture offsets 12, -8.", MapControl.TextureOffsetsCopied3DStatusText(12, -8));
        Assert.Equal("Pasted texture offsets 12, -8.", MapControl.TextureOffsetsPasted3DStatusText(12, -8));
    }

    [Theory]
    [InlineData(VisualHitKind.Floor, false, "Texture offsets reset.")]
    [InlineData(VisualHitKind.Ceiling, true, "Texture offsets, scale, rotation and brightness reset.")]
    [InlineData(VisualHitKind.Wall, false, "Texture offsets reset.")]
    [InlineData(VisualHitKind.Wall, true, "Local texture offsets, scale and brightness reset.")]
    [InlineData(VisualHitKind.Thing, false, "Thing scale reset.")]
    [InlineData(VisualHitKind.Thing, true, "Thing scale, pitch and roll reset.")]
    public void VisualTextureReset3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, bool local, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureReset3DStatusText(kind, local));

    [Theory]
    [InlineData(VisualHitKind.Floor, 0.969, 1.016, 32, 64, "Floor scale changed to 0.969, 1.016 (33 x 63).")]
    [InlineData(VisualHitKind.Ceiling, 0.969, 1.016, 32, 64, "Ceiling scale changed to 0.969, 1.016 (33 x 63).")]
    [InlineData(VisualHitKind.Wall, 0.969, 1.016, 32, 64, "Wall scale changed to 0.969, 1.016 (33 x 63).")]
    [InlineData(VisualHitKind.Thing, 1.031, 0.984, 32, 64, "Changed thing scale to 1.031, 0.984 (33 x 63).")]
    public void VisualScale3DStatusTextMatchesUdbTargetKind(
        VisualHitKind kind,
        double scaleX,
        double scaleY,
        int width,
        int height,
        string expected)
        => Assert.Equal(expected, MapControl.VisualScale3DStatusText(kind, scaleX, scaleY, width, height));

    [Theory]
    [InlineData(VisualHitKind.Floor, 5.0, "Floor rotation changed to 5")]
    [InlineData(VisualHitKind.Ceiling, 355.0, "Ceiling rotation changed to 355")]
    public void VisualRotation3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, double angle, string expected)
        => Assert.Equal(expected, MapControl.VisualRotation3DStatusText(kind, angle));

    [Theory]
    [InlineData(VisualHitKind.Wall, -1.0, 2.0, "Changed texture offsets to -1, 2.")]
    [InlineData(VisualHitKind.Floor, -1.5, 2.25, "Changed floor texture offsets to -1.5, 2.25.")]
    [InlineData(VisualHitKind.Ceiling, 3.0, -4.0, "Changed ceiling texture offsets to 3, -4.")]
    public void VisualTextureOffset3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, double x, double y, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureOffset3DStatusText(kind, x, y));

    [Theory]
    [InlineData(VisualHitKind.Floor, "FLOOR0_1", "Flood-filled floors with FLOOR0_1.")]
    [InlineData(VisualHitKind.Ceiling, "CEIL1_1", "Flood-filled ceilings with CEIL1_1.")]
    [InlineData(VisualHitKind.Wall, "STARTAN3", "Flood-filled textures with STARTAN3.")]
    public void VisualTextureFloodFill3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, string textureName, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureFloodFill3DStatusText(kind, textureName));

    [Theory]
    [InlineData(true, true, "Set upper-unpegged setting.")]
    [InlineData(true, false, "Removed upper-unpegged setting.")]
    [InlineData(false, true, "Set lower-unpegged setting.")]
    [InlineData(false, false, "Removed lower-unpegged setting.")]
    public void VisualUnpegged3DStatusTextMatchesUdb(bool upper, bool set, string expected)
        => Assert.Equal(expected, MapControl.VisualUnpegged3DStatusText(upper, set));

    [Theory]
    [InlineData(VisualHitKind.Floor, "Deleted a texture.")]
    [InlineData(VisualHitKind.Ceiling, "Deleted a texture.")]
    [InlineData(VisualHitKind.Wall, "Deleted a texture.")]
    [InlineData(VisualHitKind.Thing, "Deleted a thing.")]
    public void VisualDelete3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.VisualDelete3DStatusText(kind));

    [Theory]
    [InlineData(true, false, false, "Auto-aligned textures (X).")]
    [InlineData(false, true, false, "Auto-aligned textures (Y).")]
    [InlineData(true, true, false, "Auto-aligned textures (X and Y).")]
    [InlineData(true, false, true, "Auto-aligned textures to selected sidedefs (X).")]
    [InlineData(false, true, true, "Auto-aligned textures to selected sidedefs (Y).")]
    [InlineData(true, true, true, "Auto-aligned textures to selected sidedefs (X and Y).")]
    public void VisualAutoAlign3DStatusTextMatchesUdb(bool alignX, bool alignY, bool selected, string expected)
        => Assert.Equal(expected, MapControl.VisualAutoAlign3DStatusText(alignX, alignY, selected));

    [Theory]
    [InlineData(VisualHitKind.Floor, 0, "Changed sector brightness to 0.")]
    [InlineData(VisualHitKind.Floor, 168, "Changed sector brightness to 168.")]
    [InlineData(VisualHitKind.Ceiling, 255, "Changed ceiling brightness to 255.")]
    public void VisualBrightness3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, int brightness, string expected)
        => Assert.Equal(expected, MapControl.VisualBrightness3DStatusText(kind, brightness));

    [Theory]
    [InlineData("angle", 270, 0, 0, "Changed thing angle to 270.")]
    [InlineData("pitch", 0, 45, 0, "Changed thing pitch to 45.")]
    [InlineData("roll", 0, 0, 315, "Changed thing roll to 315.")]
    public void VisualThingOrientation3DStatusTextMatchesUdb(string orientation, int angle, int pitch, int roll, string expected)
    {
        var thing = new Thing { Angle = angle, Pitch = pitch, Roll = roll };

        Assert.Equal(expected, MapControl.VisualThingOrientation3DStatusText(thing, orientation));
    }

    [Fact]
    public void VisualHeight3DStatusTextMatchesUdbTargetKind()
    {
        var sector = new Sector { FloorHeight = 16, CeilHeight = 128 };
        var thing = new Thing { Height = 24.5 };

        Assert.Equal(
            "Changed floor height to 16.",
            MapControl.VisualHeight3DStatusText(new VisualHit(VisualHitKind.Floor, 0, new(), sector, null, true, 0, 0)));
        Assert.Equal(
            "Changed ceiling height to 128.",
            MapControl.VisualHeight3DStatusText(new VisualHit(VisualHitKind.Ceiling, 0, new(), sector, null, true, 0, 0)));
        Assert.Equal(
            "Changed thing height to 24.5.",
            MapControl.VisualHeight3DStatusText(new VisualHit(VisualHitKind.Thing, 0, new(), null, null, true, 0, 0, Thing: thing)));
    }

    [Fact]
    public void VisualThingPosition3DStatusTextMatchesUdb()
    {
        var thing = new Thing { Position = new DBuilder.Geometry.Vector2D(64, 96), Height = 24 };

        Assert.Equal("Changed thing position to 64, 96, 24.", MapControl.VisualThingPosition3DStatusText(thing));
    }

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
    [InlineData("Vertex", "Pasted vertex properties.")]
    [InlineData("Linedef", "Pasted linedef properties.")]
    [InlineData("Sidedef", "Pasted sidedef properties.")]
    [InlineData("Sector", "Pasted sector properties.")]
    [InlineData("Thing", "Pasted thing properties.")]
    public void VisualPropertiesPasted3DStatusTextMatchesUdbTargetKind(string kindName, string expected)
    {
        var kind = Enum.Parse<PastePropertiesElementKind>(kindName);

        Assert.Equal(expected, MapControl.VisualPropertiesPasted3DStatusText([kind]));
    }

    [Fact]
    public void VisualPropertiesPasted3DStatusTextMatchesUdbWallTarget()
        => Assert.Equal(
            "Pasted linedef and sidedef properties.",
            MapControl.VisualPropertiesPasted3DStatusText(
                [PastePropertiesElementKind.Linedef, PastePropertiesElementKind.Sidedef]));

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
    public void RotateVisualTargets3DUsesUdbFlatRotationStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool RotateVisualTargets3D(int thingAngleIncrement, int textureAngleIncrement)", StringComparison.Ordinal);
        int rotationIndex = body.IndexOf("VisualFlatRotation.Rotate(targets, textureAngleIncrement, _mapFormat == MapFormat.Udmf)", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualRotation3DStatusFromTargets(targets));", rotationIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(rotationIndex > methodIndex);
        Assert.True(statusIndex > rotationIndex);
        Assert.DoesNotContain("rotated {thingCount + flatCount} target", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetTargetOffsets3DUsesUdbStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ResetTargetOffsets3D()", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(\"Texture offsets reset.\");", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusIndex > methodIndex);
        Assert.DoesNotContain("Target3DChanged?.Invoke(\"reset offsets\");", body, StringComparison.Ordinal);
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
    public void CopyTexture3DUsesUdbFlatAndTextureStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void CopyTexture3D()", StringComparison.Ordinal);
        int formatterIndex = body.IndexOf("TextureCopied3DStatusText(tex, _target3D?.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling)", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(formatterIndex > methodIndex);
        Assert.DoesNotContain("copied texture {tex}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTexture3DUsesUdbStatusForLastAppliedTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ApplyTextureToTarget(string tex)", StringComparison.Ordinal);
        int loopIndex = body.IndexOf("foreach (var h in targets) ApplyTextureToHit(h, tex);", methodIndex, StringComparison.Ordinal);
        int formatterIndex = body.IndexOf("TexturePasted3DStatusText(tex, targets[^1].Kind)", loopIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(loopIndex > methodIndex);
        Assert.True(formatterIndex > loopIndex);
        Assert.DoesNotContain("TextureApplied3DStatusText", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FloodFillTexture3DUsesUdbStatusWithTextureName()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void FloodFillTexture3D()", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("FinishFloodFill3D(VisualTextureFloodFill3DStatusText(hit.Kind, fillTexture));", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusIndex > methodIndex);
        Assert.DoesNotContain("FinishFloodFill3D(\"flood-filled floors\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FinishFloodFill3D(\"flood-filled ceilings\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FinishFloodFill3D(\"flood-filled textures\")", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TextureOffsetCommandsUseUdbStatusText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int copyIndex = body.IndexOf("private void CopyTextureOffsets3D()", StringComparison.Ordinal);
        int copyStatusIndex = body.IndexOf("TextureOffsetsCopied3DStatusText(_texOffsetClipboard3D.Value.X, _texOffsetClipboard3D.Value.Y)", copyIndex, StringComparison.Ordinal);
        int pasteIndex = body.IndexOf("private void PasteTextureOffsets3D()", StringComparison.Ordinal);
        int pasteStatusIndex = body.IndexOf("TextureOffsetsPasted3DStatusText(offsets.X, offsets.Y)", pasteIndex, StringComparison.Ordinal);

        Assert.True(copyIndex >= 0);
        Assert.True(copyStatusIndex > copyIndex);
        Assert.True(pasteIndex >= 0);
        Assert.True(pasteStatusIndex > pasteIndex);
        Assert.DoesNotContain("copied offsets {_texOffsetClipboard3D.Value.X}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("pasted offsets to {targetCount}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void NudgeTargetOffset3DUsesUdbStatusForLastOffsetTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void NudgeTargetOffset3D(int deltaX, int deltaY)", StringComparison.Ordinal);
        int statusVariableIndex = body.IndexOf("string offsetStatus = string.Empty;", methodIndex, StringComparison.Ordinal);
        int wallStatusIndex = body.IndexOf("offsetStatus = VisualTextureOffset3DStatusText(VisualHitKind.Wall", statusVariableIndex, StringComparison.Ordinal);
        int flatStatusIndex = body.IndexOf("offsetStatus = VisualTextureOffset3DStatusText(", wallStatusIndex + 1, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(offsetStatus);", flatStatusIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(wallStatusIndex > statusVariableIndex);
        Assert.True(flatStatusIndex > wallStatusIndex);
        Assert.True(finalStatusIndex > flatStatusIndex);
        Assert.DoesNotContain("offset {changed} target", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetVisualTexture3DUsesUdbStatusForLastResetTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ResetVisualTexture3D(bool local)", StringComparison.Ordinal);
        int initialIndex = body.IndexOf("string resetStatus = VisualTextureReset3DStatusText(VisualHitKind.Wall, local);", methodIndex, StringComparison.Ordinal);
        int assignmentIndex = body.IndexOf("resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);", initialIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(resetStatus);", assignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(initialIndex > methodIndex);
        Assert.True(assignmentIndex > initialIndex);
        Assert.True(statusIndex > assignmentIndex);
        Assert.DoesNotContain("local texture fields reset", body, StringComparison.Ordinal);
        Assert.DoesNotContain("texture offsets reset", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeVisualScale3DUsesUdbStatusForLastScaledTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ChangeVisualScale3D(int incrementX, int incrementY)", StringComparison.Ordinal);
        int statusVariableIndex = body.IndexOf("string scaleStatus = string.Empty;", methodIndex, StringComparison.Ordinal);
        int thingStatusIndex = body.IndexOf("scaleStatus = VisualScale3DStatusText(", statusVariableIndex, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(scaleStatus);", thingStatusIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(thingStatusIndex > statusVariableIndex);
        Assert.True(finalStatusIndex > thingStatusIndex);
        Assert.DoesNotContain("scaled {changed} target", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualThingOrientation3DUsesUdbStatusForLastChangedThing()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int rotateIndex = body.IndexOf("FinishThingOrientationChange3D(things, \"Rotate thing\", \"Rotate things\", \"angle\");", StringComparison.Ordinal);
        int pitchIndex = body.IndexOf("FinishThingOrientationChange3D(things, \"Change thing pitch\", \"Change thing pitches\", \"pitch\");", StringComparison.Ordinal);
        int rollIndex = body.IndexOf("FinishThingOrientationChange3D(things, \"Change thing roll\", \"Change thing rolls\", \"roll\");", StringComparison.Ordinal);
        int finishIndex = body.IndexOf("private void FinishThingOrientationChange3D(", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingOrientation3DStatusText(things[^1], orientation));", finishIndex, StringComparison.Ordinal);

        Assert.True(rotateIndex >= 0);
        Assert.True(pitchIndex > rotateIndex);
        Assert.True(rollIndex > pitchIndex);
        Assert.True(finishIndex > rollIndex);
        Assert.True(statusIndex > finishIndex);
        Assert.DoesNotContain("rotated things", body, StringComparison.Ordinal);
        Assert.DoesNotContain("changed thing pitches", body, StringComparison.Ordinal);
        Assert.DoesNotContain("changed thing rolls", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualThingMovement3DUsesUdbPositionStatusForLastMovedThing()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int moveIndex = body.IndexOf("private bool MoveThingTargets3D(Vector2D direction)", StringComparison.Ordinal);
        int moveStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingPosition3DStatusText(things[^1]));", moveIndex, StringComparison.Ordinal);
        int placeIndex = body.IndexOf("private bool PlaceThingTargetsAtCursor3D()", StringComparison.Ordinal);
        int placeStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingPosition3DStatusText(things[^1]));", placeIndex, StringComparison.Ordinal);

        Assert.True(moveIndex >= 0);
        Assert.True(moveStatusIndex > moveIndex);
        Assert.True(placeIndex > moveStatusIndex);
        Assert.True(placeStatusIndex > placeIndex);
        Assert.DoesNotContain("moved {things.Count}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("placed {things.Count}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PasteVisualPropertiesTargetsUsesUdbVisualStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("public string PasteVisualPropertiesTargets(ISet<string>? enabledKeys = null)", StringComparison.Ordinal);
        int appliedKindsIndex = body.IndexOf("var appliedKinds = new List<PastePropertiesElementKind>();", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("VisualPropertiesPasted3DStatusText(appliedKinds)", appliedKindsIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(appliedKindsIndex > methodIndex);
        Assert.True(statusIndex > appliedKindsIndex);
        Assert.DoesNotContain("Pasted properties to {TargetText(kind, count)}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualBrightnessStep3DOnlyChangesFlatSurfaces()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AdjustTargetBrightness3D(int delta)", StringComparison.Ordinal);
        int filterIndex = body.IndexOf("if (h.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling)) continue;", methodIndex, StringComparison.Ordinal);
        int sectorWriteIndex = body.IndexOf("s.Brightness = Math.Clamp(s.Brightness + delta, 0, 255);", methodIndex, StringComparison.Ordinal);
        int statusAssignmentIndex = body.IndexOf("brightnessStatus = VisualBrightness3DStatusText(h.Kind, s.Brightness);", sectorWriteIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(brightnessStatus);", statusAssignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(filterIndex > methodIndex);
        Assert.True(sectorWriteIndex > filterIndex);
        Assert.True(statusAssignmentIndex > sectorWriteIndex);
        Assert.True(statusIndex > statusAssignmentIndex);
    }

    [Fact]
    public void AdjustTarget3DPassesThingHeightSupportToNearestModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AdjustTargetToNearest3D(bool raise, bool withinSelection)", StringComparison.Ordinal);
        int callIndex = body.IndexOf("VisualNearestHeight.Apply(", methodIndex, StringComparison.Ordinal);
        int capabilityIndex = body.IndexOf("_gameConfig?.HasThingHeight == true", callIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(callIndex > methodIndex);
        Assert.True(capabilityIndex > callIndex);
    }

    [Fact]
    public void AdjustTarget3DUsesUdbHeightStatusForLastChangedTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AdjustTarget3D(int step)", StringComparison.Ordinal);
        int statusVariableIndex = body.IndexOf("string heightStatus = string.Empty;", methodIndex, StringComparison.Ordinal);
        int applyIndex = body.IndexOf("ApplyHeightDelta(h, step);", statusVariableIndex, StringComparison.Ordinal);
        int statusAssignmentIndex = body.IndexOf("heightStatus = VisualHeight3DStatusText(h);", applyIndex, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(heightStatus);", statusAssignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(applyIndex > statusVariableIndex);
        Assert.True(statusAssignmentIndex > applyIndex);
        Assert.True(finalStatusIndex > statusAssignmentIndex);
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
    public void VisualAutoAlign3DUsesUdbStatusText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int sideIndex = body.IndexOf("private void AutoAlignSide3D(Sidedef side, bool alignX, bool alignY, string editName)", StringComparison.Ordinal);
        int sideStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: false));", sideIndex, StringComparison.Ordinal);
        int flatIndex = body.IndexOf("private bool AutoAlignFlatTargets3D(bool alignX, bool alignY)", StringComparison.Ordinal);
        int flatStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: false));", flatIndex, StringComparison.Ordinal);
        int selectedIndex = body.IndexOf("private void AutoAlignSelectedVisualTextures3D(bool alignX, bool alignY)", StringComparison.Ordinal);
        int selectedStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: true));", selectedIndex, StringComparison.Ordinal);

        Assert.True(sideIndex >= 0);
        Assert.True(sideStatusIndex > sideIndex);
        Assert.True(flatIndex > sideIndex);
        Assert.True(flatStatusIndex > flatIndex);
        Assert.True(selectedIndex > flatIndex);
        Assert.True(selectedStatusIndex > selectedIndex);
        string visual3DBody = body[sideIndex..body.IndexOf("// Adjusts the selected", selectedIndex, StringComparison.Ordinal)];
        Assert.DoesNotContain("aligned {n} sidedef", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aligned {changed} flat", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aligned {aligned} sidedef", visual3DBody, StringComparison.Ordinal);
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
    public void LookThroughSelection3DSuppressesSyntheticSuccessStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool LookThroughSelectedThing3D()", StringComparison.Ordinal);
        int nextMethodIndex = body.IndexOf("private bool AlignSelectedVisualThingsToWall3D()", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(nextMethodIndex > methodIndex);
        string methodBody = body[methodIndex..nextMethodIndex];
        Assert.DoesNotContain("looking through thing", methodBody, StringComparison.Ordinal);
        Assert.Contains("if (pose.StatusMessage != null) Target3DChanged?.Invoke(pose.StatusMessage);", methodBody, StringComparison.Ordinal);
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
    public void VisualUnpeggedToggleUsesUdbStatusText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ToggleUnpegged3D(bool upper)", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualUnpegged3DStatusText(upper, next));", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusIndex > methodIndex);
        Assert.DoesNotContain("set\" : \"removed", body, StringComparison.Ordinal);
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
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(deleteStatus);", thingIndex, StringComparison.Ordinal);
        int dispatchIndex = body.IndexOf("case \"map3d.delete-target\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf("DeleteVisualTargets3D();", dispatchIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetsIndex > methodIndex);
        Assert.True(floorIndex > targetsIndex);
        Assert.True(ceilingIndex > targetsIndex);
        Assert.True(wallIndex > targetsIndex);
        Assert.True(thingIndex > targetsIndex);
        Assert.True(statusIndex > thingIndex);
        Assert.True(dispatchIndex >= 0);
        Assert.True(handlerIndex > dispatchIndex);
        Assert.DoesNotContain("deleted {CountLabel", body, StringComparison.Ordinal);
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
