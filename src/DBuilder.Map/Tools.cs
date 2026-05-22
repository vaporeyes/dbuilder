// ABOUTME: Geometry tools ported from UDB Source/Core/Geometry/Tools.cs - boundary tracing for sector detection.
// ABOUTME: FindClosestPath walks the tightest-turn loop of linedef sides from a starting line+side back to itself.

/*
 * A focused port of UDB's sector-tracing core. FindClosestPath follows the planar subdivision from a starting
 * (line, side) by, at each vertex, choosing the angle-sorted next line (the tightest turn keeping the traced
 * face on the chosen side), until it returns to the start. This is the building block UDB layers outer/inner
 * loop classification on top of; that classification (raycasting for holes) is deferred.
 *
 * Requires MapSet.BuildIndexes() to have populated Vertex.Linedefs.
 */

using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class Tools
{
    /// <summary>
    /// Finds the linedef sides bounding the sector that would contain <paramref name="pos"/>: traces the loop
    /// of the nearest linedef on the side facing the point (retracing outward if that loop is itself a hole),
    /// then detects inner hole loops. Returns the combined outer+inner sides, or null if no enclosing loop.
    /// </summary>
    public static List<LinedefSide>? FindPotentialSectorAt(MapSet map, Vector2D pos)
    {
        var line = map.NearestLinedef(pos);
        if (line == null) return null;
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) <= 0;
        return FindPotentialSectorAt(map, line, front);
    }

    // A scanline shot to the right from a hole's rightmost vertex extends to here looking for the enclosing loop.
    private const double RightBoundary = 1e7;

    /// <summary>Finds the outer loop enclosing the line+side (retracing outward if it lands on a hole), then adds inner hole loops.</summary>
    public static List<LinedefSide>? FindPotentialSectorAt(MapSet map, Linedef line, bool front)
    {
        var all = new List<LinedefSide>();
        var poly = FindOuterLines(map, line, front, all);
        if (poly == null || all.Count < 3) return null;

        FindInnerLines(map, poly, all);
        return all;
    }

    // Traces the outermost loop enclosing the start line+side (UDB FindOuterLines). If the trace lands on an
    // inner (hole) loop, casts a scanline to the right from the loop's rightmost vertex to find the next
    // outward linedef and retraces from there, until the traced side faces inside its own loop (the true outer).
    private static EarClipPolygon? FindOuterLines(MapSet map, Linedef line, bool front, List<LinedefSide> all)
    {
        Linedef? scanline = line;
        bool scanfront = front;
        int guard = 0;

        while (scanline != null && ++guard < 1000)
        {
            var path = FindClosestPath(scanline, scanfront, turnatends: true);
            if (path == null || path.Count < 3) return null;

            var poly = new LinedefTracePath(path).MakePolygon(true);

            // The traced side faces into its own loop: this is the outer boundary we want.
            if (poly.Intersect(GetSidePoint(scanline, scanfront)))
            {
                all.AddRange(path);
                return poly;
            }

            // Otherwise this is a hole boundary. Cast a ray right from the loop's rightmost vertex.
            Vector2D rightmost = poly.First!.Value.Position;
            foreach (var ecv in poly)
                if (ecv.Position.x > rightmost.x) rightmost = ecv.Position;

            var scan = new Line2D(rightmost, new Vector2D(RightBoundary, rightmost.y));
            Linedef? foundline = null;
            double foundu = double.MaxValue;
            foreach (var ld in map.Linedefs)
            {
                if (scan.GetIntersection(ld.Start.Position.x, ld.Start.Position.y, ld.End.Position.x, ld.End.Position.y, out double u_ray, out double _))
                {
                    if (u_ray > 0.00001 && u_ray < foundu) { foundu = u_ray; foundline = ld; }
                }
            }
            if (foundline == null) return null;

            // Continue tracing the found line on the side facing back toward our region (the rightmost vertex).
            scanfront = Line2D.GetSideOfLine(foundline.Start.Position, foundline.End.Position, rightmost) <= 0;
            scanline = foundline;
        }
        return null;
    }

    // Finds hole loops fully inside the outer polygon and appends their sides to alllines (UDB FindInnerLines).
    private static void FindInnerLines(MapSet map, EarClipPolygon p, List<LinedefSide> all)
    {
        var bbox = p.CreateBBox();
        bool findmore;
        do
        {
            findmore = false;

            // Right-most vertex strictly inside the polygon that isn't part of the boundary we've collected.
            Vertex? foundv = null;
            foreach (var v in map.Vertices)
            {
                if (v.Position.x < bbox.Left || v.Position.x > bbox.Right || v.Position.y < bbox.Top || v.Position.y > bbox.Bottom) continue;
                if (foundv != null && v.Position.x < foundv.Position.x) continue;
                if (v.Linedefs.Count == 0 || !p.Intersect(v.Position)) continue;

                bool partOfBoundary = false;
                foreach (var ls in all)
                    if (ReferenceEquals(ls.Line.Start, v) || ReferenceEquals(ls.Line.End, v)) { partOfBoundary = true; break; }
                if (!partOfBoundary) foundv = v;
            }
            if (foundv == null) continue;

            // From this right-most interior vertex, the attached line closest to pointing "up" (toward +90 deg).
            const double target = Angle2D.PIHALF;
            Linedef? foundline = null;
            double foundangle = 0;
            foreach (var l in foundv.Linedefs)
            {
                double lineangle = l.Angle;
                if (ReferenceEquals(l.End, foundv)) lineangle += Angle2D.PI;
                double delta = Angle2D.Difference(target, lineangle);
                if (foundline == null || delta < foundangle) { foundline = l; foundangle = delta; }
            }
            if (foundline == null) continue;

            // Start tracing on the side facing right of the interior vertex.
            bool flFront = Line2D.GetSideOfLine(foundline.Start.Position, foundline.End.Position, foundv.Position + new Vector2D(100, 0)) < 0;
            var inner = FindClosestPath(foundline, flFront, true);
            if (inner == null) continue;

            var innerPoly = new LinedefTracePath(inner).MakePolygon(true);
            var sidePt = GetSidePoint(foundline, flFront);
            var ib = innerPoly.CreateBBox();
            bool outsideBbox = sidePt.x < ib.Left || sidePt.x > ib.Right || sidePt.y < ib.Top || sidePt.y > ib.Bottom;
            // A genuine hole: the traced side faces outside its own loop.
            if (outsideBbox || !innerPoly.Intersect(sidePt))
            {
                all.AddRange(inner);
                p.InsertChild(innerPoly);
                findmore = true;
            }
        }
        while (findmore);
    }

    // A point just off the given side of a line (front = right of start->end), used for inside/outside tests.
    private static Vector2D GetSidePoint(Linedef l, bool front)
    {
        var mid = (l.Start.Position + l.End.Position) * 0.5;
        var d = l.End.Position - l.Start.Position;
        var perp = new Vector2D(d.y, -d.x).GetNormal(); // right-hand (front) perpendicular
        return front ? mid + perp : mid - perp;
    }

    /// <summary>
    /// Traces the closest closed path of linedef sides starting at <paramref name="startline"/> on the given side,
    /// returning the ordered loop, or null if the trace can't close (open geometry). When <paramref name="turnatends"/>
    /// is true, dead-end vertices reverse along the other side of the line.
    /// </summary>
    public static List<LinedefSide>? FindClosestPath(Linedef startline, bool startfront, bool turnatends)
    {
        var path = new List<LinedefSide>();
        var tracecount = new Dictionary<Linedef, int>(ReferenceEqualityComparer.Instance);
        Linedef? nextline = startline;
        bool nextfront = startfront;
        int guard = 0, maxSteps = 100000;

        do
        {
            if (nextline == null) { path = null; break; }
            path.Add(new LinedefSide(nextline, nextfront));

            // Move to the far vertex of the directed edge.
            Vertex v = nextfront ? nextline.End : nextline.Start;

            // Sort the linedefs around v by angle relative to the line we arrived on.
            var lines = new List<Linedef>(v.Linedefs);
            lines.Sort(new LinedefAngleSorter(nextline, nextfront, v));

            if (lines.Count == 1)
            {
                // Dead end: reverse along the other side, or stop.
                if (turnatends && (!tracecount.TryGetValue(nextline, out int tc) || tc < 3))
                {
                    nextfront = !nextfront;
                }
                else { path = null; }
            }
            else
            {
                Linedef prevline = nextline;
                nextline = (lines[0] == nextline ? lines[1] : lines[0]);

                if (!tracecount.TryGetValue(nextline, out int tc2) || tc2 < 3)
                {
                    // Front side flips when consecutive lines share the same start or same end vertex.
                    if (ReferenceEquals(prevline.Start, nextline.Start) || ReferenceEquals(prevline.End, nextline.End))
                        nextfront = !nextfront;
                }
                else { path = null; }
            }

            if (nextline != null)
                tracecount[nextline] = tracecount.TryGetValue(nextline, out int c) ? c + 1 : 1;

            if (++guard > maxSteps) { path = null; break; }
        }
        while (path != null && (nextline != startline || nextfront != startfront));

        return path;
    }

    /// <summary>Returns the ordered, de-duplicated vertices of a traced loop (each side contributes its start vertex).</summary>
    public static List<Vertex> LoopVertices(IReadOnlyList<LinedefSide> path)
    {
        var verts = new List<Vertex>(path.Count);
        foreach (var ls in path)
            verts.Add(ls.Front ? ls.Line.Start : ls.Line.End);
        return verts;
    }
}
