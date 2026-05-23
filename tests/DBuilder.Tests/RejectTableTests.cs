// ABOUTME: Tests the REJECT lump parser - packed sector x sector "cannot see" bit matrix (LSB-first).

using DBuilder.IO;

namespace DBuilder.Tests;

public class RejectTableTests
{
    // Builds a reject lump for n sectors with the given (a,b) pairs marked rejected.
    private static byte[] Build(int n, params (int a, int b)[] rejected)
    {
        var data = new byte[(n * n + 7) / 8];
        foreach (var (a, b) in rejected)
        {
            int bit = a * n + b;
            data[bit >> 3] |= (byte)(1 << (bit & 7));
        }
        return data;
    }

    [Fact]
    public void ReadsSetAndUnsetBits()
    {
        var t = RejectTable.Parse(Build(3, (0, 2), (2, 1)), 3);
        Assert.True(t.HasData);
        Assert.True(t.IsRejected(0, 2));
        Assert.True(t.IsRejected(2, 1));
        Assert.False(t.IsRejected(0, 1));
        Assert.False(t.IsRejected(2, 0));
    }

    [Fact]
    public void DirectionalityIsRespected()
    {
        // Only (1,0) marked, not (0,1): the matrix is directional as stored.
        var t = RejectTable.Parse(Build(2, (1, 0)), 2);
        Assert.True(t.IsRejected(1, 0));
        Assert.False(t.IsRejected(0, 1));
    }

    [Fact]
    public void EmptyOrShortDataHasNoData()
    {
        Assert.False(RejectTable.Parse(System.Array.Empty<byte>(), 4).HasData);
        Assert.False(RejectTable.Parse(new byte[1], 4).HasData); // needs 2 bytes for 16 bits
    }

    [Fact]
    public void OutOfRangeIsNotRejected()
    {
        var t = RejectTable.Parse(Build(2, (0, 0)), 2);
        Assert.False(t.IsRejected(-1, 0));
        Assert.False(t.IsRejected(0, 5));
    }
}
