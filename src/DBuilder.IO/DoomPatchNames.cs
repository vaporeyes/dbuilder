// ABOUTME: PNAMES lump reader - the patch-name table that TEXTURE1/TEXTURE2 entries reference by index.
// ABOUTME: Layout: uint32 count, then count*8-byte ASCII names (null-padded, uppercased).

using System.IO;

namespace DBuilder.IO;

public sealed class DoomPatchNames
{
    /// <summary>Patch names, uppercased, trimmed of nulls. Indexed by patch number from TEXTURE1/2 patch records.</summary>
    public string[] Names { get; }

    public int Length => Names.Length;

    /// <summary>Index access. Throws on out-of-range like UDB's array indexer.</summary>
    public string this[int index] => Names[index];

    private DoomPatchNames(string[] names) { Names = names; }

    /// <summary>Empty patch-names table (e.g. when the lump is absent).</summary>
    public static DoomPatchNames Empty { get; } = new DoomPatchNames(System.Array.Empty<string>());

    public static DoomPatchNames FromBytes(byte[] data)
    {
        if (data.Length < 4) throw new IOException($"PNAMES too short: {data.Length} bytes");

        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        uint count = r.ReadUInt32();
        if (count > int.MaxValue / 8) throw new IOException($"PNAMES count {count} is implausibly large");

        var names = new string[count];
        for (uint i = 0; i < count; i++)
        {
            byte[] bytes = r.ReadBytes(8);
            names[i] = TrimAsciiUpper(bytes);
        }
        return new DoomPatchNames(names);
    }

    public static DoomPatchNames? FromWad(WAD wad)
    {
        var lump = wad.FindLump("PNAMES");
        if (lump == null) return null;
        return FromBytes(lump.Stream.ReadAllBytes());
    }

    private static string TrimAsciiUpper(byte[] bytes)
    {
        int end = 0;
        while (end < bytes.Length && bytes[end] != 0) end++;
        return System.Text.Encoding.ASCII.GetString(bytes, 0, end).Trim().ToUpperInvariant();
    }
}
