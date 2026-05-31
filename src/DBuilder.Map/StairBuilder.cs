// ABOUTME: Turns sectors into stairs by applying UDB-style height, flat, and wall texture options.
// ABOUTME: Also keeps legacy helpers for simple floor stepping and ceiling movement.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class StairBuilder
{
    public const int DefaultUpperUnpeggedBit = 8;
    public const int DefaultLowerUnpeggedBit = 16;

    public static IReadOnlyList<StairBuilderSectorPlan> PlanStraightSectorsFromLines(
        IReadOnlyList<Linedef> selectedLinedefs,
        StairBuilderStraightOptions options)
    {
        var sectors = new List<StairBuilderSectorPlan>();

        foreach (Linedef line in selectedLinedefs)
        {
            Vector2D direction = line.Line.GetPerpendicular().GetNormal() * (options.SideFront ? -1 : 1);

            for (int i = 0; i < options.NumberOfSectors; i++)
            {
                Vector2D v1 = line.Start.Position + direction * options.SectorDepth * i;
                Vector2D v2 = line.End.Position + direction * options.SectorDepth * i;
                Vector2D v3 = v1 + direction * options.SectorDepth;
                Vector2D v4 = v2 + direction * options.SectorDepth;

                if (options.Spacing > 0)
                {
                    Vector2D offset = direction * options.Spacing * i;
                    v1 += offset;
                    v2 += offset;
                    v3 += offset;
                    v4 += offset;
                }

                sectors.Add(new StairBuilderSectorPlan(new[] { v1, v3, v4, v2, v1 }));
            }
        }

        return sectors;
    }

    public static IReadOnlyList<StairBuilderSectorPlan> PlanCurvedSectorsFromLines(
        IReadOnlyList<Linedef> selectedLinedefs,
        StairBuilderCurvedOptions options)
    {
        var sectors = new List<StairBuilderSectorPlan>();
        if (selectedLinedefs.Count <= 1) return sectors;

        for (int l1 = 0; l1 < selectedLinedefs.Count - 1; l1++)
        {
            int l2 = l1 + 1;
            double distance = 128;
            bool fixedCurve = true;

            Linedef first = selectedLinedefs[l1];
            Linedef second = selectedLinedefs[l2];

            Vector2D s1 = options.Flipping == 1 ? first.End.Position : first.Start.Position;
            Vector2D e1 = options.Flipping == 1 ? first.Start.Position : first.End.Position;
            Vector2D s2 = options.Flipping == 2 ? second.End.Position : second.Start.Position;
            Vector2D e2 = options.Flipping == 2 ? second.Start.Position : second.End.Position;

            Line2D innerLine;
            Line2D outerLine;
            bool clockwise;
            if (Vector2D.Distance(s1, s2) < Vector2D.Distance(e1, e2))
            {
                clockwise = true;
                innerLine = new Line2D(s2, s1);
                outerLine = new Line2D(e1, e2);
            }
            else
            {
                clockwise = false;
                innerLine = new Line2D(e1, e2);
                outerLine = new Line2D(s2, s1);
            }

            double innerAngle;
            double outerAngle;
            if (first.Angle == second.Angle && options.Flipping != 1 && options.Flipping != 2)
            {
                innerAngle = 1;
                outerAngle = 1;
                distance = 0;
                fixedCurve = false;
            }
            else
            {
                if (clockwise)
                {
                    innerAngle = outerAngle = first.Angle - second.Angle;
                    if (innerAngle < 0.0) innerAngle += Angle2D.PI2;
                    if (outerAngle < 0.0) outerAngle += Angle2D.PI2;
                }
                else
                {
                    innerAngle = outerAngle = second.Angle - first.Angle;
                    if (innerAngle < 0.0) innerAngle += Angle2D.PI2;
                    if (outerAngle < 0.0) outerAngle += Angle2D.PI2;
                }

                if (options.Flipping != 0)
                {
                    if (first.Angle == second.Angle)
                    {
                        if (options.Flipping == 1)
                        {
                            innerAngle = Math.Abs(innerAngle - Angle2D.PI);
                            outerAngle = Math.Abs(outerAngle - Angle2D.PI);
                        }
                        else if (options.Flipping == 2)
                        {
                            innerAngle -= Angle2D.PI;
                            outerAngle -= Angle2D.PI;
                        }
                    }
                    else
                    {
                        innerAngle = Math.Abs(innerAngle - Angle2D.PI2);
                        outerAngle = Math.Abs(outerAngle - Angle2D.PI2);
                    }
                }
            }

            int innerVertexMultiplier = options.InnerVertexMultiplier;
            int outerVertexMultiplier = options.OuterVertexMultiplier;
            var innerVertices = GenerateCurve(innerLine, options.NumberOfSectors * innerVertexMultiplier - 1, innerAngle, false, distance, fixedCurve);
            innerVertices.Insert(0, innerLine.v1);
            innerVertices.Add(innerLine.v2);

            var outerVertices = GenerateCurve(outerLine, options.NumberOfSectors * outerVertexMultiplier - 1, outerAngle, true, distance, fixedCurve);
            outerVertices.Insert(0, outerLine.v1);
            outerVertices.Add(outerLine.v2);

            if (!clockwise)
            {
                (innerVertices, outerVertices) = (outerVertices, innerVertices);
                (innerVertexMultiplier, outerVertexMultiplier) = (outerVertexMultiplier, innerVertexMultiplier);
            }

            for (int i = 0; i < options.NumberOfSectors; i++)
            {
                var sector = new List<Vector2D>();

                for (int k = 0; k <= outerVertexMultiplier; k++)
                    sector.Add(outerVertices[i * outerVertexMultiplier + k]);

                for (int k = 0; k <= innerVertexMultiplier; k++)
                    sector.Add(innerVertices[(options.NumberOfSectors - 1 - i) * innerVertexMultiplier + k]);

                sector.Add(outerVertices[i * outerVertexMultiplier]);
                sectors.Add(new StairBuilderSectorPlan(sector));
            }
        }

        return sectors;
    }

    public static IReadOnlyList<StairBuilderSectorPlan> PlanSplineSectorsFromLines(
        IReadOnlyList<Linedef> selectedLinedefs,
        StairBuilderSplineOptions options)
    {
        var sectors = new List<StairBuilderSectorPlan>();
        if (selectedLinedefs.Count <= 1) return sectors;

        int controlPointCount = Math.Max(options.NumberOfControlPoints + 2, 2);
        for (int l1 = 0; l1 < selectedLinedefs.Count - 1; l1++)
        {
            int l2 = l1 + 1;
            Linedef first = selectedLinedefs[l1];
            Linedef second = selectedLinedefs[l2];

            Vector2D s1 = options.Flipping == 1 ? first.End.Position : first.Start.Position;
            Vector2D e1 = options.Flipping == 1 ? first.Start.Position : first.End.Position;
            Vector2D s2 = options.Flipping == 2 ? second.End.Position : second.Start.Position;
            Vector2D e2 = options.Flipping == 2 ? second.Start.Position : second.End.Position;

            SplineData innerSpline = CreateSpline(new Line2D(s2, s1), controlPointCount);
            SplineData outerSpline = CreateSpline(new Line2D(e1, e2), controlPointCount);
            List<Vector2D> innerVertices = GenerateCatmullRom(innerSpline, options.NumberOfSectors * options.InnerVertexMultiplier);
            List<Vector2D> outerVertices = GenerateCatmullRom(outerSpline, options.NumberOfSectors * options.OuterVertexMultiplier);

            for (int i = 0; i < options.NumberOfSectors; i++)
            {
                var sector = new List<Vector2D>();

                for (int k = 0; k <= options.OuterVertexMultiplier; k++)
                    sector.Add(outerVertices[i * options.OuterVertexMultiplier + k]);

                for (int k = 0; k <= options.InnerVertexMultiplier; k++)
                    sector.Add(innerVertices[(options.NumberOfSectors - 1 - i) * options.InnerVertexMultiplier + k]);

                sector.Add(outerVertices[i * options.OuterVertexMultiplier]);
                sectors.Add(new StairBuilderSectorPlan(sector));
            }
        }

        return sectors;
    }

    public static IReadOnlyList<Sector> CreateSectorsFromPlans(
        MapSet map,
        IReadOnlyList<StairBuilderSectorPlan> plans,
        StairBuilderOptions options)
    {
        var verticesByPosition = new Dictionary<Vector2D, Vertex>();
        foreach (Vertex vertex in map.Vertices)
            verticesByPosition.TryAdd(vertex.Position, vertex);

        var sectors = new List<Sector>();
        foreach (StairBuilderSectorPlan plan in plans)
        {
            var loop = new List<Vertex>();
            int pointCount = plan.Vertices.Count;
            if (pointCount > 1 && plan.Vertices[0] == plan.Vertices[^1]) pointCount--;

            for (int i = 0; i < pointCount; i++)
            {
                Vector2D point = plan.Vertices[i];
                if (!verticesByPosition.TryGetValue(point, out Vertex? vertex))
                {
                    vertex = map.AddVertex(point);
                    verticesByPosition.Add(point, vertex);
                }

                loop.Add(vertex);
            }

            Sector? sector = SectorBuilder.CreateSector(map, loop);
            if (sector != null) sectors.Add(sector);
        }

        map.BuildIndexes();
        Apply(sectors, options);
        map.BuildIndexes();

        return sectors;
    }

    /// <summary>
    /// Sets sector i's floor to <paramref name="startFloor"/> + i*<paramref name="step"/> (in list order). When
    /// <paramref name="moveCeiling"/> is true the ceiling shifts by the same delta, preserving each room's height.
    /// Returns the number of sectors changed.
    /// </summary>
    public static int Apply(IReadOnlyList<Sector> sectors, int startFloor, int step, bool moveCeiling)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            int newFloor = startFloor + i * step;
            int delta = newFloor - sectors[i].FloorHeight;
            sectors[i].FloorHeight = newFloor;
            if (moveCeiling) sectors[i].CeilHeight += delta;
        }
        return sectors.Count;
    }

    /// <summary>
    /// Applies UDB-style independent floor and ceiling height steps. Floor heights are always changed; ceiling
    /// heights are changed only when <paramref name="applyCeiling"/> is true.
    /// </summary>
    public static int Apply(IReadOnlyList<Sector> sectors, int startFloor, int floorStep,
        bool applyCeiling, int startCeiling, int ceilingStep)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            sectors[i].FloorHeight = startFloor + i * floorStep;
            if (applyCeiling) sectors[i].CeilHeight = startCeiling + i * ceilingStep;
        }
        return sectors.Count;
    }

    public static int Apply(IReadOnlyList<Sector> sectors, StairBuilderOptions options)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            Sector sector = sectors[i];
            int stepCounter = i + 1;

            if (options.ApplyFloorHeight)
            {
                int baseHeight = options.DistinctBaseHeights ? sector.FloorHeight : options.FloorBase;
                sector.FloorHeight = baseHeight + options.FloorStep * stepCounter;
            }

            if (options.ApplyCeilingHeight)
            {
                int baseHeight = options.DistinctBaseHeights ? sector.CeilHeight : options.CeilingBase;
                sector.CeilHeight = baseHeight + options.CeilingStep * stepCounter;
            }

            if (options.ApplyFloorTexture) sector.SetFloorTexture(options.FloorTexture);
            if (options.ApplyCeilingTexture) sector.SetCeilTexture(options.CeilingTexture);

            ApplySidedefOptions(sector, options);
        }

        return sectors.Count;
    }

    private static void ApplySidedefOptions(Sector sector, StairBuilderOptions options)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            Linedef? line = side.Line;
            if (line == null) continue;

            if (options.ApplyUpperTexture)
            {
                if (line.Back?.HighRequired() == true) line.Back.SetTextureHigh(options.UpperTexture);
                if (line.Front?.HighRequired() == true) line.Front.SetTextureHigh(options.UpperTexture);
                if (options.UpperUnpegged) line.Flags |= options.UpperUnpeggedBit;
            }

            if (options.ApplyLowerTexture)
            {
                if (line.Front?.LowRequired() == true) line.Front.SetTextureLow(options.LowerTexture);
                if (line.Back?.LowRequired() == true) line.Back.SetTextureLow(options.LowerTexture);
                if (options.LowerUnpegged) line.Flags |= options.LowerUnpeggedBit;
            }

            if (options.ApplyMiddleTexture)
            {
                if (line.Front?.MiddleRequired() == true) line.Front.SetTextureMid(options.MiddleTexture);
                if (line.Back?.MiddleRequired() == true) line.Back.SetTextureMid(options.MiddleTexture);
            }
        }
    }

    private static List<Vector2D> GenerateCurve(Line2D line, int vertices, double angle, bool backwards, double distance, bool fixedCurve)
    {
        var points = new List<Vector2D>(Math.Max(vertices, 0));
        double chord = line.GetLength();
        double theta = angle;
        double d = (chord / Math.Tan(theta / 2)) / 2;
        double radius = d / Math.Cos(theta / 2);
        double height = radius - d;
        double yDeform = fixedCurve ? 1 : distance / height;
        if (backwards) yDeform = -yDeform;

        for (int v = 1; v <= vertices; v++)
        {
            double a = (Math.PI - theta) / 2 + v * (theta / (vertices + 1));
            double x = Math.Cos(a) * radius;
            double y = (Math.Sin(a) * radius - d) * yDeform;
            Vector2D vertex = new Vector2D(x, y).GetRotated(line.GetAngle() + Angle2D.PIHALF);
            vertex = vertex.GetTransformed(line.GetCoordinatesAt(0.5).x, line.GetCoordinatesAt(0.5).y, 1, 1);

            points.Add(vertex);
        }

        return points;
    }

    private static SplineData CreateSpline(Line2D line, int controlPointCount)
    {
        var controlPoints = new List<Vector2D> { line.v1 };

        for (int k = 1; k <= controlPointCount - 2; k++)
            controlPoints.Add(line.GetCoordinatesAt(1.0 / (controlPointCount - 1) * k));

        controlPoints.Add(line.v2);

        return new SplineData(controlPoints, ComputeTangents(controlPoints));
    }

    private static List<Vector2D> ComputeTangents(IReadOnlyList<Vector2D> controlPoints)
    {
        var tangents = new List<Vector2D>();
        tangents.Add(controlPoints[1] - controlPoints[0]);

        for (int i = 1; i < controlPoints.Count - 1; i++)
            tangents.Add((controlPoints[i + 1] - controlPoints[i - 1]) / 2.0);

        tangents.Add(controlPoints[^1] - controlPoints[^2]);
        return tangents;
    }

    private static List<Vector2D> GenerateCatmullRom(SplineData spline, int vertexCount)
    {
        var vertices = new List<Vector2D>();
        double distance = 0;
        var controlPointDistances = new List<double>();

        for (int i = 0; i < spline.ControlPoints.Count - 1; i++)
        {
            int samples = Math.Max((int)Vector2D.Distance(spline.ControlPoints[i], spline.ControlPoints[i + 1]), 1);
            double sectionDistance = 0;
            var sectionPoints = new List<Vector2D>();

            Vector2D p0 = spline.ControlPoints[i];
            Vector2D p1 = spline.ControlPoints[i + 1];
            Vector2D t0 = spline.Tangents[i];
            Vector2D t1 = spline.Tangents[i + 1];

            for (int k = 0; k <= samples; k++)
                sectionPoints.Add(HermiteSpline(p0, t0, p1, t1, (double)k / samples));

            for (int k = 0; k < samples; k++)
                sectionDistance += Vector2D.Distance(sectionPoints[k], sectionPoints[k + 1]);

            distance += sectionDistance;
            controlPointDistances.Add(sectionDistance);
        }

        double unitHop = distance / vertexCount;
        for (int i = 0; i <= vertexCount; i++)
        {
            int section = 0;
            double distanceFromStart = i * unitHop;
            double max = 0;

            while (max < distanceFromStart)
            {
                max += controlPointDistances[section];
                if (max < distanceFromStart) section++;

                if (section > controlPointDistances.Count - 1)
                {
                    section = controlPointDistances.Count - 1;
                    max = distanceFromStart;
                }
            }

            double u;
            if (distanceFromStart == 0) u = 0;
            else if (distanceFromStart == distance) u = 1;
            else u = 1.0 - ((max - distanceFromStart) / controlPointDistances[section]);

            Vector2D p0 = spline.ControlPoints[section];
            Vector2D p1 = spline.ControlPoints[section + 1];
            Vector2D t0 = spline.Tangents[section];
            Vector2D t1 = spline.Tangents[section + 1];
            vertices.Add(HermiteSpline(p0, t0, p1, t1, u));
        }

        return vertices;
    }

    private static Vector2D HermiteSpline(Vector2D p1, Vector2D t1, Vector2D p2, Vector2D t2, double u)
    {
        double u2 = u * u;
        double u3 = u2 * u;
        double h1 = 2 * u3 - 3 * u2 + 1;
        double h2 = -2 * u3 + 3 * u2;
        double h3 = u3 - 2 * u2 + u;
        double h4 = u3 - u2;
        return p1 * h1 + p2 * h2 + t1 * h3 + t2 * h4;
    }

    private sealed record SplineData(IReadOnlyList<Vector2D> ControlPoints, IReadOnlyList<Vector2D> Tangents);
}

