// ABOUTME: LabelPositionInfo ported from DBuilder LabelPositionInfo.cs (UDB LabelPositionInfo.cs).
// ABOUTME: Plain data; anchor point and clearance radius for sector label placement.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::vector2d::Vector2D;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct LabelPositionInfo {
    pub position: Vector2D,
    pub radius: f64,
}

impl LabelPositionInfo {
    pub fn new(position: Vector2D, radius: f64) -> LabelPositionInfo {
        LabelPositionInfo { position, radius }
    }
}
