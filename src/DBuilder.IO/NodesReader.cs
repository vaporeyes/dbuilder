// ABOUTME: Parses the vanilla Doom NODES lump into BSP partition line segments for the nodes debug viewer.
// ABOUTME: Each 28-byte record: int16 x,y,dx,dy (partition), 2x4 int16 child bboxes, 2 uint16 children.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using DBuilder.Geometry;

namespace DBuilder.IO;

/// <summary>One BSP node's partition line as a segment from (x1,y1) to (x2,y2).</summary>
public readonly record struct NodePartition(int X1, int Y1, int X2, int Y2);

public enum ClassicNodesStatus
{
    Ok,
    MissingOrTooShortNodes,
    UnsupportedCompressedNodes,
    EmptyNodes,
    EmptySegs,
    SegsOverflow,
    EmptyVertices,
    EmptySubsectors,
    InvalidSubsectorSegRange
}

public enum ZNodesPayloadStatus
{
    Ok,
    MissingOrTooShort,
    UnsupportedHeader,
    DecompressionFailed,
}

public readonly record struct ClassicNodeBounds(short Top, short Bottom, short Left, short Right);

public readonly record struct ClassicNode(
    short X,
    short Y,
    short Dx,
    short Dy,
    ClassicNodeBounds RightBounds,
    ClassicNodeBounds LeftBounds,
    int RightChildIndex,
    bool RightChildIsSubsector,
    int LeftChildIndex,
    bool LeftChildIsSubsector,
    int ParentIndex)
{
    public NodePartition Partition => new(X, Y, X + Dx, Y + Dy);
}

public readonly record struct ClassicSeg(
    int StartVertex,
    int EndVertex,
    double AngleRadians,
    int LineIndex,
    bool LeftSide,
    int Offset,
    int SubsectorIndex);

public readonly record struct ClassicSubsector(int SegCount, int FirstSeg);

public readonly record struct ClassicNodeVertex(short X, short Y);

public sealed record ClassicNodesStructure(
    ClassicNodesStatus Status,
    IReadOnlyList<ClassicNode> Nodes,
    IReadOnlyList<ClassicSeg> Segs,
    IReadOnlyList<ClassicSubsector> Subsectors,
    IReadOnlyList<ClassicNodeVertex> Vertices,
    bool SegCountExceedsSignedLimit)
{
    public bool IsValid => Status == ClassicNodesStatus.Ok;

    public static ClassicNodesStructure Failure(ClassicNodesStatus status) => new(
        status,
        Array.Empty<ClassicNode>(),
        Array.Empty<ClassicSeg>(),
        Array.Empty<ClassicSubsector>(),
        Array.Empty<ClassicNodeVertex>(),
        false);
}

public sealed record ZNodesPayload(ZNodesPayloadStatus Status, string Format, byte[] Data, string? Error)
{
    public bool IsValid => Status == ZNodesPayloadStatus.Ok;

    public static ZNodesPayload Failure(ZNodesPayloadStatus status, string format = "", string? error = null)
        => new(status, format, Array.Empty<byte>(), error);
}

public static class NodesReader
{
    private const int NodeRecordSize = 28;
    private const int SegRecordSize = 12;
    private const int VertexRecordSize = 4;
    private const int SubsectorRecordSize = 4;
    private const int SubsectorFlag = 0x8000;
    private const int ChildIndexMask = 0x7FFF;
    private static readonly string[] SupportedZNodesFormats =
    [
        "XNOD",
        "XGLN",
        "XGL2",
        "XGL3",
        "ZNOD",
        "ZGLN",
        "ZGL2",
        "ZGL3",
    ];

    /// <summary>
    /// Parses a vanilla NODES lump into partition segments. Returns an empty list for empty or non-vanilla
    /// (ZDoom-extended / GL) node data, which uses a different layout this reader does not handle.
    /// </summary>
    public static List<NodePartition> Parse(byte[] data)
    {
        var list = new List<NodePartition>();
        if (data == null || data.Length < NodeRecordSize) return list;

        int count = data.Length / NodeRecordSize;
        for (int i = 0; i < count; i++)
        {
            int p = i * NodeRecordSize;
            short x = BitConverter.ToInt16(data, p);
            short y = BitConverter.ToInt16(data, p + 2);
            short dx = BitConverter.ToInt16(data, p + 4);
            short dy = BitConverter.ToInt16(data, p + 6);
            list.Add(new NodePartition(x, y, x + dx, y + dy));
        }
        return list;
    }