public sealed record StairBuilderSectorPlan(IReadOnlyList<Vector2D> Vertices);

public sealed record StairBuilderStraightOptions
{
    public int NumberOfSectors { get; init; } = 1;
    public int SectorDepth { get; init; } = 32;
    public int Spacing { get; init; }
    public bool SideFront { get; init; } = true;
}

public sealed record StairBuilderCurvedOptions
{
    public int NumberOfSectors { get; init; } = 1;
    public int OuterVertexMultiplier { get; init; } = 1;
    public int InnerVertexMultiplier { get; init; } = 1;
    public int Flipping { get; init; }
}

public sealed record StairBuilderSplineOptions
{
    public int NumberOfSectors { get; init; } = 1;
    public int OuterVertexMultiplier { get; init; } = 1;
    public int InnerVertexMultiplier { get; init; } = 1;
    public int Flipping { get; init; }
    public int NumberOfControlPoints { get; init; } = 1;
}

public sealed record StairBuilderPrefab
{
    public string Name { get; init; } = "";
    public int NumberOfSectors { get; init; } = 1;
    public int OuterVertexMultiplier { get; init; } = 1;
    public int InnerVertexMultiplier { get; init; } = 1;
    public int StairType { get; init; }
    public int SectorDepth { get; init; } = 32;
    public int Spacing { get; init; }
    public bool FrontSide { get; init; } = true;
    public bool SingleSteps { get; init; }
    public bool DistinctSectors { get; init; }
    public bool SingleDirection { get; init; }
    public bool DistinctBaseHeights { get; init; }
    public int Flipping { get; init; }
    public int NumberOfControlPoints { get; init; } = 1;
    public bool ApplyFloorHeight { get; init; }
    public int FloorStep { get; init; }
    public bool ApplyCeilingHeight { get; init; }
    public int CeilingStep { get; init; }
    public bool ApplyFloorTexture { get; init; }
    public string FloorTexture { get; init; } = "-";
    public bool ApplyCeilingTexture { get; init; }
    public string CeilingTexture { get; init; } = "-";
    public bool ApplyUpperTexture { get; init; }
    public string UpperTexture { get; init; } = "-";
    public bool UpperUnpegged { get; init; }
    public bool ApplyMiddleTexture { get; init; }
    public string MiddleTexture { get; init; } = "-";
    public bool ApplyLowerTexture { get; init; }
    public string LowerTexture { get; init; } = "-";
    public bool LowerUnpegged { get; init; }

