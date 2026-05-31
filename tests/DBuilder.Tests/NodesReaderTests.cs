// ABOUTME: Tests the vanilla NODES lump parser and classic NodesViewer structure reader.
// ABOUTME: Covers partition overlays, child flags, parent links, segs, vertices, and subsector links.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DBuilder.Geometry;
using DBuilder.IO;

namespace DBuilder.Tests;

public class NodesReaderTests
{
    private static byte[] Record(short x, short y, short dx, short dy)
    {
        var b = new byte[28]; // x,y,dx,dy then 20 bytes of bboxes+children (zeros are fine for parsing)
        BitConverter.GetBytes(x).CopyTo(b, 0);
        BitConverter.GetBytes(y).CopyTo(b, 2);
        BitConverter.GetBytes(dx).CopyTo(b, 4);
        BitConverter.GetBytes(dy).CopyTo(b, 6);
        return b;
    }

    private static byte[] NodeRecord(
        short x,
        short y,
        short dx,
        short dy,
        ushort rightChild,
        ushort leftChild)
    {
        var b = Record(x, y, dx, dy);
        BitConverter.GetBytes((short)10).CopyTo(b, 8);
        BitConverter.GetBytes((short)-10).CopyTo(b, 10);
        BitConverter.GetBytes((short)-20).CopyTo(b, 12);
        BitConverter.GetBytes((short)20).CopyTo(b, 14);
        BitConverter.GetBytes((short)30).CopyTo(b, 16);
        BitConverter.GetBytes((short)-30).CopyTo(b, 18);
        BitConverter.GetBytes((short)-40).CopyTo(b, 20);
        BitConverter.GetBytes((short)40).CopyTo(b, 22);
        BitConverter.GetBytes(rightChild).CopyTo(b, 24);
        BitConverter.GetBytes(leftChild).CopyTo(b, 26);
        return b;
    }

    private static byte[] SegRecord(ushort startVertex, ushort endVertex, short angle, ushort lineIndex, short side, short offset)
    {
        var b = new byte[12];
        BitConverter.GetBytes(startVertex).CopyTo(b, 0);
        BitConverter.GetBytes(endVertex).CopyTo(b, 2);
        BitConverter.GetBytes(angle).CopyTo(b, 4);
        BitConverter.GetBytes(lineIndex).CopyTo(b, 6);
        BitConverter.GetBytes(side).CopyTo(b, 8);
        BitConverter.GetBytes(offset).CopyTo(b, 10);
        return b;
    }

    private static byte[] VertexRecord(short x, short y)
    {
        var b = new byte[4];
        BitConverter.GetBytes(x).CopyTo(b, 0);
        BitConverter.GetBytes(y).CopyTo(b, 2);
        return b;
    }

    private static byte[] SubsectorRecord(ushort segCount, ushort firstSeg)
    {
        var b = new byte[4];
        BitConverter.GetBytes(segCount).CopyTo(b, 0);
        BitConverter.GetBytes(firstSeg).CopyTo(b, 2);
        return b;
    }

