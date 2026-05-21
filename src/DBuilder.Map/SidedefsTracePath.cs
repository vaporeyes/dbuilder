// ABOUTME: SidedefsTracePath ported from UDB Source/Core/Geometry/SidedefsTracePath.cs.
// ABOUTME: Closed-loop sidedef sequence; checks closure or materializes as an EarClipPolygon.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System.Collections.Generic;

namespace DBuilder.Map;

public sealed class SidedefsTracePath : List<Sidedef>
{
    public SidedefsTracePath() { }

    public SidedefsTracePath(SidedefsTracePath p, Sidedef add) : base(p)
    {
        base.Add(add);
    }

    // Closed when the end sidedef shares a vertex with the first.
    public bool CheckIsClosed()
    {
        if (base.Count > 1)
        {
            return (base[0].Line.Start == base[base.Count - 1].Line.Start) ||
                   (base[0].Line.Start == base[base.Count - 1].Line.End) ||
                   (base[0].Line.End == base[base.Count - 1].Line.Start) ||
                   (base[0].Line.End == base[base.Count - 1].Line.End);
        }
        return false;
    }

    public EarClipPolygon MakePolygon()
    {
        EarClipPolygon p = new EarClipPolygon();

        if (base.Count > 0)
        {
            for (int i = 0; i < base.Count; i++)
            {
                if (base[i].IsFront)
                    p.AddLast(new EarClipVertex(base[i].Line.End.Position, base[i]));
                else
                    p.AddLast(new EarClipVertex(base[i].Line.Start.Position, base[i]));
            }
        }

        return p;
    }
}