    public StairBuilderStraightOptions ToStraightOptions()
        => new()
        {
            NumberOfSectors = NumberOfSectors,
            SectorDepth = SectorDepth,
            Spacing = Spacing,
            SideFront = FrontSide,
        };

    public StairBuilderCurvedOptions ToCurvedOptions()
        => new()
        {
            NumberOfSectors = NumberOfSectors,
            OuterVertexMultiplier = OuterVertexMultiplier,
            InnerVertexMultiplier = InnerVertexMultiplier,
            Flipping = Flipping,
        };

    public StairBuilderSplineOptions ToSplineOptions()
        => new()
        {
            NumberOfSectors = NumberOfSectors,
            OuterVertexMultiplier = OuterVertexMultiplier,
            InnerVertexMultiplier = InnerVertexMultiplier,
            Flipping = Flipping,
            NumberOfControlPoints = NumberOfControlPoints,
        };

    public StairBuilderOptions ToBuilderOptions(int floorBase = 0, int ceilingBase = 0)
        => new()
        {
            ApplyFloorHeight = ApplyFloorHeight,
            FloorBase = floorBase,
            FloorStep = FloorStep,
            ApplyCeilingHeight = ApplyCeilingHeight,
            CeilingBase = ceilingBase,
            CeilingStep = CeilingStep,
            DistinctBaseHeights = DistinctBaseHeights,
            ApplyFloorTexture = ApplyFloorTexture,
            FloorTexture = FloorTexture,
            ApplyCeilingTexture = ApplyCeilingTexture,
            CeilingTexture = CeilingTexture,
            ApplyUpperTexture = ApplyUpperTexture,
            UpperTexture = UpperTexture,
            UpperUnpegged = UpperUnpegged,
            ApplyMiddleTexture = ApplyMiddleTexture,
            MiddleTexture = MiddleTexture,
            ApplyLowerTexture = ApplyLowerTexture,
            LowerTexture = LowerTexture,
            LowerUnpegged = LowerUnpegged,
        };

