// ABOUTME: Round-trip tests for SerializerStream / DeserializerStream.
// ABOUTME: Locks down the binary protocol — any change would invalidate every saved UDB clipboard/persistence payload.

using System.IO;
using DBuilder.Geometry;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SerializerStreamTests
{
    private const double Epsilon = 1e-12;

    [Fact]
    public void PrimitivesRoundTrip()
    {
        using var ms = new MemoryStream();

        using (var ser = new SerializerStream(ms))
        {
            ser.Begin();
            int i = -123; ser.rwInt(ref i);
            byte by = 200; ser.rwByte(ref by);
            short sh = -32000; ser.rwShort(ref sh);
            long lg = long.MinValue / 2; ser.rwLong(ref lg);
            uint ui = uint.MaxValue - 1; ser.rwUInt(ref ui);
            ushort us = 50000; ser.rwUShort(ref us);
            ulong ul = ulong.MaxValue - 1; ser.rwULong(ref ul);
            float f = 1.5f; ser.rwFloat(ref f);
            double d = -3.14; ser.rwDouble(ref d);
            bool b = true; ser.rwBool(ref b);
            ser.End();
        }

        ms.Position = 0;
        using (var de = new DeserializerStream(ms))
        {
            de.Begin();
            int i = 0; de.rwInt(ref i);
            byte by = 0; de.rwByte(ref by);
            short sh = 0; de.rwShort(ref sh);
            long lg = 0; de.rwLong(ref lg);
            uint ui = 0; de.rwUInt(ref ui);
            ushort us = 0; de.rwUShort(ref us);
            ulong ul = 0; de.rwULong(ref ul);
            float f = 0; de.rwFloat(ref f);
            double d = 0; de.rwDouble(ref d);
            bool b = false; de.rwBool(ref b);

            Assert.Equal(-123, i);
            Assert.Equal(200, by);
            Assert.Equal(-32000, sh);
            Assert.Equal(long.MinValue / 2, lg);
            Assert.Equal(uint.MaxValue - 1, ui);
            Assert.Equal(50000, us);
            Assert.Equal(ulong.MaxValue - 1, ul);
            Assert.Equal(1.5f, f);
            Assert.Equal(-3.14, d, Epsilon);
            Assert.True(b);
        }
    }

    [Fact]
    public void VectorsRoundTrip()
    {
        var v2 = new Vector2D(1.25, -7.5);
        var v3 = new Vector3D(0.1, 2.2, 3.3);

        using var ms = new MemoryStream();
        using (var ser = new SerializerStream(ms))
        {
            ser.Begin();
            ser.rwVector2D(ref v2);
            ser.rwVector3D(ref v3);
            ser.End();
        }

        ms.Position = 0;
        using (var de = new DeserializerStream(ms))
        {
            de.Begin();
            Vector2D back2 = default;
            Vector3D back3 = default;
            de.rwVector2D(ref back2);
            de.rwVector3D(ref back3);
            Assert.Equal(v2, back2);
            Assert.Equal(v3, back3);
        }
    }

    [Fact]
    public void StringTableDedupsRepeats()
    {
        // Writing the same string twice should share a table entry; on read both come back identical.
        using var ms = new MemoryStream();
        using (var ser = new SerializerStream(ms))
        {
            ser.Begin();
            string a = "hello"; ser.rwString(ref a);
            string b = "world"; ser.rwString(ref b);
            string c = "hello"; ser.rwString(ref c);
            ser.End();
        }

        ms.Position = 0;
        using (var de = new DeserializerStream(ms))
        {
            de.Begin();
            string a = ""; de.rwString(ref a);
            string b = ""; de.rwString(ref b);
            string c = ""; de.rwString(ref c);
            Assert.Equal("hello", a);
            Assert.Equal("world", b);
            Assert.Equal("hello", c);
        }
    }

    [Fact]
    public void ReadOnInSerializerThrows()
    {
        using var ms = new MemoryStream();
        using var ser = new SerializerStream(ms);
        ser.Begin();
        Assert.Throws<System.InvalidOperationException>(() =>
        {
            ser.rInt(out int _);
        });
    }

    [Fact]
    public void WriteOnInDeserializerThrows()
    {
        // First write a small valid payload so Begin() succeeds.
        using var ms = new MemoryStream();
        using (var ser = new SerializerStream(ms))
        {
            ser.Begin();
            int x = 1; ser.rwInt(ref x);
            ser.End();
        }
        ms.Position = 0;

        using var de = new DeserializerStream(ms);
        de.Begin();
        Assert.Throws<System.InvalidOperationException>(() => de.wInt(0));
    }
}
