// ABOUTME: LinedefTracePath ported from UDB Source/Core/Geometry/LinedefsTracePath.cs.
// ABOUTME: Closed-loop linedef sequence; can be checked for closure or materialized as an EarClipPolygon.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System.Collections.Generic;

namespace DBuilder.Map;

public sealed class LinedefTracePath : List<Linedef>
{
    public LinedefTracePath() { }

    public LinedefTracePath(IEnumerable<Linedef> lines) : base(lines) { }

    public LinedefTracePath(ICollection<LinedefSide> lines) : base(lines.Count)
    {
        foreach (LinedefSide ls in lines) base.Add(ls.Line);
    }

    public LinedefTracePath(LinedefTracePath p, Linedef add) : base(p)
    {
        base.Add(add);
    }

    // The polygon is closed when the end sidedef shares a vertex with the first.
    public bool CheckIsClosed()
    {
        if (base.Count > 1)
        {
            return (base[0].Start == base[base.Count - 1].Start) ||
                   (base[0].Start == base[base.Count - 1].End) ||
                   (base[0].End == base[base.Count - 1].Start) ||
                   (base[0].End == base[base.Count - 1].End);
        }
        return false;
    }

    // Materializes the trace as an ear-clipping polygon.
    public EarClipPolygon MakePolygon(bool startfront)
    {
        EarClipPolygon p = new EarClipPolygon();
        bool forward = startfront;

        if (base.Count > 0)
        {
            if (forward)
                p.AddLast(new EarClipVertex(base[0].Start.Position, base[0].Front));
            else
                p.AddLast(new EarClipVertex(base[0].End.Position, base[0].Back));

            for (int i = 1; i < base.Count; i++)
            {
                // Traverse direction changes when an endpoint repeats
                if ((base[i - 1].Start == base[i].Start) ||
                    (base[i - 1].End == base[i].End))
                    forward = !forward;

                if (forward)
                    p.AddLast(new EarClipVertex(base[i].Start.Position, base[i].Front));
                else
                    p.AddLast(new EarClipVertex(base[i].End.Position, base[i].Back));
            }
        }

        return p;
    }
}
