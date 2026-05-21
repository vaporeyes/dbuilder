// ABOUTME: ClippedStream invariants — window honors offset/length, seek modes, read/write boundaries.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ClippedStreamTests
{
    private static MemoryStream MakeBase(int size)
    {
        var buf = new byte[size];
        for (int i = 0; i < size; i++) buf[i] = (byte)i;
        return new MemoryStream(buf);
    }

    [Fact]
    public void ReadAllBytesReturnsExactlyTheWindow()
    {
        using var basems = MakeBase(256);
        using var clipped = new ClippedStream(basems, offset: 100, length: 50);
        byte[] data = clipped.ReadAllBytes();
        Assert.Equal(50, data.Length);
        Assert.Equal(100, data[0]);
        Assert.Equal(149, data[49]);
    }

    [Fact]
    public void SeekFromEndPositionsRelativeToWindow()
    {
        using var basems = MakeBase(256);
        using var clipped = new ClippedStream(basems, offset: 10, length: 20);
        clipped.Seek(0, SeekOrigin.End);
        Assert.Equal(20, clipped.Position);
        clipped.Seek(-5, SeekOrigin.End);
        Assert.Equal(15, clipped.Position);
        Assert.Equal(25, clipped.ReadByte()); // base[10 + 15] == 25
    }

    [Fact]
    public void SeekBeyondLengthThrows()
    {
        using var basems = MakeBase(256);
        using var clipped = new ClippedStream(basems, offset: 0, length: 10);
        Assert.Throws<System.ArgumentException>(() => clipped.Seek(11, SeekOrigin.Begin));
    }

    [Fact]
    public void WriteIntoWindowIsReflectedInBaseStream()
    {
        var basebuf = new byte[64];
        using var basems = new MemoryStream(basebuf);
        using var clipped = new ClippedStream(basems, offset: 16, length: 4);
        clipped.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, 0, 4);
        Assert.Equal(0xDE, basebuf[16]);
        Assert.Equal(0xAD, basebuf[17]);
        Assert.Equal(0xBE, basebuf[18]);
        Assert.Equal(0xEF, basebuf[19]);
        // Bytes outside the window untouched.
        Assert.Equal(0, basebuf[15]);
        Assert.Equal(0, basebuf[20]);
    }
}
