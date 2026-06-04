// ABOUTME: Models UDB ImageDrawingExample plugin and ImageExampleMode metadata.
// ABOUTME: Keeps the sample mode action, resource, and presentation intent testable without editor UI.

namespace DBuilder.Map;

public sealed record ImageExampleModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool UseByDefault);

public sealed record ImageExamplePresentationDescriptor(
    string Layer,
    string BlendingMode,
    double Alpha,
    bool Transform);

public sealed record ImageExampleLifecyclePlan(
    bool LoadEmbeddedImage,
    bool LoadImageImmediately,
    bool ThrowOnLoadFailure,
    bool CreateTexture,
    bool SetOverlayPresentation,
    bool DisposeImage,
    bool ReturnToPreviousStableMode);

public sealed record ImageExampleRedrawPlan(
    bool CallBaseRedraw,
    bool StartOverlayCleared,
    double X,
    double Y,
    double Width,
    double Height,
    string FillColor,
    bool Transform,
    bool FinishOverlay,
    bool Present);

public static class ImageExampleModeModel
{
    public const string PluginName = "Image Drawing Example";
    public const string EmbeddedImageResource = "CodeImp.DoomBuilder.Plugins.ImageDrawingExample.exampleimage.png";

    public static ImageExampleModeDescriptor ModeDescriptor { get; } = new(
        "Image Example",
        "imageexamplemode",
        "ImageIcon.png",
        300,
        "002_tools",
        UseByDefault: true);

    public static ImageExamplePresentationDescriptor Presentation { get; } = new(
        "Overlay",
        "None",
        1.0,
        Transform: false);

    public static ImageExampleLifecyclePlan EngagePlan { get; } = new(
        LoadEmbeddedImage: true,
        LoadImageImmediately: true,
        ThrowOnLoadFailure: true,
        CreateTexture: true,
        SetOverlayPresentation: true,
        DisposeImage: false,
        ReturnToPreviousStableMode: false);

    public static ImageExampleLifecyclePlan DisengagePlan { get; } = new(
        LoadEmbeddedImage: false,
        LoadImageImmediately: false,
        ThrowOnLoadFailure: false,
        CreateTexture: false,
        SetOverlayPresentation: false,
        DisposeImage: true,
        ReturnToPreviousStableMode: false);

    public static ImageExampleLifecyclePlan CancelPlan { get; } = new(
        LoadEmbeddedImage: false,
        LoadImageImmediately: false,
        ThrowOnLoadFailure: false,
        CreateTexture: false,
        SetOverlayPresentation: false,
        DisposeImage: false,
        ReturnToPreviousStableMode: true);

    public static ImageExampleRedrawPlan RedrawPlan { get; } = new(
        CallBaseRedraw: true,
        StartOverlayCleared: true,
        X: 20.0,
        Y: 20.0,
        Width: 428.0,
        Height: 332.0,
        FillColor: "White",
        Transform: false,
        FinishOverlay: true,
        Present: true);
}
