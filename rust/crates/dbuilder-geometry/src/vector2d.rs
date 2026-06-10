// ABOUTME: 2D double-precision vector ported from DBuilder Vector2D.cs (UDB Vector2D.cs).
// ABOUTME: Behavior preserved 1:1; C# operators map to Rust operator traits.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use std::fmt;
use std::ops::{Add, Div, Mul, Neg, Sub};

use crate::angle2d;
use crate::math_sign;
use crate::vector3d::Vector3D;

// C# declares this as the float literal 0.0000000001f widened to double.
pub(crate) const TINY_VALUE: f64 = 0.0000000001f32 as f64;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Vector2D {
    pub x: f64,
    pub y: f64,
}

impl Vector2D {
    pub fn new(x: f64, y: f64) -> Vector2D {
        Vector2D { x, y }
    }

    pub fn dot_product(a: Vector2D, b: Vector2D) -> f64 {
        a.x * b.x + a.y * b.y
    }

    // NOTE: this is UDB's original "cross product" for Vector2D, which is not the conventional
    // 2D cross. Preserved verbatim for compatibility with existing call sites.
    pub fn cross_product(a: Vector2D, b: Vector2D) -> Vector2D {
        Vector2D {
            x: a.y * b.x,
            y: a.x * b.y,
        }
    }

    // This reflects the vector v over mirror m. Note that mirror m must be normalized.
    // R = V - 2 * M * (M dot V)
    pub fn reflect(v: Vector2D, m: Vector2D) -> Vector2D {
        let dp = Vector2D::dot_product(m, v);
        Vector2D {
            x: v.x - (2.0 * m.x * dp),
            y: v.y - (2.0 * m.y * dp),
        }
    }

    pub fn reversed(v: Vector2D) -> Vector2D {
        Vector2D::new(-v.x, -v.y)
    }

    // This returns a vector from an angle
    pub fn from_angle(angle: f64) -> Vector2D {
        Vector2D::new(angle.sin(), -angle.cos())
    }

    // This returns a vector from an angle with a given length
    pub fn from_angle_with_length(angle: f64, length: f64) -> Vector2D {
        Vector2D::from_angle(angle) * length
    }

    // This calculates the angle between two points (static GetAngle in C#)
    pub fn get_angle_between(a: Vector2D, b: Vector2D) -> f64 {
        -f64::atan2(-(a.y - b.y), a.x - b.x) + angle2d::PIHALF
    }

    pub fn distance_sq(a: Vector2D, b: Vector2D) -> f64 {
        (a - b).get_length_sq()
    }

    pub fn distance(a: Vector2D, b: Vector2D) -> f64 {
        (a - b).get_length()
    }

    pub fn manhattan_distance(a: Vector2D, b: Vector2D) -> f64 {
        let d = a - b;
        d.x.abs() + d.y.abs()
    }

    // Perpendicular by simply making a normal
    pub fn get_perpendicular(self) -> Vector2D {
        Vector2D::new(-self.y, self.x)
    }

    pub fn get_sign(self) -> Vector2D {
        Vector2D::new(math_sign(self.x), math_sign(self.y))
    }

    pub fn get_angle(self) -> f64 {
        //mxd. Make sure the angle is in [0 .. PI2] range
        let mut angle = -f64::atan2(-self.y, self.x) + angle2d::PIHALF;
        if angle < 0.0 {
            angle += angle2d::PI2;
        }
        angle
    }

    pub fn get_length(self) -> f64 {
        (self.x * self.x + self.y * self.y).sqrt()
    }

    pub fn get_length_sq(self) -> f64 {
        self.x * self.x + self.y * self.y
    }

    pub fn get_manhattan_length(self) -> f64 {
        self.x.abs() + self.y.abs()
    }

    pub fn get_normal(self) -> Vector2D {
        let lensq = self.get_length_sq();
        if lensq > TINY_VALUE {
            let mul = 1.0 / lensq.sqrt();
            return Vector2D::new(self.x * mul, self.y * mul);
        }
        Vector2D::new(0.0, 0.0)
    }

    pub fn get_scaled(self, s: f64) -> Vector2D {
        Vector2D::new(self.x * s, self.y * s)
    }

    pub fn get_fixed_length(self, l: f64) -> Vector2D {
        self.get_normal().get_scaled(l)
    }

    pub fn get_transformed(self, offsetx: f64, offsety: f64, scalex: f64, scaley: f64) -> Vector2D {
        Vector2D::new((self.x + offsetx) * scalex, (self.y + offsety) * scaley)
    }

