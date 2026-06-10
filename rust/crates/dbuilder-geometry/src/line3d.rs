// ABOUTME: 3D line segment ported from DBuilder Line3D.cs (UDB Line3D.cs).
// ABOUTME: Color is u32 ARGB; renderer-mutated 2D projection slots kept for surface parity.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::angle2d;
use crate::vector2d::Vector2D;
use crate::vector3d::Vector3D;

#[derive(Clone, Debug, PartialEq)]
pub struct Line3D {
    pub start: Vector3D,
    pub end: Vector3D,
    pub color: u32,
    pub render_arrowhead: bool,

    // Mutated by the 2D renderer when projecting for arrow rendering. Kept for surface parity.
    pub start_2d: Vector2D,
    pub end_2d: Vector2D,
    pub skip_rendering: bool,
}

impl Line3D {
    pub const DEFAULT_COLOR: u32 = 0xffffffff;

    pub fn new(start: Vector3D, end: Vector3D) -> Line3D {
        Line3D::with_options(start, end, Line3D::DEFAULT_COLOR, true)
    }

    pub fn with_arrowhead(start: Vector3D, end: Vector3D, render_arrowhead: bool) -> Line3D {
        Line3D::with_options(start, end, Line3D::DEFAULT_COLOR, render_arrowhead)
    }

    pub fn with_color(start: Vector3D, end: Vector3D, color: u32) -> Line3D {
        Line3D::with_options(start, end, color, true)
    }

    pub fn with_options(
        start: Vector3D,
        end: Vector3D,
        color: u32,
        render_arrowhead: bool,
    ) -> Line3D {
        Line3D {
            start,
            end,
            color,
            render_arrowhead,
            start_2d: Vector2D::from(start),
            end_2d: Vector2D::from(end),
            skip_rendering: false,
        }
    }

    pub fn get_delta(&self) -> Vector3D {
        self.end - self.start
    }

    pub fn get_angle(&self) -> f64 {
        let d = Vector2D::from(self.get_delta());
        -f64::atan2(-d.y, d.x) + angle2d::PIHALF
    }

    pub fn get_angle_z(&self) -> f64 {
        let d = self.get_delta();
        f64::atan2((d.x * d.x + d.y * d.y).sqrt(), d.z)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_color_and_arrowhead() {
        let l = Line3D::new(Vector3D::new(0.0, 0.0, 0.0), Vector3D::new(1.0, 0.0, 0.0));
        assert_eq!(Line3D::DEFAULT_COLOR, l.color);
        assert!(l.render_arrowhead);
    }

    #[test]
    fn delta_is_end_minus_start() {
        let l = Line3D::new(Vector3D::new(1.0, 2.0, 3.0), Vector3D::new(4.0, 6.0, 8.0));
        assert_eq!(Vector3D::new(3.0, 4.0, 5.0), l.get_delta());
    }

    #[test]
    fn color_ctor_overrides_default() {
        let l = Line3D::with_color(
            Vector3D::new(0.0, 0.0, 0.0),
            Vector3D::new(1.0, 0.0, 0.0),
            0xff00ff00,
        );
        assert_eq!(0xff00ff00, l.color);
    }
}
