// ABOUTME: 2D-projected view frustum ported from DBuilder ProjectedFrustum2D.cs.
// ABOUTME: Preserves UDB's mixed float/double math; f32 stays f32 where C# used float.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::angle2d;
use crate::line2d::Line2D;
use crate::vector2d::Vector2D;

pub struct ProjectedFrustum2D {
    near: f32,
    far: f32,
    fov: f32,
    pos: Vector2D,
    xyangle: f32,
    zangle: f32,

    lines: [Line2D; 4],

    center: Vector2D,
    radius: f32,
}

impl ProjectedFrustum2D {
    pub fn new(
        pos: Vector2D,
        xyangle: f32,
        zangle: f32,
        near: f32,
        far: f32,
        fov: f32,
    ) -> ProjectedFrustum2D {
        // Make the corners for a forward frustum
        // Order: Left-Far, Right-Far, Left-Near, Right-Near
        let fovhalf: f32 = fov * 0.5;
        let fovhalfcos: f32 = (fovhalf as f64).cos() as f32;
        let farsidelength: f32 = far / fovhalfcos;
        let nearsidelength: f32 = near / fovhalfcos;
        let forwards = [
            pos + Vector2D::from_angle_with_length(
                (xyangle - fovhalf) as f64,
                farsidelength as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                (xyangle + fovhalf) as f64,
                farsidelength as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                (xyangle - fovhalf) as f64,
                nearsidelength as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                (xyangle + fovhalf) as f64,
                nearsidelength as f64,
            ),
        ];

        // Corners for a downward frustum
        // C#: (float)(far * 0.5f * Angle2D.SQRT2) -- f32 product widened by the double constant.
        let farradius: f32 = ((far * 0.5) as f64 * angle2d::SQRT2) as f32;
        let downwards = [
            pos + Vector2D::from_angle_with_length(
                xyangle as f64 - angle2d::PI * 0.25f32 as f64,
                farradius as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                xyangle as f64 + angle2d::PI * 0.25f32 as f64,
                farradius as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                xyangle as f64 - angle2d::PI * 0.75f32 as f64,
                farradius as f64,
            ),
            pos + Vector2D::from_angle_with_length(
                xyangle as f64 + angle2d::PI * 0.75f32 as f64,
                farradius as f64,
            ),
        ];

        // Interpolate between forward and downward based on z angle
        let d: f32 = ((zangle as f64).sin() as f32).abs();
        let corners = [
            forwards[0] * (1.0f32 - d) as f64 + downwards[0] * d as f64,
            forwards[1] * (1.0f32 - d) as f64 + downwards[1] * d as f64,
            forwards[2] * (1.0f32 - d) as f64 + downwards[2] * d as f64,
            forwards[3] * (1.0f32 - d) as f64 + downwards[3] * d as f64,
        ];

        // Frustum lines (all oriented so that their right side is inside the frustum)
        let lines = [
            Line2D::new(corners[2], corners[0]),
            Line2D::new(corners[1], corners[3]),
            Line2D::new(corners[3], corners[2]),
            Line2D::new(corners[0], corners[1]),
        ];

        let center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25;

        let mut radius2: f32 = 0.0;
        for corner in corners {
            let distance2 = Vector2D::distance_sq(center, corner) as f32;
            if distance2 > radius2 {
                radius2 = distance2;
            }
        }
        let radius = (radius2 as f64).sqrt() as f32;

        ProjectedFrustum2D {
            near,
            far,
            fov,
            pos,
            xyangle,
            zangle,
            lines,
            center,
            radius,
        }
    }

    pub fn near(&self) -> f32 {
        self.near
    }

    pub fn far(&self) -> f32 {
        self.far
    }

    pub fn fov(&self) -> f32 {
        self.fov
    }

    pub fn position(&self) -> Vector2D {
        self.pos
    }

    pub fn xy_angle(&self) -> f32 {
        self.xyangle
    }

    pub fn z_angle(&self) -> f32 {
        self.zangle
    }

    pub fn lines(&self) -> &[Line2D; 4] {
        &self.lines
    }

    pub fn center(&self) -> Vector2D {
        self.center
    }

    pub fn radius(&self) -> f32 {
        self.radius
    }

    // Checks if a specified circle is intersecting the frustum
    // NOTE: This checks only against the actual frustum and does not use the frustum circle.
    pub fn intersect_circle(&self, circlecenter: Vector2D, circleradius: f32) -> bool {
        for line in &self.lines {
            if line.get_side_of_line(circlecenter) < 0.0 {
                // Center is outside the frustum; check overlap.
                if line.get_distance_to_line_sq(circlecenter, false)
                    > (circleradius * circleradius) as f64
                {
                    return false;
                }
            }
        }

        true
    }

    pub fn intersect_box(&self, boxcenter: Vector2D, halfwidth: f64, halfheight: f64) -> bool {
        for line in &self.lines {
            let dx = line.v2.x - line.v1.x;
            let dy = line.v2.y - line.v1.y;
            let a = -dy;
            let b = dx;
            let d = -(line.v1.x * a + line.v1.y * b);
            let e = halfwidth * a.abs() + halfheight * b.abs();
            let s = boxcenter.x * a + boxcenter.y * b + d;
            if s + e < 0.0 {
                return false;
            }
        }

        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn frustum_builds_four_lines_and_positive_radius() {
        let f = ProjectedFrustum2D::new(
            Vector2D::new(0.0, 0.0),
            0.0,
            0.0,
            10.0,
            100.0,
            (std::f64::consts::PI / 2.0) as f32,
        );
        assert_eq!(4, f.lines().len());
        assert!(f.radius() > 0.0);
    }

    #[test]
    fn circle_inside_frustum_intersects() {
        // UDB's FromAngle(a) returns (sin a, -cos a), so xyangle=0 points along -Y.
        // A circle deep inside the frustum body is at (0, -50).
        let f = ProjectedFrustum2D::new(
            Vector2D::new(0.0, 0.0),
            0.0,
            0.0,
            10.0,
            100.0,
            (std::f64::consts::PI / 2.0) as f32,
        );
        assert!(f.intersect_circle(Vector2D::new(0.0, -50.0), 1.0));
    }

    #[test]
    fn circle_behind_camera_does_not_intersect() {
        // xyangle=0 points along -Y (sin 0, -cos 0); behind the camera is +Y.
        let f = ProjectedFrustum2D::new(
            Vector2D::new(0.0, 0.0),
            0.0,
            0.0,
            10.0,
            100.0,
            (std::f64::consts::PI / 4.0) as f32,
        );
        assert!(!f.intersect_circle(Vector2D::new(0.0, 10000.0), 1.0));
    }

    #[test]
    fn box_inside_frustum_intersects() {
        let f = ProjectedFrustum2D::new(
            Vector2D::new(0.0, 0.0),
            0.0,
            0.0,
            10.0,
            100.0,
            (std::f64::consts::PI / 2.0) as f32,
        );

        assert!(f.intersect_box(Vector2D::new(0.0, -50.0), 8.0, 8.0));
    }

    #[test]
    fn box_behind_camera_does_not_intersect() {
        let f = ProjectedFrustum2D::new(
            Vector2D::new(0.0, 0.0),
            0.0,
            0.0,
            10.0,
            100.0,
            (std::f64::consts::PI / 4.0) as f32,
        );

        assert!(!f.intersect_box(Vector2D::new(0.0, 1000.0), 8.0, 8.0));
    }
}
