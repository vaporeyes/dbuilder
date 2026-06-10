// ABOUTME: Infinite 3D plane ported from DBuilder Plane.cs (UDB Plane.cs).
// ABOUTME: Behavior preserved 1:1; the C# ref-out intersection becomes an Option.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::line2d::Line2D;
use crate::vector2d::Vector2D;
use crate::vector3d::Vector3D;

//
// Plane definition:
// A * x + B * y + C * z + D = 0
//
// A, B, C is the normal
// D is the offset along the normal (negative)
//
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Plane {
    normal: Vector3D,
    offset: f64,
}

impl Plane {
    pub fn new(normal: Vector3D, offset: f64) -> Plane {
        debug_assert!(
            normal.is_normalized(),
            "Attempt to create a plane with a vector that is not normalized!"
        );
        Plane { normal, offset }
    }

    pub fn from_normal_and_position(normal: Vector3D, position: Vector3D) -> Plane {
        debug_assert!(
            normal.is_normalized(),
            "Attempt to create a plane with a vector that is not normalized!"
        );
        Plane {
            normal,
            offset: -Vector3D::dot_product(normal, position),
        }
    }

    pub fn from_points(p1: Vector3D, p2: Vector3D, p3: Vector3D, up: bool) -> Plane {
        let mut normal = Vector3D::cross_product(p2 - p1, p3 - p1).get_normal();

        if (up && (normal.z < 0.0)) || (!up && (normal.z > 0.0)) {
            normal = -normal;
        }

        Plane {
            normal,
            offset: -Vector3D::dot_product(normal, p3),
        }
    }

    //mxd
    pub fn from_center_angles(center: Vector3D, anglexy: f64, anglez: f64, up: bool) -> Plane {
        let point = Vector2D::new(
            center.x + anglexy.cos() * anglez.sin(),
            center.y + anglexy.sin() * anglez.sin(),
        );
        let perpendicular = Line2D::new(Vector2D::from(center), point).get_perpendicular();

        let p2 = Vector3D::new(
            point.x + perpendicular.x,
            point.y + perpendicular.y,
            center.z + anglez.cos(),
        );
        let p3 = Vector3D::new(
            point.x - perpendicular.x,
            point.y - perpendicular.y,
            center.z + anglez.cos(),
        );

        let mut normal = Vector3D::cross_product(p2 - center, p3 - center).get_normal();

        if (up && (normal.z < 0.0)) || (!up && (normal.z > 0.0)) {
            normal = -normal;
        }

        Plane {
            normal,
            offset: -Vector3D::dot_product(normal, p3),
        }
    }

    pub fn normal(&self) -> Vector3D {
        self.normal
    }

    pub fn offset(&self) -> f64 {
        self.offset
    }

    pub fn set_offset(&mut self, offset: f64) {
        self.offset = offset;
    }

    pub fn a(&self) -> f64 {
        self.normal.x
    }

    pub fn b(&self) -> f64 {
        self.normal.y
    }

    pub fn c(&self) -> f64 {
        self.normal.z
    }

    pub fn d(&self) -> f64 {
        self.offset
    }

    /// Intersection with a line.
    /// See <http://local.wasp.uwa.edu.au/~pbourke/geometry/planeline/>
    /// Returns Some(u_ray) where the C# version returns true and sets `ref u_ray`.
    pub fn get_intersection(&self, from: Vector3D, to: Vector3D) -> Option<f64> {
        let w = Vector3D::dot_product(self.normal, from - to);
        if w != 0.0 {
            let v = Vector3D::dot_product(self.normal, from);
            return Some((self.offset + v) / w);
        }
        None
    }

    /// Smallest signed distance to the plane.
    /// Positive means the point lies on the front of the plane, negative means behind.
    /// See <http://mathworld.wolfram.com/Point-PlaneDistance.html>
    pub fn distance(&self, p: Vector3D) -> f64 {
        Vector3D::dot_product(self.normal, p) + self.offset
    }

    /// Closest point on the plane to a given point.
    pub fn closest_on_plane(&self, p: Vector3D) -> Vector3D {
        p - self.normal * self.distance(p)
    }

    /// Z on the plane at (X, Y).
    pub fn get_z(&self, pos: Vector2D) -> f64 {
        (-self.offset - Vector2D::dot_product(Vector2D::from(self.normal), pos)) / self.normal.z
    }

    /// Z on the plane at (X, Y).
    pub fn get_z_at(&self, x: f64, y: f64) -> f64 {
        (-self.offset - (self.normal.x * x + self.normal.y * y)) / self.normal.z
    }

    pub fn get_inverted(&self) -> Plane {
        Plane {
            normal: -self.normal,
            offset: -self.offset,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-9;

    #[test]
    fn distance_from_origin_plane_is_height() {
        // Plane: z = 0, normal = (0,0,1), offset = 0. Distance to (x, y, h) == h.
        let p = Plane::new(Vector3D::new(0.0, 0.0, 1.0), 0.0);
        assert!((p.distance(Vector3D::new(0.0, 0.0, 5.0)) - 5.0).abs() < EPSILON);
        assert!((p.distance(Vector3D::new(0.0, 0.0, -3.0)) - (-3.0)).abs() < EPSILON);
    }

    #[test]
    fn get_z_on_horizontal_plane_is_constant() {
        // Plane offset by 10 along +Z: equation z = 10.
        let p = Plane::from_normal_and_position(
            Vector3D::new(0.0, 0.0, 1.0),
            Vector3D::new(0.0, 0.0, 10.0),
        );
        assert!((p.get_z(Vector2D::new(123.0, -456.0)) - 10.0).abs() < EPSILON);
    }

    #[test]
    fn three_point_constructor_makes_plane_through_points() {
        let p1 = Vector3D::new(0.0, 0.0, 5.0);
        let p2 = Vector3D::new(1.0, 0.0, 5.0);
        let p3 = Vector3D::new(0.0, 1.0, 5.0);
        let p = Plane::from_points(p1, p2, p3, true);
        assert!(p.distance(p1).abs() < 1e-6);
        assert!(p.distance(p2).abs() < 1e-6);
        assert!(p.distance(p3).abs() < 1e-6);
    }

    #[test]
    fn get_intersection_hits_horizontal_plane() {
        // Plane z=0, ray from (0,0,5) to (0,0,-5): u should be 0.5.
        let p = Plane::new(Vector3D::new(0.0, 0.0, 1.0), 0.0);
        let u = p
            .get_intersection(Vector3D::new(0.0, 0.0, 5.0), Vector3D::new(0.0, 0.0, -5.0))
            .expect("ray crosses plane");
        assert!((u - 0.5).abs() < 1e-9);
    }

    #[test]
    fn get_intersection_misses_parallel_ray() {
        let p = Plane::new(Vector3D::new(0.0, 0.0, 1.0), 0.0);
        // Ray parallel to the plane at z=5 will never hit.
        assert!(p
            .get_intersection(Vector3D::new(0.0, 0.0, 5.0), Vector3D::new(1.0, 0.0, 5.0))
            .is_none());
    }

    #[test]
    fn get_inverted_flips_normal_and_offset() {
        let p = Plane::new(Vector3D::new(0.0, 0.0, 1.0), 10.0);
        let inv = p.get_inverted();
        assert_eq!(-p.a(), inv.a());
        assert_eq!(-p.b(), inv.b());
        assert_eq!(-p.c(), inv.c());
        assert_eq!(-p.d(), inv.d());
    }
}
