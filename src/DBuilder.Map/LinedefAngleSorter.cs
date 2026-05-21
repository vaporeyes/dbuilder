// ABOUTME: LinedefAngleSorter ported from UDB Source/Core/Geometry/LinedefAngleSorter.cs.
// ABOUTME: Orders linedefs around a shared vertex by relative angle to a baseline.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed class LinedefAngleSorter : IComparer<Linedef>
{
    private readonly Linedef baseline;
    private readonly bool front;
    private readonly Vertex basevertex;

    public LinedefAngleSorter(Linedef baseline, bool front, Vertex fromvertex)
    {
        this.baseline = baseline;
        this.basevertex = fromvertex;

        // Determine rotation direction
        if (baseline.End == basevertex) this.front = !front; else this.front = front;

        GC.SuppressFinalize(this);
    }

    private double CalculateRelativeAngle(Linedef a, Linedef b)
    {
        // Determine angles
        double ana = a.Angle; if (a.End == basevertex) ana += Angle2D.PI;
        double anb = b.Angle; if (b.End == basevertex) anb += Angle2D.PI;

        double n = Angle2D.Difference(ana, anb);

        // End vertices of each line that are not connected to basevertex
        Vector2D va = (a.Start == basevertex ? a.End.Position : a.Start.Position);
        Vector2D vb = (b.Start == basevertex ? b.End.Position : b.Start.Position);

        // Adjust angle based on which side it goes
        double s = Line2D.GetSideOfLine(va, vb, basevertex.Position);
        if (((s < 0) && front) || ((s > 0) && !front)) n = Angle2D.PI2 - n;

        return n;
    }

    public int Compare(Linedef? x, Linedef? y)
    {
        double ax = CalculateRelativeAngle(baseline, x!);
        double ay = CalculateRelativeAngle(baseline, y!);
        return Math.Sign(ay - ax);
    }
}
