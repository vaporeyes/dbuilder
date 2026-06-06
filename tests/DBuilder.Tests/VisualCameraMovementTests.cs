// ABOUTME: Tests UDB-style visual camera movement helpers.
// ABOUTME: Pins move-camera-to-cursor offset and invalid-hit behavior independently from the editor UI.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualCameraMovementTests
{
    [Fact]
    public void MoveCameraToCursorPlacesCameraSixtyFourUnitsFromHit()
    {
        var current = new Vector3D(0, 0, 128);
        var hit = new Vector3D(0, 0, 0);

        bool moved = VisualCameraMovement.TryMoveCameraToCursor(current, hit, out Vector3D next);

        Assert.True(moved);
        Assert.Equal(VisualCameraMovement.MoveCameraToCursorDistance, next.z, 6);
        Assert.Equal(0, next.x, 6);
        Assert.Equal(0, next.y, 6);
    }

    [Fact]
    public void MoveCameraToCursorPreservesDirectionFromHitToCamera()
    {
        var current = new Vector3D(30, 40, 0);
        var hit = new Vector3D(0, 0, 0);

        bool moved = VisualCameraMovement.TryMoveCameraToCursor(current, hit, distance: 10, out Vector3D next);

        Assert.True(moved);
        Assert.Equal(6, next.x, 6);
        Assert.Equal(8, next.y, 6);
        Assert.Equal(0, next.z, 6);
    }

    [Fact]
    public void MoveCameraToCursorRejectsInvalidPositions()
    {
        var current = new Vector3D(0, 0, 128);
        var hit = new Vector3D(double.NaN, 0, 0);

        bool moved = VisualCameraMovement.TryMoveCameraToCursor(current, hit, out Vector3D next);

        Assert.False(moved);
        Assert.Equal(current, next);
    }

    [Fact]
    public void EngagePositionRaisesCameraToDoomPlayerHeightInNormalSectors()
    {
        var sector = new Sector { FloorHeight = 16, CeilHeight = 128 };

        Vector3D position = VisualCameraMovement.PlanEngagePosition(
            new Vector2D(32, 64),
            currentZ: 24,
            sector);

        Assert.Equal(new Vector3D(32, 64, 57), position);
    }

    [Fact]
    public void EngagePositionKeepsCameraZWhenAlreadyInsideNormalSector()
    {
        var sector = new Sector { FloorHeight = 16, CeilHeight = 128 };

        Vector3D position = VisualCameraMovement.PlanEngagePosition(
            new Vector2D(32, 64),
            currentZ: 80,
            sector);

        Assert.Equal(new Vector3D(32, 64, 80), position);
    }

    [Fact]
    public void EngagePositionClampsCameraBelowCeilingWhenTooHigh()
    {
        var sector = new Sector { FloorHeight = 0, CeilHeight = 96 };

        Vector3D position = VisualCameraMovement.PlanEngagePosition(
            new Vector2D(32, 64),
            currentZ: 200,
            sector);

        Assert.Equal(new Vector3D(32, 64, 92), position);
    }

    [Fact]
    public void EngagePositionUsesLowSectorFallbackLikeUdb()
    {
        var sector = new Sector { FloorHeight = 8, CeilHeight = 30 };

        Vector3D position = VisualCameraMovement.PlanEngagePosition(
            new Vector2D(32, 64),
            currentZ: 200,
            sector);

        Assert.Equal(new Vector3D(32, 64, 24), position);
    }

    [Fact]
    public void EngagePositionPreservesZWithoutSector()
    {
        Vector3D position = VisualCameraMovement.PlanEngagePosition(
            new Vector2D(32, 64),
            currentZ: 200,
            nearestSector: null);

        Assert.Equal(new Vector3D(32, 64, 200), position);
    }

    [Fact]
    public void CenterOnCoordinatesPlacesVisualCameraAtSectorFloorPlusUdbEyeHeight()
    {
        var sector = new Sector { FloorHeight = 24, CeilHeight = 128 };

        Vector3D position = VisualCameraMovement.PlanCenterOnCoordinatesPosition(
            new Vector2D(32, 64),
            sector);

        Assert.Equal(new Vector3D(32, 64, 78), position);
    }

    [Fact]
    public void CenterOnCoordinatesUsesFlatPositionWhenSectorIsMissingLikeUdb()
    {
        Vector3D position = VisualCameraMovement.PlanCenterOnCoordinatesPosition(
            new Vector2D(32, 64),
            sector: null);

        Assert.Equal(new Vector3D(32, 64, 0), position);
    }

    [Fact]
    public void StartThingPoseUsesFirstConfiguredThingAndSectorRelativeHeightLikeUdb()
    {
        var sector = new Sector { FloorHeight = 32, CeilHeight = 160 };
        var ignored = new Thing(new Vector2D(4, 8), 1, 0);
        var start = new Thing(new Vector2D(32, 64), 32000, 90)
        {
            Height = 16,
            Sector = sector,
        };
        var later = new Thing(new Vector2D(128, 256), 32000, 180)
        {
            Height = 64,
            Sector = sector,
        };

        bool planned = VisualCameraMovement.TryPlanStartThingPose(
            new[] { ignored, start, later },
            startThingType: 32000,
            currentPosition: new Vector3D(0, 0, 0),
            out VisualCameraStartThingPlan plan);

        Assert.True(planned);
        Assert.Equal(new Vector3D(32, 64, 89), plan.Pose.Position);
        Assert.Equal(0, plan.Pose.Yaw, 0.0001);
        Assert.Equal(0, plan.Pose.Pitch, 0.0001);
        Assert.True(plan.PositionChanges);
    }

    [Fact]
    public void StartThingPoseUsesThingHeightWithoutSectorLikeUdb()
    {
        var start = new Thing(new Vector2D(32, 64), 32000, 180)
        {
            Height = 16,
        };

        bool planned = VisualCameraMovement.TryPlanStartThingPose(
            new[] { start },
            startThingType: 32000,
            currentPosition: new Vector3D(32, 64, 57),
            out VisualCameraStartThingPlan plan);

        Assert.True(planned);
        Assert.Equal(new Vector3D(32, 64, 57), plan.Pose.Position);
        Assert.Equal(Angle2D.PI * 0.5, plan.Pose.Yaw, 0.0001);
        Assert.False(plan.PositionChanges);
    }

    [Fact]
    public void StartThingPoseFailsWhenConfiguredThingIsMissing()
    {
        bool planned = VisualCameraMovement.TryPlanStartThingPose(
            new[] { new Thing(new Vector2D(32, 64), 1) },
            startThingType: 32000,
            currentPosition: new Vector3D(10, 20, 30),
            out VisualCameraStartThingPlan plan);

        Assert.False(planned);
        Assert.Equal(new Vector3D(10, 20, 30), plan.Pose.Position);
        Assert.False(plan.PositionChanges);
    }

    [Fact]
    public void ApplyingCameraPoseToStartThingStoresSectorRelativeHeightLikeUdb()
    {
        var sector = new Sector { FloorHeight = 32, CeilHeight = 160 };
        var start = new Thing(new Vector2D(0, 0), 32000, 0);

        bool applied = VisualCameraMovement.TryApplyPoseToStartThing(
            new[] { start },
            startThingType: 32000,
            new VisualCameraPose(new Vector3D(32.9, 64.8, 100.6), Angle2D.DoomToReal(90) + Angle2D.PI, 0.0),
            sector);

        Assert.True(applied);
        Assert.Equal(new Vector2D(32, 64), start.Position);
        Assert.Equal(27, start.Height);
        Assert.Equal(90, start.Angle);
    }

    [Fact]
    public void ApplyingCameraPoseToStartThingUsesAbsoluteHeightWithoutSectorLikeUdb()
    {
        var start = new Thing(new Vector2D(0, 0), 32000, 0);

        bool applied = VisualCameraMovement.TryApplyPoseToStartThing(
            new[] { start },
            startThingType: 32000,
            new VisualCameraPose(new Vector3D(32.9, 64.8, 100.6), Angle2D.DoomToReal(180) + Angle2D.PI, 0.0),
            cameraSector: null);

        Assert.True(applied);
        Assert.Equal(new Vector2D(32, 64), start.Position);
        Assert.Equal(59, start.Height);
        Assert.Equal(180, start.Angle);
    }

    [Fact]
    public void ApplyingCameraPoseToStartThingFailsWhenConfiguredThingIsMissing()
    {
        var other = new Thing(new Vector2D(0, 0), 1, 0);

        bool applied = VisualCameraMovement.TryApplyPoseToStartThing(
            new[] { other },
            startThingType: 32000,
            new VisualCameraPose(new Vector3D(32, 64, 100), 0.0, 0.0),
            cameraSector: null);

        Assert.False(applied);
        Assert.Equal(new Vector2D(0, 0), other.Position);
        Assert.Equal(0, other.Height);
        Assert.Equal(0, other.Angle);
    }

    [Fact]
    public void OrbitKeepsCameraAtRadiusAndLookingAtTarget()
    {
        var current = new Vector3D(64, 0, 0);
        var target = new Vector3D(0, 0, 0);

        bool moved = VisualCameraMovement.TryOrbit(current, target, deltaX: 20, deltaY: 0, out VisualCameraPose pose);

        Assert.True(moved);
        Assert.Equal(64, (pose.Position - target).GetLength(), 6);
        Assert.Equal(Math.Atan2(-pose.Position.y, -pose.Position.x), pose.Yaw, 0.0001);
        Assert.Equal(0, pose.Pitch, 0.0001);
    }

    [Fact]
    public void OrbitClampsVerticalPitch()
    {
        var current = new Vector3D(64, 0, 0);
        var target = new Vector3D(0, 0, 0);

        bool moved = VisualCameraMovement.TryOrbit(current, target, deltaX: 0, deltaY: 1000, out VisualCameraPose pose);

        Assert.True(moved);
        Assert.Equal(-VisualCameraMovement.MaxOrbitPitch, pose.Pitch, 0.0001);
    }

    [Fact]
    public void OrbitRejectsInvalidTarget()
    {
        var current = new Vector3D(64, 0, 0);
        var target = new Vector3D(double.NaN, 0, 0);

        bool moved = VisualCameraMovement.TryOrbit(current, target, deltaX: 1, deltaY: 1, out VisualCameraPose pose);

        Assert.False(moved);
        Assert.Equal(current, pose.Position);
    }

    [Fact]
    public void LookThroughGenericThingUsesThingPositionAngleAndPitch()
    {
        var thing = new Thing(new Vector2D(10, 20), 1, 90);
        thing.Height = 8;
        thing.SetPitch(15);

        VisualCameraPose pose = VisualCameraMovement.LookThroughThing(
            thing,
            new[] { thing },
            candidate => new Vector3D(candidate.Position, candidate.Height + 16),
            useUdmfPitch: true);

        Assert.Equal(new Vector3D(10, 20, 24), pose.Position);
        Assert.Equal(0, pose.Yaw, 0.0001);
        Assert.Equal(Angle2D.DegToRad(15), pose.Pitch, 0.0001);
    }

    [Fact]
    public void LookThroughCameraThingAimsAtTaggedTarget()
    {
        var camera = new Thing(new Vector2D(0, 0), VisualCameraMovement.AimingCameraThingType, 0);
        camera.Args[2] = 4;
        camera.Args[3] = 7;
        var target = new Thing(new Vector2D(100, 0), 1) { Tag = 7, Height = 100 };

        VisualCameraPose pose = VisualCameraMovement.LookThroughThing(
            camera,
            new[] { camera, target },
            candidate => new Vector3D(candidate.Position, candidate.Height),
            useUdmfPitch: false);

        Assert.Equal(0, pose.Yaw, 0.0001);
        Assert.Equal(Math.Atan2(100, 100), pose.Pitch, 0.0001);
        Assert.Null(pose.StatusMessage);
    }

    [Fact]
    public void LookThroughCameraThingWarnsWhenTaggedTargetIsMissingLikeUdb()
    {
        var camera = new Thing(new Vector2D(0, 0), VisualCameraMovement.AimingCameraThingType, 90);
        camera.Args[2] = 4;
        camera.Args[3] = 7;

        VisualCameraPose pose = VisualCameraMovement.LookThroughThing(
            camera,
            new[] { camera },
            candidate => new Vector3D(candidate.Position, candidate.Height),
            useUdmfPitch: false);

        Assert.Equal(new Vector3D(0, 0, 0), pose.Position);
        Assert.Equal(0, pose.Yaw, 0.0001);
        Assert.Equal(0, pose.Pitch, 0.0001);
        Assert.Equal("Camera target with Tag 7 does not exist!", pose.StatusMessage);
    }

    [Fact]
    public void LookThroughCameraThingUsesInterpolationPointPosition()
    {
        var camera = new Thing(new Vector2D(0, 0), VisualCameraMovement.AimingCameraThingType, 0);
        camera.Args[0] = 2;
        camera.Args[1] = 1;
        camera.Args[3] = 7;
        var interpolationPoint = new Thing(new Vector2D(32, 64), VisualCameraMovement.InterpolationPointThingType)
        {
            Tag = 258,
        };

        VisualCameraPose pose = VisualCameraMovement.LookThroughThing(
            camera,
            new[] { camera, interpolationPoint },
            candidate => new Vector3D(candidate.Position, candidate.Height),
            useUdmfPitch: false);

        Assert.Equal(new Vector3D(32, 64, 0), pose.Position);
    }
}
