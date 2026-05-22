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

namespace DBuilder.Map;

public static class Tools
{
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
