// ABOUTME: Tests MapControl command dispatch metadata without constructing the Avalonia control.
// ABOUTME: Keeps stable 2D and 3D command ids wired to map editing handlers.

using System.Reflection;
using DBuilder.Editor;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class MapControlCommandTests
{
    [Fact]
    public void FixedThingScaleDefaultsToUdbDisabledSetting()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("private bool _fixedThingsScale;", body, StringComparison.Ordinal);
        Assert.Contains("public bool ToggleFixedThingsScale()", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _fixedThingsScale = true;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicViewModeDefaultsToUdbNormalSetting()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("private ClassicViewMode _classicViewMode = ClassicViewMode.Wireframe;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private ClassicViewMode _classicViewMode = ClassicViewMode.FloorTextures;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SectorCreationFallbackBrightnessMatchesUdbDefault()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("DefaultFloorHeight = 0,", body, StringComparison.Ordinal);
        Assert.Contains("DefaultCeilingHeight = 128,", body, StringComparison.Ordinal);
        Assert.Contains("DefaultBrightness = 192,", body, StringComparison.Ordinal);
        Assert.Contains("CustomBrightness = _mapOptions?.CustomBrightness ?? 192,", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultFloorHeight = _mapOptions?.CustomFloorHeight ?? 0,", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultCeilingHeight = _mapOptions?.CustomCeilingHeight ?? 128,", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultBrightness = _mapOptions?.CustomBrightness ?? 192,", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultBrightness = _mapOptions?.CustomBrightness ?? 160,", body, StringComparison.Ordinal);
    }

    [Fact]
    public void LinedefRenderingUsesUdbColorPresetModel()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("private int LineColor(Linedef l)", body, StringComparison.Ordinal);
        Assert.Contains("public IReadOnlyList<LinedefColorPreset> LinedefColorPresets", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.TryGetColor(l, _linedefColorPresets, _mapFormat == MapFormat.Udmf, out int presetColor)", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.WithAlpha(presetColor, LinedefColorPresetModel.DefaultDoubleSidedAlpha)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void LinedefColorPresetDialogEditsPersistedPresetFields()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/LinedefColorPresetsDialog.cs"));

        Assert.Contains("public sealed class LinedefColorPresetsDialog : Window", body, StringComparison.Ordinal);
        Assert.Contains("new LinedefColorPreset(", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.ParseColor(_color.Text, fallback.Color)", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.ParseFlags(_flags.Text)", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.ParseFlags(_restrictedFlags.Text)", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.DefaultPresets", body, StringComparison.Ordinal);
        Assert.Contains("Title = LinedefColorPresetModel.DialogTitle;", body, StringComparison.Ordinal);
        Assert.Contains("Content = \"Delete\"", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.NewPresetName", body, StringComparison.Ordinal);
        Assert.Contains("private readonly TextBlock _warning", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.ValidationWarning(_presets, index, _isUdmf)", body, StringComparison.Ordinal);
        Assert.Contains("int index = Math.Max(0, _list.SelectedIndex);", body, StringComparison.Ordinal);
        Assert.Contains("_presets.Insert(index, new LinedefColorPreset", body, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_presets.Count == 0) _presets.AddRange(LinedefColorPresetModel.DefaultPresets);", body, StringComparison.Ordinal);
        Assert.Contains("up.Click += (_, _) => MovePreset(-1);", body, StringComparison.Ordinal);
        Assert.Contains("down.Click += (_, _) => MovePreset(1);", body, StringComparison.Ordinal);
        Assert.Contains("LinedefColorPresetModel.MovePreset(_presets, index, offset)", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FLOOR0_1", VisualHitKind.Floor, "Pasted flat \"FLOOR0_1\" on floor.")]
    [InlineData("CEIL1_1", VisualHitKind.Ceiling, "Pasted flat \"CEIL1_1\" on ceiling.")]
    [InlineData("STARTAN3", VisualHitKind.Wall, "Pasted texture \"STARTAN3\".")]
    public void TexturePasted3DStatusTextMatchesUdbTargetKind(string textureName, VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.TexturePasted3DStatusText(textureName, kind));

    [Theory]
    [InlineData("FLOOR0_1", VisualHitKind.Floor, true, "Paste floor \"FLOOR0_1\"")]
    [InlineData("CEIL1_1", VisualHitKind.Ceiling, true, "Paste ceiling \"CEIL1_1\"")]
    [InlineData("STARTAN3", VisualHitKind.Wall, true, "Paste texture \"STARTAN3\"")]
    [InlineData("FLOOR0_1", VisualHitKind.Floor, false, "Change flat \"FLOOR0_1\"")]
    [InlineData("CEIL1_1", VisualHitKind.Ceiling, false, "Change flat \"CEIL1_1\"")]
    [InlineData("STARTAN3", VisualHitKind.Wall, false, "Change texture STARTAN3")]
    public void TextureApplied3DEditNameMatchesUdbTargetKind(string textureName, VisualHitKind kind, bool pasted, string expected)
        => Assert.Equal(expected, MapControl.TextureApplied3DEditName(textureName, kind, pasted));

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
        Assert.Equal("Paste texture offsets", MapControl.TextureOffsetsPasted3DEditName());
    }

    [Theory]
    [InlineData(true, true, "Fit texture (width and height)")]
    [InlineData(true, false, "Fit texture (width)")]
    [InlineData(false, true, "Fit texture (height)")]
    public void VisualFitTexture3DEditNameMatchesUdb(bool fitWidth, bool fitHeight, string expected)
        => Assert.Equal(expected, MapControl.VisualFitTexture3DEditName(fitWidth, fitHeight));

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
    [InlineData(VisualHitKind.Floor, false, "Reset texture offsets")]
    [InlineData(VisualHitKind.Ceiling, true, "Reset texture offsets, scale, rotation and brightness")]
    [InlineData(VisualHitKind.Wall, false, "Reset texture offsets")]
    [InlineData(VisualHitKind.Wall, true, "Reset local texture offsets, scale and brightness")]
    [InlineData(VisualHitKind.Thing, false, "Reset thing scale")]
    [InlineData(VisualHitKind.Thing, true, "Reset thing scale, pitch and roll")]
    public void VisualTextureReset3DEditNameMatchesUdbTargetKind(VisualHitKind kind, bool local, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureReset3DEditName(kind, local));

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
    [InlineData(VisualHitKind.Floor, "Change texture scale")]
    [InlineData(VisualHitKind.Ceiling, "Change texture scale")]
    [InlineData(VisualHitKind.Wall, "Change wall scale")]
    [InlineData(VisualHitKind.Thing, "Change thing scale")]
    public void VisualScale3DEditNameMatchesUdbTargetKind(VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.VisualScale3DEditName(kind));

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

    [Fact]
    public void VisualTextureOffset3DEditNameMatchesUdb()
        => Assert.Equal("Change texture offsets", MapControl.VisualTextureOffset3DEditName());

    [Fact]
    public void VisualFlatTextureOffsetUnsupportedMapFormatMessageMatchesUdb()
        => Assert.Equal(
            "Floor/ceiling texture offsets cannot be changed in this map format!",
            MapControl.VisualFlatTextureOffsetUnsupportedMapFormatMessage());

    [Theory]
    [InlineData(VisualHitKind.Floor, "FLOOR0_1", "Flood-filled floors with FLOOR0_1.")]
    [InlineData(VisualHitKind.Ceiling, "CEIL1_1", "Flood-filled ceilings with CEIL1_1.")]
    [InlineData(VisualHitKind.Wall, "STARTAN3", "Flood-filled textures with STARTAN3.")]
    public void VisualTextureFloodFill3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, string textureName, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureFloodFill3DStatusText(kind, textureName));

    [Theory]
    [InlineData(VisualHitKind.Floor, "FLOOR0_1", "Flood-fill floors with FLOOR0_1")]
    [InlineData(VisualHitKind.Ceiling, "CEIL1_1", "Flood-fill ceilings with CEIL1_1")]
    [InlineData(VisualHitKind.Wall, "STARTAN3", "Flood-fill textures with STARTAN3")]
    public void VisualTextureFloodFill3DEditNameMatchesUdbTargetKind(VisualHitKind kind, string textureName, string expected)
        => Assert.Equal(expected, MapControl.VisualTextureFloodFill3DEditName(kind, textureName));

    [Theory]
    [InlineData(true, true, "Set upper-unpegged setting.")]
    [InlineData(true, false, "Removed upper-unpegged setting.")]
    [InlineData(false, true, "Set lower-unpegged setting.")]
    [InlineData(false, false, "Removed lower-unpegged setting.")]
    public void VisualUnpegged3DStatusTextMatchesUdb(bool upper, bool set, string expected)
        => Assert.Equal(expected, MapControl.VisualUnpegged3DStatusText(upper, set));

    [Theory]
    [InlineData(true, true, "Set upper-unpegged setting")]
    [InlineData(true, false, "Remove upper-unpegged setting")]
    [InlineData(false, true, "Set lower-unpegged setting")]
    [InlineData(false, false, "Remove lower-unpegged setting")]
    public void VisualUnpegged3DEditNameMatchesUdb(bool upper, bool set, string expected)
        => Assert.Equal(expected, MapControl.VisualUnpegged3DEditName(upper, set));

    [Fact]
    public void VisualSlopeToggleEmptySelectionMessageMatchesUdb()
        => Assert.Equal("Toggle Slope action requires selected surfaces!", MapControl.VisualSlopeToggleEmptySelectionMessage());

    [Theory]
    [InlineData(VisualHitKind.Floor, "Deleted a texture.")]
    [InlineData(VisualHitKind.Ceiling, "Deleted a texture.")]
    [InlineData(VisualHitKind.Wall, "Deleted a texture.")]
    [InlineData(VisualHitKind.Thing, "Deleted a thing.")]
    public void VisualDelete3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.VisualDelete3DStatusText(kind));

    [Theory]
    [InlineData(VisualHitKind.Floor, "Delete texture")]
    [InlineData(VisualHitKind.Ceiling, "Delete texture")]
    [InlineData(VisualHitKind.Wall, "Delete texture")]
    [InlineData(VisualHitKind.Thing, "Delete thing")]
    public void VisualDelete3DEditNameMatchesUdbTargetKind(VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.VisualDelete3DEditName(kind));

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
    [InlineData(true, false, false, "Auto-align textures (X)")]
    [InlineData(false, true, false, "Auto-align textures (Y)")]
    [InlineData(true, true, false, "Auto-align textures (X and Y)")]
    [InlineData(true, false, true, "Auto-align textures to selected sidedefs (X)")]
    [InlineData(false, true, true, "Auto-align textures to selected sidedefs (Y)")]
    [InlineData(true, true, true, "Auto-align textures to selected sidedefs (X and Y)")]
    public void VisualAutoAlign3DEditNameMatchesUdb(bool alignX, bool alignY, bool selected, string expected)
        => Assert.Equal(expected, MapControl.VisualAutoAlign3DEditName(alignX, alignY, selected));

    [Theory]
    [InlineData(true, "Alpha-based textures highlighting is ENABLED")]
    [InlineData(false, "Alpha-based textures highlighting is DISABLED")]
    public void AlphaBasedTextureHighlightingStatusTextMatchesUdb(bool enabled, string expected)
        => Assert.Equal(expected, MapControl.AlphaBasedTextureHighlightingStatusText(enabled));

    [Theory]
    [InlineData(VisualHitKind.Floor, 0, "Changed sector brightness to 0.")]
    [InlineData(VisualHitKind.Floor, 168, "Changed sector brightness to 168.")]
    [InlineData(VisualHitKind.Ceiling, 255, "Changed ceiling brightness to 255.")]
    [InlineData(VisualHitKind.Wall, 24, "Changed wall brightness to 24.")]
    public void VisualBrightness3DStatusTextMatchesUdbTargetKind(VisualHitKind kind, int brightness, string expected)
        => Assert.Equal(expected, MapControl.VisualBrightness3DStatusText(kind, brightness));

    [Theory]
    [InlineData(VisualHitKind.Floor, "Change sector brightness")]
    [InlineData(VisualHitKind.Ceiling, "Change ceiling brightness")]
    [InlineData(VisualHitKind.Wall, "Change wall brightness")]
    public void VisualBrightness3DEditNameMatchesUdbTargetKind(VisualHitKind kind, string expected)
        => Assert.Equal(expected, MapControl.VisualBrightness3DEditName(kind));

    [Fact]
    public void AdjustVisualCeilingBrightness3DUsesUdbRelativeCeilingLightField()
    {
        var sector = new Sector { Brightness = 160 };
        VisualHit hit = CeilingHit(sector);
        GameConfiguration config = GameConfiguration.FromText("distinctfloorandceilingbrightness = true;");

        bool changed = MapControl.AdjustVisualCeilingBrightness3D(hit, raise: true, [0, 8, 16, 32, 64], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(8, sector.GetIntegerField("lightceiling"));
        Assert.Equal("Changed ceiling brightness to 8.", status);
    }

    [Fact]
    public void AdjustVisualCeilingBrightness3DUsesUdbAbsoluteCeilingLightField()
    {
        var sector = new Sector { Brightness = 160 };
        sector.SetField("lightceilingabsolute", true);
        sector.SetIntegerField("lightceiling", 32);
        VisualHit hit = CeilingHit(sector);
        GameConfiguration config = GameConfiguration.FromText("distinctfloorandceilingbrightness = true;");

        bool changed = MapControl.AdjustVisualCeilingBrightness3D(hit, raise: false, [0, 8, 16, 32, 64], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(16, sector.GetIntegerField("lightceiling"));
        Assert.Equal("Changed ceiling brightness to 16.", status);
    }

    [Fact]
    public void AdjustVisualCeilingBrightness3DRequiresUdmfDistinctSurfaceBrightness()
    {
        var sector = new Sector { Brightness = 160 };
        VisualHit hit = CeilingHit(sector);

        bool changed = MapControl.AdjustVisualCeilingBrightness3D(hit, raise: true, [0, 8, 16, 32, 64], MapFormat.Doom, GameConfiguration.FromText(""), out string status);

        Assert.False(changed);
        Assert.Equal(0, sector.GetIntegerField("lightceiling"));
        Assert.Equal("", status);
    }

    [Fact]
    public void AdjustVisualWallBrightness3DUsesUdbRelativeWallLightField()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        VisualHit hit = WallHit(side, SidedefPart.Middle);
        GameConfiguration config = GameConfiguration.FromText("distinctwallbrightness = true;");

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: true, [0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 255], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(8, side.GetIntegerField("light"));
        Assert.Equal("Changed wall brightness to 8.", status);
    }

    [Fact]
    public void AdjustVisualWallBrightness3DUsesUdbAbsoluteWallLightField()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        side.SetField("lightabsolute", true);
        side.SetIntegerField("light", 32);
        VisualHit hit = WallHit(side, SidedefPart.Middle);
        GameConfiguration config = GameConfiguration.FromText("distinctwallbrightness = true;");

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: false, [0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 255], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(16, side.GetIntegerField("light"));
        Assert.Equal("Changed wall brightness to 16.", status);
    }

    [Fact]
    public void AdjustVisualWallBrightness3DUsesUdbPartSpecificWallLightField()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        VisualHit hit = WallHit(side, SidedefPart.Upper);
        GameConfiguration config = GameConfiguration.FromText("distinctsidedefpartbrightness = true;");

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: true, [0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 255], MapFormat.Udmf, config, out _);

        Assert.True(changed);
        Assert.Equal(8, side.GetIntegerField("light_top"));
        Assert.Equal(0, side.GetIntegerField("light"));
    }

    [Fact]
    public void AdjustVisualWallBrightness3DRequiresUdmfDistinctWallBrightness()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        VisualHit hit = WallHit(side, SidedefPart.Middle);

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: true, [0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 255], MapFormat.Doom, GameConfiguration.FromText(""), out string status);

        Assert.False(changed);
        Assert.Equal(0, side.GetIntegerField("light"));
        Assert.Equal("", status);
    }

    [Fact]
    public void AdjustVisualWallBrightness3DUsesConfiguredStepsLikeUdb()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        VisualHit hit = WallHit(side, SidedefPart.Middle);
        GameConfiguration config = GameConfiguration.FromText("distinctwallbrightness = true;");

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: true, [0, 128, 255], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(128, side.GetIntegerField("light"));
        Assert.Equal("Changed wall brightness to 128.", status);
    }

    [Fact]
    public void AdjustVisualWallBrightness3DUsesRelativeNegativeStepsLikeUdb()
    {
        Sidedef side = WallSide(new Sector { Brightness = 160 });
        side.SetIntegerField("light", -16);
        VisualHit hit = WallHit(side, SidedefPart.Middle);
        GameConfiguration config = GameConfiguration.FromText("distinctwallbrightness = true;");

        bool changed = MapControl.AdjustVisualWallBrightness3D(hit, raise: true, [0, 8, 16, 32, 64], MapFormat.Udmf, config, out string status);

        Assert.True(changed);
        Assert.Equal(-8, side.GetIntegerField("light"));
        Assert.Equal("Changed wall brightness to -8.", status);
    }

    [Theory]
    [InlineData("angle", 270, 0, 0, "Changed thing angle to 270.")]
    [InlineData("pitch", 0, 45, 0, "Changed thing pitch to 45.")]
    [InlineData("roll", 0, 0, 315, "Changed thing roll to 315.")]
    public void VisualThingOrientation3DStatusTextMatchesUdb(string orientation, int angle, int pitch, int roll, string expected)
    {
        var thing = new Thing { Angle = angle, Pitch = pitch, Roll = roll };

        Assert.Equal(expected, MapControl.VisualThingOrientation3DStatusText(thing, orientation));
    }

    [Theory]
    [InlineData("angle", "Change thing angle")]
    [InlineData("pitch", "Change thing pitch")]
    [InlineData("roll", "Change thing roll")]
    public void VisualThingOrientationEditNameMatchesUdb(string orientation, string expected)
        => Assert.Equal(expected, MapControl.VisualThingOrientationEditName(orientation));

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

    [Theory]
    [InlineData(VisualHitKind.Floor, "Change floor height")]
    [InlineData(VisualHitKind.Ceiling, "Change ceiling height")]
    [InlineData(VisualHitKind.Thing, "Change thing height")]
    [InlineData(VisualHitKind.Wall, null)]
    public void VisualHeight3DEditNameMatchesUdbTargetKind(VisualHitKind kind, string? expected)
        => Assert.Equal(expected, MapControl.VisualHeight3DEditName(kind));

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

    [Theory]
    [InlineData("Cut", 1, "Cut 1 thing.")]
    [InlineData("Cut", 2, "Cut 2 things.")]
    [InlineData("Paste", 1, "Paste 1 thing.")]
    [InlineData("Paste", 3, "Paste 3 things.")]
    public void VisualThingSelectionEditNameMatchesUdbCopyPasteUndoText(string verb, int count, string expected)
        => Assert.Equal(expected, MapControl.VisualThingSelectionEditName(verb, count));

    [Fact]
    public void VisualThingInsertedStatusTextMatchesUdb()
        => Assert.Equal("Inserted a new thing.", MapControl.VisualThingInsertedStatusText());

    [Fact]
    public void VisualMiddleTextureCreatedStatusTextMatchesUdb()
        => Assert.Equal("Created middle texture.", MapControl.VisualMiddleTextureCreatedStatusText());

    [Fact]
    public void TryCreateVisualMiddleTexture3DCreatesTextureOnBothBlankTwoSidedSides()
    {
        var (hit, front, back) = CreateTwoSidedWallHit();

        bool changed = MapControl.TryCreateVisualMiddleTexture3D(hit, "STARTAN3");

        Assert.True(changed);
        Assert.Equal("STARTAN3", front.MidTexture);
        Assert.Equal("STARTAN3", back.MidTexture);
    }

    [Fact]
    public void TryCreateVisualMiddleTexture3DPreservesExistingOtherSideMiddleTexture()
    {
        var (hit, front, back) = CreateTwoSidedWallHit();
        back.SetTextureMid("BROWN1");

        bool changed = MapControl.TryCreateVisualMiddleTexture3D(hit, "STARTAN3");

        Assert.True(changed);
        Assert.Equal("STARTAN3", front.MidTexture);
        Assert.Equal("BROWN1", back.MidTexture);
    }

    [Fact]
    public void TryCreateVisualMiddleTexture3DDoesNotOverwriteExistingTargetMiddleTexture()
    {
        var (hit, front, back) = CreateTwoSidedWallHit();
        front.SetTextureMid("STONE2");

        bool changed = MapControl.TryCreateVisualMiddleTexture3D(hit, "STARTAN3");

        Assert.False(changed);
        Assert.Equal("STONE2", front.MidTexture);
        Assert.Equal("-", back.MidTexture);
    }

    [Fact]
    public void TryCreateVisualMiddleTexture3DDoesNotCreateRequiredMiddleTexture()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        var front = new Sidedef();
        line.AttachFront(front);
        var hit = new VisualHit(VisualHitKind.Wall, 1, new Vector3D(32, 0, 64), null, line, true, 0, 128, SidedefPart.Middle);

        bool changed = MapControl.TryCreateVisualMiddleTexture3D(hit, "STARTAN3");

        Assert.False(changed);
        Assert.Equal("-", front.MidTexture);
    }

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
    [InlineData("Vertex", "Paste vertex properties")]
    [InlineData("Linedef", "Paste linedef properties")]
    [InlineData("Sidedef", "Paste sidedef properties")]
    [InlineData("Sector", "Paste sector properties")]
    [InlineData("Thing", "Paste thing properties")]
    public void VisualPropertiesPaste3DEditNameMatchesUdbTargetKind(string kindName, string expected)
    {
        var kind = Enum.Parse<PastePropertiesElementKind>(kindName);

        Assert.Equal(expected, MapControl.VisualPropertiesPaste3DEditName([kind]));
    }

    [Fact]
    public void VisualPropertiesPaste3DEditNameMatchesUdbWallTarget()
        => Assert.Equal(
            "Paste linedef and sidedef properties",
            MapControl.VisualPropertiesPaste3DEditName(
                [PastePropertiesElementKind.Linedef, PastePropertiesElementKind.Sidedef]));

    [Theory]
    [InlineData("map2d.mode-automap", "ToggleAutomapMode")]
    [InlineData("map2d.automapmode", "ToggleAutomapMode")]
    [InlineData("map2d.imageexamplemode", "ToggleImageExampleMode")]
    [InlineData("map2d.wadauthormode", "ToggleWadAuthorMode")]
    [InlineData("map2d.editselectionmode", "BeginEditSelectionMode")]
    [InlineData("map2d.split-linedefs", "SplitLinedefs")]
    [InlineData("map2d.fit-selected-textures", "FitSelectedTextures")]
    [InlineData("map2d.3dfloor.select-control-sector", "SelectThreeDFloorControlSectors")]
    [InlineData("map2d.select3dfloorcontrolsector", "SelectThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.relocate-control-sectors", "RelocateThreeDFloorControlSectors")]
    [InlineData("map2d.relocate3dfloorcontrolsectors", "RelocateThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.duplicate-geometry", "DuplicateThreeDFloorGeometry")]
    [InlineData("map2d.duplicate3dfloorgeometry", "DuplicateThreeDFloorGeometry")]
    [InlineData("map2d.select-sectors-outline", "SelectSectorsOutline")]
    [InlineData("map2d.selectsectorsoutline", "SelectSectorsOutline")]
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
    public void Map2DEditPropertiesActionRequestsPropertyEditing()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf("case \"map2d.edit-properties\":", StringComparison.Ordinal);
        int aliasIndex = body.IndexOf("case \"map2d.classicedit\":", commandIndex, StringComparison.Ordinal);
        int eventIndex = body.IndexOf("EditRequested?.Invoke();", commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(aliasIndex > commandIndex);
        Assert.True(eventIndex > commandIndex);
    }

    [Fact]
    public void MapControlChecksAllWheelAxesForShortcutDispatch()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("var wheelKeys = EditorPointerInput.WheelKeys(e.Delta.X, e.Delta.Y);", body, StringComparison.Ordinal);
        Assert.Contains("foreach (string shortcutKey in wheelKeys)", body, StringComparison.Ordinal);
        Assert.Contains("EditorCommandCatalog.ResolveShortcut(ShortcutBindings, EditorCommandScope.Map2D, shortcutKey, accel, shift, alt)", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("map2d.increasesubdivlevel", "AdjustDrawSubdivision(increase: true)")]
    [InlineData("map2d.decreasesubdivlevel", "AdjustDrawSubdivision(increase: false)")]
    [InlineData("map2d.increasebevel", "AdjustDrawBevel(increase: true)")]
    [InlineData("map2d.decreasebevel", "AdjustDrawBevel(increase: false)")]
    [InlineData("map2d.zoomin", "ZoomBy(0.8)")]
    [InlineData("map2d.zoomout", "ZoomBy(1.25)")]
    [InlineData("map2d.centerinscreen", "FitToMap()")]
    [InlineData("map2d.scrollwest", "ScrollView(-100, 0)")]
    [InlineData("map2d.scrolleast", "ScrollView(100, 0)")]
    [InlineData("map2d.scrollnorth", "ScrollView(0, 100)")]
    [InlineData("map2d.scrollsouth", "ScrollView(0, -100)")]
    [InlineData("map2d.drawlinesmode", "ToggleDrawMode(linesOnly: true)")]
    [InlineData("map2d.drawrectanglemode", "SetShapeMode(ShapeKind.Rectangle)")]
    [InlineData("map2d.drawellipsemode", "SetShapeMode(ShapeKind.Ellipse)")]
    [InlineData("map2d.drawcurvemode", "ToggleDrawMode(linesOnly: true, curve: true)")]
    [InlineData("map2d.drawgridmode", "SetShapeMode(ShapeKind.Grid)")]
    [InlineData("map2d.drawpoint", "PlaceDrawPoint(_drawCursor)")]
    [InlineData("map2d.removepoint", "RemoveDrawPoint(_drawPoints.Count - 1)")]
    [InlineData("map2d.removefirstpoint", "RemoveDrawPoint(0)")]
    [InlineData("map2d.finishdraw", "FinishDraw()")]
    [InlineData("map2d.acceptmode", "FinishDraw()")]
    [InlineData("map2d.cancelmode", "ExitDrawModes()")]
    [InlineData("map2d.insertitem", "InsertAtCursor()")]
    [InlineData("map2d.makesectormode", "MakeSectorAtCursor()")]
    [InlineData("map2d.placevisualstart", "PlaceVisualStart()")]
    [InlineData("map2d.placethings", "PlaceThingsFromSelection()")]
    [InlineData("map2d.thinglookatcursor", "PointThingsToCursor(")]
    [InlineData("map2d.syncedthingedit", "ToggleSynchronizedThingEditing()")]
    [InlineData("map2d.classicpaintselect", "BeginClassicPaintSelection()")]
    [InlineData("map2d.bridgemode", "RunBridgeCommand()")]
    [InlineData("map2d.gzdbvisualmode", "Toggle3DMode()")]
    [InlineData("map2d.curvelinesmode", "CurveSelectedLinedefs()")]
    [InlineData("map2d.fliplinedefs", "FlipLinedefs()")]
    [InlineData("map2d.flipsidedefs", "FlipSidedefs()")]
    [InlineData("map2d.selectsinglesided", "KeepSelectedLinedefsBySidedness(doubleSided: false)")]
    [InlineData("map2d.selectdoublesided", "KeepSelectedLinedefsBySidedness(doubleSided: true)")]
    [InlineData("map2d.alignlinedefs", "AlignLinedefs()")]
    [InlineData("map2d.splitlinedefs", "SplitLinedefs()")]
    [InlineData("map2d.dissolveitem", "DissolveItem()")]
    [InlineData("map2d.joinsectors", "JoinOrMergeSelectedSectors(merge: false)")]
    [InlineData("map2d.mergesectors", "JoinOrMergeSelectedSectors(merge: true)")]
    [InlineData("map2d.lowerfloor8", "AdjustSectorHeights(SectorHeightPart.Floor, -8)")]
    [InlineData("map2d.raisefloor8", "AdjustSectorHeights(SectorHeightPart.Floor, 8)")]
    [InlineData("map2d.lowerceiling8", "AdjustSectorHeights(SectorHeightPart.Ceiling, -8)")]
    [InlineData("map2d.raiseceiling8", "AdjustSectorHeights(SectorHeightPart.Ceiling, 8)")]
    [InlineData("map2d.togglecomments", "ToggleComments()")]
    [InlineData("map2d.togglefixedthingsscale", "ToggleFixedThingsScale()")]
    [InlineData("map2d.togglealwaysshowvertices", "ToggleAlwaysShowVertices()")]
    [InlineData("map2d.togglehighlight", "ToggleHighlight()")]
    [InlineData("map3d.togglehighlight", "ToggleHighlight()")]
    [InlineData("map2d.verticesmode", "SetEditMode(EditMode.Vertices)")]
    [InlineData("map2d.linedefsmode", "SetEditMode(EditMode.Linedefs)")]
    [InlineData("map2d.sectorsmode", "SetEditMode(EditMode.Sectors)")]
    [InlineData("map2d.thingsmode", "SetEditMode(EditMode.Things)")]
    [InlineData("map2d.viewmodenormal", "SetViewMode2D(ClassicViewMode.Wireframe)")]
    [InlineData("map2d.viewmodebrightness", "SetViewMode2D(ClassicViewMode.Brightness)")]
    [InlineData("map2d.viewmodefloors", "SetViewMode2D(ClassicViewMode.FloorTextures)")]
    [InlineData("map2d.flooralignmode", "SetViewMode2D(ClassicViewMode.FloorTextures)")]
    [InlineData("map2d.viewmodeceilings", "SetViewMode2D(ClassicViewMode.CeilingTextures)")]
    [InlineData("map2d.ceilingalignmode", "SetViewMode2D(ClassicViewMode.CeilingTextures)")]
    [InlineData("map2d.nextviewmode", "NextViewMode2D()")]
    [InlineData("map2d.previousviewmode", "PreviousViewMode2D()")]
    [InlineData("map2d.raisebrightness8", "AdjustSectorBrightness(raise: true)")]
    [InlineData("map2d.lowerbrightness8", "AdjustSectorBrightness(raise: false)")]
    [InlineData("map2d.applylightfogflag", "ApplyLightFogFlag()")]
    [InlineData("map2d.togglesnap", "ToggleSnapToGrid()")]
    [InlineData("map2d.togglegrid", "ToggleGridRendering()")]
    [InlineData("map2d.toggledynamicgrid", "ToggleDynamicGridSize()")]
    [InlineData("map2d.aligngridtolinedef", "AlignGridToSelectedLinedef()")]
    [InlineData("map2d.setgridorigintovertex", "SetGridOriginToSelectedVertex()")]
    [InlineData("map2d.resetgrid", "ResetGridTransform()")]
    [InlineData("map2d.smartgridtransform", "SmartGridTransform()")]
    [InlineData("map2d.griddec", "ChangeGridSize(larger: false)")]
    [InlineData("map2d.gridinc", "ChangeGridSize(larger: true)")]
    public void UdbClassicActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Fact]
    public void CurveLinedefsWarningMatchesUdbText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("const string message = \"This action requres a selection!\";", body, StringComparison.Ordinal);
    }

    [Fact]
    public void GridRenderingCommandControlsVisibleGridOnly()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("private bool _renderGrid = true;", body, StringComparison.Ordinal);
        Assert.Contains("if (!_renderGrid) { _gridLineCount = 0; return; }", body, StringComparison.Ordinal);
        Assert.Contains("public string ToggleGridRendering()", body, StringComparison.Ordinal);
        Assert.Contains("RenderGridEnabled = !RenderGridEnabled;", body, StringComparison.Ordinal);
        Assert.Contains("Grid rendering is ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"map2d.togglegrid\":\n                ToggleSnapToGrid();", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicPaintSelectUsesHeldActionState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("commandId is \"map3d.orbit\" or \"map2d.classicpaintselect\" or \"map2d.pan_view\"", body, StringComparison.Ordinal);
        Assert.Contains("if (commandId == \"map2d.classicpaintselect\") EndClassicPaintSelection();", body, StringComparison.Ordinal);
        Assert.Contains("if (_classicPaintSelectPressed)", body, StringComparison.Ordinal);
        Assert.Contains("ApplyClassicPaintSelection(_cursorWorld, e.KeyModifiers);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PanViewUsesHeldActionState()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("case \"map2d.pan_view\":", body, StringComparison.Ordinal);
        Assert.Contains("BeginHeldPanView();", body, StringComparison.Ordinal);
        Assert.Contains("if (commandId == \"map2d.pan_view\") EndHeldPanView();", body, StringComparison.Ordinal);
        Assert.Contains("if (_heldPanView)", body, StringComparison.Ordinal);
        Assert.Contains("PanViewByPointerDelta(pos);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void EditSelectionModeExposesStateAndMovesSelectionOnDrag()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("public bool EditSelectionModeActive => _editSelectionMode;", body, StringComparison.Ordinal);
        Assert.Contains("public void BeginEditSelectionMode() => SetEditSelectionMode(true);", body, StringComparison.Ordinal);
        Assert.Contains("_moveCandidate = _selectionDoneOnPress || (_editSelectionMode && HasTransformableSelection());", body, StringComparison.Ordinal);
        Assert.Contains("private bool HasTransformableSelection()", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SynchronizedThingEditingToggleExposesStateAndStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("public bool SynchronizedThingEditing => _synchronizedThingEditing;", body, StringComparison.Ordinal);
        Assert.Contains("public bool ToggleSynchronizedThingEditing()", body, StringComparison.Ordinal);
        Assert.Contains("Things editing is SYNCHRONIZED", body, StringComparison.Ordinal);
        Assert.Contains("Things editing is not synchronized", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("map3d.resettexture", "ResetVisualTexture3D(local: false)")]
    [InlineData("map3d.resettextureudmf", "ResetVisualTexture3D(local: true)")]
    [InlineData("map3d.visualfittextures", "FitSelectedVisualTextures3D()")]
    [InlineData("map3d.toggleupperunpegged", "ToggleUnpegged3D(upper: true)")]
    [InlineData("map3d.togglelowerunpegged", "ToggleUnpegged3D(upper: false)")]
    public void UdbVisualTextureActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Theory]
    [InlineData("map2d.thingaligntowall", "AlignSelectedThingsToWall()")]
    [InlineData("map3d.insertitem", "InsertThingAtTarget3D()")]
    [InlineData("map3d.copyselection", "CopyVisualThingSelection3D()")]
    [InlineData("map3d.cutselection", "CutVisualThingSelection3D()")]
    [InlineData("map3d.pasteselection", "PasteVisualThingSelection3D()")]
    [InlineData("map3d.movecameratocursor", "MoveCameraToCursor()")]
    [InlineData("map3d.movethingleft", "MoveThingTargets3D(new Vec2D(0, -_grid.GridSizeF))")]
    [InlineData("map3d.movethingright", "MoveThingTargets3D(new Vec2D(0, _grid.GridSizeF))")]
    [InlineData("map3d.movethingfwd", "MoveThingTargets3D(new Vec2D(-_grid.GridSizeF, 0))")]
    [InlineData("map3d.movethingback", "MoveThingTargets3D(new Vec2D(_grid.GridSizeF, 0))")]
    [InlineData("map3d.applycamerarotationtothings", "ApplyCameraRotationToSelectedThings3D()")]
    [InlineData("map3d.thingaligntowall", "AlignSelectedVisualThingsToWall3D()")]
    [InlineData("map3d.showvisualthings", "CycleVisualThings3D()")]
    [InlineData("map3d.alphabasedtexturehighlighting", "ToggleAlphaBasedTextureHighlighting()")]
    [InlineData("map3d.gztogglemodels", "CycleModelRenderMode()")]
    [InlineData("map3d.gztoggleenhancedrendering", "ToggleEnhancedRenderingEffects()")]
    [InlineData("map3d.toggledynamiclightsrendering", "CycleLightRenderMode()")]
    [InlineData("map3d.gztogglelights", "CycleLightRenderMode()")]
    [InlineData("map3d.toggleclassicrendering", "ToggleClassicRendering()")]
    [InlineData("map3d.togglefogrendering", "ToggleDrawFog()")]
    [InlineData("map3d.gztogglefog", "ToggleDrawFog()")]
    [InlineData("map3d.toggleskyrendering", "ToggleDrawSky()")]
    [InlineData("map3d.gztogglesky", "ToggleDrawSky()")]
    [InlineData("map3d.toggleeventlines", "ToggleEventLines()")]
    [InlineData("map3d.gztoggleeventlines", "ToggleEventLines()")]
    [InlineData("map3d.togglevisualvertices", "ToggleVisualVertices()")]
    [InlineData("map3d.gztogglevisualvertices", "ToggleVisualVertices()")]
    public void UdbVisualThingActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Theory]
    [InlineData("map3d.lookthroughthing", "LookThroughSelectedThing3D()")]
    [InlineData("map3d.visualedit", "OpenTargetDialog3D()")]
    [InlineData("map3d.clearselection", "ClearSelection3D()")]
    [InlineData("map3d.toggleslope", "ToggleSlope3D()")]
    [InlineData("map3d.resetslope", "ResetSlope3D()")]
    [InlineData("map3d.togglevisualslopepicking", "ToggleVisualSidedefSlopePicking()")]
    [InlineData("map3d.togglevisualvertexslopepicking", "ToggleVisualVertexSlopePicking()")]
    [InlineData("map3d.togglevisualvertexslopeadjacentselection", "ToggleVisualVertexSlopeAdjacentSelection()")]
    [InlineData("map3d.deleteitem", "DeleteVisualTargets3D()")]
    public void UdbVisualBaseAndSlopeActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Theory]
    [InlineData("map3d.moveforward", "map3d.move-forward")]
    [InlineData("map3d.movebackward", "map3d.move-backward")]
    [InlineData("map3d.moveleft", "map3d.move-left")]
    [InlineData("map3d.moveright", "map3d.move-right")]
    [InlineData("map3d.moveup", "map3d.move-up")]
    [InlineData("map3d.movedown", "map3d.move-down")]
    public void UdbVisualCameraMovementAliasesAreHeldCommands(string aliasCommandId, string canonicalCommandId)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains($"\"{aliasCommandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"case \"{aliasCommandId}\":", body, StringComparison.Ordinal);
        Assert.Contains($"_heldMapCommands.Contains(\"{canonicalCommandId}\") || _heldMapCommands.Contains(\"{aliasCommandId}\")", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("map3d.scaleup", "ChangeVisualScale3D(1, 1)")]
    [InlineData("map3d.scaledown", "ChangeVisualScale3D(-1, -1)")]
    [InlineData("map3d.scaleupx", "ChangeVisualScale3D(1, 0)")]
    [InlineData("map3d.scaledownx", "ChangeVisualScale3D(-1, 0)")]
    [InlineData("map3d.scaleupy", "ChangeVisualScale3D(0, 1)")]
    [InlineData("map3d.scaledowny", "ChangeVisualScale3D(0, -1)")]
    [InlineData("map3d.lowersector1", "AdjustTarget3D(-1)")]
    [InlineData("map3d.raisesector1", "AdjustTarget3D(1)")]
    [InlineData("map3d.lowersector8", "AdjustTarget3D(-8)")]
    [InlineData("map3d.raisesector8", "AdjustTarget3D(8)")]
    [InlineData("map3d.lowersector128", "AdjustTarget3D(-128)")]
    [InlineData("map3d.raisesector128", "AdjustTarget3D(128)")]
    [InlineData("map3d.lowermapelementbygridsize", "AdjustTarget3D(-_grid.GridSize)")]
    [InlineData("map3d.raisemapelementbygridsize", "AdjustTarget3D(_grid.GridSize)")]
    [InlineData("map3d.lowersectortonearest", "AdjustTargetToNearest3D(raise: false")]
    [InlineData("map3d.raisesectortonearest", "AdjustTargetToNearest3D(raise: true")]
    [InlineData("map3d.lowerbrightness8", "AdjustTargetBrightness3D(raise: false)")]
    [InlineData("map3d.raisebrightness8", "AdjustTargetBrightness3D(raise: true)")]
    [InlineData("map3d.matchbrightness", "MatchBrightness3D()")]
    public void UdbVisualAdjustmentActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Theory]
    [InlineData("map3d.togglegravity", "_walkMode = !_walkMode;")]
    [InlineData("map3d.texturecopy", "CopyTexture3D()")]
    [InlineData("map3d.texturepaste", "ApplyTexture3D()")]
    [InlineData("map3d.floodfilltextures", "FloodFillTexture3D()")]
    [InlineData("map3d.textureselect", "BrowseTexturesRequested?.Invoke")]
    [InlineData("map3d.visualautoalign", "AutoAlignTarget3D(alignX: true, alignY: true)")]
    [InlineData("map3d.visualautoalignx", "AutoAlignTarget3D(alignX: true, alignY: false)")]
    [InlineData("map3d.visualautoaligny", "AutoAlignTarget3D(alignX: false, alignY: true)")]
    [InlineData("map3d.visualautoaligntoselection", "AutoAlignSelectedVisualTextures3D(alignX: true, alignY: true)")]
    [InlineData("map3d.visualautoaligntoselectionx", "AutoAlignSelectedVisualTextures3D(alignX: true, alignY: false)")]
    [InlineData("map3d.visualautoaligntoselectiony", "AutoAlignSelectedVisualTextures3D(alignX: false, alignY: true)")]
    [InlineData("map3d.texturecopyoffsets", "CopyTextureOffsets3D()")]
    [InlineData("map3d.texturepasteoffsets", "PasteTextureOffsets3D()")]
    [InlineData("map3d.copyproperties", "CopyVisualPropertiesTarget()")]
    [InlineData("map3d.pasteproperties", "PasteVisualPropertiesTargets()")]
    [InlineData("map3d.pastepropertieswithoptions", "PastePropertiesOptionsRequested?.Invoke()")]
    public void UdbVisualTextureActionAliasesAreDispatchedToExistingHandlers(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
    }

    [Theory]
    [InlineData("map3d.movetextureleft", "NudgeTargetOffset3D(-1, 0)")]
    [InlineData("map3d.movetextureright", "NudgeTargetOffset3D(1, 0)")]
    [InlineData("map3d.movetextureup", "NudgeTargetOffset3D(0, -1)")]
    [InlineData("map3d.movetexturedown", "NudgeTargetOffset3D(0, 1)")]
    [InlineData("map3d.movetextureleft8", "NudgeTargetOffset3D(-8, 0)")]
    [InlineData("map3d.movetextureright8", "NudgeTargetOffset3D(8, 0)")]
    [InlineData("map3d.movetextureup8", "NudgeTargetOffset3D(0, -8)")]
    [InlineData("map3d.movetexturedown8", "NudgeTargetOffset3D(0, 8)")]
    [InlineData("map3d.movetextureleftgs", "NudgeTargetOffset3D(-_grid.GridSize, 0)")]
    [InlineData("map3d.movetexturerightgs", "NudgeTargetOffset3D(_grid.GridSize, 0)")]
    [InlineData("map3d.movetextureupgs", "NudgeTargetOffset3D(0, -_grid.GridSize)")]
    [InlineData("map3d.movetexturedowngs", "NudgeTargetOffset3D(0, _grid.GridSize)")]
    public void UdbVisualTextureMovementActionAliasesAreDispatched(string commandId, string handlerCall)
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf($"case \"{commandId}\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf(handlerCall, commandIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(handlerIndex > commandIndex);
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
        Assert.Contains("case \"map3d.pitchclockwise\":", body, StringComparison.Ordinal);
        Assert.Contains("case \"map3d.pitchcounterclockwise\":", body, StringComparison.Ordinal);
        Assert.Contains("case \"map3d.rollclockwise\":", body, StringComparison.Ordinal);
        Assert.Contains("case \"map3d.rollcounterclockwise\":", body, StringComparison.Ordinal);
        Assert.Contains("ChangeThingPitchTargets3D(-5);", body, StringComparison.Ordinal);
        Assert.Contains("ChangeThingPitchTargets3D(5);", body, StringComparison.Ordinal);
        Assert.Contains("ChangeThingRollTargets3D(-5);", body, StringComparison.Ordinal);
        Assert.Contains("ChangeThingRollTargets3D(5);", body, StringComparison.Ordinal);
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
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualTextureReset3DEditName(VisualHitKind.Wall, false));", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(\"Texture offsets reset.\");", editNameIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(editNameIndex > methodIndex);
        Assert.True(statusIndex > editNameIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Reset offsets\")", body, StringComparison.Ordinal);
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
        int editIndex = body.IndexOf("EditBegun?.Invoke(VisualCameraRotationEditName());", methodIndex, StringComparison.Ordinal);
        int rotateIndex = body.IndexOf("VisualThingRotation.ApplyCameraRotation", editIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Applied camera rotation and pitch to {things.Count} thing", rotateIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(editIndex > methodIndex);
        Assert.True(rotateIndex > editIndex);
        Assert.True(statusIndex > rotateIndex);
        Assert.DoesNotContain("things.Count == 1 ? \"Apply camera rotation to thing\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualCameraRotationEditNameMatchesUdb()
        => Assert.Equal("Apply camera rotation to things", MapControl.VisualCameraRotationEditName());

    [Fact]
    public void PlaceThingAtCursor3DUsesUdbInvalidHitWarning()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int commandIndex = body.IndexOf("case \"map3d.place-thing-at-cursor\":", StringComparison.Ordinal);
        int aliasIndex = body.IndexOf("case \"map3d.placethingatcursor\":", commandIndex, StringComparison.Ordinal);
        int methodIndex = body.IndexOf("private bool PlaceThingTargetsAtCursor3D()", StringComparison.Ordinal);
        int missingTargetIndex = body.IndexOf("if (_target3D is not { } target)", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Cannot place Thing here", methodIndex, StringComparison.Ordinal);
        int emptySelectionIndex = body.IndexOf("if (things.Count == 0) return false;", methodIndex, StringComparison.Ordinal);

        Assert.True(commandIndex >= 0);
        Assert.True(aliasIndex > commandIndex);
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
        int cutEditIndex = body.IndexOf("EditBegun?.Invoke(VisualThingSelectionEditName(\"Cut\", things.Count));", cutIndex, StringComparison.Ordinal);
        int cutStatusIndex = body.IndexOf("VisualThingSelectionStatusText(\"Cut\", things.Count)", cutIndex, StringComparison.Ordinal);
        int pasteIndex = body.IndexOf("private bool PasteVisualThingSelection3D()", StringComparison.Ordinal);
        int cannotPasteIndex = body.IndexOf("Cannot paste here!", pasteIndex, StringComparison.Ordinal);
        int pasteEditIndex = body.IndexOf("EditBegun?.Invoke(VisualThingSelectionEditName(\"Paste\", ClipboardThingCount(_visualThingClipboard)));", cannotPasteIndex, StringComparison.Ordinal);
        int pasteCallIndex = body.IndexOf("PasteResult result = SelectionClipboard.Paste", pasteEditIndex, StringComparison.Ordinal);
        int pasteStatusIndex = body.IndexOf("VisualThingSelectionStatusText(\"Pasted\", pasted.Count)", pasteIndex, StringComparison.Ordinal);

        Assert.True(copyIndex >= 0);
        Assert.True(copyStatusIndex > copyIndex);
        Assert.True(cutIndex >= 0);
        Assert.True(cutEditIndex > cutIndex);
        Assert.True(cutStatusIndex > cutIndex);
        Assert.True(pasteIndex >= 0);
        Assert.True(cannotPasteIndex > pasteIndex);
        Assert.True(pasteEditIndex > cannotPasteIndex);
        Assert.True(pasteCallIndex > pasteEditIndex);
        Assert.True(pasteStatusIndex > cannotPasteIndex);
    }

    [Fact]
    public void InsertThingAtTarget3DUsesUdbStatusesAndWarnings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private bool InsertThingAtTarget3D()", StringComparison.Ordinal);
        int missingTargetIndex = body.IndexOf("if (_target3D is not { } target)", methodIndex, StringComparison.Ordinal);
        int warningIndex = body.IndexOf("Cannot insert thing here!", missingTargetIndex, StringComparison.Ordinal);
        int createMiddleIndex = body.IndexOf("TryCreateVisualMiddleTexture3D(target, DefaultWallTexture3D());", warningIndex, StringComparison.Ordinal);
        int middleStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualMiddleTextureCreatedStatusText());", createMiddleIndex, StringComparison.Ordinal);
        int insertIndex = body.IndexOf("InsertThingAt(new Vec2D(target.Point.x, target.Point.y), snap: false, height: target.Point.z);", middleStatusIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingInsertedStatusText());", insertIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(missingTargetIndex > methodIndex);
        Assert.True(warningIndex > missingTargetIndex);
        Assert.True(createMiddleIndex > warningIndex);
        Assert.True(middleStatusIndex > createMiddleIndex);
        Assert.True(insertIndex > middleStatusIndex);
        Assert.True(statusIndex > insertIndex);
    }

    private static (VisualHit Hit, Sidedef Front, Sidedef Back) CreateTwoSidedWallHit()
    {
        var sector = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var otherSector = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        var front = new Sidedef { Sector = sector };
        var back = new Sidedef { Sector = otherSector };
        line.AttachFront(front);
        line.AttachBack(back);
        var hit = new VisualHit(VisualHitKind.Wall, 1, new Vector3D(32, 0, 64), sector, line, true, 0, 128, SidedefPart.Upper);
        return (hit, front, back);
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
        int editIndex = body.IndexOf("EditBegun?.Invoke(VisualFitTexture3DEditName(fitWidth: true, fitHeight: true));", resourcesIndex, StringComparison.Ordinal);
        int fitIndex = body.IndexOf("SidedefTextureFitting.Fit(", editIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetsIndex > methodIndex);
        Assert.True(warningIndex > targetsIndex);
        Assert.True(resourcesIndex > warningIndex);
        Assert.True(editIndex > resourcesIndex);
        Assert.True(fitIndex > editIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Fit visual textures\")", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FitSelectedTexturesUsesSelectionWarningBeforeResourceGuard()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("public string FitSelectedTextures()", StringComparison.Ordinal);
        int selectionIndex = body.IndexOf("if (_map.SelectedLinedefsCount == 0) return \"Select one or more linedefs to fit textures.\";", methodIndex, StringComparison.Ordinal);
        int resourcesIndex = body.IndexOf("if (_resources == null) return \"No resources loaded for texture dimensions.\";", selectionIndex, StringComparison.Ordinal);
        int editIndex = body.IndexOf("EditBegun?.Invoke(\"Fit selected textures\");", resourcesIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(selectionIndex > methodIndex);
        Assert.True(resourcesIndex > selectionIndex);
        Assert.True(editIndex > resourcesIndex);
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
    public void ApplyTexture3DUsesUdbEditNameAndStatusForLastAppliedTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int chosenIndex = body.IndexOf("ApplyTextureToTarget(name, pasted: false);", StringComparison.Ordinal);
        int pasteIndex = body.IndexOf("ApplyTextureToTarget(_texClipboard3D!, pasted: true);", chosenIndex, StringComparison.Ordinal);
        int methodIndex = body.IndexOf("private void ApplyTextureToTarget(string tex, bool pasted)", pasteIndex, StringComparison.Ordinal);
        int editIndex = body.IndexOf("EditBegun?.Invoke(TextureApplied3DEditName(tex, targets[^1].Kind, pasted));", methodIndex, StringComparison.Ordinal);
        int loopIndex = body.IndexOf("foreach (var h in targets) ApplyTextureToHit(h, tex);", editIndex, StringComparison.Ordinal);
        int formatterIndex = body.IndexOf("TexturePasted3DStatusText(tex, targets[^1].Kind)", loopIndex, StringComparison.Ordinal);

        Assert.True(chosenIndex >= 0);
        Assert.True(pasteIndex > chosenIndex);
        Assert.True(methodIndex >= 0);
        Assert.True(editIndex > methodIndex);
        Assert.True(loopIndex > editIndex);
        Assert.True(formatterIndex > loopIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Apply texture\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("TextureApplied3DStatusText", body, StringComparison.Ordinal);
    }

    [Fact]
    public void FloodFillTexture3DUsesUdbEditNameAndStatusWithTextureName()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void FloodFillTexture3D()", StringComparison.Ordinal);
        int editIndex = body.IndexOf("EditBegun?.Invoke(VisualTextureFloodFill3DEditName(hit.Kind, fillTexture));", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("FinishFloodFill3D(VisualTextureFloodFill3DStatusText(hit.Kind, fillTexture));", editIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(editIndex > methodIndex);
        Assert.True(statusIndex > editIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Flood-fill floors\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Flood-fill ceilings\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Flood-fill textures\")", body, StringComparison.Ordinal);
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
        int pasteEditIndex = body.IndexOf("EditBegun?.Invoke(TextureOffsetsPasted3DEditName());", pasteIndex, StringComparison.Ordinal);
        int pasteStatusIndex = body.IndexOf("TextureOffsetsPasted3DStatusText(offsets.X, offsets.Y)", pasteEditIndex, StringComparison.Ordinal);

        Assert.True(copyIndex >= 0);
        Assert.True(copyStatusIndex > copyIndex);
        Assert.True(pasteIndex >= 0);
        Assert.True(pasteEditIndex > pasteIndex);
        Assert.True(pasteStatusIndex > pasteEditIndex);
        Assert.DoesNotContain("copied offsets {_texOffsetClipboard3D.Value.X}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Paste offsets\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("pasted offsets to {targetCount}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void NudgeTargetOffset3DUsesUdbStatusForLastOffsetTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void NudgeTargetOffset3D(int deltaX, int deltaY)", StringComparison.Ordinal);
        int statusVariableIndex = body.IndexOf("string offsetStatus = string.Empty;", methodIndex, StringComparison.Ordinal);
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualTextureOffset3DEditName());", statusVariableIndex, StringComparison.Ordinal);
        int wallStatusIndex = body.IndexOf("offsetStatus = VisualTextureOffset3DStatusText(VisualHitKind.Wall", statusVariableIndex, StringComparison.Ordinal);
        int flatStatusIndex = body.IndexOf("offsetStatus = VisualTextureOffset3DStatusText(", wallStatusIndex + 1, StringComparison.Ordinal);
        int unsupportedFlatOffsetIndex = body.IndexOf("VisualFlatTextureOffsetUnsupportedMapFormatMessage()", flatStatusIndex, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(offsetStatus);", flatStatusIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(editNameIndex > statusVariableIndex);
        Assert.True(wallStatusIndex > editNameIndex);
        Assert.True(flatStatusIndex > wallStatusIndex);
        Assert.True(unsupportedFlatOffsetIndex > flatStatusIndex);
        Assert.True(finalStatusIndex > flatStatusIndex);
        Assert.DoesNotContain("offset {changed} target", body, StringComparison.Ordinal);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Texture offset\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"floor/ceiling texture offsets cannot be changed in this map format\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetVisualTexture3DUsesUdbStatusForLastResetTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ResetVisualTexture3D(bool local)", StringComparison.Ordinal);
        int initialIndex = body.IndexOf("string resetStatus = VisualTextureReset3DStatusText(VisualHitKind.Wall, local);", methodIndex, StringComparison.Ordinal);
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualTextureReset3DEditName(hit.Kind, local));", initialIndex, StringComparison.Ordinal);
        int assignmentIndex = body.IndexOf("resetStatus = VisualTextureReset3DStatusText(hit.Kind, local);", editNameIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(resetStatus);", assignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(initialIndex > methodIndex);
        Assert.True(editNameIndex > initialIndex);
        Assert.True(assignmentIndex > editNameIndex);
        Assert.True(statusIndex > assignmentIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Reset local texture offsets\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("local texture fields reset", body, StringComparison.Ordinal);
        Assert.DoesNotContain("texture offsets reset", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeVisualScale3DUsesUdbEditNamesAndStatusForLastScaledTarget()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void ChangeVisualScale3D(int incrementX, int incrementY)", StringComparison.Ordinal);
        int statusVariableIndex = body.IndexOf("string scaleStatus = string.Empty;", methodIndex, StringComparison.Ordinal);
        int thingEditIndex = body.IndexOf("EditBegun?.Invoke(VisualScale3DEditName(hit.Kind));", statusVariableIndex, StringComparison.Ordinal);
        int thingStatusIndex = body.IndexOf("scaleStatus = VisualScale3DStatusText(", thingEditIndex, StringComparison.Ordinal);
        int wallEditIndex = body.IndexOf("EditBegun?.Invoke(VisualScale3DEditName(hit.Kind));", thingStatusIndex, StringComparison.Ordinal);
        int flatEditIndex = body.IndexOf("EditBegun?.Invoke(VisualScale3DEditName(hit.Kind));", wallEditIndex + 1, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(scaleStatus);", thingStatusIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(thingEditIndex > statusVariableIndex);
        Assert.True(thingStatusIndex > thingEditIndex);
        Assert.True(wallEditIndex > thingStatusIndex);
        Assert.True(flatEditIndex > wallEditIndex);
        Assert.True(finalStatusIndex > thingStatusIndex);
        Assert.DoesNotContain("scaled {changed} target", body, StringComparison.Ordinal);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Change visual scale\")", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualThingOrientation3DUsesUdbEditNameBeforeMutationAndStatusForLastChangedThing()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int rotateMethodIndex = body.IndexOf("private bool RotateThingTargets3D(int angleIncrement)", StringComparison.Ordinal);
        int rotateEditIndex = body.IndexOf("BeginThingOrientationChange3D(things, VisualThingOrientationEditName(\"angle\"))", rotateMethodIndex, StringComparison.Ordinal);
        int rotateMutationIndex = body.IndexOf("VisualThingRotation.Rotate(things, angleIncrement", rotateEditIndex, StringComparison.Ordinal);
        int pitchMethodIndex = body.IndexOf("private bool ChangeThingPitchTargets3D(int increment)", StringComparison.Ordinal);
        int pitchEditIndex = body.IndexOf("BeginThingOrientationChange3D(things, VisualThingOrientationEditName(\"pitch\"))", pitchMethodIndex, StringComparison.Ordinal);
        int pitchMutationIndex = body.IndexOf("VisualThingRotation.ChangePitch(things, increment);", pitchEditIndex, StringComparison.Ordinal);
        int rollMethodIndex = body.IndexOf("private bool ChangeThingRollTargets3D(int increment)", StringComparison.Ordinal);
        int rollEditIndex = body.IndexOf("BeginThingOrientationChange3D(things, VisualThingOrientationEditName(\"roll\"))", rollMethodIndex, StringComparison.Ordinal);
        int rollMutationIndex = body.IndexOf("VisualThingRotation.ChangeRoll(things, increment);", rollEditIndex, StringComparison.Ordinal);
        int finishIndex = body.IndexOf("private void FinishThingOrientationChange3D(IReadOnlyList<Thing> things, string orientation)", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualThingOrientation3DStatusText(things[^1], orientation));", finishIndex, StringComparison.Ordinal);

        Assert.True(rotateEditIndex > rotateMethodIndex);
        Assert.True(rotateMutationIndex > rotateEditIndex);
        Assert.True(pitchEditIndex > pitchMethodIndex);
        Assert.True(pitchMutationIndex > pitchEditIndex);
        Assert.True(rollEditIndex > rollMethodIndex);
        Assert.True(rollMutationIndex > rollEditIndex);
        Assert.True(finishIndex > rollMutationIndex);
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
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualPropertiesPaste3DEditName(availableOptions.Tabs.Select(tab => tab.Kind)));", appliedKindsIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("VisualPropertiesPasted3DStatusText(appliedKinds)", editNameIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(appliedKindsIndex > methodIndex);
        Assert.True(editNameIndex > appliedKindsIndex);
        Assert.True(statusIndex > editNameIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Paste visual properties\")", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Pasted properties to {TargetText(kind, count)}", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualBrightnessStep3DUsesUdbEditNamesAndSectorFallbackStatus()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("private void AdjustTargetBrightness3D(bool raise)", StringComparison.Ordinal);
        int levelsIndex = body.IndexOf("IReadOnlyList<int> brightnessLevels = _gameConfig?.BrightnessLevels ?? [];", methodIndex, StringComparison.Ordinal);
        int wallIndex = body.IndexOf("AdjustVisualWallBrightness3D(h, raise, brightnessLevels, _mapFormat, _gameConfig, out brightnessStatus)", methodIndex, StringComparison.Ordinal);
        int wallEditIndex = body.IndexOf("EditBegun?.Invoke(VisualBrightness3DEditName(h.Kind));", wallIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("AdjustVisualCeilingBrightness3D(h, raise, brightnessLevels, _mapFormat, _gameConfig, out brightnessStatus)", wallIndex, StringComparison.Ordinal);
        int ceilingEditIndex = body.IndexOf("EditBegun?.Invoke(VisualBrightness3DEditName(h.Kind));", ceilingIndex, StringComparison.Ordinal);
        int filterIndex = body.IndexOf("if (h.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall)) continue;", methodIndex, StringComparison.Ordinal);
        int sectorEditIndex = body.IndexOf("EditBegun?.Invoke(VisualBrightness3DEditName(VisualHitKind.Floor));", filterIndex, StringComparison.Ordinal);
        int sectorWriteIndex = body.IndexOf("SectorBrightnessAdjustment.NextHigher(brightnessLevels, s.Brightness)", methodIndex, StringComparison.Ordinal);
        int statusAssignmentIndex = body.IndexOf("brightnessStatus = VisualBrightness3DStatusText(VisualHitKind.Floor, s.Brightness);", sectorWriteIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(brightnessStatus);", statusAssignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(levelsIndex > methodIndex);
        Assert.True(wallIndex > levelsIndex);
        Assert.True(wallEditIndex > wallIndex);
        Assert.True(ceilingIndex > wallEditIndex);
        Assert.True(ceilingEditIndex > ceilingIndex);
        Assert.True(filterIndex > ceilingIndex);
        Assert.True(sectorEditIndex > filterIndex);
        Assert.True(sectorWriteIndex > sectorEditIndex);
        Assert.True(statusAssignmentIndex > sectorWriteIndex);
        Assert.True(statusIndex > statusAssignmentIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Change brightness\")", body, StringComparison.Ordinal);
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
        int editIndex = body.IndexOf("EditBegun?.Invoke(editLabel);", statusVariableIndex, StringComparison.Ordinal);
        int applyIndex = body.IndexOf("ApplyHeightDelta(h, step);", editIndex, StringComparison.Ordinal);
        int statusAssignmentIndex = body.IndexOf("heightStatus = VisualHeight3DStatusText(h);", applyIndex, StringComparison.Ordinal);
        int finalStatusIndex = body.IndexOf("Target3DChanged?.Invoke(heightStatus);", statusAssignmentIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusVariableIndex > methodIndex);
        Assert.True(editIndex > statusVariableIndex);
        Assert.True(applyIndex > editIndex);
        Assert.True(statusAssignmentIndex > applyIndex);
        Assert.True(finalStatusIndex > statusAssignmentIndex);
        Assert.DoesNotContain("EditBegun?.Invoke(\"Change height\")", body, StringComparison.Ordinal);
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
        int flatEditIndex = body.IndexOf("EditBegun?.Invoke(VisualAutoAlign3DEditName(alignX, alignY, selected: false));", flatIndex, StringComparison.Ordinal);
        int flatStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: false));", flatIndex, StringComparison.Ordinal);
        int selectedIndex = body.IndexOf("private void AutoAlignSelectedVisualTextures3D(bool alignX, bool alignY)", StringComparison.Ordinal);
        int selectedEditIndex = body.IndexOf("EditBegun?.Invoke(VisualAutoAlign3DEditName(alignX, alignY, selected: true));", selectedIndex, StringComparison.Ordinal);
        int selectedStatusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualAutoAlign3DStatusText(alignX, alignY, selected: true));", selectedIndex, StringComparison.Ordinal);
        int targetIndex = body.IndexOf("AutoAlignSide3D(sd, alignX, alignY, VisualAutoAlign3DEditName(alignX, alignY, selected: false));", sideStatusIndex, StringComparison.Ordinal);

        Assert.True(sideIndex >= 0);
        Assert.True(sideStatusIndex > sideIndex);
        Assert.True(targetIndex > sideStatusIndex);
        Assert.True(flatIndex > sideIndex);
        Assert.True(flatEditIndex > flatIndex);
        Assert.True(flatStatusIndex > flatEditIndex);
        Assert.True(selectedIndex > flatIndex);
        Assert.True(selectedEditIndex > selectedIndex);
        Assert.True(selectedStatusIndex > selectedEditIndex);
        string visual3DBody = body[sideIndex..body.IndexOf("// Adjusts the selected", selectedIndex, StringComparison.Ordinal)];
        Assert.DoesNotContain("Auto-align (", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto-align flat textures", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto-align selected textures", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aligned {n} sidedef", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aligned {changed} flat", visual3DBody, StringComparison.Ordinal);
        Assert.DoesNotContain("aligned {aligned} sidedef", visual3DBody, StringComparison.Ordinal);
    }

    [Fact]
    public void AlphaBasedTextureHighlightingToggleUsesUdbStatusText()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int methodIndex = body.IndexOf("public bool ToggleAlphaBasedTextureHighlighting()", StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(AlphaBasedTextureHighlightingStatusText(_alphaBasedTextureHighlighting));", methodIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(statusIndex > methodIndex);
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
    public void VisualActionSelectionsUseOnlySelectedVisualHits()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int thingsIndex = body.IndexOf("public IReadOnlyList<Thing> SelectedVisualThingsForActions()", StringComparison.Ordinal);
        int surfacesIndex = body.IndexOf("public IReadOnlyList<VisualHit> SelectedVisualSurfacesForActions()", StringComparison.Ordinal);
        int targetFallbackIndex = body.IndexOf("_target3D?.Thing", thingsIndex, surfacesIndex - thingsIndex, StringComparison.Ordinal);
        int surfaceFilterIndex = body.IndexOf("hit.Kind is VisualHitKind.Floor or VisualHitKind.Ceiling or VisualHitKind.Wall", surfacesIndex, StringComparison.Ordinal);

        Assert.True(thingsIndex >= 0);
        Assert.True(surfacesIndex > thingsIndex);
        Assert.Equal(-1, targetFallbackIndex);
        Assert.True(surfaceFilterIndex > surfacesIndex);
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
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualUnpegged3DEditName(upper, next));", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(VisualUnpegged3DStatusText(upper, next));", editNameIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(editNameIndex > methodIndex);
        Assert.True(statusIndex > editNameIndex);
        Assert.DoesNotContain("set\" : \"removed", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Set upper unpegged\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Remove upper unpegged\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Set lower unpegged\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Remove lower unpegged\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualSlopeCommandsUseHighlightedSurfaceFallback()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int toggleIndex = body.IndexOf("private void ToggleSlope3D()", StringComparison.Ordinal);
        int toggleTargetsIndex = body.IndexOf("foreach (VisualHit hit in EditTargets3D())", toggleIndex, StringComparison.Ordinal);
        int toggleWarningIndex = body.IndexOf("Target3DChanged?.Invoke(VisualSlopeToggleEmptySelectionMessage());", toggleTargetsIndex, StringComparison.Ordinal);
        int resetIndex = body.IndexOf("private void ResetSlope3D()", StringComparison.Ordinal);
        int resetTargetsIndex = body.IndexOf("foreach (VisualHit hit in EditTargets3D())", resetIndex, StringComparison.Ordinal);

        Assert.True(toggleIndex >= 0);
        Assert.True(toggleTargetsIndex > toggleIndex);
        Assert.True(toggleWarningIndex > toggleTargetsIndex);
        Assert.True(resetIndex >= 0);
        Assert.True(resetTargetsIndex > resetIndex);
        Assert.DoesNotContain("\"Toggled Slope for 0 surfaces.\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualTexturePasteTargetsOnlySurfacesLikeUdb()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        int applyIndex = body.IndexOf("private void ApplyTextureToTarget(string tex, bool pasted)", StringComparison.Ordinal);
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
        int editNameIndex = body.IndexOf("EditBegun?.Invoke(VisualDelete3DEditName(hit.Kind));", targetsIndex, StringComparison.Ordinal);
        int floorIndex = body.IndexOf("floor.SetFloorTexture(\"-\");", methodIndex, StringComparison.Ordinal);
        int ceilingIndex = body.IndexOf("ceiling.SetCeilTexture(\"-\");", methodIndex, StringComparison.Ordinal);
        int wallIndex = body.IndexOf("side.SetTexture(hit.Part, \"-\");", methodIndex, StringComparison.Ordinal);
        int thingIndex = body.IndexOf("_map.RemoveThing(thing);", methodIndex, StringComparison.Ordinal);
        int statusIndex = body.IndexOf("Target3DChanged?.Invoke(deleteStatus);", thingIndex, StringComparison.Ordinal);
        int dispatchIndex = body.IndexOf("case \"map3d.delete-target\":", StringComparison.Ordinal);
        int handlerIndex = body.IndexOf("DeleteVisualTargets3D();", dispatchIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(targetsIndex > methodIndex);
        Assert.True(editNameIndex > targetsIndex);
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

    private static VisualHit WallHit(Sidedef side, SidedefPart part)
        => new(VisualHitKind.Wall, 1, new DBuilder.Geometry.Vector3D(0, 0, 0), side.Sector, side.Line, side.IsFront, 0, 128, part);

    private static VisualHit CeilingHit(Sector sector)
        => new(VisualHitKind.Ceiling, 1, new DBuilder.Geometry.Vector3D(0, 0, sector.CeilHeight), sector, null, true, 0, 0);

    private static Sidedef WallSide(Sector sector)
    {
        var line = new Linedef(new Vertex(new DBuilder.Geometry.Vector2D(0, 0)), new Vertex(new DBuilder.Geometry.Vector2D(64, 0)));
        var side = new Sidedef(line, true) { Sector = sector };
        line.Front = side;
        return side;
    }
}
