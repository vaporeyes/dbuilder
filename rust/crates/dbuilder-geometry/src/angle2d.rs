// ABOUTME: Angle constants and helpers ported from DBuilder Angle2D.cs (UDB Angle2D.cs).
// ABOUTME: Behavior preserved 1:1, including banker's rounding in the doom-angle conversions.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::vector2d::Vector2D;
use crate::{math_round, math_round_4};

pub const PI: f64 = std::f64::consts::PI;
pub const PIHALF: f64 = std::f64::consts::PI * 0.5;
pub const PI2: f64 = std::f64::consts::PI * 2.0;
pub const PIDEG: f64 = 57.295779513082320876798154814105;
// UDB hardcodes 1.4142135623730950488016887242097, which rounds to the same f64.
pub const SQRT2: f64 = std::f64::consts::SQRT_2;

// This converts doom angle to real angle
pub fn doom_to_real(doomangle: i32) -> f64 {
    math_round_4(normalized(deg_to_rad((doomangle + 90) as f64)))
}

// This converts real angle to doom angle
pub fn real_to_doom(realangle: f64) -> i32 {
    math_round(rad_to_deg(normalized(realangle - PIHALF))) as i32
}

// This converts degrees to radians
pub fn deg_to_rad(deg: f64) -> f64 {
    deg / PIDEG
}

// This converts radians to degrees
pub fn rad_to_deg(rad: f64) -> f64 {
    rad * PIDEG
}

// This normalizes an angle
pub fn normalized(mut a: f64) -> f64 {
    while a < 0.0 {
        a += PI2;
    }
    while a >= PI2 {
        a -= PI2;
    }
    a
}

// This returns the difference between two angles
pub fn difference(a: f64, b: f64) -> f64 {
    // Calculate delta angle
    let mut d = normalized(a) - normalized(b);

    // Make corrections for zero barrier
    if d < 0.0 {
        d += PI2;
    }
    if d > PI {
        d = PI2 - d;
    }

    d
}

//mxd. Slade 3 MathStuff::angle2DRad ripoff...
// Returns the angle between the 2d points [p1], [p2] and [p3]
pub fn get_angle(p1: Vector2D, p2: Vector2D, p3: Vector2D) -> f64 {
    let ab = Vector2D::new(p2.x - p1.x, p2.y - p1.y);
    let cb = Vector2D::new(p2.x - p3.x, p2.y - p3.y);

    // dot product
    let dot = ab.x * cb.x + ab.y * cb.y;

    // length square of both vectors
    let ab_sqr = ab.x * ab.x + ab.y * ab.y;
    let cb_sqr = cb.x * cb.x + cb.y * cb.y;

    // square of cosine of the needed angle
    let cos_sqr = dot * dot / ab_sqr / cb_sqr;

    // this is a known trigonometric equality:
    // cos(alpha * 2) = [ cos(alpha) ]^2 * 2 - 1
    let cos2 = 2.0 * cos_sqr - 1.0;

    // Here's the only invocation of the heavy function.
    // It's a good idea to check explicitly if cos2 is within [-1 .. 1] range
    let alpha2 = if cos2 <= -1.0 {
        PI
    } else if cos2 >= 1.0 {
        0.0
    } else {
        cos2.acos()
    };

    let mut rs = alpha2 * 0.5;

    // 1. If dot product of two vectors is negative - the angle is definitely above 90 degrees.
    if dot < 0.0 {
        rs = PI - rs;
    }

    // 2. Determine the sign via determinant of two vectors.
    let det = ab.x * cb.y - ab.y * cb.x;
    if det < 0.0 {
        rs = (2.0 * PI) - rs;
    }

    rs
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-9;

    #[test]
    fn deg_rad_round_trip() {
        assert!((rad_to_deg(deg_to_rad(90.0)) - 90.0).abs() < EPSILON);
        assert!((rad_to_deg(deg_to_rad(0.5)) - 0.5).abs() < EPSILON);
    }

    #[test]
    fn normalized_wraps_into_0_to_2pi() {
        assert_eq!(0.0, normalized(0.0));
        assert_eq!(PI, normalized(PI));
        assert!(normalized(PI2).abs() < EPSILON);
        assert!((normalized(-PI) - PI).abs() < EPSILON);
    }

    #[test]
    fn difference_handles_wrap_around() {
        // Angles 10 degrees apart across the 0/2pi boundary should still report ~10 deg.
        let a = deg_to_rad(355.0);
        let b = deg_to_rad(5.0);
        let diff = difference(a, b);
        assert!((diff - deg_to_rad(10.0)).abs() < 1e-6);
    }

    #[test]
    fn doom_angle_round_trip_preserves_cardinal() {
        for d in [0, 90, 180, 270] {
            let back = real_to_doom(doom_to_real(d));
            assert_eq!(d, ((back % 360) + 360) % 360);
        }
    }
}
