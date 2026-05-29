// ABOUTME: Uniform-grid spatial index over a MapSet for fast nearest/range queries on linedefs, sectors, things and vertices.
// ABOUTME: Linedefs are bucketed through crossed cells; queries expand outward ring by ring.

using System;
using System.Collections.Generic;
using System.Drawing;
using DBuilder.Geometry;

namespace DBuilder.Map;

public readonly record struct BlockMapCell(
    IReadOnlyList<Linedef> Lines,
    IReadOnlyList<Thing> Things,
    IReadOnlyList<Sector> Sectors,
    IReadOnlyList<Vertex> Vertices);

/// <summary>
/// A fixed-cell-size spatial acceleration grid built once from a MapSet snapshot. Rebuild it after the map
/// geometry changes. Points (vertices, things) bucket into a single cell; sectors bucket by bounds; linedefs
/// bucket into every cell their segment crosses, matching UDB's block traversal behavior.
/// </summary>
public sealed class BlockMap
{
    private readonly double originX, originY, rangeRight, rangeBottom, blockSize;
    private readonly int cols, rows;
    private readonly List<Linedef>[] lineCells;
    private readonly List<Sector>[] sectorCells;
    private readonly List<Thing>[] thingCells;
    private readonly List<Vertex>[] vertCells;

    private readonly record struct BlockMapBounds(double MinX, double MinY, double MaxX, double MaxY);

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
        => pos.x >= originX && pos.x < rangeRight && pos.y >= originY && pos.y < rangeBottom;

    public IReadOnlyList<Linedef> GetLinedefsAt(int col, int row)
        => IsCellInRange(col, row) ? lineCells[Index(col, row)] : Array.Empty<Linedef>();

    public IReadOnlyList<Sector> GetSectorsAt(int col, int row)
        => IsCellInRange(col, row) ? sectorCells[Index(col, row)] : Array.Empty<Sector>();

    public IReadOnlyList<Thing> GetThingsAt(int col, int row)
        => IsCellInRange(col, row) ? thingCells[Index(col, row)] : Array.Empty<Thing>();

    public IReadOnlyList<Vertex> GetVerticesAt(int col, int row)
        => IsCellInRange(col, row) ? vertCells[Index(col, row)] : Array.Empty<Vertex>();

    /// <summary>Returns a view of the block containing <paramref name="pos"/>, or null when outside range.</summary>
    public BlockMapCell? GetBlockAt(Vector2D pos)
    {
        var (col, row) = GetCellCoordinates(pos);
        return IsCellInRange(col, row) ? GetCell(col, row) : null;
    }

    /// <summary>Clears all indexed elements while preserving the fixed blockmap range.</summary>
    public void Clear()
    {
        for (int i = 0; i < lineCells.Length; i++)
        {
            lineCells[i].Clear();
            sectorCells[i].Clear();
            thingCells[i].Clear();
            vertCells[i].Clear();
        }
    }

    /// <summary>Returns cropped cell coordinates covered by the square range, matching UDB's GetSquareRange.</summary>
    public IReadOnlyList<(int Col, int Row)> GetCellRange(double left, double top, double width, double height)
    {
        int cx0 = CellX(left);
        int cy0 = CellY(top);
        int cx1 = CellX(left + width);
        int cy1 = CellY(top + height);
        var cells = new List<(int Col, int Row)>((cx1 - cx0 + 1) * (cy1 - cy0 + 1));

        for (int cx = cx0; cx <= cx1; cx++)
            for (int cy = cy0; cy <= cy1; cy++)
                cells.Add((cx, cy));

        return cells;
    }

    /// <summary>Returns the cell coordinates crossed by a line segment, matching UDB's GetLineBlocks traversal.</summary>
    public IReadOnlyList<(int Col, int Row)> GetLineCellCoordinates(Vector2D start, Vector2D end)
        => LineCellCoordinates(start, end).ToArray();

    public BlockMap(RectangleF range, double blockSize = 128.0)
        : this(new BlockMapBounds(range.Left, range.Top, range.Right, range.Bottom), blockSize)
    {
    }

    public BlockMap(double left, double top, double width, double height, double blockSize = 128.0)
        : this(new BlockMapBounds(left, top, left + width, top + height), blockSize)
    {
    }

    public BlockMap(MapSet map, double blockSize = 128.0)
        : this(GetMapBounds(map), blockSize)
    {
        AddSectors(map.Sectors);
        AddVertices(map.Vertices);
        AddThings(map.Things);
        AddLinedefs(map.Linedefs);
    }