    public Dictionary<string, object> ToSettingsDictionary()
        => new()
        {
            ["name"] = Name,
            ["numberofsectors"] = NumberOfSectors,
            ["outervertexmultiplier"] = OuterVertexMultiplier,
            ["innervertexmultiplier"] = InnerVertexMultiplier,
            ["stairtype"] = StairType,
            ["sectordepth"] = SectorDepth,
            ["spacing"] = Spacing,
            ["frontside"] = FrontSide,
            ["singlesectors"] = SingleSteps,
            ["distinctsectors"] = DistinctSectors,
            ["singledirection"] = SingleDirection,
            ["distinctbaseheights"] = DistinctBaseHeights,
            ["flipping"] = Flipping,
            ["numberofcontrolpoints"] = NumberOfControlPoints,
            ["applyfloormod"] = ApplyFloorHeight,
            ["floormod"] = FloorStep,
            ["applyceilingmod"] = ApplyCeilingHeight,
            ["ceilingmod"] = CeilingStep,
            ["applyfloortexture"] = ApplyFloorTexture,
            ["floortexture"] = FloorTexture,
            ["applyceilingtexture"] = ApplyCeilingTexture,
            ["ceilingtexture"] = CeilingTexture,
            ["applyuppertexture"] = ApplyUpperTexture,
            ["uppertexture"] = UpperTexture,
            ["upperunpegged"] = UpperUnpegged,
            ["applymiddletexture"] = ApplyMiddleTexture,
            ["middletexture"] = MiddleTexture,
            ["applylowertexture"] = ApplyLowerTexture,
            ["lowertexture"] = LowerTexture,
            ["lowerunpegged"] = LowerUnpegged,
        };

