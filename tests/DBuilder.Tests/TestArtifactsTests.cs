// ABOUTME: Verifies synthetic test fixtures can build representative IWAD, PWAD and PK3 resource stacks.
// ABOUTME: Keeps parity smoke tests independent from copyrighted game data.

using System;
using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class TestArtifactsTests
{
    [Fact]
    public void BuildsCopyrightFreeIwadPwadAndPk3ResourceStack()
    {
        string iwad = TestArtifacts.BuildIwadFile(
            ("PLAYPAL", TestArtifacts.GrayscalePlaypal()),
            ("F_START", Array.Empty<byte>()),
            ("BASEFLAT", TestArtifacts.SolidFlat(10)),
            ("F_END", Array.Empty<byte>()));
        string pwad = TestArtifacts.BuildPwadFile(
            ("F_START", Array.Empty<byte>()),
            ("PWADFLAT", TestArtifacts.SolidFlat(20)),
            ("F_END", Array.Empty<byte>()));
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/PK3FLAT.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))));

        try
        {
            using (var wad = new WAD(iwad, openreadonly: true))
                Assert.True(wad.IsIWAD);
            using (var wad = new WAD(pwad, openreadonly: true))
                Assert.False(wad.IsIWAD);

            using var resources = new ResourceManager();
            resources.AddBaseResource(iwad);
            resources.AddResource(pwad);
            resources.AddResource(pk3);

            Assert.Equal(new byte[] { 10, 10, 10, 255 }, resources.GetFlat("BASEFLAT")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 20, 20, 20, 255 }, resources.GetFlat("PWADFLAT")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 30, 31, 32, 255 }, resources.GetFlat("PK3FLAT")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(iwad);
            File.Delete(pwad);
            File.Delete(pk3);
        }
    }
}
