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
    public void RayMissingEverythingReturnsNull()
    {
        var (map, _, _) = Room();
        // Outside the room looking away from it.
        var hit = VisualPicking.Raycast(map, new Vector3D(-50, -50, 40), new Vector3D(-1, 0, 0));
        Assert.Null(hit);
    }
}
