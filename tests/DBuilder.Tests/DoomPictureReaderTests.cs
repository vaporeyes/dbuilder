// ABOUTME: DoomPictureReader verification tests.
// ABOUTME: Builds synthetic pictures byte-by-byte and checks the column/post format, transparency, multi-column patches, and tall-patch delta encoding.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class DoomPictureReaderTests
{
    // Builds a palette where every entry N maps to (R=N, G=N, B=N) so pixel-byte -> color is trivial to assert.
    private static DoomPalette GrayPalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            bytes[i * 3 + 0] = (byte)i;
            bytes[i * 3 + 1] = (byte)i;
            bytes[i * 3 + 2] = (byte)i;
        }
        return DoomPalette.FromBytes(bytes);
    }

    /// <summary>
    /// Writes a single-column picture where the column has one post spanning the full height.
    /// The post pixels are 0, 1, 2, ..., height-1 so a gray palette gives R=G=B=row.
    /// </summary>
    private static byte[] BuildSolidColumnPicture(int width, int height, int offsetX, int offsetY)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)width);
            w.Write((short)height);
            w.Write((short)offsetX);
            w.Write((short)offsetY);

            // Column offsets - all columns point to the same data block for this simple case
            int columnDataStart = 8 + width * 4;
            for (int x = 0; x < width; x++) w.Write((int)columnDataStart);

            // Column post: startY=0, count=height, padding=0, [pixels], padding=0, terminator=0xFF
            w.Write((byte)0);            // startY
            w.Write((byte)height);       // count
            w.Write((byte)0);            // padding
            for (int y = 0; y < height; y++) w.Write((byte)y);
            w.Write((byte)0);            // padding
            w.Write((byte)0xFF);         // terminator
        }
        return ms.ToArray();
    }

    [Fact]
    public void HeaderFieldsAreParsed()
    {
        byte[] data = BuildSolidColumnPicture(1, 4, offsetX: 5, offsetY: -3);
        var pic = DoomPictureReader.Decode(data, GrayPalette());

        Assert.NotNull(pic);
        Assert.Equal(1, pic!.Width);
        Assert.Equal(4, pic.Height);
        Assert.Equal(5, pic.OffsetX);
        Assert.Equal(-3, pic.OffsetY);
        Assert.Equal(1 * 4 * 4, pic.Rgba8.Length);
    }

    [Fact]
    public void SolidColumnPixelsMatchPalette()
    {
        byte[] data = BuildSolidColumnPicture(1, 4, 0, 0);
        var pic = DoomPictureReader.Decode(data, GrayPalette())!;

        // Each row's pixel byte was (byte)row; gray palette -> R=G=B=row, A=0xFF
        for (int y = 0; y < 4; y++)
        {
            int i = y * 4;
            Assert.Equal((byte)y, pic.Rgba8[i + 0]);
            Assert.Equal((byte)y, pic.Rgba8[i + 1]);
            Assert.Equal((byte)y, pic.Rgba8[i + 2]);
            Assert.Equal(0xFF,    pic.Rgba8[i + 3]);
        }
    }

    [Fact]
    public void GapsBetweenPostsAreTransparent()
    {
        // Picture: width=1, height=10. Two posts: rows 0-1 opaque, rows 4-5 opaque, rest transparent.
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)1); w.Write((short)10);
            w.Write((short)0); w.Write((short)0);
            w.Write((int)12); // column offset

            // post 1: y=0, count=2, padding, pixels (50, 60), padding
            w.Write((byte)0); w.Write((byte)2); w.Write((byte)0);
            w.Write((byte)50); w.Write((byte)60);
            w.Write((byte)0);
            // post 2: y=4, count=2, padding, pixels (70, 80), padding
            w.Write((byte)4); w.Write((byte)2); w.Write((byte)0);
            w.Write((byte)70); w.Write((byte)80);
            w.Write((byte)0);
            // terminator
            w.Write((byte)0xFF);
        }
        var pic = DoomPictureReader.Decode(ms.ToArray(), GrayPalette())!;

        // Rows 0, 1: opaque values 50, 60
        Assert.Equal(50,   pic.Rgba8[0 * 4 + 0]);  Assert.Equal(0xFF, pic.Rgba8[0 * 4 + 3]);
        Assert.Equal(60,   pic.Rgba8[1 * 4 + 0]);  Assert.Equal(0xFF, pic.Rgba8[1 * 4 + 3]);
        // Rows 2, 3: transparent
        Assert.Equal(0,    pic.Rgba8[2 * 4 + 3]);
        Assert.Equal(0,    pic.Rgba8[3 * 4 + 3]);
        // Rows 4, 5: opaque values 70, 80
        Assert.Equal(70,   pic.Rgba8[4 * 4 + 0]);  Assert.Equal(0xFF, pic.Rgba8[4 * 4 + 3]);
        Assert.Equal(80,   pic.Rgba8[5 * 4 + 0]);  Assert.Equal(0xFF, pic.Rgba8[5 * 4 + 3]);
        // Rows 6..9: transparent
        for (int y = 6; y < 10; y++) Assert.Equal(0, pic.Rgba8[y * 4 + 3]);
    }

    [Fact]
    public void MultiColumnPictureLaysOutRowMajor()
    {
        // Picture: width=3, height=2. Each column has one full post.
        // Column 0 pixels: row0=10, row1=11
        // Column 1 pixels: row0=20, row1=21
        // Column 2 pixels: row0=30, row1=31
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            int width = 3, height = 2;
            w.Write((short)width); w.Write((short)height);
            w.Write((short)0); w.Write((short)0);

            // 3 column offsets - each column occupies (1+1+1+2+1+1) = 7 bytes
            int colData = 8 + width * 4;
            w.Write((int)(colData + 0 * 7));
            w.Write((int)(colData + 1 * 7));
            w.Write((int)(colData + 2 * 7));

            void WriteColumn(byte rowOpaqueA, byte rowOpaqueB)
            {
                w.Write((byte)0);            // startY
                w.Write((byte)2);            // count
                w.Write((byte)0);            // pad
                w.Write(rowOpaqueA);
                w.Write(rowOpaqueB);
                w.Write((byte)0);            // pad
                w.Write((byte)0xFF);         // terminator
            }

            WriteColumn(10, 11);
            WriteColumn(20, 21);
            WriteColumn(30, 31);
        }

        var pic = DoomPictureReader.Decode(ms.ToArray(), GrayPalette())!;

        // Row-major layout: rgba[(row * width + col) * 4]
        // Row 0: cols 10, 20, 30
        Assert.Equal(10, pic.Rgba8[(0 * 3 + 0) * 4 + 0]);
        Assert.Equal(20, pic.Rgba8[(0 * 3 + 1) * 4 + 0]);
        Assert.Equal(30, pic.Rgba8[(0 * 3 + 2) * 4 + 0]);
        // Row 1: cols 11, 21, 31
        Assert.Equal(11, pic.Rgba8[(1 * 3 + 0) * 4 + 0]);
        Assert.Equal(21, pic.Rgba8[(1 * 3 + 1) * 4 + 0]);
        Assert.Equal(31, pic.Rgba8[(1 * 3 + 2) * 4 + 0]);
    }

    [Fact]
    public void ValidateRejectsTooShortData()
    {
        Assert.False(DoomPictureReader.Validate(new byte[3]));
        Assert.False(DoomPictureReader.Validate(new byte[7])); // header alone needs 8
    }

    [Fact]
    public void ValidateRejectsZeroDimensions()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)0); w.Write((short)5);  // width=0
            w.Write((short)0); w.Write((short)0);
        }
        Assert.False(DoomPictureReader.Validate(ms.ToArray()));
    }

    [Fact]
    public void ValidateAcceptsWellFormedHeader()
    {
        byte[] data = BuildSolidColumnPicture(1, 4, 0, 0);
        Assert.True(DoomPictureReader.Validate(data));
    }

    [Fact]
    public void DecodeReturnsNullForMalformedColumnAddress()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)1); w.Write((short)4);
            w.Write((short)0); w.Write((short)0);
            w.Write((int)9999); // column offset way past end
        }
        Assert.Null(DoomPictureReader.Decode(ms.ToArray(), GrayPalette()));
    }

    [Fact]
    public void TallPatchDeltaEncodingDecodes()
    {
        // height > 256 + post startY that doesn't advance triggers the delta path.
        // Picture: width=1, height=300. One post at y=0 with 2 pixels, then a delta-post at "y=0" meaning y_actual=0+0=0 (illegal in canonical Doom, but mxd's fix interprets non-advancing as delta and adds).
        //
        // Simpler test: emit two posts where the second's raw startY is less than the first's,
        // so the actual y becomes prev+raw. With prev=10 and raw=5, actual=15.
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write((short)1); w.Write((short)300);
            w.Write((short)0); w.Write((short)0);
            w.Write((int)12); // column offset

            // post A: y=10, count=2, pixels (100, 110)
            w.Write((byte)10); w.Write((byte)2); w.Write((byte)0);
            w.Write((byte)100); w.Write((byte)110);
            w.Write((byte)0);

            // post B: raw startY=5 (< prev raw 10), count=2, pixels (200, 210).
            // Delta rule: 5 < 10 -> actualY = 10 + 5 = 15.
            w.Write((byte)5);  w.Write((byte)2); w.Write((byte)0);
            w.Write((byte)200); w.Write((byte)210);
            w.Write((byte)0);

            w.Write((byte)0xFF);
        }

        var pic = DoomPictureReader.Decode(ms.ToArray(), GrayPalette())!;

        // Rows 10, 11: first post
        Assert.Equal(100, pic.Rgba8[10 * 4 + 0]);
        Assert.Equal(110, pic.Rgba8[11 * 4 + 0]);
        // Rows 12-14: transparent gap
        for (int y = 12; y < 15; y++) Assert.Equal(0, pic.Rgba8[y * 4 + 3]);
        // Rows 15, 16: second post (delta-resolved to actual y=15)
        Assert.Equal(200, pic.Rgba8[15 * 4 + 0]);
        Assert.Equal(210, pic.Rgba8[16 * 4 + 0]);
    }
}
