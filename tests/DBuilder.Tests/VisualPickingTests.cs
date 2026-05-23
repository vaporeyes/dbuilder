// ABOUTME: Tests for VisualPicking.Raycast - hitting sector floor/ceiling planes and sidedef walls from a ray.
// ABOUTME: Uses a single 100x100 room (floor 0, ceiling 128) and checks hit kind, distance, sector and line.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualPickingTests
{
    private static (MapSet map, Sector s, Linedef[] lines) Room()
    {
        var map = new MapSet();
        var s = map.AddSector();
        s.FloorHeight = 0;
        s.CeilHeight = 128;
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 100)),
            map.AddVertex(new Vector2D(100, 100)), map.AddVertex(new Vector2D(100, 0)),
        };
        var lines = new Linedef[4];
        for (int i = 0; i < 4; i++)
        {
            lines[i] = map.AddLinedef(v[i], v[(i + 1) % 4]);
            map.AddSidedef(lines[i], true, s);
        }
        map.BuildIndexes();
        return (map, s, lines);
    }

    [Fact]
    public void LookingDownHitsFloor()
    {
        var (map, s, _) = Room();
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 40), new Vector3D(0, 0, -1));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Floor, hit!.Kind);
        Assert.Same(s, hit.Sector);
        Assert.Equal(40, hit.Distance, 6);
        Assert.Equal(0, hit.Point.z, 6);
    }

    [Fact]
    public void LookingUpHitsCeiling()
    {
        var (map, s, _) = Room();
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 40), new Vector3D(0, 0, 1));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Ceiling, hit!.Kind);
        Assert.Same(s, hit.Sector);
        Assert.Equal(88, hit.Distance, 6); // 128 - 40
    }

    [Fact]
    public void LookingSidewaysHitsWall()
    {
        var (map, _, lines) = Room();
        // From the center looking +x, the right wall (x=100) is 50 units away.
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 40), new Vector3D(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Wall, hit!.Kind);
        Assert.Same(lines[2], hit.Line); // the (100,100)-(100,0) wall
        Assert.Equal(50, hit.Distance, 6);
        Assert.Equal(100, hit.Point.x, 6);
    }

    [Fact]
    public void NearestSurfaceWinsWhenAngledTowardFloor()
    {
        var (map, _, _) = Room();
        // Steeply downward toward the floor: floor (close) should beat the far wall.
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 40), new Vector3D(0.2, 0, -1));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Floor, hit!.Kind);
    }

    [Fact]
    public void OneSidedWallIsMiddlePart()
    {
        var (map, _, _) = Room();
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 40), new Vector3D(1, 0, 0));
        Assert.Equal(WallPart.Middle, hit!.Part);
    }

    [Fact]
    public void TwoSidedWallReportsLowerAndUpperParts()
    {
        // A single shared two-sided wall: front floor 0/ceil 128, back floor 32/ceil 96.
        var map = new MapSet();
        var fs = map.AddSector(); fs.FloorHeight = 0; fs.CeilHeight = 128;
        var bs = map.AddSector(); bs.FloorHeight = 32; bs.CeilHeight = 96;
        var a = map.AddVertex(new Vector2D(50, 0));
        var b = map.AddVertex(new Vector2D(50, 100));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, true, fs);
        map.AddSidedef(l, false, bs);
        map.BuildIndexes();

        var low = VisualPicking.Raycast(map, new Vector3D(20, 50, 16), new Vector3D(1, 0, 0));
        Assert.Equal(VisualHitKind.Wall, low!.Kind);
        Assert.Equal(WallPart.Lower, low.Part); // z=16 is within the lower step [0,32]

        var high = VisualPicking.Raycast(map, new Vector3D(20, 50, 110), new Vector3D(1, 0, 0));
        Assert.Equal(WallPart.Upper, high!.Part); // z=110 is within the upper step [96,128]
    }

    [Fact]
    public void SlopedFloorIsHitAtItsSlopedHeight()
    {
        var (map, s, _) = Room();
        // Floor sloping z = x: normal (-1,0,1)/sqrt2, offset 0 -> GetFloorZ(x,y) = x.
        double k = 1.0 / System.Math.Sqrt(2);
        s.FloorSlope = new Vector3D(-k, 0, k);
        s.FloorSlopeOffset = 0;
        Assert.True(s.HasFloorSlope);
        Assert.Equal(50, s.GetFloorZ(new Vector2D(50, 50)), 6);

        // Straight down from (50,50,200): a flat floor (height 0) would hit z=0 at distance 200;
        // the slope puts the floor at z=50, so the hit is at z=50, distance 150.
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 50, 200), new Vector3D(0, 0, -1));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Floor, hit!.Kind);
        Assert.Equal(50, hit.Point.z, 4);
        Assert.Equal(150, hit.Distance, 4);
    }

    [Fact]
    public void PicksThingBoxWhenSizerProvided()
    {
        var (map, _, _) = Room();
        var thing = map.AddThing(new Vector2D(50, 50), 1);
        map.BuildIndexes();

        // From inside the room at (50,90,28) looking -y, the thing box (r=16) front face is at y=66, dist 24.
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 90, 28), new Vector3D(0, -1, 0),
            _ => (16.0, 56.0));
        Assert.NotNull(hit);
        Assert.Equal(VisualHitKind.Thing, hit!.Kind);
        Assert.Same(thing, hit.Thing);
        Assert.Equal(24, hit.Distance, 4);
    }

    [Fact]
    public void ThingsIgnoredWithoutSizer()
    {
        var (map, _, _) = Room();
        map.AddThing(new Vector2D(50, 50), 1);
        map.BuildIndexes();
        // No sizer -> things are not picked; the back wall is hit instead.
        var hit = VisualPicking.Raycast(map, new Vector3D(50, 90, 28), new Vector3D(0, -1, 0));
        Assert.NotNull(hit);
        Assert.NotEqual(VisualHitKind.Thing, hit!.Kind);
    }

    [Fact]
    public void WallSpanFollowsSlopedFloor()
    {
        var (map, s, lines) = Room();
        // Floor sloping z = x, so at the right wall (x=100) the floor is at z=100; the wall exists z=100..128.
        double k = 1.0 / System.Math.Sqrt(2);
        s.FloorSlope = new Vector3D(-k, 0, k);
        s.FloorSlopeOffset = 0;

        // A horizontal ray at z=110 hits the right wall (within its 100..128 span).
        var high = VisualPicking.Raycast(map, new Vector3D(50, 50, 110), new Vector3D(1, 0, 0));
        Assert.NotNull(high);
        Assert.Equal(VisualHitKind.Wall, high!.Kind);
        Assert.Same(lines[2], high.Line);
        Assert.Equal(50, high.Distance, 4);

        // A ray at z=20 is below the sloped floor at x=100, so it does not hit that wall; it hits the floor.
        var low = VisualPicking.Raycast(map, new Vector3D(10, 50, 20), new Vector3D(1, 0, 0));
        Assert.NotNull(low);
        Assert.Equal(VisualHitKind.Floor, low!.Kind);
    }

    [Fact]
    public void RayMissingEverythingReturnsNull()
    {
        var (map, _, _) = Room();
        // Outside the room looking away from it.
        var hit = VisualPicking.Raycast(map, new Vector3D(-50, -50, 40), new Vector3D(-1, 0, 0));
        Assert.Null(hit);
    }
}
