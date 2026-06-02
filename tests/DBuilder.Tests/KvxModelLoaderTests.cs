// ABOUTME: Tests UDB-style KVX voxel model loading from binary model data.
// ABOUTME: Verifies slab face generation, pivot transforms, palette atlas pixels, radius, and loader errors.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class KvxModelLoaderTests
{
    [Fact]
    public void LoadsVoxelSlabsWithUdbPivotTransformUvsTextureAndRadius()
    {
        byte[] kvx = BuildKvx(
            xsize: 1,
            ysize: 1,
            zsize: 1,
            pivotX: 0.5f,
            pivotY: 0.5f,
            pivotZ: 0.5f,
            slabs: new[] { new KvxSlabSpec(0, 1, 1 | 2 | 4 | 8 | 16 | 32, new byte[] { 34 }) });

        KvxModelLoadResult result = KvxModelLoader.Load(kvx);

        Assert.Null(result.Errors);
        Assert.Equal(1, result.Radius);
        Assert.Equal(64, result.TextureWidth);
        Assert.Equal(64, result.TextureHeight);
        Assert.Equal(new PixelColor(255, 8, 0, 0), result.Palette[2]);
        Assert.Equal(new PixelColor(255, 136, 0, 0), result.TexturePixels[8 * 64 + 8]);

        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(16, mesh.Vertices.Count);
        Assert.Equal(36, mesh.Indices.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 0, 2 }, mesh.Indices.Take(6).ToArray());

        WorldVertex first = mesh.Vertices[0];
        Assert.Equal(-0.5f, first.x);
        Assert.Equal(0.5f, first.y);
        Assert.Equal(0.5f, first.z);
        Assert.Equal(2.0f / 16.0f, first.u);
        Assert.Equal(2.0f / 16.0f, first.v);
        Assert.Equal(-0.5f, result.Bounds.MinX);
        Assert.Equal(-0.5f, result.Bounds.MinY);
        Assert.Equal(-0.5f, result.Bounds.MinZ);
        Assert.Equal(0.5f, result.Bounds.MaxX);
        Assert.Equal(0.5f, result.Bounds.MaxY);
        Assert.Equal(0.5f, result.Bounds.MaxZ);
    }

    [Fact]
    public void UsesOverridePaletteAndReportsUdbStyleMalformedInputErrors()
    {
        byte[] kvx = BuildKvx(
            xsize: 1,
            ysize: 1,
            zsize: 1,
            pivotX: 0.0f,
            pivotY: 0.0f,
            pivotZ: 0.0f,
            slabs: new[] { new KvxSlabSpec(0, 1, 16, new byte[] { 1 }) });

        PixelColor[] overridePalette = Enumerable.Range(0, 256)
            .Select(i => new PixelColor(255, (byte)i, (byte)(255 - i), 10))
            .ToArray();

        KvxModelLoadResult result = KvxModelLoader.Load(kvx, overridePalette);

        Assert.Null(result.Errors);
        Assert.Equal(overridePalette[1], result.Palette[1]);
        Assert.Equal(overridePalette[1], result.TexturePixels[4]);

        byte[] badDimensions = (byte[])kvx.Clone();
        BitConverter.GetBytes(0).CopyTo(badDimensions, 4);
        Assert.Equal("KVX model dimensions must be positive", KvxModelLoader.Load(badDimensions).Errors);

        byte[] missingPalette = kvx[..^768];
        Assert.Equal("KVX model is missing palette data", KvxModelLoader.Load(missingPalette).Errors);
    }

    private static byte[] BuildKvx(
        int xsize,
        int ysize,
        int zsize,
        float pivotX,
        float pivotY,
        float pivotZ,
        IReadOnlyList<KvxSlabSpec> slabs)
    {
        using var slabData = new MemoryStream();
        using (var slabWriter = new BinaryWriter(slabData, Encoding.ASCII, leaveOpen: true))
        {
            foreach (KvxSlabSpec slab in slabs)
            {
                slabWriter.Write((byte)slab.Top);
                slabWriter.Write((byte)slab.Length);
                slabWriter.Write((byte)slab.Flags);
                foreach (byte color in slab.Colors)
                    slabWriter.Write(color);
            }
        }

        int xoffsetBytes = (xsize + 1) * 4;
        int xyoffsetBytes = xsize * (ysize + 1) * 2;
        int slabOffset = xoffsetBytes + xyoffsetBytes;
        int numBytes = 28 + slabOffset + (int)slabData.Length + 768;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(numBytes);
        writer.Write(xsize);
        writer.Write(ysize);
        writer.Write(zsize);
        writer.Write((int)(pivotX * 256.0f));
        writer.Write((int)(pivotY * 256.0f));
        writer.Write((int)(pivotZ * 256.0f));

        writer.Write(slabOffset);
        for (int i = 1; i < xsize + 1; i++)
            writer.Write(slabOffset + (int)slabData.Length);

        for (int x = 0; x < xsize; x++)
            for (int y = 0; y < ysize + 1; y++)
                writer.Write((short)0);

        writer.Write(slabData.ToArray());

        for (int i = 0; i < 256; i++)
        {
            writer.Write((byte)i);
            writer.Write((byte)0);
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }

    private sealed record KvxSlabSpec(int Top, int Length, int Flags, IReadOnlyList<byte> Colors);
}
