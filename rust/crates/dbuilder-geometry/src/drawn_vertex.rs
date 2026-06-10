// ABOUTME: DrawnVertex ported from DBuilder DrawnVertex.cs (UDB DrawnVertex.cs).
// ABOUTME: Plain data; position plus snap-to-line/vertex hints used by drawing modes.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::vector2d::Vector2D;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct DrawnVertex {
    pub pos: Vector2D,
    pub stitch: bool,
    pub stitchline: bool,
}
