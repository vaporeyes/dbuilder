// ABOUTME: 3D line segment ported from UDB Source/Core/Geometry/Line3D.cs.
// ABOUTME: PixelColor replaced with uint ARGB; renderer-mutated Start2D/End2D kept as internal slots.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Geometry;

public class Line3D
{
    public const uint DefaultColor = 0xffffffff;

    public Vector3D Start;
    public Vector3D End;
    public uint Color;
    public readonly bool RenderArrowhead;

    // Mutated by the 2D renderer when projecting for arrow rendering. Kept for surface parity.
    internal Vector2D Start2D;
    internal Vector2D End2D;
    internal bool SkipRendering = false;

    public Line3D(Vector3D start, Vector3D end) : this(start, end, DefaultColor, true) { }
    public Line3D(Vector3D start, Vector3D end, bool renderArrowhead) : this(start, end, DefaultColor, renderArrowhead) { }
    public Line3D(Vector3D start, Vector3D end, uint color) : this(start, end, color, true) { }

    public Line3D(Vector3D start, Vector3D end, uint color, bool renderArrowhead)
    {
        this.Start = start;
        this.End = end;
        this.Start2D = start;
        this.End2D = end;
        this.Color = color;
        this.RenderArrowhead = renderArrowhead;
    }

    public Vector3D GetDelta() => End - Start;

    public double GetAngle()
    {
        Vector2D d = GetDelta();
        return -Math.Atan2(-d.y, d.x) + Angle2D.PIHALF;
    }

    public double GetAngleZ()
    {
        Vector3D d = GetDelta();
        return Math.Atan2(Math.Sqrt(d.x * d.x + d.y * d.y), d.z);
    }
}