    private BlockMap(BlockMapBounds bounds, double blockSize)
    {
        this.blockSize = blockSize > 0 ? blockSize : 128.0;

        originX = Math.Min(bounds.MinX, bounds.MaxX);
        originY = Math.Min(bounds.MinY, bounds.MaxY);
        double maxX = Math.Max(bounds.MinX, bounds.MaxX);
        double maxY = Math.Max(bounds.MinY, bounds.MaxY);
        rangeRight = maxX;
        rangeBottom = maxY;
        cols = Math.Max(1, (int)Math.Floor((maxX - originX) / this.blockSize) + 1);
        rows = Math.Max(1, (int)Math.Floor((maxY - originY) / this.blockSize) + 1);

        int cells = cols * rows;
        lineCells = new List<Linedef>[cells];
        sectorCells = new List<Sector>[cells];
        thingCells = new List<Thing>[cells];
        vertCells = new List<Vertex>[cells];
        for (int i = 0; i < cells; i++)
        {
            lineCells[i] = new List<Linedef>();
            sectorCells[i] = new List<Sector>();
            thingCells[i] = new List<Thing>();
            vertCells[i] = new List<Vertex>();
        }

    }

    private static BlockMapBounds GetMapBounds(MapSet map)
    {
        var (minX, minY, maxX, maxY) = map.Bounds();
        // Things can sit outside the vertex hull, so widen the extents to include them.
        foreach (var t in map.Things)
        {
            if (t.Position.x < minX) minX = t.Position.x;
            if (t.Position.y < minY) minY = t.Position.y;
            if (t.Position.x > maxX) maxX = t.Position.x;
            if (t.Position.y > maxY) maxY = t.Position.y;
        }

        return new BlockMapBounds(minX, minY, Math.BitIncrement(maxX), Math.BitIncrement(maxY));
    }

    private int CellX(double x) => Math.Clamp((int)Math.Floor((x - originX) / blockSize), 0, cols - 1);
    private int CellY(double y) => Math.Clamp((int)Math.Floor((y - originY) / blockSize), 0, rows - 1);
    private int Index(int cx, int cy) => cy * cols + cx;
    private BlockMapCell GetCell(int col, int row)
    {
        int index = Index(col, row);
        return new BlockMapCell(lineCells[index], thingCells[index], sectorCells[index], vertCells[index]);
    }

    private IEnumerable<(int Col, int Row)> LineCellCoordinates(Vector2D start, Vector2D end)
    {
        int cx = CellX(start.x);
        int cy = CellY(start.y);
        int endX = CellX(end.x);
        int endY = CellY(end.y);

        yield return (cx, cy);
        if (cx == endX && cy == endY) yield break;

        double dx = end.x - start.x;
        double dy = end.y - start.y;
        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        double tMaxX = stepX == 0 ? double.PositiveInfinity : FirstGridT(start.x, dx, cx, stepX, originX);
        double tMaxY = stepY == 0 ? double.PositiveInfinity : FirstGridT(start.y, dy, cy, stepY, originY);
        double tDeltaX = stepX == 0 ? double.PositiveInfinity : blockSize / Math.Abs(dx);
        double tDeltaY = stepY == 0 ? double.PositiveInfinity : blockSize / Math.Abs(dy);
        int guard = cols + rows + 2;

        while ((cx != endX || cy != endY) && guard-- > 0)
        {
            if (tMaxX < tMaxY)
            {
                cx += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                cy += stepY;
                tMaxY += tDeltaY;
            }

            if (IsCellInRange(cx, cy)) yield return (cx, cy);
        }
    }

    private double FirstGridT(double start, double delta, int cell, int step, double origin)
    {
        double boundary = origin + (step > 0 ? cell + 1 : cell) * blockSize;
        return (boundary - start) / delta;
    }