    pub fn get_inv_transformed(
        self,
        invoffsetx: f64,
        invoffsety: f64,
        invscalex: f64,
        invscaley: f64,
    ) -> Vector2D {
        Vector2D::new(
            (self.x * invscalex) + invoffsetx,
            (self.y * invscaley) + invoffsety,
        )
    }

    pub fn get_rotated(self, theta: f64) -> Vector2D {
        let cos = theta.cos();
        let sin = theta.sin();
        let rx = cos * self.x - sin * self.y;
        let ry = sin * self.x + cos * self.y;
        Vector2D::new(rx, ry)
    }

    pub fn is_finite(self) -> bool {
        !self.x.is_nan() && !self.y.is_nan() && !self.x.is_infinite() && !self.y.is_infinite()
    }
}

impl From<Vector3D> for Vector2D {
    fn from(v: Vector3D) -> Vector2D {
        Vector2D::new(v.x, v.y)
    }
}

impl Add for Vector2D {
    type Output = Vector2D;
    fn add(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self.x + b.x, self.y + b.y)
    }
}

impl Add<f64> for Vector2D {
    type Output = Vector2D;
    fn add(self, b: f64) -> Vector2D {
        Vector2D::new(self.x + b, self.y + b)
    }
}

impl Add<Vector2D> for f64 {
    type Output = Vector2D;
    fn add(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self + b.x, self + b.y)
    }
}

impl Sub for Vector2D {
    type Output = Vector2D;
    fn sub(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self.x - b.x, self.y - b.y)
    }
}

impl Sub<f64> for Vector2D {
    type Output = Vector2D;
    fn sub(self, b: f64) -> Vector2D {
        Vector2D::new(self.x - b, self.y - b)
    }
}

impl Sub<Vector2D> for f64 {
    type Output = Vector2D;
    fn sub(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self - b.x, self - b.y)
    }
}

impl Neg for Vector2D {
    type Output = Vector2D;
    fn neg(self) -> Vector2D {
        Vector2D::new(-self.x, -self.y)
    }
}

impl Mul<f64> for Vector2D {
    type Output = Vector2D;
    fn mul(self, s: f64) -> Vector2D {
        Vector2D::new(self.x * s, self.y * s)
    }
}

impl Mul<Vector2D> for f64 {
    type Output = Vector2D;
    fn mul(self, a: Vector2D) -> Vector2D {
        Vector2D::new(a.x * self, a.y * self)
    }
}

impl Mul for Vector2D {
    type Output = Vector2D;
    fn mul(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self.x * b.x, self.y * b.y)
    }
}

impl Div<f64> for Vector2D {
    type Output = Vector2D;
    fn div(self, s: f64) -> Vector2D {
        Vector2D::new(self.x / s, self.y / s)
    }
}

// C# `double / Vector2D` divides the components by the scalar (preserved quirk).
impl Div<Vector2D> for f64 {
    type Output = Vector2D;
    fn div(self, a: Vector2D) -> Vector2D {
        Vector2D::new(a.x / self, a.y / self)
    }
}

impl Div for Vector2D {
    type Output = Vector2D;
    fn div(self, b: Vector2D) -> Vector2D {
        Vector2D::new(self.x / b.x, self.y / b.y)
    }
}

