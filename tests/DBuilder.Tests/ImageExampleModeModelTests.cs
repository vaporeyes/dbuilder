// ABOUTME: Tests ImageDrawingExample plugin metadata against UDB source attributes and resources.
// ABOUTME: Covers mode descriptor, plugin name, embedded image resource, and overlay-only presentation intent.

using DBuilder.Map;

namespace DBuilder.Tests;

public class ImageExampleModeModelTests
{
    [Fact]
    public void PluginMetadataMatchesUdbBuilderPlugAndMode()
    {
        Assert.Equal("Image Drawing Example", ImageExampleModeModel.PluginName);
        Assert.Equal("CodeImp.DoomBuilder.Plugins.ImageDrawingExample.exampleimage.png", ImageExampleModeModel.EmbeddedImageResource);
    }

    [Fact]
    public void ModeDescriptorMatchesUdbEditModeAttribute()
    {
        ImageExampleModeDescriptor descriptor = ImageExampleModeModel.ModeDescriptor;

        Assert.Equal("Image Example", descriptor.DisplayName);
        Assert.Equal("imageexamplemode", descriptor.SwitchAction);
        Assert.Equal("ImageIcon.png", descriptor.ButtonImage);
        Assert.Equal(300, descriptor.ButtonOrder);
        Assert.Equal("002_tools", descriptor.ButtonGroup);
        Assert.True(descriptor.UseByDefault);
    }

    [Fact]
    public void PresentationMatchesUdbOverlayLayer()
    {
        ImageExamplePresentationDescriptor presentation = ImageExampleModeModel.Presentation;

        Assert.Equal("Overlay", presentation.Layer);
        Assert.Equal("None", presentation.BlendingMode);
        Assert.Equal(1.0, presentation.Alpha);
        Assert.False(presentation.Transform);
    }
}
