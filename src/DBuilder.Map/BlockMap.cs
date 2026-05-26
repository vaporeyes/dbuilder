// ABOUTME: Uniform-grid spatial index over a MapSet for fast nearest/range queries on linedefs, things and vertices.
// ABOUTME: Linedefs are bucketed conservatively by their bounding-cell rectangle; queries expand outward ring by ring.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

/// <summary>
/// A fixed-cell-size spatial acceleration grid built once from a MapSet snapshot. Rebuild it after the map
/// geometry changes. Points (vertices, things) bucket into a single cell; linedefs bucket into every cell
/// their bounding box overlaps, so range/nearest queries never miss a candidate.
/// </summary>
public sealed class BlockMap
{
    private readonly double originX, originY, blockSize;
    private readonly int cols, rows;
    private readonly List<Linedef>[] lineCells;
    private readonly List<Thing>[] thingCells;
    private readonly List<Vertex>[] vertCells;

    public double BlockSize => blockSize;
    public int Columns => cols;
    public int Rows => rows;
    public double OriginX => originX;
    public double OriginY => originY;

    /// <summary>Number of linedefs overlapping the given block (0 when out of range).</summary>
    public int LinedefCountAt(int col, int row)
        => col < 0 || row < 0 || col >= cols || row >= rows ? 0 : lineCells[Index(col, row)].Count;

    /// <summary>Returns the unclamped block coordinates for a world position.</summary>
    public (int Col, int Row) GetCellCoordinates(Vector2D pos)
        => ((int)Math.Floor((pos.x - originX) / blockSize), (int)Math.Floor((pos.y - originY) / blockSize));

    /// <summary>Returns the center point of a block in map coordinates.</summary>
    public Vector2D GetCellCenter(int col, int row)
        => new(originX + (col + 0.5) * blockSize, originY + (row + 0.5) * blockSize);

    public bool IsCellInRange(int col, int row)
        => col >= 0 && row >= 0 && col < cols && row < rows;

    public bool IsInRange(Vector2D pos)
    {
        var (col, row) = GetCellCoordinates(pos);
        return IsCellInRange(col, row);
    }

    public IReadOnlyList<Linedef> GetLinedefsAt(int col, int row)
        => IsCellInRange(col, row) ? lineCells[Index(col, row)] : Array.Empty<Linedef>();

    public IReadOnlyList<Thing> GetThingsAt(int col, int row)
        => IsCellInRange(col, row) ? thingCells[Index(col, row)] : Array.Empty<Thing>();

    public IReadOnlyList<Vertex> GetVerticesAt(int col, int row)
        => IsCellInRange(col, row) ? vertCells[Index(col, row)] : Array.Empty<Vertex>();

    public BlockMap(MapSet map, double blockSize = 128.0)
    {
        this.blockSize = blockSize > 0 ? blockSize : 128.0;

        var (minX, minY, maxX, maxY) = map.Bounds();
        // Things can sit outside the vertex hull, so widen the extents to include them.
        foreach (var t in map.Things)
        {
            if (t.Position.x < minX) minX = t.Position.x;
            if (t.Position.y < minY) minY = t.Position.y;
            if (t.Position.x > maxX) maxX = t.Position.x;
            if (t.Position.y > maxY) maxY = t.Position.y;
        }

        originX = minX;
        originY = minY;
        cols = Math.Max(1, (int)Math.Floor((maxX - minX) / this.blockSize) + 1);
        rows = Math.Max(1, (int)Math.Floor((maxY - minY) / this.blockSize) + 1);

        int cells = cols * rows;
        lineCells = new List<Linedef>[cells];
        thingCells = new List<Thing>[cells];
        vertCells = new List<Vertex>[cells];
        for (int i = 0; i < cells; i++)
        {
            lineCells[i] = new List<Linedef>();
            thingCells[i] = new List<Thing>();
            vertCells[i] = new List<Vertex>();
        }

        foreach (var v in map.Vertices)
            vertCells[Index(CellX(v.Position.x), CellY(v.Position.y))].Add(v);
        foreach (var t in map.Things)
            thingCells[Index(CellX(t.Position.x), CellY(t.Position.y))].Add(t);
        foreach (var l in map.Linedefs)
        {
            var a = l.Start.Position;
            var b = l.End.Position;
            int cx0 = CellX(Math.Min(a.x, b.x)), cx1 = CellX(Math.Max(a.x, b.x));
            int cy0 = CellY(Math.Min(a.y, b.y)), cy1 = CellY(Math.Max(a.y, b.y));
            for (int cy = cy0; cy <= cy1; cy++)
                for (int cx = cx0; cx <= cx1; cx++)
                    lineCells[Index(cx, cy)].Add(l);
        }
    }