    public static StairBuilderPrefab FromSettingsDictionary(IReadOnlyDictionary<string, object> settings)
        => new()
        {
            Name = ReadString(settings, "name", ""),
            NumberOfSectors = ReadInt(settings, "numberofsectors", 1),
            OuterVertexMultiplier = ReadInt(settings, "outervertexmultiplier", 1),
            InnerVertexMultiplier = ReadInt(settings, "innervertexmultiplier", 1),
            StairType = ReadInt(settings, "stairtype", 0),
            SectorDepth = ReadInt(settings, "sectordepth", 32),
            Spacing = ReadInt(settings, "spacing", 0),
            FrontSide = ReadBool(settings, "frontside", true),
            SingleSteps = ReadBool(settings, "singlesectors", false),
            DistinctSectors = ReadBool(settings, "distinctsectors", false),
            SingleDirection = ReadBool(settings, "singledirection", false),
            DistinctBaseHeights = ReadBool(settings, "distinctbaseheights", false),
            Flipping = ReadInt(settings, "flipping", 0),
            NumberOfControlPoints = ReadInt(settings, "numberofcontrolpoints", 1),
            ApplyFloorHeight = ReadBool(settings, "applyfloormod", false),
            FloorStep = ReadInt(settings, "floormod", 0),
            ApplyCeilingHeight = ReadBool(settings, "applyceilingmod", false),
            CeilingStep = ReadInt(settings, "ceilingmod", 0),
            ApplyFloorTexture = ReadBool(settings, "applyfloortexture", false),
            FloorTexture = ReadString(settings, "floortexture", "-"),
            ApplyCeilingTexture = ReadBool(settings, "applyceilingtexture", false),
            CeilingTexture = ReadString(settings, "ceilingtexture", "-"),
            ApplyUpperTexture = ReadBool(settings, "applyuppertexture", false),
            UpperTexture = ReadString(settings, "uppertexture", "-"),
            UpperUnpegged = ReadBool(settings, "upperunpegged", false),
            ApplyMiddleTexture = ReadBool(settings, "applymiddletexture", false),
            MiddleTexture = ReadString(settings, "middletexture", "-"),
            ApplyLowerTexture = ReadBool(settings, "applylowertexture", false),
            LowerTexture = ReadString(settings, "lowertexture", "-"),
            LowerUnpegged = ReadBool(settings, "lowerunpegged", false),
        };

