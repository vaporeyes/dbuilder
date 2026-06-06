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

    [Fact]
    public void ApplyVertexEditUpdatesSelectedVerticesAndGroupOptions()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64, 64);
        var first = new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0);
        var second = new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0);
        var third = new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64);
        var group = new ThreeDFloorSlopeVertexGroup(8, new[] { first, second, third });
        var edit = new ThreeDFloorSlopeVertexEdit(
            Z: 32,
            Reposition: false,
            Spline: true,
            SelectedSectors: new[] { sector },
            AddSelectedSectorsToFloor: true);

        int changed = ThreeDFloorSlopes.ApplyVertexEdit(
            map,
            new[] { new ThreeDFloorSlopeVertexSelection(first, group), new ThreeDFloorSlopeVertexSelection(second, group) },
            edit);

        Assert.Equal(2, changed);
        Assert.Equal(new Vector2D(0, 0), first.Position);
        Assert.Equal(new Vector2D(64, 0), second.Position);
        Assert.Equal(32, first.Z);
        Assert.Equal(32, second.Z);
        Assert.False(group.Reposition);
        Assert.True(group.Spline);
        Assert.Equal(8, sector.Fields[ThreeDFloorSlopes.FloorPlaneIdField]);
        Assert.True(sector.HasFloorSlope);
    }

    [Fact]
    public void ApplyVertexEditIgnoresSplineForTwoPointGroups()
    {
        var map = new MapSet();
        var first = new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 16);
        var second = new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 16);
        var group = new ThreeDFloorSlopeVertexGroup(9, new[] { first, second });

        ThreeDFloorSlopes.ApplyVertexEdit(
            map,
            new[] { new ThreeDFloorSlopeVertexSelection(first, group) },
            new ThreeDFloorSlopeVertexEdit(Spline: true));

        Assert.False(group.Spline);
    }

    [Fact]
    public void ApplyVertexEditRemovesSelectedAndListedSectorBindings()
    {
        var map = new MapSet();
        Sector selected = AddSquareSector(map, 0, 64, 64);
        Sector listed = AddSquareSector(map, 128, 64, 64);
        var group = new ThreeDFloorSlopeVertexGroup(
            7,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });
        group.AddSector(map, selected, ThreeDFloorSlopePlaneType.Floor | ThreeDFloorSlopePlaneType.Ceiling);
        group.AddSector(map, listed, ThreeDFloorSlopePlaneType.Floor | ThreeDFloorSlopePlaneType.Ceiling);

        ThreeDFloorSlopes.ApplyVertexEdit(
            map,
            new[] { new ThreeDFloorSlopeVertexSelection(group.Vertices[0], group) },
            new ThreeDFloorSlopeVertexEdit(
                SelectedSectors: new[] { selected },
                RemoveSelectedSectorsFromFloor: true,
                SectorsToUnbind: new[] { listed }));

        Assert.False(selected.HasFloorSlope);
        Assert.True(selected.HasCeilSlope);
        Assert.False(selected.Fields.ContainsKey(ThreeDFloorSlopes.FloorPlaneIdField));
        Assert.False(listed.HasFloorSlope);
        Assert.False(listed.HasCeilSlope);
        Assert.DoesNotContain(listed, group.Sectors);
    }

    [Fact]
    public void AssignSectorsToGroupRemovesCompetingPlaneBinding()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64, 64);
        var first = new ThreeDFloorSlopeVertexGroup(
            1,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 64),
            });
        var second = new ThreeDFloorSlopeVertexGroup(
            2,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 64),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 64),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 64), 0),
            });
        first.AddSector(map, sector, ThreeDFloorSlopePlaneType.Floor | ThreeDFloorSlopePlaneType.Ceiling);

        int changed = ThreeDFloorSlopes.AssignSectorsToGroup(
            map,
            second,
            new[] { first, second },
            new[] { sector },
            ThreeDFloorSlopePlaneType.Floor);

        Assert.Equal(1, changed);
        Assert.Equal(2, sector.Fields[ThreeDFloorSlopes.FloorPlaneIdField]);
        Assert.Equal(1, sector.Fields[ThreeDFloorSlopes.CeilingPlaneIdField]);
        Assert.Contains(sector, first.Sectors);
        Assert.Contains(sector, second.Sectors);
        Assert.Equal(ThreeDFloorSlopePlaneType.Ceiling, first.SectorPlanes[sector]);
        Assert.Equal(ThreeDFloorSlopePlaneType.Floor, second.SectorPlanes[sector]);
    }

    [Fact]
    public void AddSlopeVertexGroupUsesFirstFreePositiveId()
    {
        var groups = new List<ThreeDFloorSlopeVertexGroup>
        {
            new(
                1,
                new[]
                {
                    new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                    new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                }),
            new(
                3,
                new[]
                {
                    new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                    new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
                }),
        };

        ThreeDFloorSlopeVertexGroup group = ThreeDFloorSlopes.AddSlopeVertexGroup(
            groups,
            new[]
            {
                new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
                new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 0),
            });

        Assert.Equal(2, group.Id);
        Assert.Contains(group, groups);
    }

    [Fact]
    public void FinishDrawCreatesFloorAndCeilingGroupsFromSelectedControlSectorHeights()
    {
        var map = new MapSet();
        Sector slopeData = map.AddSector();
        Sector control = AddSquareSector(map, 0, 64, 64);
        control.FloorHeight = -16;
        control.CeilHeight = 48;
        control.Sidedefs[0].Line.Action = ThreeDFloors.Sector3DFloorAction;
        var groups = new List<ThreeDFloorSlopeVertexGroup>();

        ThreeDFloorSlopeDrawResult result = ThreeDFloorSlopes.FinishDraw(
            map,
            groups,
            new[] { new Vector2D(0, 64), new Vector2D(64, 64), new Vector2D(64, 0) },
            new[] { control },
            ThreeDFloorSlopeDrawingMode.FloorAndCeiling,
            slopeData);

        Assert.Equal(2, result.CreatedGroups.Count);
        Assert.Equal(new[] { 1, 2 }, result.CreatedGroups.Select(group => group.Id).ToArray());
        Assert.All(result.CreatedGroups[0].Vertices, vertex => Assert.Equal(-16, vertex.Z));
        Assert.All(result.CreatedGroups[1].Vertices, vertex => Assert.Equal(48, vertex.Z));
        Assert.Equal(1, control.Fields[ThreeDFloorSlopes.FloorPlaneIdField]);
        Assert.Equal(2, control.Fields[ThreeDFloorSlopes.CeilingPlaneIdField]);
        Assert.True(control.HasFloorSlope);
        Assert.True(control.HasCeilSlope);
        Assert.Equal(true, slopeData.Fields[ThreeDFloorSlopes.SlopeDataSectorField]);
        Assert.Equal(-16.0, slopeData.Fields["user_svg1_v0_z"]);
        Assert.Equal(48.0, slopeData.Fields["user_svg2_v0_z"]);
    }

    [Fact]
    public void FinishDrawCreatesOnlyRequestedPlaneAndSkipsShortDraws()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0, 64, 64);
        var groups = new List<ThreeDFloorSlopeVertexGroup>();

        ThreeDFloorSlopeDrawResult skipped = ThreeDFloorSlopes.FinishDraw(
            map,
            groups,
            new[] { new Vector2D(0, 64) },
            new[] { sector },
            ThreeDFloorSlopeDrawingMode.Floor);
        ThreeDFloorSlopeDrawResult created = ThreeDFloorSlopes.FinishDraw(
            map,
            groups,
            new[] { new Vector2D(0, 64), new Vector2D(64, 64) },
            new[] { sector },
            ThreeDFloorSlopeDrawingMode.Ceiling);

        Assert.Empty(skipped.CreatedGroups);
        ThreeDFloorSlopeVertexGroup group = Assert.Single(created.CreatedGroups);
        Assert.Equal(1, group.Id);
        Assert.False(sector.HasFloorSlope);
        Assert.True(sector.HasCeilSlope);
        Assert.False(sector.Fields.ContainsKey(ThreeDFloorSlopes.FloorPlaneIdField));
        Assert.Equal(1, sector.Fields[ThreeDFloorSlopes.CeilingPlaneIdField]);
    }

    [Fact]
    public void FlipVertexHeightsReversesZValuesWithoutMovingDrawPoints()
    {
        var vertices = new[]
        {
            new ThreeDFloorSlopeVertex(new Vector2D(0, 0), 0),
            new ThreeDFloorSlopeVertex(new Vector2D(64, 0), 32),
            new ThreeDFloorSlopeVertex(new Vector2D(128, 0), 64),
        };

        ThreeDFloorSlopes.FlipVertexHeights(vertices);

        Assert.Equal(new Vector2D(0, 0), vertices[0].Position);
        Assert.Equal(new Vector2D(64, 0), vertices[1].Position);
        Assert.Equal(new Vector2D(128, 0), vertices[2].Position);
        Assert.Equal(64, vertices[0].Z);
        Assert.Equal(32, vertices[1].Z);
        Assert.Equal(0, vertices[2].Z);
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
