// ABOUTME: 2D-projected view frustum ported from UDB Source/Core/Geometry/ProjectedFrustum2D.cs.
// ABOUTME: Builds a planar frustum polygon plus bounding circle for fast visibility culling.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;

namespace DBuilder.Geometry;

public class ProjectedFrustum2D
{
    private readonly float near;
    private readonly float far;
    private readonly float fov;
    private readonly Vector2D pos;
    private readonly float xyangle;
    private readonly float zangle;

    private readonly Line2D[] lines;

    private readonly Vector2D center;
    private readonly float radius;

    public float Near => near;
    public float Far => far;
    public float Fov => fov;
    public Vector2D Position => pos;
    public float XYAngle => xyangle;
    public float ZAngle => zangle;
    public Line2D[] Lines => lines;
    public Vector2D Center => center;
    public float Radius => radius;

    public ProjectedFrustum2D(Vector2D pos, float xyangle, float zangle, float near, float far, float fov)
    {
        Vector2D[] forwards = new Vector2D[4];
        Vector2D[] downwards = new Vector2D[4];
        Vector2D[] corners = new Vector2D[4];

        this.pos = pos;
        this.xyangle = xyangle;
        this.zangle = zangle;
        this.near = near;
        this.far = far;
        this.fov = fov;

        // Make the corners for a forward frustum
        // Order: Left-Far, Right-Far, Left-Near, Right-Near
        float fovhalf = fov * 0.5f;
        float fovhalfcos = (float)Math.Cos(fovhalf);
        float farsidelength = far / fovhalfcos;
        float nearsidelength = near / fovhalfcos;
        forwards[0] = pos + Vector2D.FromAngle(xyangle - fovhalf, farsidelength);
        forwards[1] = pos + Vector2D.FromAngle(xyangle + fovhalf, farsidelength);
        forwards[2] = pos + Vector2D.FromAngle(xyangle - fovhalf, nearsidelength);
        forwards[3] = pos + Vector2D.FromAngle(xyangle + fovhalf, nearsidelength);

        // Corners for a downward frustum
        float farradius = (float)(far * 0.5f * Angle2D.SQRT2);
        downwards[0] = pos + Vector2D.FromAngle(xyangle - Angle2D.PI * 0.25f, farradius);
        downwards[1] = pos + Vector2D.FromAngle(xyangle + Angle2D.PI * 0.25f, farradius);
        downwards[2] = pos + Vector2D.FromAngle(xyangle - Angle2D.PI * 0.75f, farradius);
        downwards[3] = pos + Vector2D.FromAngle(xyangle + Angle2D.PI * 0.75f, farradius);

        // Interpolate between forward and downward based on z angle
        float d = Math.Abs((float)Math.Sin(zangle));
        corners[0] = forwards[0] * (1.0f - d) + downwards[0] * d;
        corners[1] = forwards[1] * (1.0f - d) + downwards[1] * d;
        corners[2] = forwards[2] * (1.0f - d) + downwards[2] * d;
        corners[3] = forwards[3] * (1.0f - d) + downwards[3] * d;

        // Frustum lines (all oriented so that their right side is inside the frustum)
        lines = new Line2D[4];
        lines[0] = new Line2D(corners[2], corners[0]);
        lines[1] = new Line2D(corners[1], corners[3]);
        lines[2] = new Line2D(corners[3], corners[2]);
        lines[3] = new Line2D(corners[0], corners[1]);

        center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

        float radius2 = 0.0f;
        for (int i = 0; i < corners.Length; i++)
        {
            float distance2 = (float)Vector2D.DistanceSq(center, corners[i]);
            if (distance2 > radius2) radius2 = distance2;
        }
        radius = (float)Math.Sqrt(radius2);
    }

    // Checks if a specified circle is intersecting the frustum
    // NOTE: This checks only against the actual frustum and does not use the frustum circle.
    public bool IntersectCircle(Vector2D circlecenter, float circleradius)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].GetSideOfLine(circlecenter) < 0)
            {
                // Center is outside the frustum; check overlap.
                if (lines[i].GetDistanceToLineSq(circlecenter, false) > (circleradius * circleradius)) return false;
            }
        }

        return true;
    }

    public bool IntersectBox(Vector2D boxcenter, double halfwidth, double halfheight)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            Line2D line = lines[i];
            double dx = line.v2.x - line.v1.x;
            double dy = line.v2.y - line.v1.y;
            double a = -dy;
            double b = dx;
            double d = -(line.v1.x * a + line.v1.y * b);
            double e = halfwidth * Math.Abs(a) + halfheight * Math.Abs(b);
            double s = boxcenter.x * a + boxcenter.y * b + d;
            if (s + e < 0.0) return false;
        }

        return true;
    }
}
