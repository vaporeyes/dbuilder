// ABOUTME: 3D double-precision vector ported from DBuilder Vector3D.cs (UDB Vector3D.cs).
// ABOUTME: Transform reads row-major translation slots M41..M43 like UDB; matrix stays f32.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use std::fmt;
use std::ops::{Add, Div, Mul, Neg, Sub};

use crate::angle2d;
use crate::matrix4x4::Matrix4x4;
use crate::vector2d::{Vector2D, TINY_VALUE};

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Vector3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

impl Vector3D {
    pub fn new(x: f64, y: f64, z: f64) -> Vector3D {
        Vector3D { x, y, z }
    }

    //mxd
    pub fn from_vector2d_with_z(v: Vector2D, z: f64) -> Vector3D {
        Vector3D { x: v.x, y: v.y, z }
    }

    pub fn cross_product(a: Vector3D, b: Vector3D) -> Vector3D {
        Vector3D {
            x: a.y * b.z - a.z * b.y,
            y: a.z * b.x - a.x * b.z,
            z: a.x * b.y - a.y * b.x,
        }
    }

    pub fn dot_product(a: Vector3D, b: Vector3D) -> f64 {
        a.x * b.x + a.y * b.y + a.z * b.z
    }

    // This reflects the vector v over mirror m. Note that mirror m must be normalized.
    pub fn reflect(v: Vector3D, m: Vector3D) -> Vector3D {
        let dp = Vector3D::dot_product(v, m);
        Vector3D {
            x: -v.x + 2.0 * m.x * dp,
            y: -v.y + 2.0 * m.y * dp,
            z: -v.z + 2.0 * m.z * dp,
        }
    }

    pub fn reversed(v: Vector3D) -> Vector3D {
        Vector3D::new(-v.x, -v.y, -v.z)
    }

    pub fn from_angle_xy(angle: f64) -> Vector3D {
        Vector3D::new(angle.sin(), -angle.cos(), 0.0)
    }

    pub fn from_angle_xy_with_length(angle: f64, length: f64) -> Vector3D {
        Vector3D::from_angle_xy(angle) * length
    }

    pub fn from_angle_xyz(anglexy: f64, anglez: f64) -> Vector3D {
        let ax = anglexy.sin() * anglez.cos();
        let ay = -anglexy.cos() * anglez.cos();
        let az = anglez.sin();
        Vector3D::new(ax, ay, az)
    }

    //mxd. Uses UDB's original element access (M11..M44 are read as M_row_col element values).
    pub fn transform(v: Vector3D, m: Matrix4x4) -> Vector3D {
        Vector3D {
            x: m.m11 as f64 * v.x + m.m21 as f64 * v.y + m.m31 as f64 * v.z + m.m41 as f64,
            y: m.m12 as f64 * v.x + m.m22 as f64 * v.y + m.m32 as f64 * v.z + m.m42 as f64,
            z: m.m13 as f64 * v.x + m.m23 as f64 * v.y + m.m33 as f64 * v.z + m.m43 as f64,
        }
    }

    //mxd
    pub fn transform_xyz(x: f64, y: f64, z: f64, m: Matrix4x4) -> Vector3D {
        Vector3D::transform(Vector3D::new(x, y, z), m)
    }

    pub fn get_angle_xy(self) -> f64 {
        -f64::atan2(-self.y, self.x) + angle2d::PIHALF
    }

    pub fn get_angle_z(self) -> f64 {
        let xy = Vector2D::new(self.x, self.y);
        f64::atan2(xy.get_length(), self.z) + angle2d::PIHALF
    }

    pub fn get_length(self) -> f64 {
        (self.x * self.x + self.y * self.y + self.z * self.z).sqrt()
    }

    pub fn get_length_sq(self) -> f64 {
        self.x * self.x + self.y * self.y + self.z * self.z
    }

    pub fn get_manhattan_length(self) -> f64 {
        self.x.abs() + self.y.abs() + self.z.abs()
    }

    pub fn get_normal(self) -> Vector3D {
        let lensq = self.get_length_sq();
        if lensq > TINY_VALUE {
            let mul = 1.0 / lensq.sqrt();
            return Vector3D::new(self.x * mul, self.y * mul, self.z * mul);
        }
        Vector3D::new(0.0, 0.0, 0.0)
    }

    pub fn get_scaled(self, s: f64) -> Vector3D {
        Vector3D::new(self.x * s, self.y * s, self.z * s)
    }

    pub fn get_fixed_length(self, l: f64) -> Vector3D {
        self.get_normal().get_scaled(l)
    }

    pub fn is_normalized(self) -> bool {
        (self.get_length_sq() - 1.0).abs() < 0.0001f32 as f64
    }

    pub fn is_finite(self) -> bool {
        !self.x.is_nan()
            && !self.y.is_nan()
            && !self.z.is_nan()
            && !self.x.is_infinite()
            && !self.y.is_infinite()
            && !self.z.is_infinite()
    }
}

impl From<Vector2D> for Vector3D {
    fn from(v: Vector2D) -> Vector3D {
        Vector3D {
            x: v.x,
            y: v.y,
            z: 0.0,
        }
    }
}

impl Add for Vector3D {
    type Output = Vector3D;
    fn add(self, b: Vector3D) -> Vector3D {
        Vector3D::new(self.x + b.x, self.y + b.y, self.z + b.z)
    }
}

impl Add<f64> for Vector3D {
    type Output = Vector3D;
    fn add(self, b: f64) -> Vector3D {
        Vector3D::new(self.x + b, self.y + b, self.z + b)
    }
}

