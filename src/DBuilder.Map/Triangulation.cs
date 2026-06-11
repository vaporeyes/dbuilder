// ABOUTME: Sector polygon triangulator ported from UDB Source/Core/Geometry/Triangulation.cs.
// ABOUTME: Three stages: trace sidedef path -> cut inner polygons into outer -> ear-clip into triangles. Serialization (ReadWrite/PostDeserialize) deferred until the clipboard module is ported.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * This process is divided into three stages:
 *   1) Tracing sector lines to find clockwise outer polygons and counter-clockwise
 *      inner polygons (holes). Arranged in a polygon tree.
 *   2) Cutting the inner polygons to make a flat list of only outer polygons by
 *      inserting bridge edges from each hole to its containing outer polygon.
 *   3) Ear-clipping the polygons to produce triangles.
 *
 * Algorithm references:
 *   - http://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed class Triangulation
{
    private ReadOnlyCollection<int> islandvertices;
    private ReadOnlyCollection<Vector2D> vertices;
    private ReadOnlyCollection<Sidedef?> sidedefs;

    /// <summary>Number of vertices contributed by each island (independent connected component of the sector).</summary>
    public ReadOnlyCollection<int> IslandVertices => islandvertices;

    /// <summary>Triangle vertices - 3 entries per triangle, vertex order is clockwise within each island.</summary>
    public ReadOnlyCollection<Vector2D> Vertices => vertices;

    /// <summary>Per-vertex source sidedef. A null entry means the vertex isn't the start of a sidedef (e.g. interior split vertex).</summary>
    public ReadOnlyCollection<Sidedef?> Sidedefs => sidedefs;

    /// <summary>
    /// True when the main trace+cut+earclip algorithm produced no triangles and the fallback (centroid fan over angle-sorted vertices, or bbox quad)
    /// was used.  Fallback geometry is visually approximate - convex shapes are exact, concave shapes overshoot their actual boundary.
    /// </summary>
    public bool IsApproximate { get; private set; }

    public Triangulation()
    {
        islandvertices = Array.AsReadOnly(new int[0]);
        vertices = Array.AsReadOnly(new Vector2D[0]);
        sidedefs = Array.AsReadOnly(new Sidedef?[0]);
    }

    public static Triangulation Create(Sector sector)
    {
        var t = new Triangulation();
        t.Triangulate(sector);
        return t;
    }

    public void Triangulate(Sector s)
    {
        var islandslist = new List<int>();
        var verticeslist = new List<Vector2D>();
        var sidedefslist = new List<Sidedef?>();

        GC.SuppressFinalize(this);
        IsApproximate = false;

        // Stage 1: trace polygons
        List<EarClipPolygon> polys = DoTrace(s);

        // Stage 2: cut inner polygons into outer
        DoCutting(polys);

        // Stage 3: ear-clip each polygon
        foreach (EarClipPolygon p in polys)
            islandslist.Add(DoEarClip(p, verticeslist, sidedefslist));

        // Stage 4 (fallback): when the main algorithm produced no triangles but the sector clearly has geometry,
        // approximate it with a centroid fan so the viewer can still draw *something* for pathological sectors
        // (self-intersecting boundaries, multi-island geometry, etc.).
        if (verticeslist.Count == 0 && s.Sidedefs.Count > 0)
        {
            int produced = CentroidFanFallback(s, verticeslist, sidedefslist);
            if (produced > 0)
            {
                islandslist.Add(produced);
                IsApproximate = true;
            }
        }

        islandvertices = Array.AsReadOnly(islandslist.ToArray());
        vertices = Array.AsReadOnly(verticeslist.ToArray());
        sidedefs = Array.AsReadOnly(sidedefslist.ToArray());
    }

    // ============================================================================================
    // Stage 4: centroid-fan fallback for sectors the main algorithm couldn't trace
    // ============================================================================================

    /// <summary>
    /// Collects all unique vertex positions from the sector's sidedefs, sorts them by angle around their centroid,
    /// and fan-triangulates from the centroid.  Convex sectors fan exactly.  Concave sectors over-cover (some
    /// triangles spill outside the real boundary) but the viewer at least gets a colored patch where the sector lives.
    /// </summary>
    /// <returns>Number of vertex entries appended to <paramref name="verts"/> (3 per triangle).</returns>
    private static int CentroidFanFallback(Sector s, List<Vector2D> verts, List<Sidedef?> sides)
    {
        // Collect unique vertex positions across all sidedefs of this sector.
        var seen = new HashSet<(double, double)>();
        var positions = new List<Vector2D>();
        foreach (var sd in s.Sidedefs)
        {
            if (sd.Other != null && sd.Sector == sd.Other.Sector) continue;
            if (sd.Line == null) continue;
            TryAdd(sd.Line.Start.Position);
            TryAdd(sd.Line.End.Position);
        }
        void TryAdd(Vector2D p)
        {
            var key = (p.x, p.y);
            if (seen.Add(key)) positions.Add(p);
        }

        if (positions.Count == 0) return 0;

        if (positions.Count < 3)
        {
            // Degenerate (single point or single segment): emit a tiny bbox quad so the viewer sees a dot.
            return EmitBboxQuad(positions, verts, sides);
        }

        // Centroid
        double cx = 0, cy = 0;
        foreach (var p in positions) { cx += p.x; cy += p.y; }
        cx /= positions.Count; cy /= positions.Count;
        var centroid = new Vector2D(cx, cy);

        // Angle-sort around centroid so the fan covers the convex hull of the vertex set.
        positions.Sort((a, b) => Math.Atan2(a.y - cy, a.x - cx).CompareTo(Math.Atan2(b.y - cy, b.x - cx)));

        int produced = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            int j = (i + 1) % positions.Count;
            // Skip near-zero-area triangles (two adjacent sorted vertices coincident with each other or centroid).
            var pa = centroid;
            var pb = positions[i];
            var pc = positions[j];
            double signedArea = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
            if (Math.Abs(signedArea) < 0.001) continue;

            verts.Add(pa); verts.Add(pb); verts.Add(pc);
            sides.Add(null); sides.Add(null); sides.Add(null);
            produced += 3;
        }
        return produced;
    }

    /// <summary>Emits a tiny axis-aligned quad covering the bounding box of <paramref name="positions"/> as a fallback for degenerate sectors.</summary>
    private static int EmitBboxQuad(List<Vector2D> positions, List<Vector2D> verts, List<Sidedef?> sides)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in positions)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        // Expand zero-width/height degenerate cases so the quad has area.
        const double minSize = 4.0;
        if (maxX - minX < minSize) { maxX = minX + minSize; }
        if (maxY - minY < minSize) { maxY = minY + minSize; }

        var a = new Vector2D(minX, minY);
        var b = new Vector2D(maxX, minY);
        var c = new Vector2D(maxX, maxY);
        var d = new Vector2D(minX, maxY);
        verts.Add(a); verts.Add(b); verts.Add(c);
        sides.Add(null); sides.Add(null); sides.Add(null);
        verts.Add(a); verts.Add(c); verts.Add(d);
        sides.Add(null); sides.Add(null); sides.Add(null);
        return 6;
    }

    // ============================================================================================
    // Stage 1: tracing
    // ============================================================================================

    private static List<EarClipPolygon> DoTrace(Sector s)
    {
        var todosides = new Dictionary<Sidedef, bool>(s.Sidedefs.Count);
        var ignores = new Dictionary<Vertex, Vertex>();
        var root = new List<EarClipPolygon>();

        // bool indicates whether the line has been visited during the current trace attempt.
        foreach (Sidedef sd in s.Sidedefs) todosides.Add(sd, false);

        // First remove all sides that refer to the same sector on both ends - they're useless for tracing.
        RemoveDoubleSidedefReferences(todosides, s.Sidedefs);

        while (todosides.Count > 0)
        {
            // Reset visited flags for this trace attempt
            foreach (Sidedef sd in s.Sidedefs) if (todosides.ContainsKey(sd)) todosides[sd] = false;

            // Right-most vertex guarantees the first traced polygon is an outer one.
            Vertex? start = FindRightMostVertex(todosides, ignores);
            if (start == null) break;

            // Trace from this start to find a closed polygon.
            SidedefsTracePath? path = DoTracePath(new SidedefsTracePath(), start, null, s, todosides);

            if (path == null)
            {
                // Sector isn't closed at this vertex - ignore and try a different start.
                ignores.Add(start, start);
            }
            else
            {
                // Mark these sides as done.
                foreach (Sidedef sd in path) todosides.Remove(sd);

                EarClipPolygon newpoly = path.MakePolygon();

                // Find where this polygon fits in our tree (outer vs nested).
                EarClipPolygon? toInsert = newpoly;
                foreach (EarClipPolygon p in root)
                {
                    if (p.InsertChild(newpoly))
                    {
                        toInsert = null;
                        break;
                    }
                }

                if (toInsert != null)
                {
                    // Top-level outer polygon
                    toInsert.Inner = false;
                    root.Add(toInsert);
                }
            }
        }

        return root;
    }

    private static SidedefsTracePath? DoTracePath(SidedefsTracePath history, Vertex fromhere, Vertex? findme, Sector sector, Dictionary<Sidedef, bool> sides)
    {
        if (fromhere == findme) return history;

        // First iteration: lock in the start vertex as the target so the trace terminates when it returns.
        if (findme == null) findme = fromhere;

        // Sides departing from 'fromhere' that belong to this sector and haven't been visited.
        var allsides = new List<Sidedef>(fromhere.Linedefs.Count * 2);
        foreach (Linedef l in fromhere.Linedefs)
        {
            // Direction matters for clockwise polygon orientation.
            if (l.Start == fromhere)
            {
                if (l.Front != null && l.Front.Sector == sector)
                {
                    if (sides.ContainsKey(l.Front) && !sides[l.Front]) allsides.Add(l.Front);
                }
            }
            else
            {
                if (l.Back != null && l.Back.Sector == sector)
                {
                    if (sides.ContainsKey(l.Back) && !sides[l.Back]) allsides.Add(l.Back);
                }
            }
        }

        // For vertices shared by 3+ sides of the same sector, take the smallest delta angle first.
        if (history.Count > 0)
        {
            var sorter = new SidedefAngleSorter(history[history.Count - 1], fromhere);
            allsides.Sort(sorter);
        }

        foreach (Sidedef s in allsides)
        {
            sides[s] = true;
            var nextpath = new SidedefsTracePath(history, s);
            Vertex nextvertex = (s.Line.Start == fromhere ? s.Line.End : s.Line.Start);

            SidedefsTracePath? result = DoTracePath(nextpath, nextvertex, findme, sector, sides);
            if (result != null) return result;
        }

        return null;
    }

    private static void RemoveDoubleSidedefReferences(Dictionary<Sidedef, bool> todosides, ICollection<Sidedef> sides)
    {
        foreach (Sidedef sd in sides)
        {
            if (sd.Other != null && sd.Sector == sd.Other.Sector)
                todosides.Remove(sd);
        }
    }

    private static Vertex? FindRightMostVertex(Dictionary<Sidedef, bool> sides, Dictionary<Vertex, Vertex> ignores)
    {
        Vertex? found = null;

        foreach (Sidedef sd in sides.Keys)
        {
            if (found == null && !ignores.ContainsKey(sd.Line.Start)) found = sd.Line.Start;
            if (found == null && !ignores.ContainsKey(sd.Line.End))   found = sd.Line.End;

            if (found != null)
            {
                if (sd.Line.Start.Position.x > found.Position.x && !ignores.ContainsKey(sd.Line.Start)) found = sd.Line.Start;
                if (sd.Line.End.Position.x   > found.Position.x && !ignores.ContainsKey(sd.Line.End))   found = sd.Line.End;
            }
        }

        return found;
    }

    // ============================================================================================
    // Stage 2: cutting (flattening the polygon tree by bridging holes to their containers)
    // ============================================================================================

    private void DoCutting(List<EarClipPolygon> polys)
    {
        var todo = new Queue<EarClipPolygon>(polys);

        while (todo.Count > 0)
        {
            EarClipPolygon p = todo.Dequeue();

            if (p.Children.Count > 0)
            {
                // Grandchildren are outer polygons relative to us; promote them to root.
                foreach (EarClipPolygon c in p.Children)
                {
                    polys.AddRange(c.Children);
                    foreach (EarClipPolygon sc in c.Children) todo.Enqueue(sc);
                    c.Children.Clear();
                }

                MergeInnerPolys(p);
            }
        }
    }

    private static void MergeInnerPolys(EarClipPolygon p)
    {
        var todo = new LinkedList<EarClipPolygon>(p.Children);

        while (todo.Count > 0)
        {
            // Pick the inner polygon with the highest-x rightmost vertex first.
            LinkedListNode<EarClipPolygon>? found = null;
            LinkedListNode<EarClipVertex>? foundstart = null;
            LinkedListNode<EarClipPolygon>? ip = todo.First;
            while (ip != null)
            {
                LinkedListNode<EarClipVertex> start = FindRightMostVertex(ip.Value);
                if (foundstart == null || start.Value.Position.x > foundstart.Value.Position.x)
                {
                    found = ip;
                    foundstart = start;
                }
                ip = ip.Next;
            }

            todo.Remove(found!);
            SplitOuterWithInner(foundstart!, p);
        }

        p.Children.Clear();
    }

    private static LinkedListNode<EarClipVertex> FindRightMostVertex(EarClipPolygon p)
    {
        LinkedListNode<EarClipVertex> found = p.First!;
        LinkedListNode<EarClipVertex>? v = found.Next;

        while (v != null)
        {
            if (v.Value.Position.x > found.Value.Position.x) found = v;
            v = v.Next;
        }

        return found;
    }

    private static void SplitOuterWithInner(LinkedListNode<EarClipVertex> start, EarClipPolygon p)
    {
        LinkedListNode<EarClipVertex>? insertbefore = null;
        double foundu = double.MaxValue;
        Vector2D foundpos = new Vector2D();

        // Scan a horizontal ray from 'start' to the right, looking for the first outer-polygon edge it hits.
        LinkedListNode<EarClipVertex> pr = FindRightMostVertex(p);
        double startx = start.Value.Position.x;
        double endx = pr.Value.Position.x + 10.0;
        var starttoright = new Line2D(start.Value.Position, new Vector2D(endx, start.Value.Position.y));

        double bonus = starttoright.GetNearestOnLine(new Vector2D(start.Value.Position.x + 0.1, start.Value.Position.y));

        LinkedListNode<EarClipVertex> v1 = p.Last!;
        LinkedListNode<EarClipVertex>? v2 = p.First;
        while (v2 != null)
        {
            if ((v1.Value.Position.x > startx || v2.Value.Position.x > startx) &&
                (v1.Value.Position.x < endx   || v2.Value.Position.x < endx))
            {
                var pl = new Line2D(v1.Value.Position, v2.Value.Position);
                pl.GetIntersection(starttoright, out double u, out double ul);
                if (double.IsNaN(u))
                {
                    // Horizontal outer-polygon edge - overlaps the scan ray, need extra care to pick the right join point.
                    if (v1.Value.Position.y == start.Value.Position.y)
                    {
                        u = starttoright.GetNearestOnLine(v1.Value.Position);
                        ul = starttoright.GetNearestOnLine(v2.Value.Position);

                        if (u < 0.0) u = double.MaxValue;
                        if (ul < 0.0) ul = double.MaxValue;

                        double insert_u = Math.Min(u, ul);
                        Vector2D inserpos = starttoright.GetCoordinatesAt(insert_u);

                        if (v1.Value.Position.x > v2.Value.Position.x)
                        {
                            // Edge goes right->left (toward start) - always insert AFTER it.
                            LinkedListNode<EarClipVertex> v3 = v2.Next ?? v2.List!.First!;
                            if (v3.Value.Position.y < v2.Value.Position.y) insert_u -= bonus;

                            if (insert_u <= foundu)
                            {
                                insertbefore = v2.Next ?? v2.List!.First;
                                foundu = insert_u;
                                foundpos = inserpos;
                            }
                        }
                        else
                        {
                            // Edge goes left->right (away from start) - always insert BEFORE it.
                            LinkedListNode<EarClipVertex> v3 = v1.Previous ?? v1.List!.Last!;
                            if (v3.Value.Position.y > v1.Value.Position.y) insert_u -= bonus;

                            if (insert_u <= foundu)
                            {
                                insertbefore = v2;
                                foundu = insert_u;
                                foundpos = inserpos;
                            }
                        }
                    }
                }
                else if (ul >= 0.0 && ul <= 1.0 && u > 0.0 && u <= foundu)
                {
                    insertbefore = v2;
                    foundu = u;
                    foundpos = starttoright.GetCoordinatesAt(u);
                }
            }

            v1 = v2;
            v2 = v2.Next;
        }

        if (insertbefore != null)
        {
            Sidedef? sd = (insertbefore.Previous == null)
                ? insertbefore.List!.Last!.Value.Sidedef
                : insertbefore.Previous.Value.Sidedef;

            var split = new EarClipVertex(foundpos, null);
            p.AddBefore(insertbefore, new EarClipVertex(split, sd));

            LinkedListNode<EarClipVertex>? walker = start;
            do
            {
                p.AddBefore(insertbefore, new EarClipVertex(walker!.Value));
                walker = walker.Next ?? walker.List!.First;
            }
            while (walker != start);

            p.AddBefore(insertbefore, new EarClipVertex(start.Value, sd));
            if (split.Position != insertbefore.Value.Position)
                p.AddBefore(insertbefore, new EarClipVertex(split, sd));
        }
    }

    // ============================================================================================
    // Stage 3: ear clipping
    // ============================================================================================

    private int DoEarClip(EarClipPolygon poly, List<Vector2D> verticeslist, List<Sidedef?> sidedefslist)
    {
        var verts = new LinkedList<EarClipVertex>();
        var convexes = new List<EarClipVertex>(poly.Count);
        var reflexes = new LinkedList<EarClipVertex>();
        var eartips = new LinkedList<EarClipVertex>();
        int countvertices = 0;

        // Build the linked list and link each vertex back to its node.
        foreach (EarClipVertex vec in poly) vec.SetVertsLink(verts.AddLast(vec));

        // Remove zero-length edges (would otherwise corrupt the ear-test math).
        LinkedListNode<EarClipVertex>? n1 = verts.First;
        do
        {
            LinkedListNode<EarClipVertex> n2 = n1!.Next ?? verts.First!;
            Vector2D d = n1.Value.Position - n2.Value.Position;
            while (Math.Abs(d.x) < 0.00001f && Math.Abs(d.y) < 0.00001f)
            {
                n2.Value.Remove();
                n2 = n1.Next ?? verts.First!;
                if (n2 != null) d = n1.Value.Position - n2.Value.Position; else break;
            }
            n1 = n2;
        }
        while (n1 != verts.First);

        // Collinear-vertex optimization: vertices where both adjacent lines have the same angle are useless.
        n1 = verts.First;
        while (n1 != null)
        {
            LinkedListNode<EarClipVertex>? next = n1.Next;
            EarClipVertex[] t = GetTriangle(n1.Value);
            var a = new Line2D(t[0].Position, t[1].Position);
            var b = new Line2D(t[1].Position, t[2].Position);
            if (Math.Abs(Angle2D.Difference(a.GetAngle(), b.GetAngle())) < 0.00001f)
                n1.Value.Remove();
            n1 = next;
        }

        // Categorize each vertex as reflex (>180 deg corner) or convex.
        foreach (EarClipVertex vv in verts)
        {
            if (IsReflex(GetTriangle(vv))) vv.AddReflex(reflexes);
            else convexes.Add(vv);
        }

        // Of the convex vertices, the ones whose triangle contains no reflex vertex are valid ear tips.
        foreach (EarClipVertex cv in convexes)
        {
            EarClipVertex[] t = GetTriangle(cv);
            if (CheckValidEar(t, reflexes)) cv.AddEarTip(eartips);
        }

        // Clip ears one at a time.
        while (eartips.Count > 0 && verts.Count > 2)
        {
            EarClipVertex v = eartips.First!.Value;
            EarClipVertex[] t = GetTriangle(v);

            // Only emit non-degenerate triangles.
            if (TriangleHasArea(t))
            {
                AddTriangleToList(t, verticeslist, sidedefslist, last: verts.Count == 3);
                countvertices += 3;
            }

            v.Remove();
            EarClipVertex v1 = t[0];
            EarClipVertex v2 = t[2];

            // Re-classify the two neighbours that lost their corner.
            EarClipVertex[] t1 = GetTriangle(v1);
            if (IsReflex(t1)) { if (!v1.IsReflex) v1.AddReflex(reflexes); v1.RemoveEarTip(); }
            else { v1.RemoveReflex(); }

            EarClipVertex[] t2 = GetTriangle(v2);
            if (IsReflex(t2)) { if (!v2.IsReflex) v2.AddReflex(reflexes); v2.RemoveEarTip(); }
            else { v2.RemoveReflex(); }

            if (!v1.IsReflex && CheckValidEar(t1, reflexes)) v1.AddEarTip(eartips); else v1.RemoveEarTip();
            if (!v2.IsReflex && CheckValidEar(t2, reflexes)) v2.AddEarTip(eartips); else v2.RemoveEarTip();
        }

        // Free any remaining vertices.
        foreach (EarClipVertex ecv in verts) ecv.Dispose();

        return countvertices;
    }

    // Checks if ear-triangle t contains any reflex vertex (would invalidate the ear).
    private static bool CheckValidEar(EarClipVertex[] t, LinkedList<EarClipVertex> reflexes)
    {
        Vector2D pos0 = t[0].Position;
        Vector2D pos1 = t[1].Position;
        Vector2D pos2 = t[2].Position;

        foreach (EarClipVertex rv in reflexes)
        {
            if (rv.Position == pos0 || rv.Position == pos1 || rv.Position == pos2) continue;
            if (!TriangleHasArea(t)) continue;

            Vector2D vpos = rv.MainListNode!.Value.Position;

            // Fast bbox reject
            if (vpos.x < Math.Min(pos0.x, Math.Min(pos1.x, pos2.x)) ||
                vpos.x > Math.Max(pos0.x, Math.Max(pos1.x, pos2.x)) ||
                vpos.y < Math.Min(pos0.y, Math.Min(pos1.y, pos2.y)) ||
                vpos.y > Math.Max(pos0.y, Math.Max(pos1.y, pos2.y))) continue;

            double lineside01 = Line2D.GetSideOfLine(pos0, pos1, vpos);
            double lineside12 = Line2D.GetSideOfLine(pos1, pos2, vpos);
            double lineside20 = Line2D.GetSideOfLine(pos2, pos0, vpos);
            double u_on_line = 0.5;

            if (lineside01 == 0.0) u_on_line = Line2D.GetNearestOnLine(pos0, pos1, vpos);
            else if (lineside12 == 0.0) u_on_line = Line2D.GetNearestOnLine(pos1, pos2, vpos);
            else if (lineside20 == 0.0) u_on_line = Line2D.GetNearestOnLine(pos2, pos0, vpos);

            if (lineside01 == 0.0 || lineside12 == 0.0 || lineside20 == 0.0)
            {
                // On an edge - decide by where the adjacent lines go.
                if (u_on_line < 0.0 || u_on_line > 1.0) continue;

                LinkedListNode<EarClipVertex> p = rv.MainListNode!;
                LinkedListNode<EarClipVertex> p1 = p.Previous ?? p.List!.Last!;
                if (LineInsideTriangle(t, vpos, p1.Value.Position)) return false;

                LinkedListNode<EarClipVertex> p2 = p.Next ?? p.List!.First!;
                if (LineInsideTriangle(t, vpos, p2.Value.Position)) return false;

                continue;
            }

            if (lineside01 < 0.0 && lineside12 < 0.0 && lineside20 < 0.0) return false;
        }

        return true;
    }

    private static EarClipVertex[] GetTriangle(EarClipVertex v)
    {
        return new[]
        {
            (v.MainListNode!.Previous == null) ? v.MainListNode.List!.Last!.Value : v.MainListNode.Previous.Value,
            v,
            (v.MainListNode.Next == null) ? v.MainListNode.List!.First!.Value : v.MainListNode.Next.Value
        };
    }

    private static bool IsReflex(EarClipVertex[] t)
    {
        return Line2D.GetSideOfLine(t[0].Position, t[2].Position, t[1].Position) < 0.0;
    }

    private static bool LineInsideTriangle(EarClipVertex[] t, Vector2D p1, Vector2D p2)
    {
        double s01 = Line2D.GetSideOfLine(t[0].Position, t[1].Position, p2);
        double s12 = Line2D.GetSideOfLine(t[1].Position, t[2].Position, p2);
        double s20 = Line2D.GetSideOfLine(t[2].Position, t[0].Position, p2);
        double p2_on_edge = 2.0;
        double p1_on_same_edge = 2.0;

        if (s01 < 0.0 && s12 < 0.0 && s20 < 0.0) return true;

        if (s01 == 0.0)
        {
            p2_on_edge = Line2D.GetNearestOnLine(t[0].Position, t[1].Position, p2);
            p1_on_same_edge = Line2D.GetSideOfLine(t[0].Position, t[1].Position, p1);
        }
        else if (s12 == 0.0)
        {
            p2_on_edge = Line2D.GetNearestOnLine(t[1].Position, t[2].Position, p2);
            p1_on_same_edge = Line2D.GetSideOfLine(t[1].Position, t[2].Position, p1);
        }
        else if (s20 == 0.0)
        {
            p2_on_edge = Line2D.GetNearestOnLine(t[2].Position, t[0].Position, p2);
            p1_on_same_edge = Line2D.GetSideOfLine(t[2].Position, t[0].Position, p1);
        }

        if (p2_on_edge >= 0.0 && p2_on_edge <= 1.0)
        {
            if (p1_on_same_edge == 0.0) return false;
        }

        // Full line-vs-triangle-edge intersection check.
        var p = new Line2D(p1, p2);
        var t01 = new Line2D(t[0].Position, t[1].Position);
        var t12 = new Line2D(t[1].Position, t[2].Position);
        var t20 = new Line2D(t[2].Position, t[0].Position);
        if (t01.GetIntersection(p, out _, out _)) return true;
        if (t12.GetIntersection(p, out _, out _)) return true;
        if (t20.GetIntersection(p, out _, out _)) return true;

        return false;
    }

    private static bool TriangleHasArea(EarClipVertex[] t)
    {
        Vector2D tp0 = t[0].Position;
        Vector2D tp1 = t[1].Position;
        Vector2D tp2 = t[2].Position;
        return (tp0.x * (tp1.y - tp2.y) +
                tp1.x * (tp2.y - tp0.y) +
                tp2.x * (tp0.y - tp1.y)) != 0.0;
    }

    private static void AddTriangleToList(EarClipVertex[] triangle, List<Vector2D> verticeslist, List<Sidedef?> sidedefslist, bool last)
    {
        EarClipVertex v0 = triangle[0];
        EarClipVertex v1 = triangle[1];
        EarClipVertex v2 = triangle[2];

        verticeslist.Add(v0.Position);
        sidedefslist.Add(v0.Sidedef);
        verticeslist.Add(v1.Position);
        sidedefslist.Add(v1.Sidedef);
        verticeslist.Add(v2.Position);
        sidedefslist.Add(last ? v2.Sidedef : null);

        // Once a vertex has been clipped, it no longer lies along a sidedef boundary.
        v0.Sidedef = null;
    }
}
