// ABOUTME: Verifies UDB-compatible visual pickable contract and pick result fields.
// ABOUTME: Locks down direct field assignment and ref-based accurate pick calls.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class VisualPickResultTests
{
    [Fact]
    public void VisualPickResultStoresUdbNamedFields()
    {
        var pickable = new PickableStub();
        var hitpos = new Vector3D(1, 2, 3);

        var result = new VisualPickResult
        {
            picked = pickable,
            u_ray = 0.5,
            hitpos = hitpos,
        };

        Assert.Same(pickable, result.picked);
        Assert.Equal(0.5, result.u_ray);
        Assert.Equal(hitpos, result.hitpos);
    }

    [Fact]
    public void VisualPickResultDefaultFieldsMatchStructDefaults()
    {
        var result = default(VisualPickResult);

        Assert.Null(result.picked);
        Assert.Equal(0, result.u_ray);
        Assert.Equal(default, result.hitpos);
    }

    [Fact]
    public void VisualPickableContractUsesSelectionAndRefDistance()
    {
        IVisualPickable pickable = new PickableStub
        {
            Selected = true,
            AccurateDistance = 12.25,
        };

        double uRay = 3;

        Assert.True(pickable.Selected);
        Assert.True(pickable.PickFastReject(new Vector3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(1, 0, 0)));
        Assert.True(pickable.PickAccurate(new Vector3D(0, 0, 0), new Vector3D(2, 0, 0), new Vector3D(1, 0, 0), ref uRay));
        Assert.Equal(12.25, uRay);
    }

    private sealed class PickableStub : IVisualPickable
    {
        public bool Selected { get; set; }

        public double AccurateDistance { get; set; }

        public bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir) => true;

        public bool PickAccurate(Vector3D from, Vector3D to, Vector3D dir, ref double u_ray)
        {
            u_ray = AccurateDistance;
            return true;
        }
    }
}
