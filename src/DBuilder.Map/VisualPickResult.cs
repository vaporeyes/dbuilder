// ABOUTME: Defines UDB-compatible visual picking result and pickable object contracts.
// ABOUTME: Provides source-compatible field and method names for visual mode ports.

using DBuilder.Geometry;

namespace DBuilder.Map;

public interface IVisualPickable
{
    bool Selected { get; set; }

    bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir);

    bool PickAccurate(Vector3D from, Vector3D to, Vector3D dir, ref double u_ray);
}

public struct VisualPickResult
{
    public IVisualPickable? picked;
    public double u_ray;
    public Vector3D hitpos;
}