    private static int ReadInt(IReadOnlyDictionary<string, object> settings, string key, int fallback)
        => settings.TryGetValue(key, out object? value) && value is int typed ? typed : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, object> settings, string key, bool fallback)
        => settings.TryGetValue(key, out object? value) && value is bool typed ? typed : fallback;

    private static string ReadString(IReadOnlyDictionary<string, object> settings, string key, string fallback)
        => settings.TryGetValue(key, out object? value) && value is string typed ? typed : fallback;
}

public static class StairBuilderPrefabSettings
{
    public const string DefaultPrefabName = "[Default]";
    public const string PreviousPrefabName = "[Previous]";

    public static Dictionary<string, object> ToSettingsDictionary(IReadOnlyList<StairBuilderPrefab> prefabs)
    {
        var settings = new Dictionary<string, object>();
        for (int i = 0; i < prefabs.Count; i++)
            settings["prefab" + (i + 1)] = prefabs[i].ToSettingsDictionary();

        return settings;
    }

    public static IReadOnlyList<StairBuilderPrefab> FromSettingsDictionary(IReadOnlyDictionary<string, object> settings)
    {
        var entries = new List<(int Index, IReadOnlyDictionary<string, object> Settings)>();
        foreach ((string key, object value) in settings)
        {
            if (value is IReadOnlyDictionary<string, object> prefabSettings)
            {
                int index = ReadPrefabIndex(key);
                if (index > 0) entries.Add((index, prefabSettings));
            }
        }

        entries.Sort((left, right) => left.Index.CompareTo(right.Index));

        var prefabs = new List<StairBuilderPrefab>();
        foreach ((_, IReadOnlyDictionary<string, object> prefabSettings) in entries)
            prefabs.Add(StairBuilderPrefab.FromSettingsDictionary(prefabSettings));

        return prefabs;
    }