    /// <summary>Nearest linedef to <paramref name="pos"/> (bounded segment distance) within <paramref name="maxRange"/>, or null.</summary>
    public Linedef? NearestLinedef(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(lineCells, pos, maxRange, static (l, p) => SegmentDistanceSq(l, p));

    /// <summary>Adds a set of linedefs to the fixed blockmap range.</summary>
    public void AddLinedefs(IEnumerable<Linedef> lines)
    {
        foreach (var line in lines) AddLinedef(line);
    }

    /// <summary>Adds a linedef to every crossed block in the fixed blockmap range.</summary>
    public void AddLinedef(Linedef line)
    {
        if (!BoxIntersectsRange(
            Math.Min(line.Start.Position.x, line.End.Position.x),
            Math.Min(line.Start.Position.y, line.End.Position.y),
            Math.Max(line.Start.Position.x, line.End.Position.x),
            Math.Max(line.Start.Position.y, line.End.Position.y)))
        {
            return;
        }

        foreach (var (col, row) in LineCellCoordinates(line.Start.Position, line.End.Position))
            lineCells[Index(col, row)].Add(line);
    }

    /// <summary>
    /// Finds the nearest linedef within a bounded range using UDB's candidate block selection rule.
    /// Small ranges test the center and four corner blocks; larger ranges test the full square block range.
    /// </summary>
    public Linedef? NearestLinedefRange(Vector2D pos, double maxRange)
    {
        if (maxRange < 0) return null;

        double bestSq = maxRange * maxRange;
        Linedef? best = null;
        var processed = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);

        foreach (var (col, row) in LinedefRangeCandidateCells(pos, maxRange))
        {
            foreach (var line in GetLinedefsAt(col, row))
            {
                if (!processed.Add(line)) continue;

                double distance = SegmentDistanceSq(line, pos);
                if (distance < bestSq)
                {
                    best = line;
                    bestSq = distance;
                }
            }
        }

        return best;
    }

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

    /// <summary>
    /// Returns the sector that contains a linedef's start, midpoint, and end, or null when it crosses a boundary.
    /// </summary>
    public Sector? GetSectorContaining(Linedef line)
    {
        var start = GetSectorAt(line.Start.Position);
        if (start == null) return null;
        if (!ReferenceEquals(start, GetSectorAt(line.GetCenterPoint()))) return null;
        if (!ReferenceEquals(start, GetSectorAt(line.End.Position))) return null;
        return start;
    }

    /// <summary>Nearest thing to <paramref name="pos"/> within <paramref name="maxRange"/>, or null.</summary>
    public Thing? NearestThing(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(thingCells, pos, maxRange, static (t, p) => t.DistanceToSq(p));

    /// <summary>Adds a set of things to the fixed blockmap range.</summary>
    public void AddThings(IEnumerable<Thing> things)
    {
        foreach (var thing in things) AddThing(thing);
    }

    /// <summary>Adds a thing to its containing block when it is inside the fixed blockmap range.</summary>
    public void AddThing(Thing thing)
    {
        var (col, row) = GetCellCoordinates(thing.Position);
        if (IsCellInRange(col, row)) thingCells[Index(col, row)].Add(thing);
    }

    /// <summary>Nearest vertex to <paramref name="pos"/> within <paramref name="maxRange"/>, or null.</summary>
    public Vertex? NearestVertex(Vector2D pos, double maxRange = double.MaxValue)
        => Nearest(vertCells, pos, maxRange, static (v, p) => DistSq(v.Position, p));

    /// <summary>Adds a set of vertices to the fixed blockmap range.</summary>
    public void AddVertices(IEnumerable<Vertex> vertices)
    {
        foreach (var vertex in vertices) AddVertex(vertex);
    }

    /// <summary>Adds a vertex to its containing block when it is inside the fixed blockmap range.</summary>
    public void AddVertex(Vertex vertex)
    {
        var (col, row) = GetCellCoordinates(vertex.Position);
        if (IsCellInRange(col, row)) vertCells[Index(col, row)].Add(vertex);
    }

    /// <summary>All distinct linedefs in cells overlapped by the square of half-size <paramref name="range"/> around pos.</summary>
    public IReadOnlyCollection<Linedef> GetLinedefsNear(Vector2D pos, double range)
        => Gather(lineCells, pos, range);

    /// <summary>All distinct sectors in cells overlapped by the square of half-size <paramref name="range"/> around pos.</summary>
    public IReadOnlyCollection<Sector> GetSectorsNear(Vector2D pos, double range)
        => Gather(sectorCells, pos, range);

    /// <summary>Adds a set of sectors to the fixed blockmap range.</summary>
    public void AddSectors(IEnumerable<Sector> sectors)
    {
        foreach (var sector in sectors) AddSector(sector);
    }

    /// <summary>Adds a sector to every block overlapped by its bounds in the fixed blockmap range.</summary>
    public void AddSector(Sector sector)
    {
        if (!TryGetSectorBounds(sector, out double minX, out double minY, out double maxX, out double maxY)) return;
        if (!BoxIntersectsRange(minX, minY, maxX, maxY)) return;

        int cx0 = CellX(minX);
        int cy0 = CellY(minY);
        int cx1 = CellX(maxX);
        int cy1 = CellY(maxY);
        for (int cx = cx0; cx <= cx1; cx++)
            for (int cy = cy0; cy <= cy1; cy++)
                sectorCells[Index(cx, cy)].Add(sector);
    }

    /// <summary>Returns sectors in the point's block whose traced polygon contains <paramref name="pos"/>.</summary>
    public IReadOnlyCollection<Sector> GetContainingSectors(Vector2D pos)
    {
        var (col, row) = GetCellCoordinates(pos);
        if (!IsCellInRange(col, row)) return Array.Empty<Sector>();

        var sectors = new List<Sector>();
        foreach (var sector in GetSectorsAt(col, row))
            if (SectorContainsPoint(sector, pos)) sectors.Add(sector);
        return sectors;
    }

    /// <summary>
    /// Returns the containing sector for a point using UDB's blockmap sector candidate behavior.
    /// Multiple containing sectors are disambiguated by the nearest candidate linedef side.
    /// </summary>
    public Sector? GetContainingSector(Vector2D pos)
    {
        var sectors = GetContainingSectors(pos);
        if (sectors.Count == 0) return null;
        if (sectors.Count == 1) return sectors.First();

        var candidateLines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        foreach (var sector in sectors)
            foreach (var side in sector.Sidedefs)
                candidateLines.Add(side.Line);

        Linedef? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var line in candidateLines)
        {
            double distance = SegmentDistanceSq(line, pos);
            if (distance >= nearestDistance) continue;

            nearest = line;
            nearestDistance = distance;
        }

        if (nearest == null) return null;

        bool front = Line2D.GetSideOfLine(nearest.Start.Position, nearest.End.Position, pos) <= 0;
        return (front ? nearest.Front : nearest.Back)?.Sector;
    }

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

