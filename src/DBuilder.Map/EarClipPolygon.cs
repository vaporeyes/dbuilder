// ABOUTME: Polygon container for ear-clipping triangulation ported from UDB Source/Core/Geometry/EarClipPolygon.cs.
// ABOUTME: Tree of nested polygons (outer + holes), with bbox/area/point-in-polygon helpers.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using DBuilder.Geometry;

namespace DBuilder.Map;

[Serializable]
public sealed class EarClipPolygon : LinkedList<EarClipVertex>
{
    private List<EarClipPolygon> children;
    private bool inner;

    public List<EarClipPolygon> Children => children;
    public bool Inner { get => inner; set => inner = value; }

    public EarClipPolygon()
    {
        children = new List<EarClipPolygon>();
    }

    internal EarClipPolygon(EarClipPolygon p, EarClipVertex add) : base(p)
    {
        base.AddLast(add);
        children = new List<EarClipPolygon>();
    }

    // Merges another polygon into this one
    public void Add(EarClipPolygon p)
    {
        foreach (EarClipVertex v in p) base.AddLast(v);
    }

    public double CalculateArea()
    {
        double area = 0;
        LinkedListNode<EarClipVertex>? v = First;
        do
        {
            EarClipVertex v1 = v!.Value;
            EarClipVertex v2 = (v.Next != null) ? v.Next.Value : First!.Value;

            area += (v2.Position.x + v1.Position.x) * (v2.Position.y - v1.Position.y);

            v = v.Next;
        }
        while (v != null);
        return Math.Abs(area * 0.5f);
    }

    // Bounding box of the outer polygon
    public RectangleF CreateBBox()
    {
        double left = float.MaxValue;
        double right = float.MinValue;
        double top = float.MaxValue;
        double bottom = float.MinValue;
        foreach (EarClipVertex v in this)
        {
            if (v.Position.x < left) left = v.Position.x;
            if (v.Position.x > right) right = v.Position.x;
            if (v.Position.y < top) top = v.Position.y;
            if (v.Position.y > bottom) bottom = v.Position.y;
        }
        return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
    }

    // Point in polygon (mxd: skips horizontal edges, uses inclusive-on-upper convention)
    // See: http://paulbourke.net/geometry/polygonmesh/index.html#insidepoly
    public bool Intersect(Vector2D p)
    {
        Vector2D v1 = base.Last!.Value.Position;
        LinkedListNode<EarClipVertex>? n = base.First;
        uint c = 0;
        Vector2D v2;

        while (n != null)
        {
            v2 = n.Value.Position;

            if (v1.y != v2.y //mxd. line is not horizontal
              && p.y >  (v1.y < v2.y ? v1.y : v2.y)
              && p.y <= (v1.y > v2.y ? v1.y : v2.y)
              && (p.x < (v1.x < v2.x ? v1.x : v2.x) || (p.x <= (v1.x > v2.x ? v1.x : v2.x)
                    && (v1.x == v2.x || p.x <= ((p.y - v1.y) * (v2.x - v1.x) / (v2.y - v1.y) + v1.x)))))
                c++;

            v1 = v2;
            n = n.Next;
        }

        // Inside this polygon when we crossed an odd number of polygon lines
        if (c % 2 != 0)
        {
            // Check if not inside the children
            foreach (EarClipPolygon child in children)
            {
                if (child.Intersect(p)) return false;
            }
            return true;
        }
        return false;
    }

    // Inserts a polygon if it is a child of this one
    public bool InsertChild(EarClipPolygon p)
    {
        if (p.Count == 0) return false;

        foreach (EarClipPolygon child in children)
        {
            if (child.InsertChild(p)) return true;
        }

        if (this.Intersect(p.First!.Value.Position))
        {
            p.Inner = !inner;
            children.Add(p);
            return true;
        }

        return false;
    }
}
