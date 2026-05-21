// ABOUTME: Decoded Doom-picture-format image - sprite/wall-patch dimensions, render offsets, and RGBA8 bytes.
// ABOUTME: Output buffer is laid out row-major, top-to-bottom, RGBA bytes per pixel, ready for Texture.SetPixelsRgba8.

namespace DBuilder.IO;

public sealed class DoomPicture
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>X render offset: pixels to the left of the sprite hot-spot. Used by the engine when blitting; not needed for raw display.</summary>
    public int OffsetX { get; }

    /// <summary>Y render offset: pixels above the sprite hot-spot.</summary>
    public int OffsetY { get; }

    /// <summary>Row-major RGBA8 bytes, length = Width * Height * 4. Transparent pixels are zeroed (R=G=B=A=0).</summary>
    public byte[] Rgba8 { get; }

    public DoomPicture(int width, int height, int offsetX, int offsetY, byte[] rgba8)
    {
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Rgba8 = rgba8;
    }
}
