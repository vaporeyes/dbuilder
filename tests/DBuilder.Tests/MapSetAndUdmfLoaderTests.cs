// ABOUTME: Tests covering the expanded Map skeleton, MapSet container, and UdmfMapLoader.
// ABOUTME: A small UDMF source maps to the expected vertex/sector/sidedef/linedef/thing topology.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapSetAndUdmfLoaderTests
{
    private const string SimpleUdmfRoom = """
        namespace = "Doom";

        vertex { x = 0;   y = 0;   }
        vertex { x = 256; y = 0;   }
        vertex { x = 256; y = 256; }
        vertex { x = 0;   y = 256; }

        sector { heightfloor = 0; heightceiling = 128; texturefloor = "FLOOR1"; textureceiling = "CEIL1"; lightlevel = 192; }

        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }

        linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
        linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
        linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
        linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }

        thing { x = 128.0; y = 128.0; angle = 90; type = 1;  skill1 = true; skill2 = true; skill3 = true; }
        thing { x = 64.0;  y = 64.0;  angle = 0;  type = 2014; }
        """;

    [Fact]
    public void LoadsBasicCounts()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out var parser);
        Assert.NotNull(map);
        Assert.Equal("Doom", map!.Namespace);
        Assert.Equal(4, map.Vertices.Count);
        Assert.Equal(1, map.Sectors.Count);
        Assert.Equal(4, map.Sidedefs.Count);
        Assert.Equal(4, map.Linedefs.Count);
        Assert.Equal(2, map.Things.Count);
    }

    [Fact]
    public void CreateElementAliasesMatchUdbCreationSurface()
    {
        var map = new MapSet();

        Vertex start = map.CreateVertex(new Vector2D(0, 0));
        Vertex end = map.CreateVertex(new Vector2D(64, 0));
        Linedef line = map.CreateLinedef(start, end);
        Sector sector = map.CreateSector();
        Sidedef side = map.CreateSidedef(line, front: true, sector);
        Thing thing = map.CreateThing();

        Assert.Same(start, map.Vertices[0]);
        Assert.Same(end, map.Vertices[1]);
        Assert.Same(line, map.Linedefs[0]);
        Assert.Same(sector, map.Sectors[0]);
        Assert.Same(side, map.Sidedefs[0]);
        Assert.Same(thing, map.Things[0]);
        Assert.Equal(0, start.Index);
        Assert.Equal(1, end.Index);
        Assert.Equal(0, line.Index);
        Assert.Equal(0, sector.Index);
        Assert.Equal(0, side.Index);
        Assert.Equal(0, thing.Index);
        Assert.Same(start, line.Start);
        Assert.Same(end, line.End);
        Assert.Same(side, line.Front);
        Assert.Same(sector, side.Sector);
        Assert.Equal(new Vector2D(), thing.Position);
        Assert.Equal(0, thing.Type);
    }

    [Fact]
    public void IndexedCreateElementAliasesUseUdbSwapPlacement()
    {
        var map = new MapSet();
        Vertex first = map.CreateVertex(new Vector2D(0, 0));
        Vertex second = map.CreateVertex(new Vector2D(64, 0));

        Vertex inserted = map.CreateVertex(0, new Vector2D(128, 0));

        Assert.Same(inserted, map.Vertices[0]);
        Assert.Same(second, map.Vertices[1]);
        Assert.Same(first, map.Vertices[2]);
        Assert.Equal(0, inserted.Index);
        Assert.Equal(1, second.Index);
        Assert.Equal(2, first.Index);

        Linedef firstLine = map.CreateLinedef(first, second);
        Linedef insertedLine = map.CreateLinedef(0, inserted, second);

        Assert.Same(insertedLine, map.Linedefs[0]);
        Assert.Same(firstLine, map.Linedefs[1]);
        Assert.Equal(0, insertedLine.Index);
        Assert.Equal(1, firstLine.Index);

        Sector firstSector = map.CreateSector();
        Sector insertedSector = map.CreateSector(0);

        Assert.Same(insertedSector, map.Sectors[0]);
        Assert.Same(firstSector, map.Sectors[1]);
        Assert.Equal(0, insertedSector.Index);
        Assert.Equal(1, firstSector.Index);

        Sidedef firstSide = map.CreateSidedef(firstLine, front: true, firstSector);
        Sidedef insertedSide = map.CreateSidedef(0, insertedLine, front: true, insertedSector);

        Assert.Same(insertedSide, map.Sidedefs[0]);
        Assert.Same(firstSide, map.Sidedefs[1]);
        Assert.Equal(0, insertedSide.Index);
        Assert.Equal(1, firstSide.Index);
        Assert.Same(insertedSide, insertedLine.Front);

        Thing firstThing = map.CreateThing();
        Thing insertedThing = map.CreateThing(0);

        Assert.Same(insertedThing, map.Things[0]);
        Assert.Same(firstThing, map.Things[1]);
        Assert.Equal(0, insertedThing.Index);
        Assert.Equal(1, firstThing.Index);
    }

    [Fact]
    public void IndexedCreateElementAliasesAppendAtCurrentCount()
    {
        var map = new MapSet();

        Vertex vertex = map.CreateVertex(0, new Vector2D(0, 0));
        Sector sector = map.CreateSector(0);
        Thing thing = map.CreateThing(0);

        Assert.Same(vertex, map.Vertices[0]);
        Assert.Same(sector, map.Sectors[0]);
        Assert.Same(thing, map.Things[0]);
    }

    [Fact]
    public void VertexPositionsRoundTrip()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        Assert.Equal(new Vector2D(0, 0),     map.Vertices[0].Position);
        Assert.Equal(new Vector2D(256, 0),   map.Vertices[1].Position);
        Assert.Equal(new Vector2D(256, 256), map.Vertices[2].Position);
        Assert.Equal(new Vector2D(0, 256),   map.Vertices[3].Position);
    }

    [Fact]
    public void SectorFieldsLoad()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        var s = map.Sectors[0];
        Assert.Equal(0, s.FloorHeight);
        Assert.Equal(128, s.CeilHeight);
        Assert.Equal("FLOOR1", s.FloorTexture);
        Assert.Equal("CEIL1", s.CeilTexture);
        Assert.Equal(192, s.Brightness);
    }

    [Fact]
    public void SidedefsBackrefSector()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        Assert.All(map.Sidedefs, sd => Assert.Same(map.Sectors[0], sd.Sector));
        Assert.All(map.Sidedefs, sd => Assert.Equal("STARTAN", sd.MidTexture));
    }

    [Fact]
    public void LinedefsLinkVerticesAndFrontSidedef()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        for (int i = 0; i < 4; i++)
        {
            var l = map.Linedefs[i];
            Assert.Same(map.Vertices[i], l.Start);
            Assert.Same(map.Vertices[(i + 1) % 4], l.End);
            Assert.NotNull(l.Front);
            Assert.Same(map.Sidedefs[i], l.Front);
            Assert.Same(l, l.Front!.Line);
            Assert.True(l.Front.IsFront);
            Assert.Null(l.Back);
            Assert.Contains("blocking", l.UdmfFlags);
        }
    }

    [Fact]
    public void ThingsLoadWithPositionTypeFlags()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        Assert.Equal(new Vector2D(128.0, 128.0), map.Things[0].Position);
        Assert.Equal(90, map.Things[0].Angle);
        Assert.Equal(1, map.Things[0].Type);
        Assert.Contains("skill1", map.Things[0].UdmfFlags);
        Assert.Contains("skill2", map.Things[0].UdmfFlags);
        Assert.Contains("skill3", map.Things[0].UdmfFlags);

        Assert.Equal(2014, map.Things[1].Type);
        Assert.Empty(map.Things[1].UdmfFlags);
    }

    [Fact]
    public void BoundsCoverVertexRange()
    {
        var map = UdmfMapLoader.Load(SimpleUdmfRoom, out _)!;
        var (minX, minY, maxX, maxY) = map.Bounds();
        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
        Assert.Equal(256, maxX);
        Assert.Equal(256, maxY);
    }

    [Fact]
    public void EmptyMapBoundsIsZero()
    {
        var map = new MapSet();
        Assert.Equal((0.0, 0.0, 0.0, 0.0), map.Bounds());
    }

    [Fact]
    public void LinedefBackSidedefIsLinked()
    {
        // Two adjacent rooms sharing a linedef with two sidedefs.
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0;   y = 0;   }
            vertex { x = 100; y = 0;   }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; lightlevel = 160; }
            sidedef { sector = 0; }
            sidedef { sector = 1; }
            linedef { v1 = 0; v2 = 1; sidefront = 0; sideback = 1; twosided = true; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;
        var l = map.Linedefs[0];
        Assert.NotNull(l.Front);
        Assert.NotNull(l.Back);
        Assert.True(l.Front!.IsFront);
        Assert.False(l.Back!.IsFront);
        Assert.Same(map.Sectors[0], l.Front.Sector);
        Assert.Same(map.Sectors[1], l.Back.Sector);
    }

    [Fact]
    public void InvalidVertexReferencesSkipLinedef()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            linedef { v1 = 0; v2 = 99; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Single(map.Vertices);
        Assert.Empty(map.Linedefs);
    }

    [Fact]
    public void ZeroLengthLinedefsAreSkipped()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 0; y = 0; }
            linedef { v1 = 0; v2 = 1; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
    }

    [Fact]
    public void NearZeroLengthLinedefsAreSkipped()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 0.00005; y = 0; }
            linedef { v1 = 0; v2 = 1; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(2, map.Vertices.Count);
        Assert.Empty(map.Linedefs);
    }

    [Fact]
    public void InvalidSectorReferencesSkipSidedef()
    {
        const string udmf = """
            namespace = "Doom";
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; }
            sidedef { sector = 99; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Single(map.Sectors);
        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void LinedefSidedefReferencesUseOriginalSidedefIndices()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 64; y = 0; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; }
            sidedef { sector = 99; texturemiddle = "BAD"; }
            sidedef { sector = 0; texturemiddle = "GOOD"; }
            linedef { v1 = 0; v2 = 1; sidefront = 1; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Single(map.Sidedefs);
        Assert.Single(map.Linedefs);
        Assert.Same(map.Sidedefs[0], map.Linedefs[0].Front);
        Assert.Equal("GOOD", map.Linedefs[0].Front!.MidTexture);
    }

    [Fact]
    public void ReusedSidedefReferenceCreatesIndependentSidedefs()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 64; y = 0; }
            vertex { x = 128; y = 0; }
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; }
            sidedef { sector = 0; texturemiddle = "SHARED"; }
            linedef { v1 = 0; v2 = 1; sidefront = 0; }
            linedef { v1 = 1; v2 = 2; sidefront = 0; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(2, map.Sidedefs.Count);
        Assert.NotSame(map.Linedefs[0].Front, map.Linedefs[1].Front);
        Assert.Same(map.Linedefs[0], map.Linedefs[0].Front!.Line);
        Assert.Same(map.Linedefs[1], map.Linedefs[1].Front!.Line);
        Assert.Equal("SHARED", map.Linedefs[0].Front!.MidTexture);
        Assert.Equal("SHARED", map.Linedefs[1].Front!.MidTexture);
    }

    [Fact]
    public void UnreferencedSidedefsAreSkipped()
    {
        const string udmf = """
            namespace = "Doom";
            sector { heightfloor = 0; heightceiling = 64; texturefloor = "A"; textureceiling = "B"; }
            sidedef { sector = 0; texturemiddle = "ORPHAN"; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void MoreIdsSkipZeroDuplicatesAndDropPrimaryZero()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 64; y = 0; }
            sector { id = 0; moreids = "0 6 6 7"; }
            linedef { v1 = 0; v2 = 1; id = 5; moreids = "5 0 8 8"; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(new[] { 6, 7 }, map.Sectors[0].Tags);
        Assert.Equal(new[] { 5, 8 }, map.Linedefs[0].Tags);
    }

    [Fact]
    public void MoreIdsUseUdbSpaceOnlyTokenSeparator()
    {
        const string udmf = """
            namespace = "Doom";
            vertex { x = 0; y = 0; }
            vertex { x = 64; y = 0; }
            sector { id = 5; moreids = "6,7 8	9"; }
            linedef { v1 = 0; v2 = 1; id = 11; moreids = "12,13 14	15"; }
            """;

        var map = UdmfMapLoader.Load(udmf, out _)!;

        Assert.Equal(new[] { 5 }, map.Sectors[0].Tags);
        Assert.Equal(new[] { 11 }, map.Linedefs[0].Tags);
    }

    [Fact]
    public void MalformedUdmfReturnsNull()
    {
        var map = UdmfMapLoader.Load("namespace = wat;", out var parser);
        Assert.Null(map);
        Assert.NotEqual(0, parser.ErrorResult);
    }
}
