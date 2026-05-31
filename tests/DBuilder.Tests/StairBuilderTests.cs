// ABOUTME: Tests StairBuilder stepping floor heights and UDB-style option application.
// ABOUTME: Covers independent height bases, flat changes, wall texture filling, and unpegged flags.

using System.Collections.Generic;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class StairBuilderTests
{
    private static List<Sector> Sectors(int count)
    {
        var map = new MapSet();
        var list = new List<Sector>();
        for (int i = 0; i < count; i++)
        {
            var s = map.AddSector();
            s.FloorHeight = 0;
            s.CeilHeight = 128;
            list.Add(s);
        }
        return list;
    }

    [Fact]
    public void StepsFloorHeightsInOrder()
    {
        var s = Sectors(3);
        StairBuilder.Apply(s, startFloor: 0, step: 16, moveCeiling: false);
        Assert.Equal(0, s[0].FloorHeight);
        Assert.Equal(16, s[1].FloorHeight);
        Assert.Equal(32, s[2].FloorHeight);
    }

    [Fact]
    public void MoveCeilingPreservesHeadroom()
    {
        var s = Sectors(3); // each room is 128 tall
        StairBuilder.Apply(s, startFloor: 0, step: 16, moveCeiling: true);
        Assert.Equal(128, s[0].CeilHeight);
        Assert.Equal(144, s[1].CeilHeight); // floor 16 + 128
        Assert.Equal(160, s[2].CeilHeight); // floor 32 + 128
        Assert.Equal(128, s[2].CeilHeight - s[2].FloorHeight); // headroom preserved
    }

    [Fact]
    public void WithoutMoveCeilingTheCeilingStays()
    {
        var s = Sectors(2);
        StairBuilder.Apply(s, startFloor: 64, step: 24, moveCeiling: false);
        Assert.Equal(64, s[0].FloorHeight);
        Assert.Equal(88, s[1].FloorHeight);
        Assert.Equal(128, s[1].CeilHeight); // unchanged
    }

    [Fact]
    public void NegativeStepDescends()
    {
        var s = Sectors(3);
        StairBuilder.Apply(s, startFloor: 0, step: -8, moveCeiling: false);
        Assert.Equal(-8, s[1].FloorHeight);
        Assert.Equal(-16, s[2].FloorHeight);
    }

    [Fact]
    public void IndependentCeilingStepMatchesUdbHeightModification()
    {
        var s = Sectors(3);

        StairBuilder.Apply(s, startFloor: 16, floorStep: 8, applyCeiling: true, startCeiling: 128, ceilingStep: 4);

        Assert.Equal(new[] { 16, 24, 32 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 128, 132, 136 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void IndependentCeilingStepCanLeaveCeilingsUnchanged()
    {
        var s = Sectors(2);

        StairBuilder.Apply(s, startFloor: 32, floorStep: 16, applyCeiling: false, startCeiling: 256, ceilingStep: 32);

        Assert.Equal(32, s[0].FloorHeight);
        Assert.Equal(48, s[1].FloorHeight);
        Assert.Equal(128, s[0].CeilHeight);
        Assert.Equal(128, s[1].CeilHeight);
    }

    [Fact]
    public void OptionsUseOneBasedStepCountersWithGlobalBaseHeights()
    {
        var s = Sectors(3);

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorBase = 0,
            FloorStep = 8,
            ApplyCeilingHeight = true,
            CeilingBase = 128,
            CeilingStep = 4
        });

        Assert.Equal(new[] { 8, 16, 24 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 132, 136, 140 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void OptionsCanUseDistinctExistingBaseHeights()
    {
        var s = Sectors(2);
        s[0].FloorHeight = 16;
        s[1].FloorHeight = 48;
        s[0].CeilHeight = 128;
        s[1].CeilHeight = 192;

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorStep = 8,
            ApplyCeilingHeight = true,
            CeilingStep = -4,
            DistinctBaseHeights = true
        });

        Assert.Equal(new[] { 24, 64 }, s.ConvertAll(sector => sector.FloorHeight));
        Assert.Equal(new[] { 124, 184 }, s.ConvertAll(sector => sector.CeilHeight));
    }

    [Fact]
    public void OptionsApplyFloorAndCeilingFlats()
    {
        var s = Sectors(2);

        StairBuilder.Apply(s, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyFloorTexture = true,
            FloorTexture = "STEP1",
            ApplyCeilingTexture = true,
            CeilingTexture = "CEIL1"
        });

        Assert.All(s, sector => Assert.Equal("STEP1", sector.FloorTexture));
        Assert.All(s, sector => Assert.Equal("CEIL1", sector.CeilTexture));
    }

    [Fact]
    public void OptionsFillRequiredUpperLowerTexturesAndSetUnpeggedFlags()
    {
        var map = new MapSet();
        Sector front = map.AddSector();
        Sector back = map.AddSector();
        front.FloorHeight = 0;
        front.CeilHeight = 128;
        back.FloorHeight = 16;
        back.CeilHeight = 96;
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef frontSide = map.AddSidedef(line, true, front);
        Sidedef backSide = map.AddSidedef(line, false, back);
        map.BuildIndexes();

        StairBuilder.Apply(new[] { front }, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyUpperTexture = true,
            UpperTexture = "UPPER",
            UpperUnpegged = true,
            ApplyLowerTexture = true,
            LowerTexture = "LOWER",
            LowerUnpegged = true
        });

        Assert.Equal("UPPER", frontSide.HighTexture);
        Assert.Equal("-", backSide.HighTexture);
        Assert.Equal("LOWER", frontSide.LowTexture);
        Assert.Equal("-", backSide.LowTexture);
        Assert.Equal(StairBuilder.DefaultUpperUnpeggedBit | StairBuilder.DefaultLowerUnpeggedBit, line.Flags);
    }

    [Fact]
    public void OptionsFillRequiredMiddleTextureOnOneSidedLines()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef side = map.AddSidedef(line, true, sector);
        map.BuildIndexes();

        StairBuilder.Apply(new[] { sector }, new StairBuilderOptions
        {
            FloorStep = 0,
            ApplyMiddleTexture = true,
            MiddleTexture = "MID"
        });

        Assert.Equal("MID", side.MidTexture);
    }

    [Fact]
    public void StraightLinePlanBuildsFrontSideLoopsLikeUdb()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { line },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 2,
                SectorDepth = 32,
                SideFront = true
            });

        Assert.Equal(2, plan.Count);
        Assert.Equal(
            new[] { new Vector2D(0, 0), new Vector2D(0, -32), new Vector2D(64, -32), new Vector2D(64, 0), new Vector2D(0, 0) },
            plan[0].Vertices);
        Assert.Equal(
            new[] { new Vector2D(0, -32), new Vector2D(0, -64), new Vector2D(64, -64), new Vector2D(64, -32), new Vector2D(0, -32) },
            plan[1].Vertices);
    }

    [Fact]
    public void StraightLinePlanBuildsBackSideLoopsAndAppliesSpacing()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { line },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 2,
                SectorDepth = 32,
                Spacing = 8,
                SideFront = false
            });

        Assert.Equal(
            new[] { new Vector2D(0, 40), new Vector2D(0, 72), new Vector2D(64, 72), new Vector2D(64, 40), new Vector2D(0, 40) },
            plan[1].Vertices);
    }

    [Fact]
    public void StraightLinePlanCreatesIndependentLoopsForEachSelectedLine()
    {
        var map = new MapSet();
        Linedef first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Linedef second = map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(128, 64)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { first, second },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 1,
                SectorDepth = 16,
                SideFront = true
            });

        Assert.Equal(2, plan.Count);
        Assert.Equal(
            new[] { new Vector2D(0, 0), new Vector2D(0, -16), new Vector2D(64, -16), new Vector2D(64, 0), new Vector2D(0, 0) },
            plan[0].Vertices);
        Assert.Equal(
            new[] { new Vector2D(128, 0), new Vector2D(144, 0), new Vector2D(144, 64), new Vector2D(128, 64), new Vector2D(128, 0) },
            plan[1].Vertices);
    }

    [Fact]
    public void CreateSectorsFromPlansMaterializesLoopsAndAppliesOptions()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanStraightSectorsFromLines(
            new[] { line },
            new StairBuilderStraightOptions
            {
                NumberOfSectors = 2,
                SectorDepth = 32,
                SideFront = true
            });

        IReadOnlyList<Sector> sectors = StairBuilder.CreateSectorsFromPlans(
            map,
            plan,
            new StairBuilderOptions
            {
                FloorBase = 0,
                FloorStep = 8,
                ApplyCeilingHeight = true,
                CeilingBase = 128,
                CeilingStep = 4,
                ApplyFloorTexture = true,
                FloorTexture = "STEPFLAT"
            });

        Assert.Equal(2, sectors.Count);
        Assert.Equal(new[] { 8, 16 }, sectors.Select(sector => sector.FloorHeight));
        Assert.Equal(new[] { 132, 136 }, sectors.Select(sector => sector.CeilHeight));
        Assert.All(sectors, sector => Assert.Equal("STEPFLAT", sector.FloorTexture));
        Assert.Equal(6, map.Vertices.Count);
        Assert.Equal(7, map.Linedefs.Count);
        Assert.Contains(map.Linedefs, linedef => linedef.Front?.Sector != null && linedef.Back?.Sector != null);
    }

    [Fact]
    public void CreateSectorsFromPlansSkipsDegenerateLoops()
    {
        var map = new MapSet();
        IReadOnlyList<Sector> sectors = StairBuilder.CreateSectorsFromPlans(
            map,
            new[]
            {
                new StairBuilderSectorPlan(new[] { new Vector2D(0, 0), new Vector2D(32, 0), new Vector2D(0, 0) })
            },
            new StairBuilderOptions { FloorStep = 8 });

        Assert.Empty(sectors);
        Assert.Empty(map.Sectors);
    }

    [Fact]
    public void CurvedLinePlanRequiresAtLeastTwoSelectedLines()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanCurvedSectorsFromLines(
            new[] { line },
            new StairBuilderCurvedOptions { NumberOfSectors = 2 });

        Assert.Empty(plan);
    }

    [Fact]
    public void CurvedLinePlanBuildsClosedLoopsBetweenSelectedLines()
    {
        var map = new MapSet();
        Linedef first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Linedef second = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanCurvedSectorsFromLines(
            new[] { first, second },
            new StairBuilderCurvedOptions
            {
                NumberOfSectors = 2,
                InnerVertexMultiplier = 1,
                OuterVertexMultiplier = 1
            });

        Assert.Equal(2, plan.Count);
        Assert.Equal(5, plan[0].Vertices.Count);
        Assert.Equal(plan[0].Vertices[0], plan[0].Vertices[^1]);
        Assert.Equal(plan[1].Vertices[0], plan[1].Vertices[^1]);
        Assert.Equal(new Vector2D(64, 0), plan[0].Vertices[0]);
        Assert.Equal(new Vector2D(64, 64), plan[1].Vertices[1]);
        Assert.All(plan.SelectMany(sector => sector.Vertices), vertex => Assert.True(vertex.IsFinite()));
    }

    [Fact]
    public void CurvedLinePlanHonorsVertexMultipliers()
    {
        var map = new MapSet();
        Linedef first = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Linedef second = map.AddLinedef(map.AddVertex(new Vector2D(0, 64)), map.AddVertex(new Vector2D(64, 64)));

        IReadOnlyList<StairBuilderSectorPlan> plan = StairBuilder.PlanCurvedSectorsFromLines(
            new[] { first, second },
            new StairBuilderCurvedOptions
            {
                NumberOfSectors = 2,
                InnerVertexMultiplier = 2,
                OuterVertexMultiplier = 3
            });

        Assert.Equal(2, plan.Count);
        Assert.All(plan, sector => Assert.Equal(8, sector.Vertices.Count));
    }

    [Fact]
    public void PrefabSettingsDictionaryUsesUdbKeys()
    {
        var prefab = new StairBuilderPrefab
        {
            Name = "Test",
            NumberOfSectors = 5,
            OuterVertexMultiplier = 3,
            InnerVertexMultiplier = 2,
            StairType = 1,
            SectorDepth = 64,
            Spacing = 8,
            FrontSide = false,
            SingleSteps = true,
            DistinctSectors = true,
            SingleDirection = true,
            DistinctBaseHeights = true,
            Flipping = 2,
            NumberOfControlPoints = 4,
            ApplyFloorHeight = true,
            FloorStep = 16,
            ApplyCeilingHeight = true,
            CeilingStep = -8,
            ApplyFloorTexture = true,
            FloorTexture = "FLOOR",
            ApplyCeilingTexture = true,
            CeilingTexture = "CEIL",
            ApplyUpperTexture = true,
            UpperTexture = "UPPER",
            UpperUnpegged = true,
            ApplyMiddleTexture = true,
            MiddleTexture = "MID",
            ApplyLowerTexture = true,
            LowerTexture = "LOWER",
            LowerUnpegged = true
        };

        Dictionary<string, object> settings = prefab.ToSettingsDictionary();

        Assert.Equal("Test", settings["name"]);
        Assert.Equal(5, settings["numberofsectors"]);
        Assert.Equal(3, settings["outervertexmultiplier"]);
        Assert.Equal(2, settings["innervertexmultiplier"]);
        Assert.Equal(1, settings["stairtype"]);
        Assert.Equal(64, settings["sectordepth"]);
        Assert.Equal(8, settings["spacing"]);
        Assert.False((bool)settings["frontside"]);
        Assert.True((bool)settings["singlesectors"]);
        Assert.True((bool)settings["distinctsectors"]);
        Assert.True((bool)settings["singledirection"]);
        Assert.True((bool)settings["distinctbaseheights"]);
        Assert.Equal(2, settings["flipping"]);
        Assert.Equal(4, settings["numberofcontrolpoints"]);
        Assert.True((bool)settings["applyfloormod"]);
        Assert.Equal(16, settings["floormod"]);
        Assert.True((bool)settings["applyceilingmod"]);
        Assert.Equal(-8, settings["ceilingmod"]);
        Assert.True((bool)settings["applyfloortexture"]);
        Assert.Equal("FLOOR", settings["floortexture"]);
        Assert.True((bool)settings["applyceilingtexture"]);
        Assert.Equal("CEIL", settings["ceilingtexture"]);
        Assert.True((bool)settings["applyuppertexture"]);
        Assert.Equal("UPPER", settings["uppertexture"]);
        Assert.True((bool)settings["upperunpegged"]);
        Assert.True((bool)settings["applymiddletexture"]);
        Assert.Equal("MID", settings["middletexture"]);
        Assert.True((bool)settings["applylowertexture"]);
        Assert.Equal("LOWER", settings["lowertexture"]);
        Assert.True((bool)settings["lowerunpegged"]);
        Assert.False(settings.ContainsKey("singlesteps"));
    }

    [Fact]
    public void PrefabSettingsDictionaryRoundTripsAndUsesUdbDefaults()
    {
        var settings = new Dictionary<string, object>
        {
            ["name"] = "Loaded",
            ["singlesectors"] = true,
            ["floormod"] = 12,
            ["floortexture"] = "STEP"
        };

        StairBuilderPrefab prefab = StairBuilderPrefab.FromSettingsDictionary(settings);

        Assert.Equal("Loaded", prefab.Name);
        Assert.True(prefab.SingleSteps);
        Assert.Equal(12, prefab.FloorStep);
        Assert.Equal("STEP", prefab.FloorTexture);
        Assert.Equal(1, prefab.NumberOfSectors);
        Assert.Equal(1, prefab.OuterVertexMultiplier);
        Assert.Equal(1, prefab.InnerVertexMultiplier);
        Assert.Equal(32, prefab.SectorDepth);
        Assert.True(prefab.FrontSide);
        Assert.Equal("-", prefab.CeilingTexture);
        Assert.Equal("-", prefab.UpperTexture);
        Assert.Equal("-", prefab.MiddleTexture);
        Assert.Equal("-", prefab.LowerTexture);
    }

    [Fact]
    public void PrefabCollectionSettingsUseUdbPrefabNumberKeys()
    {
        var prefabs = new[]
        {
            new StairBuilderPrefab { Name = "[Default]", NumberOfSectors = 1 },
            new StairBuilderPrefab { Name = "[Previous]", NumberOfSectors = 3 },
            new StairBuilderPrefab { Name = "Wide", NumberOfSectors = 5 }
        };

        Dictionary<string, object> settings = StairBuilderPrefabSettings.ToSettingsDictionary(prefabs);

        Assert.Equal(new[] { "prefab1", "prefab2", "prefab3" }, settings.Keys);
        Assert.Equal("[Default]", ((Dictionary<string, object>)settings["prefab1"])["name"]);
        Assert.Equal(3, ((Dictionary<string, object>)settings["prefab2"])["numberofsectors"]);
        Assert.Equal("Wide", ((Dictionary<string, object>)settings["prefab3"])["name"]);
    }

    [Fact]
    public void PrefabCollectionSettingsLoadInStoredOrderAndIgnoreOtherKeys()
    {
        var settings = new Dictionary<string, object>
        {
            ["prefab1"] = new Dictionary<string, object> { ["name"] = "[Default]", ["numberofsectors"] = 1 },
            ["ignored"] = new Dictionary<string, object> { ["name"] = "Ignored" },
            ["prefab2"] = new Dictionary<string, object> { ["name"] = "[Previous]", ["numberofsectors"] = 4 }
        };

        IReadOnlyList<StairBuilderPrefab> prefabs = StairBuilderPrefabSettings.FromSettingsDictionary(settings);

        Assert.Equal(2, prefabs.Count);
        Assert.Equal("[Default]", prefabs[0].Name);
        Assert.Equal(1, prefabs[0].NumberOfSectors);
        Assert.Equal("[Previous]", prefabs[1].Name);
        Assert.Equal(4, prefabs[1].NumberOfSectors);
    }

    [Fact]
    public void PrefabCreatesStraightOptionsForLoadedFormState()
    {
        var prefab = new StairBuilderPrefab
        {
            NumberOfSectors = 4,
            SectorDepth = 48,
            Spacing = 12,
            FrontSide = false
        };

        StairBuilderStraightOptions options = prefab.ToStraightOptions();

        Assert.Equal(4, options.NumberOfSectors);
        Assert.Equal(48, options.SectorDepth);
        Assert.Equal(12, options.Spacing);
        Assert.False(options.SideFront);
    }

    [Fact]
    public void PrefabCreatesCurvedOptionsForLoadedFormState()
    {
        var prefab = new StairBuilderPrefab
        {
            NumberOfSectors = 4,
            OuterVertexMultiplier = 3,
            InnerVertexMultiplier = 2,
            Flipping = 1
        };

        StairBuilderCurvedOptions options = prefab.ToCurvedOptions();

        Assert.Equal(4, options.NumberOfSectors);
        Assert.Equal(3, options.OuterVertexMultiplier);
        Assert.Equal(2, options.InnerVertexMultiplier);
        Assert.Equal(1, options.Flipping);
    }

    [Fact]
    public void PrefabCreatesBuilderOptionsForLoadedFormState()
    {
        var prefab = new StairBuilderPrefab
        {
            ApplyFloorHeight = true,
            FloorStep = 8,
            ApplyCeilingHeight = true,
            CeilingStep = 4,
            DistinctBaseHeights = true,
            ApplyFloorTexture = true,
            FloorTexture = "FLOOR",
            ApplyCeilingTexture = true,
            CeilingTexture = "CEIL",
            ApplyUpperTexture = true,
            UpperTexture = "UP",
            UpperUnpegged = true,
            ApplyMiddleTexture = true,
            MiddleTexture = "MID",
            ApplyLowerTexture = true,
            LowerTexture = "LOW",
            LowerUnpegged = true
        };

        StairBuilderOptions options = prefab.ToBuilderOptions(floorBase: 24, ceilingBase: 160);

        Assert.True(options.ApplyFloorHeight);
        Assert.Equal(24, options.FloorBase);
        Assert.Equal(8, options.FloorStep);
        Assert.True(options.ApplyCeilingHeight);
        Assert.Equal(160, options.CeilingBase);
        Assert.Equal(4, options.CeilingStep);
        Assert.True(options.DistinctBaseHeights);
        Assert.True(options.ApplyFloorTexture);
        Assert.Equal("FLOOR", options.FloorTexture);
        Assert.True(options.ApplyCeilingTexture);
        Assert.Equal("CEIL", options.CeilingTexture);
        Assert.True(options.ApplyUpperTexture);
        Assert.Equal("UP", options.UpperTexture);
        Assert.True(options.UpperUnpegged);
        Assert.True(options.ApplyMiddleTexture);
        Assert.Equal("MID", options.MiddleTexture);
        Assert.True(options.ApplyLowerTexture);
        Assert.Equal("LOW", options.LowerTexture);
        Assert.True(options.LowerUnpegged);
    }
}
