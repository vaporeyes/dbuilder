// ABOUTME: Tests CPU-side image data helpers used by resource loading and generated editor images.
// ABOUTME: Covers dynamic image creation and update validation for RGBA buffers.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ImageDataTests
{
    [Fact]
    public void SolidColorImageFillsEveryPixel()
    {
        var image = ImageData.CreateSolidColor(2, 1, 10, 20, 30, 40);

        Assert.False(image.IsDynamic);
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 10, 20, 30, 40 }, image.Rgba);
    }

    [Fact]
    public void UnknownImageCreatesOpaqueCheckerPattern()
    {
        var image = ImageData.CreateUnknown(8, 8);

        Assert.Equal(8, image.Width);
        Assert.Equal(8, image.Height);
        Assert.Equal(new byte[] { 224, 32, 224, 255 }, image.Rgba[0..4]);
        Assert.Equal(new byte[] { 32, 32, 32, 255 }, image.Rgba[4..8]);
    }

    [Fact]
    public void DynamicImageCopiesInitialPixelsAndAllowsSameSizeUpdate()
    {
        byte[] initial = { 1, 2, 3, 4 };
        var image = ImageData.CreateDynamic(1, 1, initial);
        initial[0] = 9;

        Assert.True(image.IsDynamic);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Rgba);

        byte[] updated = { 5, 6, 7, 8 };
        image.UpdatePixels(updated);

        Assert.Equal(updated, image.Rgba);
    }

    [Fact]
    public void DynamicImageRejectsMismatchedPixelBuffer()
    {
        Assert.Throws<ArgumentException>(() => ImageData.CreateDynamic(2, 2, new byte[4]));

        var image = ImageData.CreateDynamic(1, 1, new byte[4]);

        Assert.Throws<ArgumentException>(() => image.UpdatePixels(new byte[8]));
    }

    [Fact]
    public void StaticImageCannotBeUpdated()
    {
        var image = new ImageData(1, 1, new byte[4]);

        Assert.Throws<InvalidOperationException>(() => image.UpdatePixels(new byte[4]));
    }

    [Fact]
    public void CreateIndexedStoresPaletteIndexInRedAndPreservesAlphaAndMetadata()
    {
        byte[] paletteBytes = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            paletteBytes[i * 3] = (byte)i;
            paletteBytes[i * 3 + 1] = (byte)i;
            paletteBytes[i * 3 + 2] = (byte)i;
        }

        var palette = DoomPalette.FromBytes(paletteBytes);
        var image = new ImageData(
            2,
            1,
            [10, 10, 10, 128, 100, 99, 101, 255],
            OffsetX: 3,
            OffsetY: 4,
            ScaleX: 2.0,
            ScaleY: 3.0);

        ImageData indexed = image.CreateIndexed(palette);

        Assert.Equal(2, indexed.Width);
        Assert.Equal(1, indexed.Height);
        Assert.Equal(3, indexed.OffsetX);
        Assert.Equal(4, indexed.OffsetY);
        Assert.Equal(2.0, indexed.ScaleX);
        Assert.Equal(3.0, indexed.ScaleY);
        Assert.Equal(new byte[] { 10, 0, 0, 128, 100, 0, 0, 255 }, indexed.Rgba);
        Assert.False(indexed.IsDynamic);
    }

    [Fact]
    public void CreateIndexedRejectsInvalidSourceBuffer()
    {
        var palette = DoomPalette.FromBytes(new byte[768]);
        var image = new ImageData(2, 2, new byte[4]);

        Assert.Throws<ArgumentException>(() => image.CreateIndexed(palette));
    }
}
