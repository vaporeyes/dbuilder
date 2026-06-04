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

    [Fact]
    public void EngagePlanLoadsResourceAndCreatesOverlayPresentationLikeUdb()
    {
        ImageExampleLifecyclePlan plan = ImageExampleModeModel.EngagePlan;

        Assert.True(plan.LoadEmbeddedImage);
        Assert.True(plan.LoadImageImmediately);
        Assert.True(plan.ThrowOnLoadFailure);
        Assert.True(plan.CreateTexture);
        Assert.True(plan.SetOverlayPresentation);
        Assert.False(plan.DisposeImage);
        Assert.False(plan.ReturnToPreviousStableMode);
    }

    [Fact]
    public void DisengageAndCancelPlansMatchUdbModeLifecycle()
    {
        ImageExampleLifecyclePlan disengage = ImageExampleModeModel.DisengagePlan;
        ImageExampleLifecyclePlan cancel = ImageExampleModeModel.CancelPlan;

        Assert.True(disengage.DisposeImage);
        Assert.False(disengage.ReturnToPreviousStableMode);
        Assert.False(disengage.LoadEmbeddedImage);

        Assert.True(cancel.ReturnToPreviousStableMode);
        Assert.False(cancel.DisposeImage);
        Assert.False(cancel.SetOverlayPresentation);
    }

    [Fact]
    public void RedrawPlanMatchesUdbScreenSpaceImageRectangle()
    {
        ImageExampleRedrawPlan plan = ImageExampleModeModel.RedrawPlan;

        Assert.True(plan.CallBaseRedraw);
        Assert.True(plan.StartOverlayCleared);
        Assert.Equal(20.0, plan.X);
        Assert.Equal(20.0, plan.Y);
        Assert.Equal(428.0, plan.Width);
        Assert.Equal(332.0, plan.Height);
        Assert.Equal("White", plan.FillColor);
        Assert.False(plan.Transform);
        Assert.True(plan.FinishOverlay);
        Assert.True(plan.Present);
    }
}