impl Add<Vector3D> for f64 {
    type Output = Vector3D;
    fn add(self, a: Vector3D) -> Vector3D {
        Vector3D::new(a.x + self, a.y + self, a.z + self)
    }
}

impl Sub for Vector3D {
    type Output = Vector3D;
    fn sub(self, b: Vector3D) -> Vector3D {
        Vector3D::new(self.x - b.x, self.y - b.y, self.z - b.z)
    }
}

impl Sub<f64> for Vector3D {
    type Output = Vector3D;
    fn sub(self, b: f64) -> Vector3D {
        Vector3D::new(self.x - b, self.y - b, self.z - b)
    }
}

impl Sub<Vector3D> for f64 {
    type Output = Vector3D;
    fn sub(self, b: Vector3D) -> Vector3D {
        Vector3D::new(self - b.x, self - b.y, self - b.z)
    }
}

impl Neg for Vector3D {
    type Output = Vector3D;
    fn neg(self) -> Vector3D {
        Vector3D::new(-self.x, -self.y, -self.z)
    }
}

impl Mul<f64> for Vector3D {
    type Output = Vector3D;
    fn mul(self, s: f64) -> Vector3D {
        Vector3D::new(self.x * s, self.y * s, self.z * s)
    }
}

impl Mul<Vector3D> for f64 {
    type Output = Vector3D;
    fn mul(self, a: Vector3D) -> Vector3D {
        Vector3D::new(a.x * self, a.y * self, a.z * self)
    }
}

impl Mul for Vector3D {
    type Output = Vector3D;
    fn mul(self, b: Vector3D) -> Vector3D {
        Vector3D::new(self.x * b.x, self.y * b.y, self.z * b.z)
    }
}

impl Div<f64> for Vector3D {
    type Output = Vector3D;
    fn div(self, s: f64) -> Vector3D {
        Vector3D::new(self.x / s, self.y / s, self.z / s)
    }
}

// C# `double / Vector3D` divides the components by the scalar (preserved quirk).
impl Div<Vector3D> for f64 {
    type Output = Vector3D;
    fn div(self, a: Vector3D) -> Vector3D {
        Vector3D::new(a.x / self, a.y / self, a.z / self)
    }
}

impl Div for Vector3D {
    type Output = Vector3D;
    fn div(self, b: Vector3D) -> Vector3D {
        Vector3D::new(self.x / b.x, self.y / b.y, self.z / b.z)
    }
}

impl fmt::Display for Vector3D {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}, {}, {}", self.x, self.y, self.z)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-12;

    #[test]
    fn cross_product_matches_right_hand_rule() {
        let x = Vector3D::new(1.0, 0.0, 0.0);
        let y = Vector3D::new(0.0, 1.0, 0.0);
        let z = Vector3D::cross_product(x, y);
        assert_eq!(Vector3D::new(0.0, 0.0, 1.0), z);
    }

    #[test]
    fn dot_product() {
        let a = Vector3D::new(1.0, 2.0, 3.0);
        let b = Vector3D::new(4.0, -5.0, 6.0);
        assert_eq!(12.0, Vector3D::dot_product(a, b));
    }

    #[test]
    fn length_and_normal() {
        let v = Vector3D::new(0.0, 3.0, 4.0);
        assert!((v.get_length_sq() - 25.0).abs() < EPSILON);
        assert!((v.get_length() - 5.0).abs() < EPSILON);
        let n = v.get_normal();
        assert!((n.get_length() - 1.0).abs() < EPSILON);
        assert!(n.is_normalized());
    }

    #[test]
    fn normalize_zero_is_zero() {
        let v = Vector3D::new(0.0, 0.0, 0.0).get_normal();
        assert_eq!(Vector3D::new(0.0, 0.0, 0.0), v);
    }

    #[test]
    fn transform_by_identity_is_identity() {
        let v = Vector3D::new(1.0, 2.0, 3.0);
        let t = Vector3D::transform(v, Matrix4x4::identity());
        assert!((t.x - 1.0).abs() < EPSILON);
        assert!((t.y - 2.0).abs() < EPSILON);
        assert!((t.z - 3.0).abs() < EPSILON);
    }

    #[test]
    fn transform_by_translation() {
        // System.Numerics convention: translation lives in M41/M42/M43 (row 4, last row).
        // UDB's Transform reads M41/M42/M43 as the additive offset, which matches.
        let v = Vector3D::new(1.0, 2.0, 3.0);
        let m = Matrix4x4::create_translation(10.0, 20.0, 30.0);
        let t = Vector3D::transform(v, m);
        assert!((t.x - 11.0).abs() < EPSILON);
        assert!((t.y - 22.0).abs() < EPSILON);
        assert!((t.z - 33.0).abs() < EPSILON);
    }

    #[test]
    fn is_finite_catches_nan_and_infinity() {
        assert!(Vector3D::new(1.0, 2.0, 3.0).is_finite());
        assert!(!Vector3D::new(f64::NAN, 0.0, 0.0).is_finite());
        assert!(!Vector3D::new(0.0, 0.0, f64::NEG_INFINITY).is_finite());
    }

    #[test]
    fn equality_consistent() {
        let a = Vector3D::new(1.0, 2.0, 3.0);
        let b = Vector3D::new(1.0, 2.0, 3.0);
        assert!(a == b);
    }

    #[test]
    fn conversion_to_vector2d_drops_z() {
        let v2 = Vector2D::from(Vector3D::new(5.0, 6.0, 7.0));
        assert_eq!(5.0, v2.x);
        assert_eq!(6.0, v2.y);
    }
}