    public static ClassicNodesStructure ParseClassicStructures(
        byte[] nodesData,
        byte[] segsData,
        byte[] verticesData,
        byte[] subsectorsData)
    {
        if (nodesData == null || nodesData.Length < 4)
            return ClassicNodesStructure.Failure(ClassicNodesStatus.MissingOrTooShortNodes);

        if (HasHeader(nodesData, "ZNOD") || HasHeader(nodesData, "XNOD"))
            return ClassicNodesStructure.Failure(ClassicNodesStatus.UnsupportedCompressedNodes);

        int nodeCount = nodesData.Length / NodeRecordSize;
        if (nodeCount < 1) return ClassicNodesStructure.Failure(ClassicNodesStatus.EmptyNodes);

        int segCount = segsData?.Length / SegRecordSize ?? 0;
        if (segCount < 1) return ClassicNodesStructure.Failure(ClassicNodesStatus.EmptySegs);
        if (segCount >= ushort.MaxValue) return ClassicNodesStructure.Failure(ClassicNodesStatus.SegsOverflow);

        int vertexCount = verticesData?.Length / VertexRecordSize ?? 0;
        if (vertexCount < 1) return ClassicNodesStructure.Failure(ClassicNodesStatus.EmptyVertices);

        int subsectorCount = subsectorsData?.Length / SubsectorRecordSize ?? 0;
        if (subsectorCount < 1) return ClassicNodesStructure.Failure(ClassicNodesStatus.EmptySubsectors);

        ClassicNode[] nodes = ParseClassicNodes(nodesData, nodeCount);
        ClassicSeg[] segs = ParseClassicSegs(segsData!, segCount);
        ClassicNodeVertex[] vertices = ParseClassicVertices(verticesData!, vertexCount);
        ClassicSubsector[] subsectors = ParseClassicSubsectors(subsectorsData!, subsectorCount);

        ClassicNodesStatus linkStatus = LinkSubsectors(segs, subsectors);
        if (linkStatus != ClassicNodesStatus.Ok) return ClassicNodesStructure.Failure(linkStatus);

        return new ClassicNodesStructure(
            ClassicNodesStatus.Ok,
            nodes,
            segs,
            subsectors,
            vertices,
            segCount >= short.MaxValue);
    }

    public static bool HasSupportedZNodesHeader(byte[] data)
        => data != null
        && data.Length >= 4
        && Array.IndexOf(SupportedZNodesFormats, Encoding.ASCII.GetString(data, 0, 4)) >= 0;

    public static ZNodesPayload ExtractZNodesPayload(byte[] data)
    {
        if (data == null || data.Length < 4)
            return ZNodesPayload.Failure(ZNodesPayloadStatus.MissingOrTooShort);

        string format = Encoding.ASCII.GetString(data, 0, 4);
        if (Array.IndexOf(SupportedZNodesFormats, format) < 0)
            return ZNodesPayload.Failure(ZNodesPayloadStatus.UnsupportedHeader, format);

        if (format[0] != 'Z')
        {
            var payload = new byte[data.Length - 4];
            Array.Copy(data, 4, payload, 0, payload.Length);
            return new ZNodesPayload(ZNodesPayloadStatus.Ok, format, payload, null);
        }

        try
        {
            using var compressed = new MemoryStream(data, 4, data.Length - 4, writable: false);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var payload = new MemoryStream();
            zlib.CopyTo(payload);
            return new ZNodesPayload(ZNodesPayloadStatus.Ok, format, payload.ToArray(), null);
        }
        catch (InvalidDataException ex)
        {
            return ZNodesPayload.Failure(ZNodesPayloadStatus.DecompressionFailed, format, ex.Message);
        }
    }

