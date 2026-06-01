// ABOUTME: Tests UDB-style visual thing movement translation helpers.
// ABOUTME: Covers camera-quadrant relative movement and cursor placement offset preservation.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualThingMovementTests
{
    [Fact]
    public void RelativeMovementUsesUnrotatedDirectionInFirstCameraQuadrant()
    {
        Vector3D[] coordinates = [new(10, 20, 3)];

        IReadOnlyList<Vector3D> translated = VisualThingMovement.TranslateRelative(
            coordinates,
            new Vector2D(0, -32),
            Angle2D.DegToRad(90));

        Assert.Equal(new Vector3D(10, -12, 3), translated[0]);
    }

    [Fact]
    public void RelativeMovementRotatesByCameraQuadrant()
    {
        Vector3D[] coordinates = [new(10, 20, 3)];

        IReadOnlyList<Vector3D> translated = VisualThingMovement.TranslateRelative(
            coordinates,
            new Vector2D(0, -32),
            Angle2D.DegToRad(180));

        Assert.Equal(new Vector3D(42, 20, 3), translated[0]);
    }

    [Fact]
    public void CursorPlacementMovesSingleThingToCursorAndPreservesHeight()
    {
        Vector3D[] coordinates = [new(10, 20, 7)];

        IReadOnlyList<Vector3D> translated = VisualThingMovement.TranslateToCursor(coordinates, new Vector2D(64.4, 80.6));

        Assert.Equal(new Vector3D(64.4, 80.6, 7), translated[0]);
    }

    [Fact]
    public void CursorPlacementPreservesGroupOffsetsAroundSelectionCenter()
    {
        Vector3D[] coordinates =
        [
            new(0, 0, 1),
            new(20, 10, 2),
        ];

        IReadOnlyList<Vector3D> translated = VisualThingMovement.TranslateToCursor(coordinates, new Vector2D(100.2, 200.7));

        Assert.Equal(new Vector3D(90, 196, 1), translated[0]);
        Assert.Equal(new Vector3D(110, 206, 2), translated[1]);
    }
}
