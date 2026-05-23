// ABOUTME: Parses the vanilla Doom NODES lump into BSP partition line segments for the nodes debug viewer.
// ABOUTME: Each 28-byte record: int16 x,y,dx,dy (partition), 2x4 int16 child bboxes, 2 uint16 children.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

/// <summary>One BSP node's partition line as a segment from (x1,y1) to (x2,y2).</summary>
public readonly record struct NodePartition(int X1, int Y1, int X2, int Y2);

public static class NodesReader
{
    private const int RecordSize = 28;

    /// <summary>
    /// Parses a vanilla NODES lump into partition segments. Returns an empty list for empty or non-vanilla
    /// (ZDoom-extended / GL) node data, which uses a different layout this reader does not handle.
    /// </summary>
    public static List<NodePartition> Parse(byte[] data)
    {
        var list = new List<NodePartition>();
        if (data == null || data.Length < RecordSize) return list;

        int count = data.Length / RecordSize;
        for (int i = 0; i < count; i++)
        {
            int p = i * RecordSize;
            short x = BitConverter.ToInt16(data, p);
            short y = BitConverter.ToInt16(data, p + 2);
            short dx = BitConverter.ToInt16(data, p + 4);
            short dy = BitConverter.ToInt16(data, p + 6);
            list.Add(new NodePartition(x, y, x + dx, y + dy));
        }
        return list;
    }
}
