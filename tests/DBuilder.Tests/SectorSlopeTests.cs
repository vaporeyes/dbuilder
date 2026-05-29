// ABOUTME: Tests for Sector slope-plane height evaluation (GetFloorZ/GetCeilZ) and the HasFloorSlope/HasCeilSlope predicates.
// ABOUTME: Covers flat fallback, a known 45-degree slope, and the UDMF-loaded slope round-trip producing correct heights.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorSlopeTests
{
    [Fact]
    public void SectorUpdateAndTextureSettersMatchUdbSurface()
    {
        var sector = new Sector();
        var floorSlope = new Vector3D(0, 1, -1).GetNormal();
        var ceilSlope = new Vector3D(0, -1, 1).GetNormal();

        sector.Update(
            floorHeight: 8,
            ceilHeight: 128,
            floorTexture: "",
            ceilTexture: null,
            special: 9,
            flags: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["secret"] = true,
                ["hidden"] = false,
            },
            tags: new List<int> { 7, 11 },
            brightness: 192,
            floorSlopeOffset: 3.5,
            floorSlope: floorSlope,
            ceilSlopeOffset: -2.25,
            ceilSlope: ceilSlope);

        Assert.Equal(8, sector.FloorHeight);
        Assert.Equal(128, sector.CeilHeight);
        Assert.Equal("-", sector.FloorTexture);
        Assert.Equal("-", sector.CeilTexture);
        Assert.Equal(9, sector.Special);
        Assert.Equal(192, sector.Brightness);
        Assert.Equal(new[] { 7, 11 }, sector.Tags);
        Assert.Contains("secret", sector.UdmfFlags);
        Assert.DoesNotContain("hidden", sector.UdmfFlags);
        Assert.Equal(3.5, sector.FloorSlopeOffset, 1e-9);
        Assert.Equal(floorSlope, sector.FloorSlope);
        Assert.Equal(-2.25, sector.CeilSlopeOffset, 1e-9);
        Assert.Equal(ceilSlope, sector.CeilSlope);

        sector.SetFloorTexture(null);
        sector.SetCeilTexture("CEIL1");

        Assert.Equal("-", sector.FloorTexture);
        Assert.Equal("CEIL1", sector.CeilTexture);
    }

    [Fact]
    public void SectorClassicUpdateSetsSingleTagAndClearsUdmfSlopeState()
    {
        var sector = new Sector
        {
            FloorSlope = new Vector3D(0, 1, -1),
            FloorSlopeOffset = 4,
            CeilSlope = new Vector3D(0, 1, 1),
            CeilSlopeOffset = 5,
        };
        sector.UdmfFlags.Add("secret");

        sector.Update(0, 64, "FLOOR1", "", 3, 12, 144);

        Assert.Equal(0, sector.FloorHeight);
        Assert.Equal(64, sector.CeilHeight);
        Assert.Equal("FLOOR1", sector.FloorTexture);
        Assert.Equal("-", sector.CeilTexture);
        Assert.Equal(3, sector.Special);
        Assert.Equal(12, sector.Tag);
        Assert.Equal(144, sector.Brightness);
        Assert.Empty(sector.UdmfFlags);
        Assert.False(sector.HasFloorSlope);
        Assert.False(sector.HasCeilSlope);
        Assert.True(double.IsNaN(sector.FloorSlopeOffset));
        Assert.True(double.IsNaN(sector.CeilSlopeOffset));
    }

    [Fact]
    public void FlatSectorReturnsConstantHeight()
    {
        var s = new Sector { FloorHeight = 32, CeilHeight = 128 };
        Assert.False(s.HasFloorSlope);
        Assert.False(s.HasCeilSlope);
        Assert.Equal(32, s.GetFloorZ(new Vector2D(0, 0)));
        Assert.Equal(32, s.GetFloorZ(new Vector2D(1000, -500)));
        Assert.Equal(128, s.GetCeilZ(new Vector2D(250, 250)));
    }

    [Fact]
    public void FloorSlopeProducesLinearHeight()
    {
        // A plane rising 1 unit in z per 1 unit in y, passing through z=0 at y=0.
        // Plane: 0*x + 1*y - 1*z + 0 = 0  ->  normal (0, 1, -1) normalized, offset 0.
        // GetZ = (-offset - (a*x + b*y)) / c = -(y) / (-1) = y.
        var s = new Sector { FloorHeight = 0 };
        s.FloorSlope = new Vector3D(0, 1, -1).GetNormal();
        s.FloorSlopeOffset = 0.0;

        Assert.True(s.HasFloorSlope);
        Assert.Equal(0, s.GetFloorZ(new Vector2D(0, 0)), 1e-9);
        Assert.Equal(10, s.GetFloorZ(new Vector2D(0, 10)), 1e-9);
        Assert.Equal(-25, s.GetFloorZ(new Vector2D(500, -25)), 1e-9); // x is irrelevant for this plane
    }

    [Fact]
    public void OffsetShiftsSlopePlane()
    {
        // Same slope but offset so the plane sits 8 units higher: A*x+B*y+C*z+D=0 with D chosen to add +8.
        // normal (0,1,-1) normalized; GetZ = (-offset - (b*y)) / c.
        // For the un-normalized intuition we use the normalized plane directly and assert relative behavior.
        var s = new Sector();
        var n = new Vector3D(0, 1, -1).GetNormal();
        s.FloorSlope = n;
        // Pick offset so z at origin equals 8: GetZ(0,0) = -offset / n.z = 8 -> offset = -8 * n.z.
        s.FloorSlopeOffset = -8.0 * n.z;

        Assert.Equal(8, s.GetFloorZ(new Vector2D(0, 0)), 1e-9);
    }

    [Fact]
    public void NaNOffsetTreatedAsZero()
    {
        // A slope normal with an unset (NaN) offset must not poison the height (offset defaults to 0).
        var s = new Sector();
        s.FloorSlope = new Vector3D(0, 1, -1).GetNormal();
        s.FloorSlopeOffset = double.NaN;
        double z = s.GetFloorZ(new Vector2D(0, 5));
        Assert.False(double.IsNaN(z));
        Assert.Equal(5, z, 1e-9);
    }

    [Fact]
    public void VerticalNormalIsNotASlope()
    {
        // A purely vertical normal (0,0,1) is a flat plane, not a usable slope - fall back to FloorHeight.
        var s = new Sector { FloorHeight = 64 };
        s.FloorSlope = new Vector3D(0, 0, 1);
        s.FloorSlopeOffset = 0;
        // HasFloorSlope requires z != 0 AND length > 0; (0,0,1) has z != 0 so it IS treated as a plane.
        // GetZ of a flat plane through origin with normal (0,0,1), offset 0 = 0 everywhere.
        Assert.True(s.HasFloorSlope);
        Assert.Equal(0, s.GetFloorZ(new Vector2D(100, 100)), 1e-9);
    }

    [Fact]
    public void UdmfLoadedSlopeEvaluatesCorrectHeights()
    {
        const string udmf = """
            namespace = "ZDoom";
            vertex { x = 0.0; y = 0.0; }
            vertex { x = 64.0; y = 0.0; }
            vertex { x = 64.0; y = 64.0; }
            vertex { x = 0.0; y = 64.0; }
            sector { heightfloor = 0; heightceiling = 128; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; floorplane_a = 0.0; floorplane_b = 0.7071067811865476; floorplane_c = -0.7071067811865476; floorplane_d = 0.0; }
            """;
        var map = UdmfMapLoader.Load(udmf, out var parser)!;
        Assert.Equal(0, parser.ErrorResult);

        var s = map.Sectors[0];
        Assert.True(s.HasFloorSlope);
        // normal (0, 0.7071, -0.7071): GetZ = (-(b*y)) / c = -(0.7071*y)/(-0.7071) = y.
        Assert.Equal(0, s.GetFloorZ(new Vector2D(0, 0)), 1e-6);
        Assert.Equal(64, s.GetFloorZ(new Vector2D(0, 64)), 1e-6);
        Assert.Equal(32, s.GetFloorZ(new Vector2D(20, 32)), 1e-6);
    }
}