    private static int ReadPrefabIndex(string key)
    {
        const string prefix = "prefab";
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) return -1;
        if (key.Length == prefix.Length) return -1;

        for (int i = prefix.Length; i < key.Length; i++)
        {
            char c = key[i];
            if (c < '0' || c > '9') return -1;
        }

        return int.TryParse(key.AsSpan(prefix.Length), out int index) ? index : -1;
    }

    public static string CreateSuggestedName(IReadOnlyList<StairBuilderPrefab> prefabs)
    {
        for (int i = 1; i < int.MaxValue; i++)
        {
            string name = "Prefab #" + i;
            if (!ContainsName(prefabs, name)) return name;
        }

        return "Prefab";
    }

    public static bool IsReservedManualName(string name)
    {
        string trimmed = name.Trim();
        return trimmed == DefaultPrefabName || trimmed == PreviousPrefabName;
    }

    public static int PreviousInsertPosition(IReadOnlyList<StairBuilderPrefab> prefabs)
    {
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].Name == DefaultPrefabName) return i + 1;
        }

        return 0;
    }

    public static StairBuilderPrefabSaveResult SaveManualPrefab(
        IReadOnlyList<StairBuilderPrefab> prefabs,
        StairBuilderPrefab prefab,
        bool overwriteDuplicate)
    {
        string name = prefab.Name.Trim();
        if (name.Length == 0) return new StairBuilderPrefabSaveResult(StairBuilderPrefabSaveStatus.EmptyName, prefabs, -1);
        if (IsReservedManualName(name)) return new StairBuilderPrefabSaveResult(StairBuilderPrefabSaveStatus.ReservedName, prefabs, -1);

        int overwrite = IndexOfName(prefabs, name);
        if (overwrite >= 0 && !overwriteDuplicate)
            return new StairBuilderPrefabSaveResult(StairBuilderPrefabSaveStatus.DuplicateName, prefabs, overwrite);

        return SavePrefab(prefabs, prefab with { Name = name }, overwrite >= 0, overwrite, -1);
    }

    public static StairBuilderPrefabSaveResult SaveForcedPrefab(
        IReadOnlyList<StairBuilderPrefab> prefabs,
        StairBuilderPrefab prefab,
        int position)
    {
        string name = prefab.Name.Trim();
        if (name.Length == 0) return new StairBuilderPrefabSaveResult(StairBuilderPrefabSaveStatus.EmptyName, prefabs, -1);

        int overwrite = IndexOfName(prefabs, name);
        return SavePrefab(prefabs, prefab with { Name = name }, overwrite >= 0, overwrite, position);
    }

    private static StairBuilderPrefabSaveResult SavePrefab(
        IReadOnlyList<StairBuilderPrefab> prefabs,
        StairBuilderPrefab prefab,
        bool overwriteExisting,
        int overwrite,
        int position)
    {
        var saved = new List<StairBuilderPrefab>(prefabs);
        int savedIndex;

        if (overwriteExisting)
        {
            saved.RemoveAt(overwrite);
            saved.Insert(overwrite, prefab);
            savedIndex = overwrite;
        }
        else if (position == -1)
        {
            saved.Add(prefab);
            savedIndex = saved.Count - 1;
        }
        else
        {
            saved.Insert(position, prefab);
            savedIndex = position;
        }

        return new StairBuilderPrefabSaveResult(StairBuilderPrefabSaveStatus.Saved, saved, savedIndex);
    }

    private static bool ContainsName(IReadOnlyList<StairBuilderPrefab> prefabs, string name)
        => IndexOfName(prefabs, name) >= 0;

    private static int IndexOfName(IReadOnlyList<StairBuilderPrefab> prefabs, string name)
    {
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].Name == name) return i;
        }

        return -1;
    }
}

