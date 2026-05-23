// ABOUTME: Tests the vanilla NODES lump parser - partition line segments from 28-byte records.

using System;
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
}
