// ABOUTME: Tests Boom ANIMATED binary parsing and the built-in vanilla Doom animation table.
// ABOUTME: Built-in test uses a PK3 with FWATER1..4 flats and NO ANIMDEFS, so only the hardcoded table can drive it.

using System;
using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class BoomAnimatedTests
{
    private static void WriteName(byte[] buf, int off, string name)
    {
        var b = Encoding.ASCII.GetBytes(name);
        Array.Copy(b, 0, buf, off, Math.Min(b.Length, 9));
    }

    [Fact]
    public void ParsesAnimatedRecords()
    {
        var data = new byte[23 * 2];
        data[0] = 0;                         // flat
        WriteName(data, 1, "FWATER4");       // last
        WriteName(data, 10, "FWATER1");      // first
        BitConverter.GetBytes(8).CopyTo(data, 19); // speed
        data[23] = 0xFF;                     // terminator record

        var entries = BoomAnimated.Parse(data);
        Assert.Single(entries);
        Assert.False(entries[0].IsTexture);
        Assert.Equal("FWATER1", entries[0].First);
        Assert.Equal("FWATER4", entries[0].Last);
        Assert.Equal(8, entries[0].Tics);
    }

    [Fact]
    public void TextureFlagDetected()
    {
        var data = new byte[23 * 2];
        data[0] = 1;                         // texture
        WriteName(data, 1, "BLODGR4");
        WriteName(data, 10, "BLODGR1");
        BitConverter.GetBytes(8).CopyTo(data, 19);
        data[23] = 0xFF;
        Assert.True(BoomAnimated.Parse(data)[0].IsTexture);
    }

    [Fact]
    public void BuiltinVanillaAnimationsApplyWithoutAnyLump()
    {
        // FWATER1..4 flats present, but no ANIMATED/ANIMDEFS lump: the hardcoded Doom table must still animate.
        byte[] Px(byte v) => TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, v, v, v, 255));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/FWATER1.png", Px(1)),
            ("flats/FWATER2.png", Px(2)),
            ("flats/FWATER3.png", Px(3)),
            ("flats/FWATER4.png", Px(4)));
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.True(rm.HasAnimations);
            Assert.Equal("FWATER1", rm.CurrentFlatFrame("FWATER1", 0.0));
            Assert.Equal("FWATER2", rm.CurrentFlatFrame("FWATER1", 0.25)); // tics 8 -> step 1 by ~0.23s
        }
        finally { File.Delete(pk3); }
    }
}
