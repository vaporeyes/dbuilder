// ABOUTME: Clamps selection drag deltas so selected map elements stay within game configuration boundaries.
// ABOUTME: Mirrors UDB's optional map-boundary drag guard for classic 2D geometry and thing movement.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public readonly record struct MapDragBoundary(double Left, double Right, double Bottom, double Top);

public static class SelectionBoundaryClamp
{
    public static Vector2D ClampDelta(
        IEnumerable<Vector2D> positions,
        Vector2D requestedDelta,
        MapDragBoundary boundary)
    {
        bool hasPosition = false;
        double minX = 0;
        double maxX = 0;
        double minY = 0;
        double maxY = 0;

        foreach (Vector2D position in positions)
        {
            if (!hasPosition)
            {
                minX = maxX = position.x;
                minY = maxY = position.y;
                hasPosition = true;
                continue;
            }

            minX = Math.Min(minX, position.x);
            maxX = Math.Max(maxX, position.x);
            minY = Math.Min(minY, position.y);
            maxY = Math.Max(maxY, position.y);
        }

        if (!hasPosition) return requestedDelta;

        double x = requestedDelta.x;
        double y = requestedDelta.y;

        if (minX + x < boundary.Left) x = boundary.Left - minX;
        if (maxX + x > boundary.Right) x = boundary.Right - maxX;
        if (maxY + y > boundary.Top) y = boundary.Top - maxY;
        if (minY + y < boundary.Bottom) y = boundary.Bottom - minY;

        return new Vector2D(x, y);
    }
}
