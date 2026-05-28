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
}
