// ABOUTME: Parses the Doom REJECT lump - a packed sector x sector bit matrix of "cannot see" relationships.
// ABOUTME: Bit (a*sectorCount + b), LSB-first per byte, set means a monster in sector a cannot see sector b.

namespace DBuilder.IO;

public sealed class RejectTable
{
    private readonly byte[] data;
    private readonly int n;

    private RejectTable(byte[] data, int sectorCount) { this.data = data; n = sectorCount; }

    /// <summary>Wraps REJECT lump bytes for a map with <paramref name="sectorCount"/> sectors.</summary>
    public static RejectTable Parse(byte[] data, int sectorCount) => new(data ?? System.Array.Empty<byte>(), sectorCount);

    /// <summary>True when there are enough bytes for the full sectorCount^2 bit matrix.</summary>
    public bool HasData => n > 0 && data.Length >= (n * n + 7) / 8;

    /// <summary>True when a monster in sector <paramref name="a"/> cannot see sector <paramref name="b"/>.</summary>
    public bool IsRejected(int a, int b)
    {
        if (!HasData || a < 0 || b < 0 || a >= n || b >= n) return false;
        int bit = a * n + b;
        int index = bit >> 3, offset = bit & 7;
        return index < data.Length && (data[index] & (1 << offset)) != 0;
    }
}