    private IEnumerable<(int Col, int Row)> LinedefRangeCandidateCells(Vector2D pos, double maxRange)
    {
        if (maxRange <= blockSize)
        {
            var cells = new (int Col, int Row)[]
            {
                GetCellCoordinates(pos),
                GetCellCoordinates(new Vector2D(pos.x + maxRange, pos.y + maxRange)),
                GetCellCoordinates(new Vector2D(pos.x + maxRange, pos.y - maxRange)),
                GetCellCoordinates(new Vector2D(pos.x - maxRange, pos.y + maxRange)),
                GetCellCoordinates(new Vector2D(pos.x - maxRange, pos.y - maxRange)),
            };
            var seen = new HashSet<(int Col, int Row)>();
            foreach (var cell in cells)
            {
                if (!IsCellInRange(cell.Col, cell.Row)) continue;
                if (seen.Add(cell)) yield return cell;
            }

            yield break;
        }

        foreach (var cell in GetCellRange(pos.x - maxRange, pos.y - maxRange, maxRange * 2, maxRange * 2))
            yield return cell;
    }

    private static bool TryGetSectorBounds(
        Sector sector,
        out double minX,
        out double minY,
        out double maxX,
        out double maxY)
    {
        minX = minY = double.PositiveInfinity;
        maxX = maxY = double.NegativeInfinity;
        bool any = false;

        foreach (var side in sector.Sidedefs)
        {
            var start = side.Line.Start.Position;
            var end = side.Line.End.Position;
            minX = Math.Min(minX, Math.Min(start.x, end.x));
            minY = Math.Min(minY, Math.Min(start.y, end.y));
            maxX = Math.Max(maxX, Math.Max(start.x, end.x));
            maxY = Math.Max(maxY, Math.Max(start.y, end.y));
            any = true;
        }

        return any;
    }

    private bool BoxIntersectsRange(double minX, double minY, double maxX, double maxY)
    {
        double right = originX + cols * blockSize;
        double bottom = originY + rows * blockSize;
        return maxX >= originX && maxY >= originY && minX <= right && minY <= bottom;
    }

    private static bool SectorContainsPoint(Sector sector, Vector2D pos)
    {
        var path = new SidedefsTracePath();
        foreach (var side in sector.Sidedefs)
            path.Add(side);
        return path.Count > 2 && path.CheckIsClosed() && path.MakePolygon().Intersect(pos);
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
        => l.SafeDistanceToSq(pos, bounded: true);
}
