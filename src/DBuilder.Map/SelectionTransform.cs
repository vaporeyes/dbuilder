// ABOUTME: Geometric transforms (flip, rotate, scale) over the current selection about its bounding-box center.
// ABOUTME: Moves the implied vertices and selected things, adjusting thing angles for flips/rotations.

using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class SelectionTransform
{
    public enum Op { FlipHorizontal, FlipVertical, RotateCW, RotateCCW }

    /// <summary>Applies a flip/rotate to the selection about its center. Returns false when nothing is selected.</summary>
    public static bool Apply(MapSet map, Op op)
    {
        var verts = map.SelectedGeometryVertices();
        var things = map.GetSelectedThings();
        if (verts.Count == 0 && things.Count == 0) return false;

        var (cx, cy) = Center(verts, things);
        foreach (var v in verts) v.Position = Move(v.Position, op, cx, cy);
        foreach (var t in things) { t.Position = Move(t.Position, op, cx, cy); t.Angle = Angle(t.Angle, op); }
        return true;
    }

    /// <summary>Scales the selection about its center by <paramref name="factor"/>. Returns false when nothing is selected.</summary>
    public static bool Scale(MapSet map, double factor)
    {
        var verts = map.SelectedGeometryVertices();
        var things = map.GetSelectedThings();
        if (verts.Count == 0 && things.Count == 0) return false;

        var (cx, cy) = Center(verts, things);
        foreach (var v in verts) v.Position = new Vector2D(cx + (v.Position.x - cx) * factor, cy + (v.Position.y - cy) * factor);
        foreach (var t in things) t.Position = new Vector2D(cx + (t.Position.x - cx) * factor, cy + (t.Position.y - cy) * factor);
        return true;
    }

    public static bool Rotate(MapSet map, double radians, bool snapToUdbGrid = false)
    {
        var verts = map.SelectedGeometryVertices();
        var things = map.GetSelectedThings();
        if (verts.Count == 0 && things.Count == 0) return false;

        double rotation = snapToUdbGrid ? EditSelectionTransform.SnapRotationToUdbGrid(radians) : radians;
        var (cx, cy) = Center(verts, things);
        var center = new Vector2D(cx, cy);
        foreach (var v in verts) v.Position = RotatePoint(v.Position, center, rotation);
        foreach (var t in things)
        {
            t.Position = RotatePoint(t.Position, center, rotation);
            t.Angle = Angle2D.RealToDoom(Angle2D.Normalized(Angle2D.DoomToReal(t.Angle) + rotation));
        }

        return true;
    }

    private static (double cx, double cy) Center(HashSet<Vertex> verts, List<Thing> things)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void Expand(Vector2D p)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }
        foreach (var v in verts) Expand(v.Position);
        foreach (var t in things) Expand(t.Position);
        return ((minX + maxX) * 0.5, (minY + maxY) * 0.5);
    }

    private static Vector2D Move(Vector2D p, Op op, double cx, double cy) => op switch
    {
        Op.FlipHorizontal => new Vector2D(2 * cx - p.x, p.y),
        Op.FlipVertical => new Vector2D(p.x, 2 * cy - p.y),
        // CCW 90: (dx,dy) -> (-dy, dx);  CW 90: (dx,dy) -> (dy, -dx), about the center.
        Op.RotateCCW => new Vector2D(cx - (p.y - cy), cy + (p.x - cx)),
        Op.RotateCW => new Vector2D(cx + (p.y - cy), cy - (p.x - cx)),
        _ => p,
    };

    private static Vector2D RotatePoint(Vector2D point, Vector2D center, double rotation)
        => (point - center).GetRotated(rotation) + center;

    private static int Angle(int a, Op op)
    {
        int r = op switch
        {
            Op.FlipHorizontal => 180 - a, // mirror about the vertical axis (east<->west)
            Op.FlipVertical => -a,        // mirror about the horizontal axis (north<->south)
            Op.RotateCCW => a + 90,
            Op.RotateCW => a - 90,
            _ => a,
        };
        return ((r % 360) + 360) % 360;
    }
}
