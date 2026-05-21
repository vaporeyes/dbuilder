// ABOUTME: SidedefAngleSorter ported from UDB Source/Core/Geometry/SidedefAngleSorter.cs.
// ABOUTME: Orders sidedefs around a shared vertex by relative angle to a baseline sidedef.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed class SidedefAngleSorter : IComparer<Sidedef>
{
    private Sidedef baseside;
    private Vertex basevertex;

    public SidedefAngleSorter(Sidedef baseside, Vertex fromvertex)
    {
        this.baseside = baseside;
        this.basevertex = fromvertex;
        GC.SuppressFinalize(this);
    }

    private double CalculateRelativeAngle(Sidedef a, Sidedef b)
    {
        double ana = a.Line.Angle; if (a.Line.End == basevertex) ana += Angle2D.PI;
        double anb = b.Line.Angle; if (b.Line.End == basevertex) anb += Angle2D.PI;

        double n = Angle2D.Difference(ana, anb);

        Vector2D va = (a.Line.Start == basevertex ? a.Line.End.Position : a.Line.Start.Position);
        Vector2D vb = (b.Line.Start == basevertex ? b.Line.End.Position : b.Line.Start.Position);

        // Determine rotation direction
        bool dir = baseside.IsFront;
        if (baseside.Line.End == basevertex) dir = !dir;

        double s = Line2D.GetSideOfLine(va, vb, basevertex.Position);
        if ((s < 0) && dir) n = Angle2D.PI2 - n;
        if ((s > 0) && !dir) n = Angle2D.PI2 - n;

        return n;
    }

    public int Compare(Sidedef? x, Sidedef? y)
    {
        // In a release build without debugger attached, x == y sometimes still falls through;
        // the explicit short-circuit here matches UDB's defensive check.
        if (x == y) return 0;

        double ax = CalculateRelativeAngle(baseside, x!);
        double ay = CalculateRelativeAngle(baseside, y!);
        return Math.Sign(ay - ax);
    }
}
