// ABOUTME: Rust port of DBuilder.Geometry, itself ported from UDB Source/Core/Geometry.
// ABOUTME: Pure double-precision math; module names mirror the C# source files 1:1.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

// Ports preserve C# control flow verbatim; manual clamps and range checks keep
// the original NaN and boundary semantics.
#![allow(clippy::manual_clamp, clippy::manual_range_contains)]

pub mod angle2d;
pub mod curve_tools;
pub mod drawn_vertex;
pub mod interpolation_tools;
pub mod label_position_info;
pub mod line2d;
pub mod line3d;
pub mod matrix4x4;
pub mod plane;
pub mod projected_frustum2d;
pub mod vector2d;
pub mod vector3d;

pub use curve_tools::{Curve, CurveSegment, CurveSegmentType};
pub use drawn_vertex::DrawnVertex;
pub use interpolation_tools::Mode;
pub use label_position_info::LabelPositionInfo;
pub use line2d::{Line2D, RectF};
pub use line3d::Line3D;
pub use matrix4x4::Matrix4x4;
pub use plane::Plane;
pub use projected_frustum2d::ProjectedFrustum2D;
pub use vector2d::Vector2D;
pub use vector3d::Vector3D;

// C# Math.Round uses banker's rounding (round half to even); Rust f64::round does not.
pub(crate) fn math_round(value: f64) -> f64 {
    value.round_ties_even()
}

// Mirrors C# Math.Round(value, 4) for the angle ranges UDB feeds through it.
pub(crate) fn math_round_4(value: f64) -> f64 {
    (value * 10000.0).round_ties_even() / 10000.0
}

// Mirrors C# Math.Sign for in-range doubles. C# throws on NaN; this returns 0 instead,
// which no UDB call site relies on.
pub(crate) fn math_sign(value: f64) -> f64 {
    if value > 0.0 {
        1.0
    } else if value < 0.0 {
        -1.0
    } else {
        0.0
    }
}
