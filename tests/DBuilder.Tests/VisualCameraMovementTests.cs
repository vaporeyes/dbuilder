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
}
