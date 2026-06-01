// ABOUTME: Tests UDB BuilderModes edit-selection setting keys, defaults, and enum fallback behavior.
// ABOUTME: Covers precise-position and sector height-adjust option persistence without UI.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class EditSelectionModeSettingsTests
{
    [Fact]
    public void SettingsUseUdbKeysAndDefaults()
    {
        EditSelectionModeSettings settings = EditSelectionModeSettings.FromDictionary(new Dictionary<string, object?>());

        Assert.True(settings.UsePrecisePosition);
        Assert.Equal(EditSelectionHeightAdjustMode.None, settings.HeightAdjustMode);

        var target = new Dictionary<string, object?>();
        new EditSelectionModeSettings(false, EditSelectionHeightAdjustMode.AdjustCeilings).WriteTo(target);

        Assert.Equal(false, target[EditSelectionModeSettings.UsePrecisePositionKey]);
        Assert.Equal((int)EditSelectionHeightAdjustMode.AdjustCeilings, target[EditSelectionModeSettings.HeightAdjustModeKey]);
    }

    [Theory]
    [InlineData(0, EditSelectionHeightAdjustMode.None)]
    [InlineData(1, EditSelectionHeightAdjustMode.AdjustFloors)]
    [InlineData(2, EditSelectionHeightAdjustMode.AdjustCeilings)]
    [InlineData(3, EditSelectionHeightAdjustMode.AdjustBoth)]
    [InlineData(99, EditSelectionHeightAdjustMode.None)]
    public void SettingsReadUdbHeightAdjustModeIntegers(int persistedValue, EditSelectionHeightAdjustMode expected)
    {
        EditSelectionModeSettings settings = EditSelectionModeSettings.FromDictionary(new Dictionary<string, object?>
        {
            [EditSelectionModeSettings.UsePrecisePositionKey] = false,
            [EditSelectionModeSettings.HeightAdjustModeKey] = persistedValue,
        });

        Assert.False(settings.UsePrecisePosition);
        Assert.Equal(expected, settings.HeightAdjustMode);
    }

    [Fact]
    public void SettingsNormalizeDeserializedHeightAdjustMode()
    {
        var settings = new EditSelectionModeSettings(
            HeightAdjustMode: (EditSelectionHeightAdjustMode)99);

        Assert.Equal(EditSelectionHeightAdjustMode.None, settings.Normalized().HeightAdjustMode);
    }

    [Theory]
    [InlineData(7.0, 0.0)]
    [InlineData(8.0, 15.0)]
    [InlineData(44.0, 45.0)]
    [InlineData(-10.0, 345.0)]
    [InlineData(361.0, 0.0)]
    public void TransformRotationSnapsToNearestUdbGridVector(double degrees, double expectedDegrees)
    {
        double snapped = EditSelectionTransform.SnapRotationToUdbGrid(Angle2D.DegToRad(degrees));

        Assert.Equal(expectedDegrees, Angle2D.RadToDeg(snapped), 3);
    }

    [Fact]
    public void AdjustSectorHeightsAppliesSelectedUdbFloorAndCeilingModes()
    {
        var sector = new Sector { FloorHeight = 8, CeilHeight = 96 };

        EditSelectionTransform.AdjustSectorHeights(
            new[] { sector },
            EditSelectionHeightAdjustMode.AdjustFloors,
            oldFloorHeight: 8,
            oldCeilingHeight: 96,
            outsideFloorHeight: 24,
            outsideCeilingHeight: 128,
            udmf: false);

        Assert.Equal(24, sector.FloorHeight);
        Assert.Equal(96, sector.CeilHeight);

        EditSelectionTransform.AdjustSectorHeights(
            new[] { sector },
            EditSelectionHeightAdjustMode.AdjustCeilings,
            oldFloorHeight: 24,
            oldCeilingHeight: 96,
            outsideFloorHeight: 40,
            outsideCeilingHeight: 112,
            udmf: false);

        Assert.Equal(24, sector.FloorHeight);
        Assert.Equal(112, sector.CeilHeight);
    }

    [Fact]
    public void AdjustSectorHeightsLeavesSectorsUnchangedWithoutOutsideHeights()
    {
        var sector = new Sector { FloorHeight = 8, CeilHeight = 96 };

        EditSelectionTransform.AdjustSectorHeights(
            new[] { sector },
            EditSelectionHeightAdjustMode.AdjustBoth,
            oldFloorHeight: 8,
            oldCeilingHeight: 96,
            outsideFloorHeight: null,
            outsideCeilingHeight: null,
            udmf: false);

        Assert.Equal(8, sector.FloorHeight);
        Assert.Equal(96, sector.CeilHeight);
    }

    [Fact]
    public void AdjustSectorHeightsMovesUdmfSlopeOffsets()
    {
        var sector = new Sector
        {
            FloorHeight = 0,
            CeilHeight = 128,
            FloorSlope = new Vector3D(1, 0, 1),
            FloorSlopeOffset = 64,
            CeilSlope = new Vector3D(0, 1, 1),
            CeilSlopeOffset = 96,
        };

        EditSelectionTransform.AdjustSectorHeights(
            new[] { sector },
            EditSelectionHeightAdjustMode.AdjustBoth,
            oldFloorHeight: 0,
            oldCeilingHeight: 128,
            outsideFloorHeight: 16,
            outsideCeilingHeight: 160,
            udmf: true);

        Assert.Equal(16, sector.FloorHeight);
        Assert.Equal(160, sector.CeilHeight);
        Assert.Equal(64 - 16 * Math.Sin(sector.FloorSlope.GetAngleZ()), sector.FloorSlopeOffset, 6);
        Assert.Equal(96 - 32 * Math.Sin(sector.CeilSlope.GetAngleZ()), sector.CeilSlopeOffset, 6);
    }

    [Fact]
    public void AdjustSectorHeightsMovesTriangleVertexHeights()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(64, 0));
        var c = map.AddVertex(new Vector2D(0, 64));
        a.ZFloor = 1;
        b.ZFloor = 2;
        c.ZFloor = double.NaN;
        a.ZCeiling = 10;
        b.ZCeiling = 11;
        c.ZCeiling = 12;
        Sector sector = SectorBuilder.CreateSector(map, new[] { a, b, c })!;
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        map.BuildIndexes();

        EditSelectionTransform.AdjustSectorHeights(
            new[] { sector },
            EditSelectionHeightAdjustMode.AdjustBoth,
            oldFloorHeight: 0,
            oldCeilingHeight: 128,
            outsideFloorHeight: 8,
            outsideCeilingHeight: 144,
            udmf: true);

        Assert.Equal(9, a.ZFloor);
        Assert.Equal(10, b.ZFloor);
        Assert.True(double.IsNaN(c.ZFloor));
        Assert.Equal(26, a.ZCeiling);
        Assert.Equal(27, b.ZCeiling);
        Assert.Equal(28, c.ZCeiling);
    }
}
