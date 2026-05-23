// ABOUTME: Converts DBuilder.IO ImageData (RGBA8) into an Avalonia bitmap for UI thumbnails/previews.
// ABOUTME: Copies row-by-row honoring the writable-bitmap framebuffer stride.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DBuilder.IO;

namespace DBuilder.Editor;

public static class BitmapConvert
{
    /// <summary>Builds an Avalonia bitmap from RGBA8 image data, or null for empty/invalid input.</summary>
    public static Bitmap? ToBitmap(ImageData? img)
    {
        if (img is null || img.Width <= 0 || img.Height <= 0) return null;
        var wb = new WriteableBitmap(new PixelSize(img.Width, img.Height), new Vector(96, 96),
            PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
        {
            int rowBytes = img.Width * 4;
            for (int y = 0; y < img.Height; y++)
                Marshal.Copy(img.Rgba, y * rowBytes, IntPtr.Add(fb.Address, y * fb.RowBytes), rowBytes);
        }
        return wb;
    }
}