    private static ClassicNode[] ParseClassicNodes(byte[] data, int count)
    {
        var nodes = new ClassicNode[count];
        var parents = new int[count];
        Array.Fill(parents, -1);

        for (int i = 0; i < count; i++)
        {
            int p = i * NodeRecordSize;
            int rightChild = BitConverter.ToUInt16(data, p + 24);
            int leftChild = BitConverter.ToUInt16(data, p + 26);

            nodes[i] = new ClassicNode(
                BitConverter.ToInt16(data, p),
                BitConverter.ToInt16(data, p + 2),
                BitConverter.ToInt16(data, p + 4),
                BitConverter.ToInt16(data, p + 6),
                new ClassicNodeBounds(
                    BitConverter.ToInt16(data, p + 8),
                    BitConverter.ToInt16(data, p + 10),
                    BitConverter.ToInt16(data, p + 12),
                    BitConverter.ToInt16(data, p + 14)),
                new ClassicNodeBounds(
                    BitConverter.ToInt16(data, p + 16),
                    BitConverter.ToInt16(data, p + 18),
                    BitConverter.ToInt16(data, p + 20),
                    BitConverter.ToInt16(data, p + 22)),
                rightChild & ChildIndexMask,
                (rightChild & SubsectorFlag) != 0,
                leftChild & ChildIndexMask,
                (leftChild & SubsectorFlag) != 0,
                -1);
        }

        for (int i = count - 1; i >= 0; i--)
        {
            ClassicNode node = nodes[i];
            if (!node.RightChildIsSubsector && node.RightChildIndex >= 0 && node.RightChildIndex < count)
                parents[node.RightChildIndex] = i;
            if (!node.LeftChildIsSubsector && node.LeftChildIndex >= 0 && node.LeftChildIndex < count)
                parents[node.LeftChildIndex] = i;
        }

        for (int i = 0; i < count; i++) nodes[i] = nodes[i] with { ParentIndex = parents[i] };
        return nodes;
    }

    private static ClassicSeg[] ParseClassicSegs(byte[] data, int count)
    {
        var segs = new ClassicSeg[count];
        for (int i = 0; i < count; i++)
        {
            int p = i * SegRecordSize;
            int angleDegrees = NormalizeDegrees(BitConverter.ToInt16(data, p + 4) / 182 + 90);
            segs[i] = new ClassicSeg(
                BitConverter.ToUInt16(data, p),
                BitConverter.ToUInt16(data, p + 2),
                Angle2D.DegToRad(angleDegrees),
                BitConverter.ToUInt16(data, p + 6),
                BitConverter.ToInt16(data, p + 8) != 0,
                BitConverter.ToInt16(data, p + 10),
                -1);
        }
        return segs;
    }

    private static ClassicNodeVertex[] ParseClassicVertices(byte[] data, int count)
    {
        var vertices = new ClassicNodeVertex[count];
        for (int i = 0; i < count; i++)
        {
            int p = i * VertexRecordSize;
            vertices[i] = new ClassicNodeVertex(
                BitConverter.ToInt16(data, p),
                BitConverter.ToInt16(data, p + 2));
        }
        return vertices;
    }

    private static ClassicSubsector[] ParseClassicSubsectors(byte[] data, int count)
    {
        var subsectors = new ClassicSubsector[count];
        for (int i = 0; i < count; i++)
        {
            int p = i * SubsectorRecordSize;
            subsectors[i] = new ClassicSubsector(
                BitConverter.ToUInt16(data, p),
                BitConverter.ToUInt16(data, p + 2));
        }
        return subsectors;
    }

    private static ClassicNodesStatus LinkSubsectors(ClassicSeg[] segs, ClassicSubsector[] subsectors)
    {
        for (int i = 0; i < subsectors.Length; i++)
        {
            ClassicSubsector subsector = subsectors[i];
            if (subsector.FirstSeg < 0 || subsector.SegCount < 0 || subsector.FirstSeg + subsector.SegCount > segs.Length)
                return ClassicNodesStatus.InvalidSubsectorSegRange;

            for (int s = subsector.FirstSeg; s < subsector.FirstSeg + subsector.SegCount; s++)
                segs[s] = segs[s] with { SubsectorIndex = i };
        }

        return ClassicNodesStatus.Ok;
    }

    private static bool HasHeader(byte[] data, string header)
        => data.Length >= header.Length
        && data[0] == header[0]
        && data[1] == header[1]
        && data[2] == header[2]
        && data[3] == header[3];

    private static int NormalizeDegrees(int degrees)
    {
        int normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
