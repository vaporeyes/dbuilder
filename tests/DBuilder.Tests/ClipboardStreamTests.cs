// ABOUTME: Tests for ClipboardStreamWriter/Reader binary copy-paste format.
// ABOUTME: Round-trips entire maps and selection subsets; verifies append semantics, args/flags preservation, two-sided line linking.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ClipboardStreamTests
{
    private static MapSet BuildSampleMap()
    {
        var map = new MapSet { Namespace = "Doom" };
        var sA = new Sector { Index = 0, FloorHeight = 0, CeilHeight = 128, FloorTexture = "FLOOR1", CeilTexture = "CEIL1", Brightness = 192, Tag = 7 };
        sA.UdmfFlags.Add("secret");
        var sB = new Sector { Index = 1, FloorHeight = 8, CeilHeight = 120, FloorTexture = "FLOOR2", CeilTexture = "CEIL2", Brightness = 160, Special = 9 };
        map.Sectors.Add(sA); map.Sectors.Add(sB);

        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(100, 0));
        var v2 = new Vertex(new Vector2D(100, 100));
        var v3 = new Vertex(new Vector2D(0, 100));
        map.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        Linedef MakeLine(Vertex a, Vertex b, Sector secFront, Sector? secBack = null)
        {
            var l = new Linedef(a, b) { Flags = 0x0001, Action = 80, Tag = 3 };
            l.Args[0] = 11; l.Args[3] = 99;
            l.UdmfFlags.Add("blocking");
            var front = new Sidedef(l, true) { Sector = secFront, HighTexture = "HI", MidTexture = "MID", LowTexture = "LO", OffsetX = 4, OffsetY = 8 };
            front.UdmfFlags.Add("lightabsolute");
            l.Front = front;
            map.Sidedefs.Add(front);
            if (secBack != null)
            {
                var back = new Sidedef(l, false) { Sector = secBack, MidTexture = "BACK" };
                l.Back = back;
                map.Sidedefs.Add(back);
            }
            map.Linedefs.Add(l);
            return l;
        }
        MakeLine(v0, v1, sA);
        MakeLine(v1, v2, sA, sB); // two-sided
        MakeLine(v2, v3, sA);
        MakeLine(v3, v0, sA);

        var t = new Thing { Position = new Vector2D(50, 50), Height = 16, Angle = 90, Type = 3001, Tag = 42, Flags = 0x0007, Action = 12 };
        t.Args[1] = 50;
        t.UdmfFlags.Add("skill3");
        t.UdmfFlags.Add("ambush");
        map.Things.Add(t);

        map.BuildIndexes();
        return map;
    }

    [Fact]
    public void RoundTripWholeMapPreservesCounts()
    {
        var src = BuildSampleMap();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;

        var dst = new MapSet();
        var result = ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(src.Vertices.Count, dst.Vertices.Count);
        Assert.Equal(src.Sectors.Count,  dst.Sectors.Count);
        Assert.Equal(src.Sidedefs.Count, dst.Sidedefs.Count);
        Assert.Equal(src.Linedefs.Count, dst.Linedefs.Count);
        Assert.Equal(src.Things.Count,   dst.Things.Count);

        Assert.Equal(src.Vertices.Count, result.VertexCount);
        Assert.Equal(0, result.FirstVertex);
    }

    [Fact]
    public void RoundTripPreservesTopology()
    {
        var src = BuildSampleMap();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        for (int i = 0; i < src.Vertices.Count; i++)
            Assert.Equal(src.Vertices[i].Position, dst.Vertices[i].Position);

        // Sectors
        for (int i = 0; i < src.Sectors.Count; i++)
        {
            var so = src.Sectors[i];
            var sd = dst.Sectors[i];
            Assert.Equal(so.FloorHeight,  sd.FloorHeight);
            Assert.Equal(so.CeilHeight,   sd.CeilHeight);
            Assert.Equal(so.FloorTexture, sd.FloorTexture);
            Assert.Equal(so.CeilTexture,  sd.CeilTexture);
            Assert.Equal(so.Brightness,   sd.Brightness);
            Assert.Equal(so.Special,      sd.Special);
            Assert.Equal(so.Tag,          sd.Tag);
            Assert.Equal(so.UdmfFlags.OrderBy(s => s), sd.UdmfFlags.OrderBy(s => s));
        }

        // Sidedefs back-link to their cloned sectors
        for (int i = 0; i < src.Sidedefs.Count; i++)
        {
            int srcSectorIdx = src.Sectors.IndexOf(src.Sidedefs[i].Sector!);
            Assert.Same(dst.Sectors[srcSectorIdx], dst.Sidedefs[i].Sector);
            Assert.Equal(src.Sidedefs[i].HighTexture, dst.Sidedefs[i].HighTexture);
            Assert.Equal(src.Sidedefs[i].MidTexture,  dst.Sidedefs[i].MidTexture);
            Assert.Equal(src.Sidedefs[i].LowTexture,  dst.Sidedefs[i].LowTexture);
            Assert.Equal(src.Sidedefs[i].UdmfFlags.OrderBy(s => s), dst.Sidedefs[i].UdmfFlags.OrderBy(s => s));
        }

        // Linedefs - args, flags, vertex refs, sidedef refs
        for (int i = 0; i < src.Linedefs.Count; i++)
        {
            var lo = src.Linedefs[i];
            var ld = dst.Linedefs[i];
            Assert.Equal(lo.Flags,  ld.Flags);
            Assert.Equal(lo.Action, ld.Action);
            Assert.Equal(lo.Tag,    ld.Tag);
            for (int a = 0; a < 5; a++) Assert.Equal(lo.Args[a], ld.Args[a]);
            Assert.Equal(lo.UdmfFlags.OrderBy(s => s), ld.UdmfFlags.OrderBy(s => s));

            Assert.Same(dst.Vertices[src.Vertices.IndexOf(lo.Start)], ld.Start);
            Assert.Same(dst.Vertices[src.Vertices.IndexOf(lo.End)],   ld.End);

            if (lo.Front == null) Assert.Null(ld.Front);
            else Assert.Same(dst.Sidedefs[src.Sidedefs.IndexOf(lo.Front)], ld.Front);
            if (lo.Back == null) Assert.Null(ld.Back);
            else Assert.Same(dst.Sidedefs[src.Sidedefs.IndexOf(lo.Back)], ld.Back);
        }

        // Things - position, height, args, flags
        var to = src.Things[0];
        var td = dst.Things[0];
        Assert.Equal(to.Position, td.Position);
        Assert.Equal(to.Height,   td.Height);
        Assert.Equal(to.Angle,    td.Angle);
        Assert.Equal(to.Type,     td.Type);
        Assert.Equal(to.Tag,      td.Tag);
        Assert.Equal(to.Action,   td.Action);
        Assert.Equal(to.Flags,    td.Flags);
        for (int a = 0; a < 5; a++) Assert.Equal(to.Args[a], td.Args[a]);
        Assert.Equal(to.UdmfFlags.OrderBy(s => s), td.UdmfFlags.OrderBy(s => s));
    }

    [Fact]
    public void TwoSidedLineRoundTripsBothSidedefs()
    {
        var src = BuildSampleMap();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        var l = dst.Linedefs[1]; // the two-sided one in the fixture
        Assert.NotNull(l.Front);
        Assert.NotNull(l.Back);
        Assert.Same(dst.Sectors[0], l.Front!.Sector);
        Assert.Same(dst.Sectors[1], l.Back!.Sector);
        Assert.True(l.Front.IsFront);
        Assert.False(l.Back.IsFront);
    }

    [Fact]
    public void PasteAppendsToExistingMap()
    {
        // Build a destination with some pre-existing content, paste a copy of the sample on top,
        // and verify the paste lands *after* the existing elements.
        var dst = BuildSampleMap();
        int v0 = dst.Vertices.Count;
        int s0 = dst.Sectors.Count;
        int sd0 = dst.Sidedefs.Count;
        int l0 = dst.Linedefs.Count;
        int t0 = dst.Things.Count;

        var src = BuildSampleMap();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;

        var result = ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(v0, result.FirstVertex);
        Assert.Equal(s0, result.FirstSector);
        Assert.Equal(sd0, result.FirstSidedef);
        Assert.Equal(l0, result.FirstLinedef);
        Assert.Equal(t0, result.FirstThing);
        Assert.Equal(v0 + src.Vertices.Count, dst.Vertices.Count);

        // The pasted linedefs reference pasted vertices, not original ones.
        Assert.Same(dst.Vertices[v0], dst.Linedefs[l0].Start);
    }

    [Fact]
    public void SelectionSubsetWriteWorks()
    {
        // Copy only the two-sided line and what it transitively needs.
        var src = BuildSampleMap();
        var line = src.Linedefs[1];
        var verts = new List<Vertex> { line.Start, line.End };
        var sides = new List<Sidedef>();
        if (line.Front != null) sides.Add(line.Front);
        if (line.Back != null)  sides.Add(line.Back);
        var sectors = sides.Where(s => s.Sector != null).Select(s => s.Sector!).Distinct().ToList();

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(verts, new[] { line }, sides, sectors, System.Array.Empty<Thing>(), ms);
        ms.Position = 0;

        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(2, dst.Vertices.Count);
        Assert.Equal(2, dst.Sectors.Count);
        Assert.Equal(2, dst.Sidedefs.Count);
        Assert.Single(dst.Linedefs);
        Assert.Empty(dst.Things);

        var l = dst.Linedefs[0];
        Assert.NotNull(l.Front);
        Assert.NotNull(l.Back);
        Assert.Same(dst.Sidedefs[0], l.Front);
        Assert.Same(dst.Sidedefs[1], l.Back);
    }

    [Fact]
    public void CustomFieldsRoundTrip()
    {
        var src = new MapSet();
        var sector = new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" };
        sector.Fields["lightcolor"] = 16711680;       // int
        sector.Fields["xscalefloor"] = 2.5;           // double
        sector.Fields["hidden"] = true;               // bool
        sector.Fields["comment"] = "lava";            // string
        src.Sectors.Add(sector);
        var v = new Vertex(new Vector2D(0, 0));
        v.Fields["zfloor"] = -8.0;
        src.Vertices.Add(v);

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        var rs = dst.Sectors[0];
        Assert.Equal(16711680, (int)rs.Fields["lightcolor"]);
        Assert.Equal(2.5, (double)rs.Fields["xscalefloor"]);
        Assert.True((bool)rs.Fields["hidden"]);
        Assert.Equal("lava", (string)rs.Fields["comment"]);
        Assert.Equal(-8.0, (double)dst.Vertices[0].Fields["zfloor"]);
    }

    [Fact]
    public void ThingPitchRollScaleRoundTrip()
    {
        var src = new MapSet();
        src.Things.Add(new Thing
        {
            Position = new Vector2D(10, 20),
            Type = 3001,
            Pitch = 30,
            Roll = 45,
            ScaleX = 2.0,
            ScaleY = 0.5,
        });

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        var t = dst.Things[0];
        Assert.Equal(30, t.Pitch);
        Assert.Equal(45, t.Roll);
        Assert.Equal(2.0, t.ScaleX);
        Assert.Equal(0.5, t.ScaleY);
    }

    [Fact]
    public void VertexZAndSlopesRoundTrip()
    {
        var src = new MapSet();
        var v = new Vertex(new Vector2D(0, 0)) { ZFloor = -8.0, ZCeiling = 200.0 };
        src.Vertices.Add(v);
        var s = new Sector
        {
            Index = 0,
            FloorTexture = "-",
            CeilTexture = "-",
            FloorSlope = new Vector3D(0, 0.7071, 0.7071),
            FloorSlopeOffset = -32.0,
        };
        src.Sectors.Add(s);

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(-8.0, dst.Vertices[0].ZFloor);
        Assert.Equal(200.0, dst.Vertices[0].ZCeiling);
        Assert.Equal(-32.0, dst.Sectors[0].FloorSlopeOffset);
        Assert.Equal(0.7071, dst.Sectors[0].FloorSlope.z, 1e-9);
        // Unset ceiling slope round-trips as zero vector + NaN offset.
        Assert.Equal(0, dst.Sectors[0].CeilSlope.GetLengthSq());
        Assert.True(double.IsNaN(dst.Sectors[0].CeilSlopeOffset));
    }

    [Fact]
    public void MultiTagsRoundTrip()
    {
        var src = new MapSet();
        var sector = new Sector { Index = 0, FloorTexture = "-", CeilTexture = "-" };
        sector.Tags.AddRange(new[] { 5, 6, 7 });
        src.Sectors.Add(sector);
        var v0 = new Vertex(new Vector2D(0, 0));
        var v1 = new Vertex(new Vector2D(10, 0));
        src.Vertices.Add(v0); src.Vertices.Add(v1);
        var l = new Linedef(v0, v1);
        l.Tags.AddRange(new[] { 3, 9 });
        var sd = new Sidedef(l, true) { Sector = sector };
        l.Front = sd;
        src.Sidedefs.Add(sd); src.Linedefs.Add(l);

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(new[] { 5, 6, 7 }, dst.Sectors[0].Tags);
        Assert.Equal(new[] { 3, 9 }, dst.Linedefs[0].Tags);
    }

    [Fact]
    public void SelectionGroupsRoundTrip()
    {
        var src = BuildSampleMap();
        src.Vertices[0].Groups = MapSet.GroupMask(1);
        src.Sectors[0].Groups = MapSet.GroupMask(2);
        src.Linedefs[0].Groups = MapSet.GroupMask(3);
        src.Things[0].Groups = MapSet.GroupMask(4);

        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(MapSet.GroupMask(1), dst.Vertices[0].Groups);
        Assert.Equal(MapSet.GroupMask(2), dst.Sectors[0].Groups);
        Assert.Equal(MapSet.GroupMask(3), dst.Linedefs[0].Groups);
        Assert.Equal(MapSet.GroupMask(4), dst.Things[0].Groups);
    }

    [Fact]
    public void EmptyMapRoundTripsCleanly()
    {
        var src = new MapSet();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        ms.Position = 0;
        var dst = new MapSet();
        var r = ClipboardStreamReader.Read(dst, ms);
        Assert.Equal(0, r.VertexCount);
        Assert.Equal(0, r.SectorCount);
        Assert.Equal(0, r.LinedefCount);
        Assert.Equal(0, r.ThingCount);
    }

    [Fact]
    public void InvalidVertexReferencesSkipLinedefAndSidedef()
    {
        var ms = BuildMalformedClipboard(lineV2: 99);

        var dst = new MapSet();
        var result = ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(2, result.VertexCount);
        Assert.Empty(dst.Linedefs);
        Assert.Empty(dst.Sidedefs);
    }

    [Fact]
    public void ZeroLengthLinedefsAreSkippedWithSidedefs()
    {
        var ms = BuildMalformedClipboard(secondVertexX: 0);

        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Equal(2, dst.Vertices.Count);
        Assert.Empty(dst.Linedefs);
        Assert.Empty(dst.Sidedefs);
    }

    [Fact]
    public void InvalidSectorReferencesSkipClipboardSidedef()
    {
        var ms = BuildMalformedClipboard(sidedefSector: 99);

        var dst = new MapSet();
        ClipboardStreamReader.Read(dst, ms);

        Assert.Single(dst.Linedefs);
        Assert.Empty(dst.Sidedefs);
        Assert.Null(dst.Linedefs[0].Front);
    }

    [Fact]
    public void StreamPositionAdvancesPastBlob()
    {
        // After reading, the stream should be positioned at the end of the written data
        // so callers can verify "no trailing garbage" with stream.Position == stream.Length.
        var src = BuildSampleMap();
        var ms = new MemoryStream();
        ClipboardStreamWriter.Write(src, ms);
        long written = ms.Position;
        ms.Position = 0;
        ClipboardStreamReader.Read(new MapSet(), ms);
        Assert.Equal(written, ms.Position);
    }

    private static MemoryStream BuildMalformedClipboard(int lineV2 = 1, double secondVertexX = 64, int sidedefSector = 0)
    {
        var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        w.Write(2); // header vertices
        w.Write(1); // header sectors
        w.Write(1); // header linedefs
        w.Write(0); // header things

        w.Write(2); // vertices
        WriteVertex(w, 0, 0);
        WriteVertex(w, secondVertexX, 0);

        w.Write(1); // sectors
        w.Write(0); // special
        w.Write(0); // floor height
        w.Write(64); // ceiling height
        w.Write(160); // brightness
        WriteTags(w);
        WriteString(w, "-");
        WriteString(w, "-");
        for (int i = 0; i < 8; i++) w.Write(double.NaN);
        w.Write(0); // groups
        w.Write(0); // sector udmf flags
        WriteCustomFields(w);

        w.Write(1); // sidedefs
        w.Write(0); // offset x
        w.Write(0); // offset y
        w.Write(sidedefSector);
        WriteString(w, "-");
        WriteString(w, "MID");
        WriteString(w, "-");
        w.Write(0); // sidedef udmf flags
        WriteCustomFields(w);

        w.Write(1); // linedefs
        w.Write(0); // v1
        w.Write(lineV2);
        w.Write(0); // sidefront
        w.Write(-1); // sideback
        w.Write(0); // action
        for (int i = 0; i < 5; i++) w.Write(0); // args
        w.Write(0); // flags
        WriteTags(w);
        w.Write(0); // groups
        w.Write(0); // udmf flags
        WriteCustomFields(w);

        w.Write(0); // things
        w.Flush();
        ms.Position = 0;
        return ms;
    }

    private static void WriteVertex(BinaryWriter w, double x, double y)
    {
        w.Write(x);
        w.Write(y);
        w.Write(double.NaN);
        w.Write(double.NaN);
        w.Write(0); // groups
        WriteCustomFields(w);
    }

    private static void WriteString(BinaryWriter w, string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    private static void WriteTags(BinaryWriter w, params int[] tags)
    {
        w.Write(tags.Length);
        foreach (int tag in tags) w.Write(tag);
    }

    private static void WriteCustomFields(BinaryWriter w)
        => w.Write(0);
}