impl fmt::Display for Vector2D {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}, {}", self.x, self.y)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-12;

    #[test]
    fn add_subtract() {
        let a = Vector2D::new(3.0, 4.0);
        let b = Vector2D::new(1.0, 2.0);
        assert_eq!(Vector2D::new(4.0, 6.0), a + b);
        assert_eq!(Vector2D::new(2.0, 2.0), a - b);
        assert_eq!(Vector2D::new(-3.0, -4.0), -a);
    }

    #[test]
    fn scalar_ops() {
        let a = Vector2D::new(2.0, 4.0);
        assert_eq!(Vector2D::new(4.0, 8.0), a * 2.0);
        assert_eq!(Vector2D::new(4.0, 8.0), 2.0 * a);
        assert_eq!(Vector2D::new(1.0, 2.0), a / 2.0);
    }

    #[test]
    fn dot_product() {
        let a = Vector2D::new(3.0, 4.0);
        let b = Vector2D::new(2.0, 1.0);
        assert_eq!(10.0, Vector2D::dot_product(a, b));
    }

    #[test]
    fn cross_product_preserves_udb_quirk() {
        // UDB's Vector2D.CrossProduct returns (a.y * b.x, a.x * b.y), not a scalar cross.
        let a = Vector2D::new(2.0, 3.0);
        let b = Vector2D::new(5.0, 7.0);
        assert_eq!(Vector2D::new(15.0, 14.0), Vector2D::cross_product(a, b));
    }

    #[test]
    fn length_and_length_sq() {
        let v = Vector2D::new(3.0, 4.0);
        assert!((v.get_length_sq() - 25.0).abs() < EPSILON);
        assert!((v.get_length() - 5.0).abs() < EPSILON);
        assert!((v.get_manhattan_length() - 7.0).abs() < EPSILON);
    }

    #[test]
    fn normalize() {
        let v = Vector2D::new(3.0, 4.0).get_normal();
        assert!((v.get_length() - 1.0).abs() < EPSILON);
        assert!((v.x - 0.6).abs() < EPSILON);
        assert!((v.y - 0.8).abs() < EPSILON);
    }

    #[test]
    fn normalize_zero_is_zero() {
        // UDB returns (0,0) when the vector is effectively zero rather than NaN.
        let v = Vector2D::new(0.0, 0.0).get_normal();
        assert_eq!(0.0, v.x);
        assert_eq!(0.0, v.y);
    }

    #[test]
    fn perpendicular() {
        let v = Vector2D::new(1.0, 0.0).get_perpendicular();
        assert_eq!(Vector2D::new(0.0, 1.0), v);
    }

    #[test]
    fn rotate_pi_over_two() {
        let v = Vector2D::new(1.0, 0.0).get_rotated(crate::angle2d::PIHALF);
        assert!(v.x.abs() < 1e-9);
        assert!((v.y - 1.0).abs() < 1e-9);
    }

    #[test]
    fn reflect() {
        // Reflecting (1, -1) over the X axis normal (0, 1) yields (1, 1).
        let v = Vector2D::new(1.0, -1.0);
        let m = Vector2D::new(0.0, 1.0);
        let r = Vector2D::reflect(v, m);
        assert!((r.x - 1.0).abs() < EPSILON);
        assert!((r.y - 1.0).abs() < EPSILON);
    }

    #[test]
    fn distance_matches_pythagoras() {
        let a = Vector2D::new(0.0, 0.0);
        let b = Vector2D::new(3.0, 4.0);
        assert!((Vector2D::distance(a, b) - 5.0).abs() < EPSILON);
        assert!((Vector2D::distance_sq(a, b) - 25.0).abs() < EPSILON);
        assert!((Vector2D::manhattan_distance(a, b) - 7.0).abs() < EPSILON);
    }

    #[test]
    fn from_angle_zero_points_down_negative_y() {
        // UDB convention: FromAngle(a) = (sin a, -cos a).
        let v = Vector2D::from_angle(0.0);
        assert!(v.x.abs() < EPSILON);
        assert!((v.y - (-1.0)).abs() < EPSILON);
    }

    #[test]
    fn get_angle_is_in_0_to_2pi_range() {
        let angles = [
            Vector2D::new(1.0, 0.0).get_angle(),
            Vector2D::new(0.0, 1.0).get_angle(),
            Vector2D::new(-1.0, 0.0).get_angle(),
            Vector2D::new(0.0, -1.0).get_angle(),
        ];
        for angle in angles {
            assert!((0.0..crate::angle2d::PI2).contains(&angle));
        }
    }

    #[test]
    fn is_finite_catches_nan_and_infinity() {
        assert!(Vector2D::new(1.0, 2.0).is_finite());
        assert!(!Vector2D::new(f64::NAN, 0.0).is_finite());
        assert!(!Vector2D::new(0.0, f64::INFINITY).is_finite());
    }

    #[test]
    fn equality_consistent() {
        let a = Vector2D::new(1.5, -2.25);
        let b = Vector2D::new(1.5, -2.25);
        assert!(a == b);
        assert!(!(a != b));
    }

    #[test]
    fn conversion_to_vector3d() {
        let v = Vector3D::from(Vector2D::new(2.0, 3.0));
        assert_eq!(2.0, v.x);
        assert_eq!(3.0, v.y);
        assert_eq!(0.0, v.z);
    }

    #[test]
    fn get_sign_matches_csharp_math_sign() {
        let v = Vector2D::new(-5.0, 0.0).get_sign();
        assert_eq!(Vector2D::new(-1.0, 0.0), v);
        let v = Vector2D::new(3.0, -0.0).get_sign();
        assert_eq!(Vector2D::new(1.0, 0.0), v);
    }
}