    private int CellX(double x) => Math.Clamp((int)Math.Floor((x - originX) / blockSize), 0, cols - 1);
    private int CellY(double y) => Math.Clamp((int)Math.Floor((y - originY) / blockSize), 0, rows - 1);
    private int Index(int cx, int cy) => cy * cols + cx;

    /// <summary>Nearest linedef to <paramref name="pos"/> (bounded segment distance) within <paramref name="maxRange"/>, or null.</summary>
    public Linedef? NearestLinedef(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(lineCells, pos, maxRange, static (l, p) => SegmentDistanceSq(l, p));

    /// <summary>
    /// Returns the sector containing <paramref name="pos"/> using the same nearest-linedef side rule as
    /// <see cref="MapSet.GetSectorAt"/>, accelerated by this blockmap's linedef grid.
    /// </summary>
    public Sector? GetSectorAt(Vector2D pos)
    {
        var line = NearestLinedef(pos);
        if (line == null) return null;
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) < 0;
        return (front ? line.Front : line.Back)?.Sector;
    }

    /// <summary>Nearest thing to <paramref name="pos"/> within <paramref name="maxRange"/>, or null.</summary>
    public Thing? NearestThing(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(thingCells, pos, maxRange, static (t, p) => DistSq(t.Position, p));

    /// <summary>Nearest vertex to <paramref name="pos"/> within <paramref name="maxRange"/>, or null.</summary>
    public Vertex? NearestVertex(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(vertCells, pos, maxRange, static (v, p) => DistSq(v.Position, p));

    /// <summary>All distinct linedefs whose bounding box overlaps the square of half-size <paramref name="range"/> around pos.</summary>
    public IReadOnlyCollection<Linedef> GetLinedefsNear(Vector2D pos, double range)
        => Gather(lineCells, pos, range);

    /// <summary>All distinct things in cells overlapping the square of half-size <paramref name="range"/> around pos.</summary>
    public IReadOnlyCollection<Thing> GetThingsNear(Vector2D pos, double range)
        => Gather(thingCells, pos, range);

    /// <summary>All distinct vertices in cells overlapping the square of half-size <paramref name="range"/> around pos.</summary>
    public IReadOnlyCollection<Vertex> GetVerticesNear(Vector2D pos, double range)
        => Gather(vertCells, pos, range);

    private HashSet<T> Gather<T>(List<T>[] cells, Vector2D pos, double range) where T : class
    {
        var set = new HashSet<T>();
        int cx0 = CellX(pos.x - range), cx1 = CellX(pos.x + range);
        int cy0 = CellY(pos.y - range), cy1 = CellY(pos.y + range);
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                foreach (var item in cells[Index(cx, cy)]) set.Add(item);
        return set;
    }

    // Expanding-ring nearest search: process the center cell, then successive Chebyshev shells, stopping once
    // the best distance is within the guaranteed minimum distance to any unvisited shell.
    private T? Nearest<T>(List<T>[] cells, Vector2D pos, double maxRange, Func<T, Vector2D, double> distSq) where T : class
    {
        int ccx = (int)Math.Floor((pos.x - originX) / blockSize);
        int ccy = (int)Math.Floor((pos.y - originY) / blockSize);
        double bestSq = maxRange == double.MaxValue ? double.MaxValue : maxRange * maxRange;
        T? best = null;
        int maxRing = cols + rows;

        for (int r = 0; r <= maxRing; r++)
        {
            // Ring r's nearest possible geometry is at least (r-1)*blockSize away; bail if that exceeds maxRange.
            if (best == null && maxRange != double.MaxValue && (double)(r - 1) * blockSize > maxRange) break;

            for (int cy = ccy - r; cy <= ccy + r; cy++)
            {
                if (cy < 0 || cy >= rows) continue;
                for (int cx = ccx - r; cx <= ccx + r; cx++)
                {
                    if (cx < 0 || cx >= cols) continue;
                    if (Math.Max(Math.Abs(cx - ccx), Math.Abs(cy - ccy)) != r) continue; // shell only
                    foreach (var item in cells[Index(cx, cy)])
                    {
                        double d = distSq(item, pos);
                        if (d < bestSq) { bestSq = d; best = item; }
                    }
                }
            }

            // Any cell in ring r+1 is at least r*blockSize away, so a closer hit is impossible past here.
            if (best != null && Math.Sqrt(bestSq) <= (double)r * blockSize) break;
        }
        return best;
    }

    private static double DistSq(Vector2D a, Vector2D b)
    {
        double dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    // Bounded point-to-segment squared distance, guarding against zero-length (degenerate) linedefs.
    private static double SegmentDistanceSq(Linedef l, Vector2D pos)
    {
        var a = l.Start.Position;
        var b = l.End.Position;
        double lenSq = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (lenSq < 1e-12) return DistSq(a, pos);
        return Line2D.GetDistanceToLineSq(a, b, pos, bounded: true);
    }
}
