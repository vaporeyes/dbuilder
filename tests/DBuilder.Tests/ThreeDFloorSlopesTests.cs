// ABOUTME: Tests UDB 3D Floor Mode slope vertex group persistence and plane application.
// ABOUTME: Covers slope data sector fields, group loading, and sector floor or ceiling binding.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThreeDFloorSlopesTests
{
    [Fact]
    public void StoreAndLoadGroupsUseUdbSlopeDataSectorFields()
    {
        var sector = new Sector();
        var group = new ThreeDFloorSlopeVertexGroup(
            3,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            })
        {
            Reposition = false,
            Spline = true,
        };

        ThreeDFloorSlopes.StoreGroupsInSector(sector, new[] { group });
        IReadOnlyList<ThreeDFloorSlopeVertexGroup> loaded = ThreeDFloorSlopes.LoadGroupsFromSector(sector);

        ThreeDFloorSlopeVertexGroup result = Assert.Single(loaded);
        Assert.Equal(3, result.Id);
        Assert.False(result.Reposition);
        Assert.True(result.Spline);
        Assert.Equal(3, result.Vertices.Count);
        Assert.Equal(new Vector2D(64, 64), result.Vertices[2].Position);
        Assert.Equal(64, result.Vertices[2].Z);
        Assert.Equal(true, sector.Fields[ThreeDFloorSlopes.SlopeDataSectorField]);
        Assert.Equal(ThreeDFloorSlopes.SlopeDataSectorComment, sector.Fields["comment"]);
        Assert.Equal(0.0, sector.Fields["user_svg3_v0_x"]);
        Assert.Equal(false, sector.Fields["user_svg3_reposition"]);
        Assert.Equal(true, sector.Fields["user_svg3_spline"]);
    }

    [Fact]
    public void LoadGroupsOrdersByGroupId()
    {
        var sector = new Sector();
        sector.Fields["user_svg9_v0_x"] = 0.0;
        sector.Fields["user_svg9_v1_x"] = 64.0;
        sector.Fields["user_svg2_v0_x"] = 0.0;
        sector.Fields["user_svg2_v1_x"] = 64.0;

        int[] ids = ThreeDFloorSlopes.LoadGroupsFromSector(sector).Select(group => group.Id).ToArray();

        Assert.Equal(new[] { 2, 9 }, ids);
    }

    [Fact]
    public void AddSectorAppliesFloorSlopeAndStoresPlaneId()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64, 64);
        var group = new ThreeDFloorSlopeVertexGroup(
            7,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });

        group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Floor);

        Assert.Equal(7, sector.Fields[ThreeDFloorSlopes.FloorPlaneIdField]);
        Assert.True(sector.HasFloorSlope);
        Assert.Equal(0, sector.GetFloorZ(new Vector2D(0, 0)), 1e-6);
        Assert.Equal(64, sector.GetFloorZ(new Vector2D(64, 64)), 1e-6);
        Assert.Equal(group.Height, sector.FloorHeight);
        Assert.Contains(sector, group.Sectors);
    }

    [Fact]
    public void ApplyGroupsFindsBoundSectorFieldsAndAppliesCeilingSlope()
    {
        var map = new MapSet();
        Sector slopeData = map.AddSector();
        var group = new ThreeDFloorSlopeVertexGroup(
            4,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 128),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 128),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });
        ThreeDFloorSlopes.StoreGroupsInSector(slopeData, new[] { group });

        Sector target = AddSquareSector(map, 0, 64, 64);
        target.Fields[ThreeDFloorSlopes.CeilingPlaneIdField] = 4;

        IReadOnlyList<ThreeDFloorSlopeVertexGroup> loaded = ThreeDFloorSlopes.LoadGroupsFromSector(ThreeDFloorSlopes.GetSlopeDataSector(map));
        int changed = ThreeDFloorSlopes.ApplyGroups(map, loaded);

        Assert.Equal(1, changed);
        Assert.True(target.HasCeilSlope);
        Assert.Equal(128, target.GetCeilZ(new Vector2D(0, 0)), 1e-6);
        Assert.Equal(64, target.GetCeilZ(new Vector2D(64, 64)), 1e-6);
    }

    [Fact]
    public void RemoveSectorClearsPlaneFieldsAndSlope()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64, 64);
        var group = new ThreeDFloorSlopeVertexGroup(
            7,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });
        group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Floor | ThreeDFloorSlopePlaneType.Ceiling);

        group.RemoveSector(sector, ThreeDFloorSlopePlaneType.Floor);

        Assert.False(sector.HasFloorSlope);
        Assert.True(sector.HasCeilSlope);
        Assert.False(sector.Fields.ContainsKey(ThreeDFloorSlopes.FloorPlaneIdField));
        Assert.Equal(7, sector.Fields[ThreeDFloorSlopes.CeilingPlaneIdField]);
    }

    [Fact]
    public void ControlSectorPlaneRecordsTaggedSectorsForHighlightingOnly()
    {
        var map = new MapSet();
        Sector control = AddSquareSector(map, 0, 64, 64);
        Sector target = AddSquareSector(map, 128, 64, 64);
        target.Tag = 12;
        Linedef actionLine = control.Sidedefs[0].Line;
        actionLine.Action = ThreeDFloors.Sector3DFloorAction;
        actionLine.Args[0] = 12;
        var group = new ThreeDFloorSlopeVertexGroup(
            5,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });

        group.AddSector(map, control, ThreeDFloorSlopePlaneType.Floor);

        Assert.Contains(target, group.TaggedSectors);
        Assert.True(group.SectorPlanes.ContainsKey(target));
        Assert.False(target.HasFloorSlope);
        Assert.DoesNotContain(target, group.Sectors);
    }

    [Fact]
    public void TwoPointGroupsCreatePerpendicularThirdPointAndRejectDegenerateLine()
    {
        var valid = new ThreeDFloorSlopeVertexGroup(
            1,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 16),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 16),
            });
        var invalid = new ThreeDFloorSlopeVertexGroup(
            2,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 16),
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 32),
            });

        Assert.True(valid.VerticesAreValid());
        Assert.Equal(16, valid.Height);
        Assert.False(invalid.VerticesAreValid());
    }

    private static Sector AddSquareSector(MapSet map, double left, double top, double size)
    {
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(left, top)),
            map.AddVertex(new Vector2D(left + size, top)),
            map.AddVertex(new Vector2D(left + size, top - size)),
            map.AddVertex(new Vector2D(left, top - size)),
        };

        Sector sector = SectorBuilder.CreateSector(map, vertices)!;
        map.BuildIndexes();
        return sector;
    }
}
