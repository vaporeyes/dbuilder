// ABOUTME: Tests for SidedefTextureAlignment - propagating X and Y offsets along connected walls.
// ABOUTME: Covers straight runs, bidirectional branches, modulo wrap, texture-break stops, and closed loop guards.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefTextureAlignmentTests
{
    // Builds a chain of front-sided one-sided walls along the x-axis from the given x positions.
    private static (MapSet map, List<Linedef> lines) Chain(string tex, params double[] xs)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var verts = new List<Vertex>();
        foreach (var x in xs) verts.Add(map.AddVertex(new Vector2D(x, 0)));
        var lines = new List<Linedef>();
        for (int i = 0; i < verts.Count - 1; i++)
        {
            var l = map.AddLinedef(verts[i], verts[i + 1]);
            var sd = map.AddSidedef(l, true, sector);
            sd.MidTexture = tex;
            lines.Add(l);
        }
        map.BuildIndexes();
        return (map, lines);
    }

    [Fact]
    public void AlignsStraightRun()
    {
        var (_, lines) = Chain("WALL", 0, 64, 160, 224); // lengths 64, 96, 64
        int n = SidedefTextureAlignment.AutoAlignX(lines[0].Front!, 128);
        Assert.Equal(2, n);
        Assert.Equal(0, lines[0].Front!.OffsetX);   // anchor unchanged
        Assert.Equal(64, lines[1].Front!.OffsetX);  // +64
        Assert.Equal(32, lines[2].Front!.OffsetX);  // 64+96 = 160 -> mod 128
    }

    [Fact]
    public void RespectsNonZeroAnchorOffset()
    {
        var (_, lines) = Chain("WALL", 0, 64, 160);
        lines[0].Front!.OffsetX = 10;
        SidedefTextureAlignment.AutoAlignX(lines[0].Front!, 128);
        Assert.Equal(10, lines[0].Front!.OffsetX);
        Assert.Equal(74, lines[1].Front!.OffsetX); // 10 + 64
    }

    [Fact]
    public void AlignsBothDirectionsFromMiddleAnchor()
    {
        var (_, lines) = Chain("WALL", 0, 64, 160, 224);
        lines[1].Front!.OffsetX = 20;

        int n = SidedefTextureAlignment.AutoAlignX(lines[1].Front!, 128);

        Assert.Equal(2, n);
        Assert.Equal(84, lines[0].Front!.OffsetX);  // 20 - 64 -> wrapped
        Assert.Equal(20, lines[1].Front!.OffsetX);  // anchor unchanged
        Assert.Equal(116, lines[2].Front!.OffsetX); // 20 + 96
    }

    [Fact]
    public void AlignsBranchesAtConnectedVertex()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var start = map.AddVertex(new Vector2D(0, 0));
        var junction = map.AddVertex(new Vector2D(64, 0));
        var north = map.AddVertex(new Vector2D(64, 64));
        var east = map.AddVertex(new Vector2D(128, 0));
        var anchor = map.AddLinedef(start, junction);
        var branchA = map.AddLinedef(junction, north);
        var branchB = map.AddLinedef(junction, east);
        map.AddSidedef(anchor, true, sector).MidTexture = "WALL";
        map.AddSidedef(branchA, true, sector).MidTexture = "WALL";
        map.AddSidedef(branchB, true, sector).MidTexture = "WALL";
        map.BuildIndexes();

        int n = SidedefTextureAlignment.AutoAlignX(anchor.Front!, 128);

        Assert.Equal(2, n);
        Assert.Equal(64, branchA.Front!.OffsetX);
        Assert.Equal(64, branchB.Front!.OffsetX);
    }

    [Fact]
    public void StopsAtTextureChange()
    {
        var (_, lines) = Chain("WALL", 0, 64, 160, 224);
        lines[2].Front!.MidTexture = "OTHER"; // breaks the chain after line 1
        int n = SidedefTextureAlignment.AutoAlignX(lines[0].Front!, 128);
        Assert.Equal(1, n);
        Assert.Equal(64, lines[1].Front!.OffsetX);
        Assert.Equal(0, lines[2].Front!.OffsetX); // untouched
    }

    [Fact]
    public void ClosedLoopTerminatesAndCountsEachOnce()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)),
            map.AddVertex(new Vector2D(64, 64)), map.AddVertex(new Vector2D(64, 0)),
        };
        var lines = new List<Linedef>();
        for (int i = 0; i < 4; i++)
        {
            var l = map.AddLinedef(v[i], v[(i + 1) % 4]);
            map.AddSidedef(l, true, sector).MidTexture = "WALL";
            lines.Add(l);
        }
        map.BuildIndexes();

        int n = SidedefTextureAlignment.AutoAlignX(lines[0].Front!, 64);
        Assert.Equal(3, n); // the other three sides; the start is revisited and stops the walk
        Assert.Equal(0, lines[1].Front!.OffsetX); // 64 mod 64
    }

    [Fact]
    public void ZeroTextureWidthDoesNotThrow()
    {
        var (_, lines) = Chain("WALL", 0, 64, 128);
        var ex = Record.Exception(() => SidedefTextureAlignment.AutoAlignX(lines[0].Front!, 0));
        Assert.Null(ex);
    }

    // Builds a chain where each wall belongs to its own sector with a specified ceiling height.
    private static (MapSet map, List<Linedef> lines) HeightChain(string tex, params int[] ceilings)
    {
        var map = new MapSet();
        var verts = new List<Vertex>();
        for (int i = 0; i <= ceilings.Length; i++) verts.Add(map.AddVertex(new Vector2D(i * 64, 0)));
        var lines = new List<Linedef>();
        for (int i = 0; i < ceilings.Length; i++)
        {
            var sector = map.AddSector();
            sector.CeilHeight = ceilings[i];
            var l = map.AddLinedef(verts[i], verts[i + 1]);
            map.AddSidedef(l, true, sector).MidTexture = tex;
            lines.Add(l);
        }
        map.BuildIndexes();
        return (map, lines);
    }

    [Fact]
    public void AlignsYByCeilingDeltas()
    {
        var (_, lines) = HeightChain("WALL", 128, 96, 160);
        int n = SidedefTextureAlignment.AutoAlignY(lines[0].Front!, 128);
        Assert.Equal(2, n);
        Assert.Equal(0, lines[0].Front!.OffsetY);   // anchor
        Assert.Equal(32, lines[1].Front!.OffsetY);  // +(128-96)
        Assert.Equal(96, lines[2].Front!.OffsetY);  // 32+(96-160) = -32 -> mod 128
    }

    [Fact]
    public void EqualCeilingsLeaveYOffsetUnchanged()
    {
        var (_, lines) = HeightChain("WALL", 100, 100, 100);
        SidedefTextureAlignment.AutoAlignY(lines[0].Front!, 128);
        Assert.Equal(0, lines[1].Front!.OffsetY);
        Assert.Equal(0, lines[2].Front!.OffsetY);
    }

    [Fact]
    public void AlignsYBothDirectionsFromMiddleAnchor()
    {
        var (_, lines) = HeightChain("WALL", 100, 80, 120);
        lines[1].Front!.OffsetY = 5;

        int n = SidedefTextureAlignment.AutoAlignY(lines[1].Front!, 128);

        Assert.Equal(2, n);
        Assert.Equal(113, lines[0].Front!.OffsetY); // 5 + 80 - 100 -> wrapped
        Assert.Equal(5, lines[1].Front!.OffsetY);   // anchor unchanged
        Assert.Equal(93, lines[2].Front!.OffsetY);  // 5 + 80 - 120 -> wrapped
    }
}
