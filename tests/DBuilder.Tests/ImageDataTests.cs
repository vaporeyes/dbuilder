// ABOUTME: Tests CPU-side image data helpers used by resource loading and generated editor images.
// ABOUTME: Covers dynamic image creation and update validation for RGBA buffers.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ImageDataTests
{
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
