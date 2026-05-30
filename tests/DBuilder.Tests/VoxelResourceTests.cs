// ABOUTME: Tests ResourceManager voxel resource discovery and VOXELDEF sprite mapping.
// ABOUTME: Uses synthetic WAD and PK3 fixtures instead of copyrighted voxel assets.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class VoxelResourceTests
{
    [Fact]
    public void DiscoversVoxelModelsFromWadAndPk3Resources()
    {
        byte[] wadVoxel = { 1, 2, 3, 4 };
        byte[] pk3Voxel = { 5, 6, 7, 8 };
        string wad = TestArtifacts.BuildPwadFile(
            ("VX_START", []),
            ("BAR1", wadVoxel),
            ("VX_END", []));
        string pk3 = TestArtifacts.BuildPk3(("voxels/CYBR.kvx", pk3Voxel));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);
            resources.AddResource(pk3);

            Assert.Contains("BAR1", resources.GetVoxelNames());
            Assert.Contains("CYBR", resources.GetVoxelNames());
            Assert.Equal(wadVoxel, resources.GetVoxelBytes("BAR1"));
            Assert.Equal(pk3Voxel, resources.GetVoxelBytes("CYBR"));
        }
        finally
        {
            File.Delete(wad);
            File.Delete(pk3);
        }
    }

    [Fact]
    public void ConfiguredWadVoxelRangesProvideAndPrioritizeVoxelModels()
    {
        var config = GameConfiguration.FromText("""
            voxels
            {
                models { start = "VM_START"; end = "VM_END"; }
            }
            """);
        byte[] fallbackVoxel = { 1, 2, 3, 4 };
        byte[] configuredVoxel = { 9, 8, 7, 6 };
        string wad = TestArtifacts.BuildPwadFile(
            ("VX_START", []),
            ("BAR1", fallbackVoxel),
            ("VX_END", []),
            ("VM_START", []),
            ("BAR1", configuredVoxel),
            ("VM_END", []));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(wad);

            Assert.Equal(fallbackVoxel, resources.GetVoxelBytes("BAR1"));

            resources.Configuration = config;

            Assert.Contains("BAR1", resources.GetVoxelNames());
            Assert.Equal(configuredVoxel, resources.GetVoxelBytes("BAR1"));
        }
        finally
        {
            File.Delete(wad);
        }
    }

    [Fact]
    public void VoxeldefMappedSpritesResolveToVoxelPlaceholders()
    {
        byte[] voxel = { 9, 10, 11, 12 };
        string pk3 = TestArtifacts.BuildPk3(
            ("VOXELDEF.txt", Encoding.ASCII.GetBytes("BAR1 = models/barrel.kvx { scale = 1.5 }\n")),
            ("models/BARREL.kvx", voxel));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            Assert.Equal("MODELS/BARREL.KVX", resources.GetVoxelModelForSprite("BAR1A0"));
            Assert.Equal(voxel, resources.GetVoxelBytes("models/barrel.kvx"));

            var sprite = resources.GetSprite("BAR1A0");
            Assert.NotNull(sprite);
            Assert.Equal(64, sprite!.Width);
            Assert.Equal(64, sprite.Height);
            Assert.Equal(32, sprite.OffsetX);
            Assert.Equal(63, sprite.OffsetY);
            Assert.Equal(255, sprite.Rgba[3]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void DirectVoxelModelNamesResolveToVoxelPlaceholders()
    {
        byte[] voxel = { 13, 14, 15, 16 };
        string pk3 = TestArtifacts.BuildPk3(("voxels/BAR1.kvx", voxel));

        try
        {
            using var resources = new ResourceManager();
            resources.AddResource(pk3);

            var sprite = resources.GetSprite("BAR1A0");

            Assert.NotNull(sprite);
            Assert.Equal(64, sprite!.Width);
            Assert.Equal(64, sprite.Height);
            Assert.Equal(32, sprite.OffsetX);
            Assert.Equal(63, sprite.OffsetY);
            Assert.Equal(255, sprite.Rgba[3]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }
}