    private static byte[] ZNodesLump(string header, byte[] payload)
    {
        byte[] bytes = new byte[4 + payload.Length];
        Encoding.ASCII.GetBytes(header).CopyTo(bytes, 0);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] CompressedZNodesLump(string header, byte[] payload)
    {
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes(header));
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(payload);
        return output.ToArray();
    }

    [Fact]
    public void ParsesPartitionSegmentEndpoints()
    {
        var parts = NodesReader.Parse(Record(100, 200, 50, -30));
        Assert.Single(parts);
        Assert.Equal(100, parts[0].X1);
        Assert.Equal(200, parts[0].Y1);
        Assert.Equal(150, parts[0].X2);  // x + dx
        Assert.Equal(170, parts[0].Y2);  // y + dy
    }

    [Fact]
    public void ParsesMultipleRecords()
    {
        var data = new byte[56];
        Record(0, 0, 64, 0).CopyTo(data, 0);
        Record(10, 10, 0, 64).CopyTo(data, 28);
        var parts = NodesReader.Parse(data);
        Assert.Equal(2, parts.Count);
        Assert.Equal(64, parts[0].X2);
        Assert.Equal(74, parts[1].Y2);
    }

    [Fact]
    public void EmptyOrShortDataYieldsNone()
    {
        Assert.Empty(NodesReader.Parse(System.Array.Empty<byte>()));
        Assert.Empty(NodesReader.Parse(new byte[10])); // less than one record
    }

    [Fact]
    public void ClassicStructuresParseNodeChildrenAndParents()
    {
        var nodes = new byte[56];
        NodeRecord(0, 0, 64, 0, 0x8002, 1).CopyTo(nodes, 0);
        NodeRecord(10, 20, 0, -32, 0x8000, 0x8001).CopyTo(nodes, 28);

        var result = NodesReader.ParseClassicStructures(
            nodes,
            SegRecord(0, 1, 0, 3, 1, -16),
            VertexRecord(-128, 256),
            SubsectorRecord(1, 0));

        Assert.True(result.IsValid);
        Assert.Equal(ClassicNodesStatus.Ok, result.Status);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(-1, result.Nodes[0].ParentIndex);
        Assert.Equal(0, result.Nodes[1].ParentIndex);
        Assert.Equal(2, result.Nodes[0].RightChildIndex);
        Assert.True(result.Nodes[0].RightChildIsSubsector);
        Assert.Equal(1, result.Nodes[0].LeftChildIndex);
        Assert.False(result.Nodes[0].LeftChildIsSubsector);
        Assert.Equal(new NodePartition(0, 0, 64, 0), result.Nodes[0].Partition);
        Assert.Equal(new ClassicNodeBounds(10, -10, -20, 20), result.Nodes[0].RightBounds);
        Assert.Equal(new ClassicNodeBounds(30, -30, -40, 40), result.Nodes[0].LeftBounds);
    }

    [Fact]
    public void ClassicStructuresParseSegsVerticesAndSubsectorLinks()
    {
        var nodes = NodeRecord(0, 0, 64, 0, 0x8000, 0x8001);
        var segs = new byte[36];
        SegRecord(0, 1, 0, 3, 1, -16).CopyTo(segs, 0);
        SegRecord(1, 2, 182, 4, 0, 24).CopyTo(segs, 12);
        SegRecord(2, 0, -182, 5, 1, 48).CopyTo(segs, 24);
        var vertices = new byte[12];
        VertexRecord(-128, 256).CopyTo(vertices, 0);
        VertexRecord(64, -32).CopyTo(vertices, 4);
        VertexRecord(99, 101).CopyTo(vertices, 8);
        var subsectors = new byte[8];
        SubsectorRecord(2, 0).CopyTo(subsectors, 0);
        SubsectorRecord(1, 2).CopyTo(subsectors, 4);

        var result = NodesReader.ParseClassicStructures(nodes, segs, vertices, subsectors);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Segs.Count);
        Assert.Equal(0, result.Segs[0].SubsectorIndex);
        Assert.Equal(0, result.Segs[1].SubsectorIndex);
        Assert.Equal(1, result.Segs[2].SubsectorIndex);
        Assert.Equal(Math.PI / 2, result.Segs[0].AngleRadians, precision: 6);
        Assert.Equal(3, result.Segs[0].LineIndex);
        Assert.True(result.Segs[0].LeftSide);
        Assert.Equal(-16, result.Segs[0].Offset);
        Assert.Equal(new ClassicNodeVertex(-128, 256), result.Vertices[0]);
        Assert.Equal(new ClassicSubsector(2, 0), result.Subsectors[0]);
    }

    [Fact]
    public void ClassicStructuresRejectUnsupportedCompressedHeaders()
    {
        byte[] nodes = Encoding.ASCII.GetBytes("ZNOD");

        var result = NodesReader.ParseClassicStructures(
            nodes,
            SegRecord(0, 1, 0, 0, 0, 0),
            VertexRecord(0, 0),
            SubsectorRecord(1, 0));

        Assert.False(result.IsValid);
        Assert.Equal(ClassicNodesStatus.UnsupportedCompressedNodes, result.Status);
    }

    [Theory]
    [InlineData("XNOD")]
    [InlineData("XGLN")]
    [InlineData("XGL2")]
    [InlineData("XGL3")]
    [InlineData("ZNOD")]
    [InlineData("ZGLN")]
    [InlineData("ZGL2")]
    [InlineData("ZGL3")]
    public void RecognizesSupportedZNodesHeaders(string header)
    {
        Assert.True(NodesReader.HasSupportedZNodesHeader(ZNodesLump(header, Array.Empty<byte>())));
    }

    [Fact]
    public void ExtractZNodesPayloadCopiesUncompressedPayload()
    {
        byte[] lump = ZNodesLump("XGL3", new byte[] { 1, 2, 3, 4 });

        ZNodesPayload payload = NodesReader.ExtractZNodesPayload(lump);

        Assert.True(payload.IsValid);
        Assert.Equal(ZNodesPayloadStatus.Ok, payload.Status);
        Assert.Equal("XGL3", payload.Format);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, payload.Data);
        Assert.Null(payload.Error);
    }

    [Fact]
    public void ExtractZNodesPayloadDecompressesCompressedPayload()
    {
        byte[] lump = CompressedZNodesLump("ZGLN", new byte[] { 9, 8, 7 });

        ZNodesPayload payload = NodesReader.ExtractZNodesPayload(lump);

        Assert.True(payload.IsValid);
        Assert.Equal("ZGLN", payload.Format);
        Assert.Equal(new byte[] { 9, 8, 7 }, payload.Data);
    }

    [Fact]
    public void ExtractZNodesPayloadReportsUnsupportedHeader()
    {
        ZNodesPayload payload = NodesReader.ExtractZNodesPayload(ZNodesLump("ABCD", Array.Empty<byte>()));

        Assert.False(payload.IsValid);
        Assert.Equal(ZNodesPayloadStatus.UnsupportedHeader, payload.Status);
        Assert.Equal("ABCD", payload.Format);
    }

    [Fact]
    public void ExtractZNodesPayloadReportsDecompressionFailure()
    {
        ZNodesPayload payload = NodesReader.ExtractZNodesPayload(ZNodesLump("ZNOD", new byte[] { 1, 2, 3 }));

        Assert.False(payload.IsValid);
        Assert.Equal(ZNodesPayloadStatus.DecompressionFailed, payload.Status);
        Assert.Equal("ZNOD", payload.Format);
        Assert.NotNull(payload.Error);
    }

    [Fact]
    public void ClassicStructuresRejectInvalidSubsectorRanges()
    {
        var result = NodesReader.ParseClassicStructures(
            NodeRecord(0, 0, 64, 0, 0x8000, 0x8001),
            SegRecord(0, 1, 0, 0, 0, 0),
            VertexRecord(0, 0),
            SubsectorRecord(2, 0));

        Assert.False(result.IsValid);
        Assert.Equal(ClassicNodesStatus.InvalidSubsectorSegRange, result.Status);
    }

    [Fact]
    public void ClassicSubsectorPolygonsClipByBspSplits()
    {
        var segs = new byte[24];
        SegRecord(10, 11, 0, 0, 0, 0).CopyTo(segs, 0);
        SegRecord(10, 11, 0, 0, 0, 0).CopyTo(segs, 12);
        var subsectors = new byte[8];
        SubsectorRecord(1, 0).CopyTo(subsectors, 0);
        SubsectorRecord(1, 1).CopyTo(subsectors, 4);
        ClassicNodesStructure structure = NodesReader.ParseClassicStructures(
            NodeRecord(0, 0, 0, 64, 0x8000, 0x8001),
            segs,
            VertexRecord(0, 0),
            subsectors);

        IReadOnlyList<ClassicSubsectorPolygon> polygons = NodesReader.BuildClassicSubsectorPolygons(structure, 64);

        Assert.Equal(2, polygons.Count);
        AssertPoints(polygons[0].Points, new Vector2D(0, 64), new Vector2D(64, 64), new Vector2D(64, -64), new Vector2D(0, -64));
        AssertPoints(polygons[1].Points, new Vector2D(-64, 64), new Vector2D(0, 64), new Vector2D(0, -64), new Vector2D(-64, -64));
    }

    [Fact]
    public void ClassicSubsectorPolygonsClipBySubsectorSegs()
    {
        var segs = new byte[24];
        SegRecord(0, 1, 0, 0, 0, 0).CopyTo(segs, 0);
        SegRecord(10, 11, 0, 0, 0, 0).CopyTo(segs, 12);
        var vertices = new byte[8];
        VertexRecord(32, -64).CopyTo(vertices, 0);
        VertexRecord(32, 64).CopyTo(vertices, 4);
        var subsectors = new byte[8];
        SubsectorRecord(1, 0).CopyTo(subsectors, 0);
        SubsectorRecord(1, 1).CopyTo(subsectors, 4);
        ClassicNodesStructure structure = NodesReader.ParseClassicStructures(
            NodeRecord(0, 0, 0, 64, 0x8000, 0x8001),
            segs,
            vertices,
            subsectors);

        IReadOnlyList<ClassicSubsectorPolygon> polygons = NodesReader.BuildClassicSubsectorPolygons(structure, 64);

        AssertPoints(polygons[0].Points, new Vector2D(32, 64), new Vector2D(64, 64), new Vector2D(64, -64), new Vector2D(32, -64));
        AssertPoints(polygons[1].Points, new Vector2D(-64, 64), new Vector2D(0, 64), new Vector2D(0, -64), new Vector2D(-64, -64));
    }

    [Fact]
    public void FindsClassicSubsectorAtPointByTreeAndPolygon()
    {
        var segs = new byte[24];
        SegRecord(0, 1, 0, 0, 0, 0).CopyTo(segs, 0);
        SegRecord(10, 11, 0, 0, 0, 0).CopyTo(segs, 12);
        var vertices = new byte[8];
        VertexRecord(32, -64).CopyTo(vertices, 0);
        VertexRecord(32, 64).CopyTo(vertices, 4);
        var subsectors = new byte[8];
        SubsectorRecord(1, 0).CopyTo(subsectors, 0);
        SubsectorRecord(1, 1).CopyTo(subsectors, 4);
        ClassicNodesStructure structure = NodesReader.ParseClassicStructures(
            NodeRecord(0, 0, 0, 64, 0x8000, 0x8001),
            segs,
            vertices,
            subsectors);
        IReadOnlyList<ClassicSubsectorPolygon> polygons = NodesReader.BuildClassicSubsectorPolygons(structure, 64);

        Assert.Equal(0, NodesReader.FindClassicSubsectorAtPoint(structure, polygons, new Vector2D(40, 0)));
        Assert.Equal(1, NodesReader.FindClassicSubsectorAtPoint(structure, polygons, new Vector2D(-10, 0)));
        Assert.Equal(-1, NodesReader.FindClassicSubsectorAtPoint(structure, polygons, new Vector2D(10, 0)));
    }

    private static void AssertPoints(IReadOnlyList<Vector2D> actual, params Vector2D[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].x, actual[i].x, precision: 6);
            Assert.Equal(expected[i].y, actual[i].y, precision: 6);
        }
    }
}