public sealed record StairBuilderPrefabSaveResult(
    StairBuilderPrefabSaveStatus Status,
    IReadOnlyList<StairBuilderPrefab> Prefabs,
    int Index);

public enum StairBuilderPrefabSaveStatus
{
    Saved,
    EmptyName,
    ReservedName,
    DuplicateName
}

public sealed class StairBuilderOptions
{
    public bool ApplyFloorHeight { get; init; } = true;
    public int FloorBase { get; init; }
    public int FloorStep { get; init; }
    public bool ApplyCeilingHeight { get; init; }
    public int CeilingBase { get; init; }
    public int CeilingStep { get; init; }
    public bool DistinctBaseHeights { get; init; }
    public bool ApplyFloorTexture { get; init; }
    public string FloorTexture { get; init; } = "-";
    public bool ApplyCeilingTexture { get; init; }
    public string CeilingTexture { get; init; } = "-";
    public bool ApplyUpperTexture { get; init; }
    public string UpperTexture { get; init; } = "-";
    public bool UpperUnpegged { get; init; }
    public int UpperUnpeggedBit { get; init; } = StairBuilder.DefaultUpperUnpeggedBit;
    public bool ApplyMiddleTexture { get; init; }
    public string MiddleTexture { get; init; } = "-";
    public bool ApplyLowerTexture { get; init; }
    public string LowerTexture { get; init; } = "-";
    public bool LowerUnpegged { get; init; }
    public int LowerUnpeggedBit { get; init; } = StairBuilder.DefaultLowerUnpeggedBit;
}
