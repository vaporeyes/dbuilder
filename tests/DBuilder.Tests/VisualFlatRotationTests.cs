// ABOUTME: Verifies UDB-style visual flat rotation field updates for floor and ceiling targets.
// ABOUTME: Covers UDMF gating, angle wrapping, and duplicate surface handling.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class VisualFlatRotationTests
{
    [Fact]
    public void RotateUpdatesFloorAndCeilingRotationFields()
    {
        var sector = new Sector();
        var floor = new VisualHit(VisualHitKind.Floor, 0, new Vector3D(), sector, null, true, 0, 0);
        var ceiling = new VisualHit(VisualHitKind.Ceiling, 0, new Vector3D(), sector, null, false, 0, 0);

        int changed = VisualFlatRotation.Rotate(new[] { floor, ceiling }, 5, isUdmf: true);

        Assert.Equal(2, changed);
        Assert.Equal(5.0, sector.GetFloatField("rotationfloor", 0.0), 1e-9);
        Assert.Equal(5.0, sector.GetFloatField("rotationceiling", 0.0), 1e-9);
    }

    [Fact]
    public void RotateWrapsNegativeAnglesLikeUdb()
    {
        var sector = new Sector();
        var floor = new VisualHit(VisualHitKind.Floor, 0, new Vector3D(), sector, null, true, 0, 0);

        int changed = VisualFlatRotation.Rotate(new[] { floor }, -5, isUdmf: true);

        Assert.Equal(1, changed);
        Assert.Equal(355.0, sector.GetFloatField("rotationfloor", 0.0), 1e-9);
    }

    [Fact]
    public void RotateIgnoresDuplicateSurfacesInOneAction()
    {
        var sector = new Sector();
        var floor = new VisualHit(VisualHitKind.Floor, 0, new Vector3D(), sector, null, true, 0, 0);

        int changed = VisualFlatRotation.Rotate(new[] { floor, floor }, 5, isUdmf: true);

        Assert.Equal(1, changed);
        Assert.Equal(5.0, sector.GetFloatField("rotationfloor", 0.0), 1e-9);
    }

    [Fact]
    public void RotateDoesNothingOutsideUdmf()
    {
        var sector = new Sector();
        var floor = new VisualHit(VisualHitKind.Floor, 0, new Vector3D(), sector, null, true, 0, 0);

        int changed = VisualFlatRotation.Rotate(new[] { floor }, 5, isUdmf: false);

        Assert.Equal(0, changed);
        Assert.False(sector.Fields.ContainsKey("rotationfloor"));
    }
}
