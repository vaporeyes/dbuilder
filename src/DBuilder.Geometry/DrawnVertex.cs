// ABOUTME: DrawnVertex ported from UDB Source/Core/Geometry/DrawnVertex.cs.
// ABOUTME: Plain data — position plus snap-to-line/vertex hints used by drawing modes.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

namespace DBuilder.Geometry;

public struct DrawnVertex
{
    public Vector2D pos;
    public bool stitch;
    public bool stitchline;
}
