// ABOUTME: Tests ResourceManager priority and namespace behavior for textures, flats, and sprites.
// ABOUTME: Uses synthetic PK3 resources so override order and same-name conflicts stay deterministic.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ResourcePriorityTests
{
    private static byte[] DoomPatch(byte index)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write((short)1);
        w.Write((short)1);
        w.Write((short)0);
        w.Write((short)0);
        w.Write((int)12);
        w.Write((byte)0);
        w.Write((byte)1);
        w.Write((byte)0);
        w.Write(index);
        w.Write((byte)0);
        w.Write((byte)0xFF);
        return ms.ToArray();
    }

    private static byte[] BuildNestedWadBytes(params (string name, byte[] bytes)[] lumps)
    {
        string path = TestArtifacts.BuildPwadFile(lumps);
        try
        {
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LaterPk3OverridesEarlierTextureAndSprite()
    {
        string lower = TestArtifacts.BuildPk3(
            ("textures/OVERRIDE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))),
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))));
        string higher = TestArtifacts.BuildPk3(
            ("textures/OVERRIDE.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 30, 31, 32, 255))),
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            Assert.Equal(new byte[] { 30, 31, 32, 255 }, rm.GetWallTexture("OVERRIDE")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetSprite("POSSA0")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }

    [Fact]
    public void LaterSpritePrefixOverridesEarlierRotationSet()
    {
        string lower = TestArtifacts.BuildPk3(
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))));
        string higher = TestArtifacts.BuildPk3(
            ("sprites/POSSA1.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetSprite("POSSA0")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }

    [Fact]
    public void NestedWadSpriteOverridesFolderSpriteInsidePk3()
    {
        byte[] nested = BuildNestedWadBytes(
            ("PLAYPAL", TestArtifacts.GrayscalePlaypal()),
            ("S_START", []),
            ("POSSA0", DoomPatch(70)),
            ("S_END", []));
        string pk3 = TestArtifacts.BuildPk3(
            ("sprites/POSSA0.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 20, 21, 22, 255))),
            ("nested.wad", nested));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(new byte[] { 70, 70, 70, 255 }, rm.GetSprite("POSSA0")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void NestedWadVoxelOverridesFolderVoxelInsidePk3()
    {
        byte[] nestedVoxel = { 9, 8, 7, 6 };
        byte[] folderVoxel = { 1, 2, 3, 4 };
        byte[] nested = BuildNestedWadBytes(
            ("VX_START", []),
            ("BAR1", nestedVoxel),
            ("VX_END", []));
        string pk3 = TestArtifacts.BuildPk3(
            ("voxels/BAR1.kvx", folderVoxel),
            ("nested.wad", nested));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(nestedVoxel, rm.GetVoxelBytes("BAR1"));
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void NestedWadModelOverridesFolderModelInsidePk3()
    {
        byte[] nestedModel = { 12, 13, 14 };
        byte[] folderModel = { 1, 2, 3 };
        byte[] nested = BuildNestedWadBytes(("ZOMBIE", nestedModel));
        string pk3 = TestArtifacts.BuildPk3(
            ("models/zombie.md3", folderModel),
            ("nested.wad", nested));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(nestedModel, rm.GetModelResourceBytes("models/zombie.md3"));
        }
        finally
        {
            File.Delete(pk3);
        }
    }

    [Fact]
    public void SameNameEntriesResolveByRequestedNamespace()
    {
        string pk3 = TestArtifacts.BuildPk3(
            ("flats/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 1, 2, 3, 255))),
            ("textures/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 4, 5, 6, 255))),
            ("sprites/SHARED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 7, 8, 9, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);

            Assert.Equal(new byte[] { 1, 2, 3, 255 }, rm.GetFlat("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 4, 5, 6, 255 }, rm.GetWallTexture("SHARED")!.Rgba[0..4]);
            Assert.Equal(new byte[] { 7, 8, 9, 255 }, rm.GetSprite("SHARED")!.Rgba[0..4]);
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void LaterSingleImageOverridesEarlierTexturesDefinition()
    {
        string lowerTextures =
            "WallTexture STACKED, 1, 1\n" +
            "{\n" +
            "    Patch \"LOWER\", 0, 0\n" +
            "}\n";
        string lower = TestArtifacts.BuildPk3(
            ("TEXTURES.txt", Encoding.ASCII.GetBytes(lowerTextures)),
            ("patches/LOWER.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 10, 11, 12, 255))));
        string higher = TestArtifacts.BuildPk3(
            ("textures/STACKED.png", TestArtifacts.Png(1, 1, TestArtifacts.SolidRgba(1, 1, 40, 41, 42, 255))));

        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(lower);
            rm.AddResource(higher);

            Assert.Equal(new byte[] { 40, 41, 42, 255 }, rm.GetWallTexture("STACKED")!.Rgba[0..4]);
        }
        finally
        {
            File.Delete(lower);
            File.Delete(higher);
        }
    }
}
