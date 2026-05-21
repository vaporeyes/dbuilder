// ABOUTME: Lump-name encoding round-trip and full in-memory WAD round-trip tests.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class LumpAndWadTests
{
    [Fact]
    public void FixedNamePadsTo8BytesAndUppercases()
    {
        byte[] fn = Lump.MakeFixedName("things", WAD.ENCODING);
        Assert.Equal(8, fn.Length);
        Assert.Equal("THINGS", Encoding.ASCII.GetString(fn, 0, 6));
        Assert.Equal(0, fn[6]);
        Assert.Equal(0, fn[7]);
    }

    [Fact]
    public void NormalNameStripsNullPadding()
    {
        byte[] fn = new byte[] { (byte)'L', (byte)'I', (byte)'N', (byte)'E', (byte)'D', (byte)'E', (byte)'F', (byte)'S' };
        Assert.Equal("LINEDEFS", Lump.MakeNormalName(fn, WAD.ENCODING));

        byte[] padded = new byte[] { (byte)'M', (byte)'A', (byte)'P', (byte)'0', (byte)'1', 0, 0, 0 };
        Assert.Equal("MAP01", Lump.MakeNormalName(padded, WAD.ENCODING));
    }

    [Fact]
    public void MakeLongNameTruncatesAtClassicLength()
    {
        long classic = Lump.MakeLongName("SUPERLONGTEXTURENAME", useLongNames: false);
        long classicTruncated = Lump.MakeLongName("SUPERLON", useLongNames: false);
        Assert.Equal(classicTruncated, classic);
    }

    [Fact]
    public void MakeLongNameLongVariantDoesNotTruncate()
    {
        long longName = Lump.MakeLongName("SUPERLONGTEXTURENAME", useLongNames: true);
        long classic = Lump.MakeLongName("SUPERLONGTEXTURENAME", useLongNames: false);
        Assert.NotEqual(classic, longName);
    }

    [Fact]
    public void EmptyWadInMemoryHasPwadHeader()
    {
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        Assert.False(wad.IsIWAD);
        Assert.Empty(wad.Lumps);

        ms.Position = 0;
        var sig = new byte[4];
        ms.Read(sig, 0, 4);
        Assert.Equal("PWAD", Encoding.ASCII.GetString(sig));
    }

    [Fact]
    public void InsertedLumpsRoundTripThroughWad()
    {
        byte[] thingsData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        byte[] linedefsData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        var ms = new MemoryStream();
        // Write
        using (var wad = new WAD(ms))
        {
            var things = wad.Insert("THINGS", 0, thingsData.Length)!;
            things.Stream.Write(thingsData, 0, thingsData.Length);

            var linedefs = wad.Insert("LINEDEFS", 1, linedefsData.Length)!;
            linedefs.Stream.Write(linedefsData, 0, linedefsData.Length);

            wad.WriteHeaders();
        }

        // Re-open the same MemoryStream and verify the lumps come back
        ms.Position = 0;
        using (var wad2 = new WAD(ms, openreadonly: true))
        {
            Assert.Equal(2, wad2.Lumps.Count);
            Assert.Equal("THINGS", wad2.Lumps[0].Name);
            Assert.Equal("LINEDEFS", wad2.Lumps[1].Name);

            var bytesThings = wad2.Lumps[0].Stream.ReadAllBytes();
            Assert.Equal(thingsData, bytesThings);

            var bytesLines = wad2.Lumps[1].Stream.ReadAllBytes();
            Assert.Equal(linedefsData, bytesLines);
        }
    }

    [Fact]
    public void FindLumpFindsByCaseInsensitiveName()
    {
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        wad.Insert("MAP01", 0, 0);
        wad.Insert("VERTEXES", 1, 0);

        Assert.NotNull(wad.FindLump("map01"));
        Assert.NotNull(wad.FindLump("VERTEXES"));
        Assert.Null(wad.FindLump("MAP02"));
    }

    [Fact]
    public void FindLumpReturnsNullForNamesOver8Chars()
    {
        // FindLumpIndex shortcuts long names; for the WAD-level API names over 8 chars are invalid.
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        wad.Insert("MAP01", 0, 0);
        Assert.Null(wad.FindLump("VERYLONGNAME"));
    }

    [Fact]
    public void RemoveAtDropsLump()
    {
        using var ms = new MemoryStream();
        using var wad = new WAD(ms);
        wad.Insert("A", 0, 4);
        wad.Insert("B", 1, 4);
        wad.Insert("C", 2, 4);
        Assert.Equal(3, wad.Lumps.Count);

        wad.RemoveAt(1);
        Assert.Equal(2, wad.Lumps.Count);
        Assert.Equal("A", wad.Lumps[0].Name);
        Assert.Equal("C", wad.Lumps[1].Name);
    }

    [Fact]
    public void ReadOnlyWadRejectsMutation()
    {
        // Build a tiny valid WAD first
        var ms = new MemoryStream();
        using (var wad = new WAD(ms))
        {
            wad.Insert("X", 0, 0);
            wad.WriteHeaders();
        }

        ms.Position = 0;
        using var readOnly = new WAD(ms, openreadonly: true);
        Assert.True(readOnly.IsReadOnly);
        // Insert returns null on readonly archives ([ZZ]'s behavior).
        Assert.Null(readOnly.Insert("Y", 0, 0));
    }
}
