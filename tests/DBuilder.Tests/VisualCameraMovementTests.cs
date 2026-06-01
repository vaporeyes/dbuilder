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
