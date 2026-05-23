// ABOUTME: Minimal self-contained PNG decoder producing RGBA8 ImageData, used for PK3/PNG resources.
// ABOUTME: Handles color types 0/2/3/4/6, bit depths 1/2/4/8/16 (non-interlaced), via .NET ZLibStream + PNG unfiltering.

using System;
using System.IO;
using System.IO.Compression;

namespace DBuilder.IO;

/// <summary>Decodes a PNG byte stream into RGBA8 pixels. Returns null for malformed or unsupported (interlaced) data.</summary>
public static class PngDecoder
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>True if the bytes begin with the PNG signature.</summary>
    public static bool IsPng(byte[] data) => data.Length >= 8 && HasSignature(data);

    public static ImageData? Decode(byte[] data)
    {
        try { return DecodeCore(data); }
        catch { return null; }
    }

    private static ImageData? DecodeCore(byte[] data)
    {
        if (data.Length < 8 || !HasSignature(data)) return null;

        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        int grabX = 0, grabY = 0;     // sprite offsets from a grAb chunk (SLADE/ZDoom)
        byte[]? palette = null;       // RGB triples
        byte[]? trns = null;          // palette alpha (or color-key, unused for non-palette)
        var idat = new MemoryStream();

        int pos = 8;
        while (pos + 8 <= data.Length)
        {
            int len = ReadBE32(data, pos);
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int dataStart = pos + 8;
            if (dataStart + len + 4 > data.Length) break; // truncated chunk (+4 CRC)

            switch (type)
            {
                case "IHDR":
                    width = ReadBE32(data, dataStart);
                    height = ReadBE32(data, dataStart + 4);
                    bitDepth = data[dataStart + 8];
                    colorType = data[dataStart + 9];
                    interlace = data[dataStart + 12];
                    break;
                case "PLTE":
                    palette = new byte[len];
                    Array.Copy(data, dataStart, palette, 0, len);
                    break;
                case "tRNS":
                    trns = new byte[len];
                    Array.Copy(data, dataStart, trns, 0, len);
                    break;
                case "grAb": // SLADE/ZDoom sprite offset (signed int32 x, y)
                    if (len >= 8) { grabX = ReadBE32(data, dataStart); grabY = ReadBE32(data, dataStart + 4); }
                    break;
                case "IDAT":
                    idat.Write(data, dataStart, len);
                    break;
                case "IEND":
                    pos = data.Length; // stop
                    break;
            }
            if (type == "IEND") break;
            pos = dataStart + len + 4; // skip data + CRC
        }

        if (width <= 0 || height <= 0 || interlace != 0) return null; // Adam7 interlacing unsupported

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (channels == 0) return null;
        if (colorType == 3 && palette == null) return null;

        int bitsPerPixel = channels * bitDepth;
        int bpp = Math.Max(1, (bitsPerPixel + 7) / 8);
        int stride = (width * bitsPerPixel + 7) / 8;

        byte[] raw = Inflate(idat.ToArray());
        if (raw.Length < (long)(stride + 1) * height) return null;

        byte[] pixels = Unfilter(raw, width, height, stride, bpp);
        return new ImageData(width, height, ToRgba(pixels, width, height, stride, bitDepth, colorType, palette, trns), grabX, grabY);
    }

    // Reverses PNG per-scanline filtering (None/Sub/Up/Average/Paeth) into a contiguous pixel buffer.
    private static byte[] Unfilter(byte[] raw, int width, int height, int stride, int bpp)
    {
        var outBuf = new byte[stride * height];
        int src = 0;
        for (int y = 0; y < height; y++)
        {
            int filter = raw[src++];
            int rowStart = y * stride;
            int prevStart = rowStart - stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= bpp ? outBuf[rowStart + x - bpp] : 0;
                int b = y > 0 ? outBuf[prevStart + x] : 0;
                int c = (y > 0 && x >= bpp) ? outBuf[prevStart + x - bpp] : 0;
                int val = raw[src++];
                int recon = filter switch
                {
                    0 => val,
                    1 => val + a,
                    2 => val + b,
                    3 => val + ((a + b) >> 1),
                    4 => val + Paeth(a, b, c),
                    _ => val,
                };
                outBuf[rowStart + x] = (byte)recon;
            }
        }
        return outBuf;
    }

    private static byte[] ToRgba(byte[] px, int width, int height, int stride, int bitDepth, int colorType, byte[]? palette, byte[]? trns)
    {
        var rgba = new byte[width * height * 4];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                byte r, g, b, a = 255;
                switch (colorType)
                {
                    case 0: // grayscale
                        {
                            int v = Sample(px, row, x, bitDepth, 1, 0);
                            byte gv = Scale(v, bitDepth);
                            r = g = b = gv;
                            break;
                        }
                    case 2: // RGB
                        r = SampleByte(px, row, x, bitDepth, 3, 0);
                        g = SampleByte(px, row, x, bitDepth, 3, 1);
                        b = SampleByte(px, row, x, bitDepth, 3, 2);
                        break;
                    case 3: // palette
                        {
                            int idx = Sample(px, row, x, bitDepth, 1, 0);
                            int pi = idx * 3;
                            r = (palette != null && pi + 2 < palette.Length) ? palette[pi] : (byte)0;
                            g = (palette != null && pi + 2 < palette.Length) ? palette[pi + 1] : (byte)0;
                            b = (palette != null && pi + 2 < palette.Length) ? palette[pi + 2] : (byte)0;
                            a = (trns != null && idx < trns.Length) ? trns[idx] : (byte)255;
                            break;
                        }
                    case 4: // grayscale + alpha
                        {
                            byte gv = SampleByte(px, row, x, bitDepth, 2, 0);
                            r = g = b = gv;
                            a = SampleByte(px, row, x, bitDepth, 2, 1);
                            break;
                        }
                    default: // 6 = RGBA
                        r = SampleByte(px, row, x, bitDepth, 4, 0);
                        g = SampleByte(px, row, x, bitDepth, 4, 1);
                        b = SampleByte(px, row, x, bitDepth, 4, 2);
                        a = SampleByte(px, row, x, bitDepth, 4, 3);
                        break;
                }
                rgba[o++] = r; rgba[o++] = g; rgba[o++] = b; rgba[o++] = a;
            }
        }
        return rgba;
    }

    // Raw sample value (0..maxval) for the given channel, honoring bit depth (1/2/4/8/16).
    private static int Sample(byte[] px, int rowStart, int x, int bitDepth, int channels, int channel)
    {
        if (bitDepth == 8) return px[rowStart + x * channels + channel];
        if (bitDepth == 16) return px[rowStart + (x * channels + channel) * 2]; // high byte
        // sub-byte: channels is 1 for the sub-byte color types we support (gray/palette)
        int bitIndex = x * bitDepth;
        int byteIndex = rowStart + (bitIndex >> 3);
        int shift = 8 - bitDepth - (bitIndex & 7);
        int mask = (1 << bitDepth) - 1;
        return (px[byteIndex] >> shift) & mask;
    }

    // Sample scaled to a byte (0..255).
    private static byte SampleByte(byte[] px, int rowStart, int x, int bitDepth, int channels, int channel)
        => Scale(bitDepth == 8 ? px[rowStart + x * channels + channel]
               : bitDepth == 16 ? px[rowStart + (x * channels + channel) * 2]
               : Sample(px, rowStart, x, bitDepth, channels, channel), bitDepth);

    private static byte Scale(int value, int bitDepth) => bitDepth switch
    {
        8 or 16 => (byte)value,
        1 => (byte)(value * 255),
        2 => (byte)(value * 85),
        4 => (byte)(value * 17),
        _ => (byte)value,
    };

    private static byte[] Inflate(byte[] zlib)
    {
        using var src = new MemoryStream(zlib);
        using var z = new ZLibStream(src, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        z.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
    }

    private static bool HasSignature(byte[] d)
    {
        for (int i = 0; i < 8; i++) if (d[i] != Signature[i]) return false;
        return true;
    }

    private static int ReadBE32(byte[] d, int p) => (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
}
