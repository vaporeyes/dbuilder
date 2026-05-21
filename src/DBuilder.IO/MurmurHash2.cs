// ABOUTME: MurmurHash2 32-bit ported from UDB Source/Core/General/MurmurHash2.cs.
// ABOUTME: Used by Lump.MakeLongName for stable cross-session texture-name hashing.

/*
 * Original Code: HashTableHashing.MurmurHash2 by Davy Landman (MPL 1.1 / GPL 2.0 / LGPL 2.1).
 * UDB integration: Copyright (c) 2007 Pascal vd Heiden, GPL v2 or later.
 */

using System;
using System.Text;

namespace DBuilder.IO;

public static class MurmurHash2
{
    private const uint m = 0x5bd1e995;
    private const int r = 24;

    public static uint Hash(string data)
        => Hash(Encoding.ASCII.GetBytes(data), 0xc58f1a7b);

    private static unsafe uint Hash(byte[] data, uint seed)
    {
        int length = data.Length;
        if (length == 0) return 0;

        uint h = seed ^ (uint)length;
        int remainingBytes = length & 3; // mod 4
        int numberOfLoops = length >> 2; // div 4

        fixed (byte* firstByte = &data[0])
        {
            uint* realData = (uint*)firstByte;
            while (numberOfLoops != 0)
            {
                uint k = *realData;
                k *= m;
                k ^= k >> r;
                k *= m;

                h *= m;
                h ^= k;
                numberOfLoops--;
                realData++;
            }

            switch (remainingBytes)
            {
                case 3:
                    h ^= (ushort)(*realData);
                    h ^= ((uint)(*(((byte*)(realData)) + 2))) << 16;
                    h *= m;
                    break;
                case 2:
                    h ^= (ushort)(*realData);
                    h *= m;
                    break;
                case 1:
                    h ^= *((byte*)realData);
                    h *= m;
                    break;
            }
        }

        // Final mixes ensure the last few bytes are well-incorporated.
        h ^= h >> 13;
        h *= m;
        h ^= h >> 15;

        return h;
    }
}
