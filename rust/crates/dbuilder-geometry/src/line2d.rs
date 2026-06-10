// ABOUTME: 2D line segment ported from DBuilder Line2D.cs (UDB Line2D.cs).
// ABOUTME: RectF stands in for System.Drawing.RectangleF; out-params become tuples/Options.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use std::fmt;

use crate::angle2d;
use crate::vector2d::Vector2D;

// Minimal stand-in for System.Drawing.RectangleF; fields stay f32 to mirror C# precision.
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct RectF {
    pub left: f32,
    pub top: f32,
    pub right: f32,
    pub bottom: f32,
}

impl RectF {
    // Mirrors `new RectangleF(x, y, width, height)`.
    pub fn from_ltwh(x: f32, y: f32, width: f32, height: f32) -> RectF {
        RectF {
            left: x,
            top: y,
            right: x + width,
            bottom: y + height,
        }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Line2D {
    pub v1: Vector2D,
    pub v2: Vector2D,
}

impl Line2D {
    pub fn new(v1: Vector2D, v2: Vector2D) -> Line2D {
        Line2D { v1, v2 }
    }

    pub fn from_coords(x1: f64, y1: f64, x2: f64, y2: f64) -> Line2D {
        Line2D {
            v1: Vector2D::new(x1, y1),
            v2: Vector2D::new(x2, y2),
        }
    }

    // NOTE: UDB has `Line2D(Linedef)`. Reintroduce when the Map module is ported.

    pub fn get_length_static(dx: f64, dy: f64) -> f64 {
        Line2D::get_length_sq_static(dx, dy).sqrt()
    }

    pub fn get_length_sq_static(dx: f64, dy: f64) -> f64 {
        dx * dx + dy * dy
    }

    pub fn get_normal_static(dx: f64, dy: f64) -> Vector2D {
        Vector2D::new(dx, dy).get_normal()
    }

    //mxd. This tests if given lines intersect (bounded, like UDB's no-out overload chain).
    pub fn lines_intersect(line1: Line2D, line2: Line2D) -> bool {
        Line2D::get_intersection(
            line1.v1, line1.v2, line2.v1.x, line2.v1.y, line2.v2.x, line2.v2.y, true,
        )
        .0
    }

    //mxd. Gets intersection point between given lines
    pub fn get_intersection_point(line1: Line2D, line2: Line2D, bounded: bool) -> Vector2D {
        let (found, u_ray, _) = Line2D::get_intersection(
            line1.v1, line1.v2, line2.v1.x, line2.v1.y, line2.v2.x, line2.v2.y, bounded,
        );
        if found {
            return Line2D::get_coordinates_at_static(line2.v1, line2.v2, u_ray);
        }

        Vector2D::new(f64::NAN, f64::NAN)
    }

    // Returns (found, u_ray, u_line). Mirrors the C# out-param overload exactly, including
    // NaN u values when the lines are parallel and computed u values on a bounded miss.
    pub fn get_intersection(
        v1: Vector2D,
        v2: Vector2D,
        x3: f64,
        y3: f64,
        x4: f64,
        y4: f64,
        bounded: bool,
    ) -> (bool, f64, f64) {
        // Calculate divider
        let div = (y4 - y3) * (v2.x - v1.x) - (x4 - x3) * (v2.y - v1.y);

        if div != 0.0 {
            // Calculate the intersection distance from the line
            let u_line = ((x4 - x3) * (v1.y - y3) - (y4 - y3) * (v1.x - x3)) / div;

            // Calculate the intersection distance from the ray
            let u_ray = ((v2.x - v1.x) * (v1.y - y3) - (v2.y - v1.y) * (v1.x - x3)) / div;

            if bounded && (u_ray < 0.0 || u_ray > 1.0 || u_line < 0.0 || u_line > 1.0) {
                return (false, u_ray, u_line); //mxd
            }
            return (true, u_ray, u_line);
        }

        (false, f64::NAN, f64::NAN)
    }

    // Side test: < 0 = front (right), > 0 = back (left), 0 = on the line
    pub fn get_side_of_line_static(v1: Vector2D, v2: Vector2D, p: Vector2D) -> f64 {
        (p.y - v1.y) * (v2.x - v1.x) - (p.x - v1.x) * (v2.y - v1.y)
    }

    pub fn get_distance_to_line_static(
        v1: Vector2D,
        v2: Vector2D,
        p: Vector2D,
        bounded: bool,
    ) -> f64 {
        Line2D::get_distance_to_line_sq_static(v1, v2, p, bounded).sqrt()
    }

    pub fn get_distance_to_line_sq_static(
        v1: Vector2D,
        v2: Vector2D,
        p: Vector2D,
        bounded: bool,
    ) -> f64 {
        let length_sq = Line2D::get_length_sq_static(v2.x - v1.x, v2.y - v1.y);
        if length_sq == 0.0 {
            return Vector2D::distance_sq(v1, p);
        }

        let mut u = ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / length_sq;

        if bounded {
            if u < 0.0 {
                u = 0.0;
            } else if u > 1.0 {
                u = 1.0;
            }
        }

        let i = v1 + u * (v2 - v1);

        let ldx = p.x - i.x;
        let ldy = p.y - i.y;
        ldx * ldx + ldy * ldy
    }

    pub fn get_nearest_on_line_static(v1: Vector2D, v2: Vector2D, p: Vector2D) -> f64 {
        let length_sq = Line2D::get_length_sq_static(v2.x - v1.x, v2.y - v1.y);
        if length_sq == 0.0 {
            return 0.0;
        }

        ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / length_sq
    }

    pub fn get_nearest_point_on_line_static(
        v1: Vector2D,
        v2: Vector2D,
        p: Vector2D,
        bounded: bool,
    ) -> Vector2D {
        let length_sq = Line2D::get_length_sq_static(v2.x - v1.x, v2.y - v1.y);
        if length_sq == 0.0 {
            return v1;
        }

        let mut u = ((p.x - v1.x) * (v2.x - v1.x) + (p.y - v1.y) * (v2.y - v1.y)) / length_sq;
        if bounded {
            if u < 0.0 {
                u = 0.0;
            } else if u > 1.0 {
                u = 1.0;
            }
        }

        Line2D::get_coordinates_at_static(v1, v2, u)
    }

    pub fn get_coordinates_at_static(v1: Vector2D, v2: Vector2D, u: f64) -> Vector2D {
        Vector2D::new(v1.x + u * (v2.x - v1.x), v1.y + u * (v2.y - v1.y))
    }

    fn is_equal_float(a: f64, b: f64) -> bool {
        (a - b).abs() < 0.0001f32 as f64
    }

    // Some random self-written algorithm instead of Cohen-Sutherland algorithm which used to hang up randomly
    // Returns None where the C# version sets `intersects = false`.
    pub fn clip_to_rectangle(line: Line2D, rect: RectF) -> Option<Line2D> {
        let mut rate_xy = 0.0;
        if line.v2.y != line.v1.y {
            let dx = line.v2.x - line.v1.x;
            let dy = line.v2.y - line.v1.y;
            rate_xy = dx / dy;
        }

        let (mut x1, mut y1) = (line.v1.x, line.v1.y);
        let (mut x2, mut y2) = (line.v2.x, line.v2.y);

        let top = rect.top as f64;
        let left = rect.left as f64;
        let bottom = rect.bottom as f64;
        let right = rect.right as f64;

        for _ in 0..2 {
            // check x1,y1
            if y1 < top {
                x1 += (top - y1) * rate_xy;
                y1 = top;
            }
            if x1 < left {
                if rate_xy != 0.0 {
                    y1 += (left - x1) / rate_xy;
                }
                x1 = left;
            }
            // check x2,y2
            if y2 < top {
                x2 += (top - y2) * rate_xy;
                y2 = top;
            }
            if x2 < left {
                if rate_xy != 0.0 {
                    y2 += (left - x2) / rate_xy;
                }
                x2 = left;
            }
            // check x1,y1
            if y1 > bottom {
                x1 -= (y1 - bottom) * rate_xy;
                y1 = bottom;
            }
            if x1 > right {
                if rate_xy != 0.0 {
                    y1 -= (x1 - right) / rate_xy;
                }
                x1 = right;
            }
            // check x2,y2
            if y2 > bottom {
                x2 -= (y2 - bottom) * rate_xy;
                y2 = bottom;
            }
            if x2 > right {
                if rate_xy != 0.0 {
                    y2 -= (x2 - right) / rate_xy;
                }
                x2 = right;
            }
        }

        if (Line2D::is_equal_float(x1, x2)
            && (Line2D::is_equal_float(x1, left) || Line2D::is_equal_float(x1, right)))
            || (Line2D::is_equal_float(y1, y2)
                && (Line2D::is_equal_float(y1, bottom) || Line2D::is_equal_float(y1, top)))
        {
            return None;
        }

        Some(Line2D::from_coords(x1, y1, x2, y2))
    }

    // Perpendicular by simply making a normal
    pub fn get_perpendicular(self) -> Vector2D {
        let d = self.get_delta();
        Vector2D::new(-d.y, d.x)
    }

    pub fn get_angle(self) -> f64 {
        let d = self.get_delta();
        -f64::atan2(-d.y, d.x) + angle2d::PIHALF
    }

    pub fn get_delta(self) -> Vector2D {
        self.v2 - self.v1
    }

    pub fn get_length(self) -> f64 {
        Line2D::get_length_static(self.v2.x - self.v1.x, self.v2.y - self.v1.y)
    }

    pub fn get_length_sq(self) -> f64 {
        Line2D::get_length_sq_static(self.v2.x - self.v1.x, self.v2.y - self.v1.y)
    }

    pub fn get_intersection_with(self, ray: Line2D, bounded: bool) -> (bool, f64, f64) {
        Line2D::get_intersection(
            self.v1, self.v2, ray.v1.x, ray.v1.y, ray.v2.x, ray.v2.y, bounded,
        )
    }

    pub fn get_side_of_line(self, p: Vector2D) -> f64 {
        Line2D::get_side_of_line_static(self.v1, self.v2, p)
    }

    pub fn get_distance_to_line(self, p: Vector2D, bounded: bool) -> f64 {
        Line2D::get_distance_to_line_static(self.v1, self.v2, p, bounded)
    }

    pub fn get_distance_to_line_sq(self, p: Vector2D, bounded: bool) -> f64 {
        Line2D::get_distance_to_line_sq_static(self.v1, self.v2, p, bounded)
    }

    pub fn get_nearest_on_line(self, p: Vector2D) -> f64 {
        Line2D::get_nearest_on_line_static(self.v1, self.v2, p)
    }

    pub fn get_nearest_point_on_line(self, p: Vector2D, bounded: bool) -> Vector2D {
        Line2D::get_nearest_point_on_line_static(self.v1, self.v2, p, bounded)
    }

    pub fn get_coordinates_at(self, u: f64) -> Vector2D {
        Line2D::get_coordinates_at_static(self.v1, self.v2, u)
    }

    pub fn get_transformed(self, offsetx: f64, offsety: f64, scalex: f64, scaley: f64) -> Line2D {
        Line2D::new(
            self.v1.get_transformed(offsetx, offsety, scalex, scaley),
            self.v2.get_transformed(offsetx, offsety, scalex, scaley),
        )
    }

    pub fn get_inv_transformed(
        self,
        invoffsetx: f64,
        invoffsety: f64,
        invscalex: f64,
        invscaley: f64,
    ) -> Line2D {
        Line2D::new(
            self.v1
                .get_inv_transformed(invoffsetx, invoffsety, invscalex, invscaley),
            self.v2
                .get_inv_transformed(invoffsetx, invoffsety, invscalex, invscaley),
        )
    }
}

impl fmt::Display for Line2D {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "({}) - ({})", self.v1, self.v2)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-9;

    #[test]
    fn delta_and_length() {
        let l = Line2D::from_coords(0.0, 0.0, 3.0, 4.0);
        assert_eq!(Vector2D::new(3.0, 4.0), l.get_delta());
        assert!((l.get_length() - 5.0).abs() < EPSILON);
        assert!((l.get_length_sq() - 25.0).abs() < EPSILON);
    }

    #[test]
    fn perpendicular_rotates_90_deg_left() {
        // (1,0) -> perpendicular -> (0,1) by UDB's convention (-dy, dx).
        let p = Line2D::from_coords(0.0, 0.0, 1.0, 0.0).get_perpendicular();
        assert_eq!(Vector2D::new(0.0, 1.0), p);
    }

    #[test]
    fn crossing_lines_intersect() {
        // Horizontal line y=0 and vertical x=0 cross at origin.
        let a = Line2D::from_coords(-1.0, 0.0, 1.0, 0.0);
        let b = Line2D::from_coords(0.0, -1.0, 0.0, 1.0);
        assert!(Line2D::lines_intersect(a, b));
    }

    #[test]
    fn parallel_lines_do_not_intersect() {
        let a = Line2D::from_coords(0.0, 0.0, 1.0, 0.0);
        let b = Line2D::from_coords(0.0, 1.0, 1.0, 1.0);
        assert!(!Line2D::lines_intersect(a, b));
    }

    #[test]
    fn intersection_point_matches_geometry() {
        let a = Line2D::from_coords(-1.0, 0.0, 1.0, 0.0);
        let b = Line2D::from_coords(0.0, -1.0, 0.0, 1.0);
        let p = Line2D::get_intersection_point(a, b, true);
        assert!(p.x.abs() < EPSILON);
        assert!(p.y.abs() < EPSILON);
    }

    #[test]
    fn side_of_line_sign_splits_left_right() {
        // Line from (0,0) to (1,0): point above (y>0) is back, below is front per UDB sign convention.
        let v1 = Vector2D::new(0.0, 0.0);
        let v2 = Vector2D::new(1.0, 0.0);
        let above = Line2D::get_side_of_line_static(v1, v2, Vector2D::new(0.5, 1.0));
        let below = Line2D::get_side_of_line_static(v1, v2, Vector2D::new(0.5, -1.0));
        assert!(above > 0.0 && below < 0.0);
    }

    #[test]
    fn distance_to_line_with_bounded_clamp() {
        let v1 = Vector2D::new(0.0, 0.0);
        let v2 = Vector2D::new(10.0, 0.0);
        // Point at (5, 3) projects onto the line at (5, 0); perpendicular distance is 3.
        assert!(
            (Line2D::get_distance_to_line_static(v1, v2, Vector2D::new(5.0, 3.0), true) - 3.0)
                .abs()
                < EPSILON
        );
        // Point at (15, 0) is beyond the line endpoint; bounded distance is 5 (to (10,0)).
        assert!(
            (Line2D::get_distance_to_line_static(v1, v2, Vector2D::new(15.0, 0.0), true) - 5.0)
                .abs()
                < EPSILON
        );
        // Unbounded: that same point is on the infinite line extension, distance 0.
        assert!(
            Line2D::get_distance_to_line_static(v1, v2, Vector2D::new(15.0, 0.0), false).abs()
                < EPSILON
        );
    }

    #[test]
    fn nearest_on_line_gives_0_1_for_endpoints() {
        let v1 = Vector2D::new(0.0, 0.0);
        let v2 = Vector2D::new(10.0, 0.0);
        assert!(
            Line2D::get_nearest_on_line_static(v1, v2, Vector2D::new(0.0, 5.0)).abs() < EPSILON
        );
        assert!(
            (Line2D::get_nearest_on_line_static(v1, v2, Vector2D::new(10.0, 5.0)) - 1.0).abs()
                < EPSILON
        );
        assert!(
            (Line2D::get_nearest_on_line_static(v1, v2, Vector2D::new(5.0, 5.0)) - 0.5).abs()
                < EPSILON
        );
    }

    #[test]
    fn coordinates_at_interpolates() {
        let v1 = Vector2D::new(0.0, 0.0);
        let v2 = Vector2D::new(10.0, 0.0);
        assert_eq!(
            Vector2D::new(5.0, 0.0),
            Line2D::get_coordinates_at_static(v1, v2, 0.5)
        );
    }

    #[test]
    fn nearest_point_on_line_clamps_when_bounded() {
        let v1 = Vector2D::new(0.0, 0.0);
        let v2 = Vector2D::new(10.0, 0.0);

        assert_eq!(
            Vector2D::new(5.0, 0.0),
            Line2D::get_nearest_point_on_line_static(v1, v2, Vector2D::new(5.0, 3.0), true)
        );
        assert_eq!(
            Vector2D::new(10.0, 0.0),
            Line2D::get_nearest_point_on_line_static(v1, v2, Vector2D::new(15.0, 3.0), true)
        );
        assert_eq!(
            Vector2D::new(15.0, 0.0),
            Line2D::get_nearest_point_on_line_static(v1, v2, Vector2D::new(15.0, 3.0), false)
        );
    }

    #[test]
    fn nearest_point_on_degenerate_line_returns_start() {
        let line = Line2D::from_coords(3.0, 4.0, 3.0, 4.0);

        assert_eq!(
            Vector2D::new(3.0, 4.0),
            line.get_nearest_point_on_line(Vector2D::new(100.0, 200.0), true)
        );
    }

    #[test]
    fn degenerate_line_distance_uses_start_point_distance() {
        let line = Line2D::from_coords(3.0, 4.0, 3.0, 4.0);

        assert!(
            (Line2D::get_distance_to_line_sq_static(
                line.v1,
                line.v2,
                Vector2D::new(6.0, 8.0),
                true
            ) - 25.0)
                .abs()
                < EPSILON
        );
        assert!((line.get_distance_to_line(Vector2D::new(6.0, 8.0), false) - 5.0).abs() < EPSILON);
    }

    #[test]
    fn nearest_on_degenerate_line_returns_start_parameter() {
        let line = Line2D::from_coords(3.0, 4.0, 3.0, 4.0);

        assert!(
            Line2D::get_nearest_on_line_static(line.v1, line.v2, Vector2D::new(6.0, 8.0)).abs()
                < EPSILON
        );
        assert!(line.get_nearest_on_line(Vector2D::new(6.0, 8.0)).abs() < EPSILON);
    }

    #[test]
    fn transform_round_trip() {
        // Forward: (x+off)*scale. Inverse: x*invScale + invOff. To round-trip we need invOff = -off and invScale = 1/scale.
        let l = Line2D::from_coords(2.0, 3.0, 4.0, 5.0);
        let t = l.get_transformed(1.0, 1.0, 2.0, 2.0);
        let back = t.get_inv_transformed(-1.0, -1.0, 0.5, 0.5);
        assert!((l.v1.x - back.v1.x).abs() < EPSILON);
        assert!((l.v1.y - back.v1.y).abs() < EPSILON);
        assert!((l.v2.x - back.v2.x).abs() < EPSILON);
        assert!((l.v2.y - back.v2.y).abs() < EPSILON);
    }

    #[test]
    fn clip_to_rectangle_keeps_line_inside() {
        let rect = RectF::from_ltwh(0.0, 0.0, 10.0, 10.0);
        let l = Line2D::from_coords(1.0, 1.0, 9.0, 9.0);
        let clipped = Line2D::clip_to_rectangle(l, rect).expect("line inside rectangle intersects");
        assert_eq!(l.v1, clipped.v1);
        assert_eq!(l.v2, clipped.v2);
    }

    #[test]
    fn clip_to_rectangle_clips_crossing_diagonal_to_box() {
        let rect = RectF::from_ltwh(0.0, 0.0, 10.0, 10.0);

        let clipped = Line2D::clip_to_rectangle(Line2D::from_coords(-5.0, -5.0, 15.0, 15.0), rect)
            .expect("crossing diagonal intersects");

        assert!(clipped.v1.x.abs() < EPSILON);
        assert!(clipped.v1.y.abs() < EPSILON);
        assert!((clipped.v2.x - 10.0).abs() < EPSILON);
        assert!((clipped.v2.y - 10.0).abs() < EPSILON);
    }

    #[test]
    fn clip_to_rectangle_rejects_line_collapsed_to_rectangle_edge() {
        let rect = RectF::from_ltwh(0.0, 0.0, 10.0, 10.0);

        let clipped = Line2D::clip_to_rectangle(Line2D::from_coords(-5.0, 0.0, 15.0, 0.0), rect);

        assert!(clipped.is_none());
    }
}
