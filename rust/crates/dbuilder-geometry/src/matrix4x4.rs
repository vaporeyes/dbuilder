// ABOUTME: Minimal row-major 4x4 float matrix standing in for System.Numerics.Matrix4x4.
// ABOUTME: Only what Vector3D::transform needs; fields stay f32 to mirror C# precision.

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Matrix4x4 {
    pub m11: f32,
    pub m12: f32,
    pub m13: f32,
    pub m14: f32,
    pub m21: f32,
    pub m22: f32,
    pub m23: f32,
    pub m24: f32,
    pub m31: f32,
    pub m32: f32,
    pub m33: f32,
    pub m34: f32,
    pub m41: f32,
    pub m42: f32,
    pub m43: f32,
    pub m44: f32,
}

impl Matrix4x4 {
    pub fn identity() -> Matrix4x4 {
        Matrix4x4 {
            m11: 1.0,
            m12: 0.0,
            m13: 0.0,
            m14: 0.0,
            m21: 0.0,
            m22: 1.0,
            m23: 0.0,
            m24: 0.0,
            m31: 0.0,
            m32: 0.0,
            m33: 1.0,
            m34: 0.0,
            m41: 0.0,
            m42: 0.0,
            m43: 0.0,
            m44: 1.0,
        }
    }

    // System.Numerics convention: translation lives in M41/M42/M43 (row 4).
    pub fn create_translation(x: f32, y: f32, z: f32) -> Matrix4x4 {
        let mut m = Matrix4x4::identity();
        m.m41 = x;
        m.m42 = y;
        m.m43 = z;
        m
    }
}
