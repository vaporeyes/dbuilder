// ABOUTME: Easing and interpolation utilities ported from DBuilder InterpolationTools.cs.
// ABOUTME: Color overloads operate on u32 ARGB; Mode is a closed enum with an i32 fallback.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::angle2d;
use crate::math_round;

// Discriminant order mirrors the C# enum (LINEAR = 0, EASE_IN_OUT_SINE = 1, ...).
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum Mode {
    #[default]
    Linear,
    EaseInOutSine,
    EaseInSine,
    EaseOutSine,
}

impl Mode {
    // Mirrors C# NormalizeMode: undefined persisted values fall back to LINEAR.
    pub fn from_i32(value: i32) -> Mode {
        match value {
            1 => Mode::EaseInOutSine,
            2 => Mode::EaseInSine,
            3 => Mode::EaseOutSine,
            _ => Mode::Linear,
        }
    }
}

pub fn interpolate(val1: f64, val2: f64, delta: f64, mode: Mode) -> f64 {
    match mode {
        Mode::Linear => linear(val1, val2, delta),
        Mode::EaseInSine => ease_in_sine(val1, val2, delta),
        Mode::EaseOutSine => ease_out_sine(val1, val2, delta),
        Mode::EaseInOutSine => ease_in_out_sine(val1, val2, delta),
    }
}

// Based on Robert Penner's original easing equations (http://www.robertpenner.com/easing/)
pub fn linear(val1: f64, val2: f64, delta: f64) -> f64 {
    delta * val2 + (1.0 - delta) * val1
}

/// Sinusoidal easing in: accelerating from zero velocity.
pub fn ease_in_sine(val1: f64, val2: f64, delta: f64) -> f64 {
    let f_val1 = val1;
    let f_val2 = val2 - f_val1;
    -f_val2 * (delta * angle2d::PIHALF).cos() + f_val2 + f_val1
}

/// Sinusoidal easing out: decelerating to zero velocity.
pub fn ease_out_sine(val1: f64, val2: f64, delta: f64) -> f64 {
    (val2 - val1) * (delta * angle2d::PIHALF).sin() + val1
}

/// Sinusoidal easing in/out: acceleration until halfway, then deceleration.
pub fn ease_in_out_sine(val1: f64, val2: f64, delta: f64) -> f64 {
    -(val2 - val1) / 2.0 * ((angle2d::PI * delta).cos() - 1.0) + val1
}

//mxd
pub fn interpolate_color(c1: u32, c2: u32, delta: f64) -> u32 {
    let invdelta = 1.0 - delta;
    let (a1, r1, g1, b1) = split_argb(c1);
    let (a2, r2, g2, b2) = split_argb(c2);
    let a = (a1 as f64 * invdelta + a2 as f64 * delta) as u8;
    let r = (r1 as f64 * invdelta + r2 as f64 * delta) as u8;
    let g = (g1 as f64 * invdelta + g2 as f64 * delta) as u8;
    let b = (b1 as f64 * invdelta + b2 as f64 * delta) as u8;
    combine_argb(a, r, g, b)
}

//mxd
pub fn interpolate_color_with_mode(c1: u32, c2: u32, delta: f64, mode: Mode) -> u32 {
    let (a1, r1, g1, b1) = split_argb(c1);
    let (a2, r2, g2, b2) = split_argb(c2);
    let a = math_round(interpolate(a1 as f64, a2 as f64, delta, mode)) as u8;
    let r = math_round(interpolate(r1 as f64, r2 as f64, delta, mode)) as u8;
    let g = math_round(interpolate(g1 as f64, g2 as f64, delta, mode)) as u8;
    let b = math_round(interpolate(b1 as f64, b2 as f64, delta, mode)) as u8;
    combine_argb(a, r, g, b)
}

fn split_argb(c: u32) -> (u8, u8, u8, u8) {
    (
        ((c >> 24) & 0xff) as u8,
        ((c >> 16) & 0xff) as u8,
        ((c >> 8) & 0xff) as u8,
        (c & 0xff) as u8,
    )
}

fn combine_argb(a: u8, r: u8, g: u8, b: u8) -> u32 {
    ((a as u32) << 24) | ((r as u32) << 16) | ((g as u32) << 8) | (b as u32)
}

#[cfg(test)]
mod tests {
    use super::*;

    const EPSILON: f64 = 1e-9;

    #[test]
    fn linear_hits_endpoints() {
        assert!(linear(0.0, 10.0, 0.0).abs() < EPSILON);
        assert!((linear(0.0, 10.0, 1.0) - 10.0).abs() < EPSILON);
        assert!((linear(0.0, 10.0, 0.5) - 5.0).abs() < EPSILON);
    }

    #[test]
    fn all_modes_preserve_endpoints() {
        for mode in [
            Mode::Linear,
            Mode::EaseInSine,
            Mode::EaseOutSine,
            Mode::EaseInOutSine,
        ] {
            assert!(interpolate(0.0, 10.0, 0.0, mode).abs() < 1e-6);
            assert!((interpolate(0.0, 10.0, 1.0, mode) - 10.0).abs() < 1e-6);
        }
    }

    #[test]
    fn from_i32_falls_back_to_linear_for_unknown_modes() {
        assert_eq!(Mode::Linear, Mode::from_i32(999));
        assert_eq!(Mode::EaseInOutSine, Mode::from_i32(1));
        assert_eq!(Mode::EaseInSine, Mode::from_i32(2));
        assert_eq!(Mode::EaseOutSine, Mode::from_i32(3));
    }

    #[test]
    fn interpolate_color_midpoint_averages_argb() {
        // Halfway between 0x00000000 and 0xffffffff should be ~(127, 127, 127, 127).
        let mid = interpolate_color(0x00000000, 0xffffffff, 0.5);
        let (a, r, g, b) = split_argb(mid);
        for ch in [a, r, g, b] {
            assert!((126..=128).contains(&ch));
        }
    }

    #[test]
    fn interpolate_color_with_mode_rounds_channels() {
        let mid = interpolate_color_with_mode(0xff000000, 0xffffffff, 0.5, Mode::Linear);
        let (a, r, g, b) = split_argb(mid);
        assert_eq!(255, a);
        for ch in [r, g, b] {
            assert!((127..=128).contains(&ch));
        }
    }
}
