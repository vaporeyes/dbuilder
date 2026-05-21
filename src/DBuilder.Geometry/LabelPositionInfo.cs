// ABOUTME: LabelPositionInfo ported from UDB Source/Core/Geometry/LabelPositionInfo.cs.
// ABOUTME: Plain data — anchor point and clearance radius for sector label placement.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

namespace DBuilder.Geometry;

public struct LabelPositionInfo
{
    public Vector2D position;
    public double radius;

    public LabelPositionInfo(Vector2D position, double radius)
    {
        this.position = position;
        this.radius = radius;
    }
}
