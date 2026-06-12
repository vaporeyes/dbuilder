// ABOUTME: Tests for the UDB-style map boundary guard used while dragging selected geometry.
// ABOUTME: Verifies drag deltas clamp to configured map bounds without changing unrestricted moves.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class SelectionBoundaryClampTests
{
    private static readonly MapDragBoundary Boundary = new(-100, 100, -50, 50);

    [Fact]
    public void AllowsDeltaWhenSelectionStaysInsideBounds()
    {
        Vector2D delta = SelectionBoundaryClamp.ClampDelta(
            new[] { new Vector2D(-20, -10), new Vector2D(30, 20) },
            new Vector2D(10, -5),
            Boundary);

        Assert.Equal(new Vector2D(10, -5), delta);
    }

    [Fact]
    public void ClampsDeltaToKeepSelectionInsideBounds()
    {
        Vector2D delta = SelectionBoundaryClamp.ClampDelta(
            new[] { new Vector2D(-90, -40), new Vector2D(80, 45) },
            new Vector2D(30, 20),
            Boundary);

        Assert.Equal(new Vector2D(20, 5), delta);
    }

    [Fact]
    public void EmptySelectionKeepsRequestedDelta()
    {
        Vector2D delta = SelectionBoundaryClamp.ClampDelta(
            Array.Empty<Vector2D>(),
            new Vector2D(200, -200),
            Boundary);

        Assert.Equal(new Vector2D(200, -200), delta);
    }
}
