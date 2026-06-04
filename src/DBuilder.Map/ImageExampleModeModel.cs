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
}
