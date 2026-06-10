// ABOUTME: Bezier curve utilities ported from DBuilder CurveTools.cs (UDB CurveTools.cs).
// ABOUTME: Pure math; behavior preserved 1:1 including float-literal widening and int casts.

/*
 * mxd. Ported from Cubic Bezier curve tools by Andy Woodruff (http://cartogrammar.com/source/CubicBezier.as)
 */

use crate::angle2d;
use crate::math_round;
use crate::vector2d::Vector2D;
use crate::vector3d::Vector3D;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum CurveSegmentType {
    #[default]
    Line,
    Quadratic,
    Cubic,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct CurveSegment {
    pub points: Vec<Vector2D>,
    pub start: Vector2D,
    pub end: Vector2D,
    pub cp_start: Vector2D,
    pub cp_mid: Vector2D,
    pub cp_end: Vector2D,
    pub curve_type: CurveSegmentType,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct Curve {
    pub segments: Vec<CurveSegment>,
    pub shape: Vec<Vector2D>,
}

impl Curve {
    pub fn new() -> Curve {
        Curve::default()
    }

    pub fn update_shape(&mut self) {
        self.shape = Vec::new();
        for segment in &self.segments {
            for point in &segment.points {
                if self.shape.is_empty() || *point != *self.shape.last().expect("non-empty shape") {
                    self.shape.push(*point);
                }
            }
        }
    }
}

// "default" values: z = 0.5, angleFactor = 0.75; if targetSegmentLength <= 0, will return lines
pub fn curve_through_points(
    points: &[Vector2D],
    mut z: f32,
    mut angle_factor: f32,
    target_segment_length: i32,
) -> Curve {
    let mut result = Curve::new();

    // First calculate all the curve control points
    // None of this junk will do any good if there are only two points
    if points.len() > 2 && target_segment_length > 0 {
        // Two control points (of a cubic Bezier curve) for each point
        let mut control_pts: Vec<[Vector2D; 2]> = Vec::new();

        // Make sure z is between 0 and 1 (too messy otherwise)
        if z <= 0.0 {
            z = 0.1;
        } else if z > 1.0 {
            z = 1.0;
        }

        // Make sure angleFactor is between 0 and 1
        if angle_factor < 0.0 {
            angle_factor = 0.0;
        } else if angle_factor > 1.0 {
            angle_factor = 1.0;
        }

        let mut first_pt = 1usize;
        let mut last_pt = points.len() - 1;

        // Check if this is a closed line (the first and last points are the same)
        if points[0].x == points[points.len() - 1].x && points[0].y == points[points.len() - 1].y {
            first_pt = 0;
            last_pt = points.len();
        } else {
            // dummy entry
            control_pts.push([Vector2D::default(), Vector2D::default()]);
        }

        for i in first_pt..last_pt {
            // The previous, current, and next points
            let p0 = if i == 0 {
                points[points.len() - 2]
            } else {
                points[i - 1]
            };
            let p1 = points[i];
            let p2 = if i + 1 == points.len() {
                points[1]
            } else {
                points[i + 1]
            };

            let mut a = Vector2D::distance(p0, p1);
            if a < 0.001 {
                a = 0.001f32 as f64;
            }
            let mut b = Vector2D::distance(p1, p2);
            if b < 0.001 {
                b = 0.001f32 as f64;
            }
            let mut c = Vector2D::distance(p0, p2);
            if c < 0.001 {
                c = 0.001f32 as f64;
            }

            let mut cos = (b * b + a * a - c * c) / (2.0 * b * a);
            if cos < -1.0 {
                cos = -1.0;
            } else if cos > 1.0 {
                cos = 1.0;
            }

            let big_c = cos.acos();

            let mut a_pt = Vector2D::new(p0.x - p1.x, p0.y - p1.y);
            let b_pt = Vector2D::new(p1.x, p1.y);
            let mut c_pt = Vector2D::new(p2.x - p1.x, p2.y - p1.y);

            if a > b {
                a_pt = a_pt.get_normal() * b;
            } else if b > a {
                c_pt = c_pt.get_normal() * a;
            }

            a_pt = a_pt + p1;
            c_pt = c_pt + p1;

            let ax = b_pt.x - a_pt.x;
            let ay = b_pt.y - a_pt.y;
            let bx = b_pt.x - c_pt.x;
            let by = b_pt.y - c_pt.y;
            let mut rx = ax + bx;
            let mut ry = ay + by;

            // Correct for three points in a line by finding the angle between just two of them
            if rx == 0.0 && ry == 0.0 {
                rx = -bx;
                ry = by;
            }

            // Switch rx and ry when y or x difference is 0
            if ay == 0.0 && by == 0.0 {
                rx = 0.0;
                ry = 1.0;
            } else if ax == 0.0 && bx == 0.0 {
                rx = 1.0;
                ry = 0.0;
            }

            let theta = f64::atan2(ry, rx);

            let mut control_dist = a.min(b) * z as f64;
            let control_scale_factor = big_c / angle2d::PI;
            control_dist *=
                (1.0f32 - angle_factor) as f64 + angle_factor as f64 * control_scale_factor;
            let control_angle = theta + angle2d::PIHALF;

            let mut control_point2 = Vector2D::new(control_dist, 0.0);
            let mut control_point1 = Vector2D::new(control_dist, 0.0);
            control_point2 = control_point2.get_rotated(control_angle);
            control_point1 = control_point1.get_rotated(control_angle + angle2d::PI);

            control_point1 = control_point1 + p1;
            control_point2 = control_point2 + p1;

            if Vector2D::distance(control_point2, p2) > Vector2D::distance(control_point1, p2) {
                control_pts.push([control_point2, control_point1]);
            } else {
                control_pts.push([control_point1, control_point2]);
            }
        }

        // Quadratic Bezier from the first to second points if line not closed.
        if first_pt == 1 {
            let length = (points[1] - points[0]).get_length();
            let num_steps = 1.max(math_round(length / target_segment_length as f64) as i32);
            let mut segment = CurveSegment {
                start: points[0],
                cp_mid: control_pts[1][0],
                end: points[1],
                ..CurveSegment::default()
            };
            create_quadratic_curve(&mut segment, num_steps);

            result.segments.push(segment);
        }

        // Cubic Bezier curves through the penultimate point, or through the last point if closed.
        for i in first_pt..last_pt - 1 {
            let length = (points[i + 1] - points[i]).get_length();
            let num_steps = 1.max(math_round(length / target_segment_length as f64) as i32);

            let mut segment = CurveSegment {
                cp_start: control_pts[i][1],
                cp_end: control_pts[i + 1][0],
                start: points[i],
                end: points[i + 1],
                ..CurveSegment::default()
            };
            create_cubic_curve(&mut segment, num_steps);

            result.segments.push(segment);
        }

        // Last quadratic Bezier if not closed.
        if last_pt == points.len() - 1 {
            let length = (points[last_pt] - points[last_pt - 1]).get_length();
            let num_steps = 1.max(math_round(length / target_segment_length as f64) as i32);

            let mut segment = CurveSegment {
                start: points[last_pt - 1],
                cp_mid: control_pts[last_pt - 1][1],
                end: points[last_pt],
                ..CurveSegment::default()
            };
            create_quadratic_curve(&mut segment, num_steps);

            result.segments.push(segment);
        }
    } else if points.len() >= 2 {
        for i in 0..points.len() - 1 {
            let mut segment = CurveSegment {
                start: points[i],
                end: points[i + 1],
                ..CurveSegment::default()
            };
            segment.points = vec![segment.start, segment.end];
            result.segments.push(segment);
        }
    }

    result.update_shape();
    result
}

pub fn create_quadratic_curve(segment: &mut CurveSegment, steps: i32) {
    segment.curve_type = CurveSegmentType::Quadratic;
    segment.points = get_quadratic_curve(segment.start, segment.cp_mid, segment.end, steps)
        .expect("steps must be >= 0");
}

// 3-point quadratic Bezier
pub fn get_quadratic_curve(
    p1: Vector2D,
    p2: Vector2D,
    p3: Vector2D,
    steps: i32,
) -> Option<Vec<Vector2D>> {
    if steps < 0 {
        return None;
    }
    if steps == 0 {
        return Some(vec![p1]);
    }

    let total_steps = (steps + 1) as usize;
    let mut points = Vec::with_capacity(total_steps);
    let step = 1.0 / steps as f64;
    let mut cur_step = 0.0;

    for _ in 0..total_steps {
        points.push(get_point_on_quadratic_curve(p1, p2, p3, cur_step));
        cur_step += step;
    }
    Some(points)
}

pub fn create_cubic_curve(segment: &mut CurveSegment, steps: i32) {
    segment.curve_type = CurveSegmentType::Cubic;
    segment.points = get_cubic_curve(
        segment.start,
        segment.end,
        segment.cp_start,
        segment.cp_end,
        steps,
    )
    .expect("steps must be >= 0");
}

// 4-point cubic Bezier
pub fn get_cubic_curve(
    p1: Vector2D,
    p2: Vector2D,
    cp1: Vector2D,
    cp2: Vector2D,
    steps: i32,
) -> Option<Vec<Vector2D>> {
    if steps < 0 {
        return None;
    }
    if steps == 0 {
        return Some(vec![p1]);
    }

    let total_steps = (steps + 1) as usize;
    let mut points = Vec::with_capacity(total_steps);
    let step = 1.0 / steps as f64;
    let mut cur_step = 0.0;

    for _ in 0..total_steps {
        points.push(get_point_on_cubic_curve(p1, p2, cp1, cp2, cur_step));
        cur_step += step;
    }
    Some(points)
}

pub fn get_point_on_curve(segment: &CurveSegment, delta: f64) -> Vector2D {
    match segment.curve_type {
        CurveSegmentType::Quadratic => {
            get_point_on_quadratic_curve(segment.start, segment.cp_mid, segment.end, delta)
        }
        CurveSegmentType::Cubic => get_point_on_cubic_curve(
            segment.start,
            segment.end,
            segment.cp_start,
            segment.cp_end,
            delta,
        ),
        CurveSegmentType::Line => get_point_on_line(segment.start, segment.end, delta),
    }
}

pub fn get_point_on_quadratic_curve(
    p1: Vector2D,
    p2: Vector2D,
    p3: Vector2D,
    delta: f64,
) -> Vector2D {
    let inv_delta = 1.0 - delta;
    let m1 = inv_delta * inv_delta;
    let m2 = 2.0 * inv_delta * delta;
    let m3 = delta * delta;
    let px = m1 * p1.x + m2 * p2.x + m3 * p3.x;
    let py = m1 * p1.y + m2 * p2.y + m3 * p3.y;
    Vector2D::new(px, py)
}

pub fn get_point_on_cubic_curve(
    p1: Vector2D,
    p2: Vector2D,
    cp1: Vector2D,
    cp2: Vector2D,
    delta: f64,
) -> Vector2D {
    let inv_delta = 1.0 - delta;
    let m1 = inv_delta * inv_delta * inv_delta;
    let m2 = 3.0 * delta * inv_delta * inv_delta;
    let m3 = 3.0 * delta * delta * inv_delta;
    let m4 = delta * delta * delta;
    let px = m1 * p1.x + m2 * cp1.x + m3 * cp2.x + m4 * p2.x;
    let py = m1 * p1.y + m2 * cp1.y + m3 * cp2.y + m4 * p2.y;
    Vector2D::new(px, py)
}

pub fn hermite_spline_2d(
    p1: Vector2D,
    t1: Vector2D,
    p2: Vector2D,
    t2: Vector2D,
    u: f32,
) -> Vector2D {
    let u = u as f64;
    let u2 = u * u;
    let u3 = u2 * u;
    let h1 = 2.0 * u3 - 3.0 * u2 + 1.0;
    let h2 = -2.0 * u3 + 3.0 * u2;
    let h3 = u3 - 2.0 * u2 + u;
    let h4 = u3 - u2;
    h1 * p1 + h2 * p2 + h3 * t1 + h4 * t2
}

pub fn hermite_spline_3d(
    p1: Vector3D,
    t1: Vector3D,
    p2: Vector3D,
    t2: Vector3D,
    u: f32,
) -> Vector3D {
    let u = u as f64;
    let u2 = u * u;
    let u3 = u2 * u;
    let h1 = 2.0 * u3 - 3.0 * u2 + 1.0;
    let h2 = -2.0 * u3 + 3.0 * u2;
    let h3 = u3 - 2.0 * u2 + u;
    let h4 = u3 - u2;
    h1 * p1 + h2 * p2 + h3 * t1 + h4 * t2
}

// basically 2-point bezier
// NOTE: UDB truncates the interpolated coordinates to int here; preserved verbatim.
pub fn get_point_on_line(p1: Vector2D, p2: Vector2D, delta: f64) -> Vector2D {
    Vector2D::new(
        ((1.0 - delta) * p1.x + delta * p2.x) as i32 as f64,
        ((1.0 - delta) * p1.y + delta * p2.y) as i32 as f64,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-9;

    #[test]
    fn quadratic_bezier_endpoints_match_inputs() {
        let p1 = Vector2D::new(0.0, 0.0);
        let p2 = Vector2D::new(5.0, 10.0);
        let p3 = Vector2D::new(10.0, 0.0);
        assert_eq!(p1, get_point_on_quadratic_curve(p1, p2, p3, 0.0));
        assert_eq!(p3, get_point_on_quadratic_curve(p1, p2, p3, 1.0));
    }

    #[test]
    fn cubic_bezier_endpoints_match_inputs() {
        // Note UDB's GetCubicCurve signature: (start, end, cp1, cp2).
        let p1 = Vector2D::new(0.0, 0.0);
        let p2 = Vector2D::new(10.0, 0.0);
        let cp1 = Vector2D::new(3.0, 5.0);
        let cp2 = Vector2D::new(7.0, 5.0);
        assert_eq!(p1, get_point_on_cubic_curve(p1, p2, cp1, cp2, 0.0));
        assert_eq!(p2, get_point_on_cubic_curve(p1, p2, cp1, cp2, 1.0));
    }

    #[test]
    fn get_quadratic_curve_produces_requested_step_count() {
        let pts = get_quadratic_curve(
            Vector2D::new(0.0, 0.0),
            Vector2D::new(5.0, 10.0),
            Vector2D::new(10.0, 0.0),
            10,
        )
        .expect("non-negative steps");
        assert_eq!(11, pts.len()); // steps + 1
    }

    #[test]
    fn get_quadratic_curve_with_zero_steps_returns_start_point() {
        let p1 = Vector2D::new(0.0, 0.0);

        let pts = get_quadratic_curve(p1, Vector2D::new(5.0, 10.0), Vector2D::new(10.0, 0.0), 0)
            .expect("non-negative steps");

        assert_eq!(vec![p1], pts);
    }

    #[test]
    fn get_quadratic_curve_with_negative_steps_returns_none() {
        assert!(get_quadratic_curve(
            Vector2D::new(0.0, 0.0),
            Vector2D::new(5.0, 10.0),
            Vector2D::new(10.0, 0.0),
            -1,
        )
        .is_none());
    }

    #[test]
    fn get_cubic_curve_produces_requested_step_count() {
        let pts = get_cubic_curve(
            Vector2D::new(0.0, 0.0),
            Vector2D::new(10.0, 0.0),
            Vector2D::new(3.0, 5.0),
            Vector2D::new(7.0, 5.0),
            20,
        )
        .expect("non-negative steps");
        assert_eq!(21, pts.len());
    }

    #[test]
    fn get_cubic_curve_with_zero_steps_returns_start_point() {
        let p1 = Vector2D::new(0.0, 0.0);

        let pts = get_cubic_curve(
            p1,
            Vector2D::new(10.0, 0.0),
            Vector2D::new(3.0, 5.0),
            Vector2D::new(7.0, 5.0),
            0,
        )
        .expect("non-negative steps");

        assert_eq!(vec![p1], pts);
    }

    #[test]
    fn curve_through_points_two_point_fallback_produces_line_segments() {
        let curve = curve_through_points(
            &[Vector2D::new(0.0, 0.0), Vector2D::new(10.0, 0.0)],
            0.5,
            0.75,
            5,
        );
        // Fewer than 3 input points falls into the line-segment branch.
        assert_eq!(1, curve.segments.len());
        assert_eq!(2, curve.segments[0].points.len());
    }

    #[test]
    fn curve_through_points_three_inputs_builds_segments() {
        let pts = [
            Vector2D::new(0.0, 0.0),
            Vector2D::new(5.0, 10.0),
            Vector2D::new(10.0, 0.0),
        ];
        let curve = curve_through_points(&pts, 0.5, 0.75, 5);
        assert!(!curve.segments.is_empty());
        assert!(!curve.shape.is_empty());
    }

    #[test]
    fn hermite_spline_vector2d_matches_endpoints() {
        let p1 = Vector2D::new(0.0, 0.0);
        let p2 = Vector2D::new(10.0, 2.0);
        let t1 = Vector2D::new(4.0, 8.0);
        let t2 = Vector2D::new(-2.0, 6.0);

        assert_eq!(p1, hermite_spline_2d(p1, t1, p2, t2, 0.0));
        assert_eq!(p2, hermite_spline_2d(p1, t1, p2, t2, 1.0));
    }

    #[test]
    fn hermite_spline_vector2d_interpolates_with_tangents() {
        let result = hermite_spline_2d(
            Vector2D::new(0.0, 0.0),
            Vector2D::new(4.0, 8.0),
            Vector2D::new(10.0, 2.0),
            Vector2D::new(-2.0, 6.0),
            0.25,
        );

        assert!((result.x - 2.21875).abs() < EPSILON);
        assert!((result.y - 1.15625).abs() < EPSILON);
    }

    #[test]
    fn hermite_spline_vector3d_matches_endpoints() {
        let p1 = Vector3D::new(1.0, 2.0, 3.0);
        let p2 = Vector3D::new(9.0, 6.0, 0.0);
        let t1 = Vector3D::new(4.0, -2.0, 1.0);
        let t2 = Vector3D::new(-3.0, 5.0, 2.0);

        assert_eq!(p1, hermite_spline_3d(p1, t1, p2, t2, 0.0));
        assert_eq!(p2, hermite_spline_3d(p1, t1, p2, t2, 1.0));
    }

    #[test]
    fn hermite_spline_vector3d_interpolates_with_tangents() {
        let result = hermite_spline_3d(
            Vector3D::new(1.0, 2.0, 3.0),
            Vector3D::new(4.0, -2.0, 1.0),
            Vector3D::new(9.0, 6.0, 0.0),
            Vector3D::new(-3.0, 5.0, 2.0),
            0.5,
        );

        assert!((result.x - 5.875).abs() < EPSILON);
        assert!((result.y - 3.125).abs() < EPSILON);
        assert!((result.z - 1.375).abs() < EPSILON);
    }

    #[test]
    fn get_point_on_line_truncates_to_int_like_udb() {
        // UDB casts the interpolated coordinates to int; 7.5 truncates to 7.
        let p = get_point_on_line(Vector2D::new(0.0, 0.0), Vector2D::new(15.0, 15.0), 0.5);
        assert_eq!(Vector2D::new(7.0, 7.0), p);
    }
}
