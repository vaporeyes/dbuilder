// ABOUTME: Maps classic 2D editor modes to the UDB reference manual topics.
// ABOUTME: Keeps Help > About This Editing Mode behavior testable outside the editor window.

namespace DBuilder.IO;

public static class EditModeHelpModel
{
    public const string VerticesTopic = "e_vertices.html";
    public const string LinedefsTopic = "e_linedefs.html";
    public const string SectorsTopic = "e_sectors.html";
    public const string ThingsTopic = "e_things.html";

    public static string TopicForMode(string mode)
        => mode switch
        {
            "Vertices" => VerticesTopic,
            "Linedefs" => LinedefsTopic,
            "Sectors" => SectorsTopic,
            "Things" => ThingsTopic,
            _ => ReferenceManualModel.IntroductionTopic,
        };

    public static string StatusText(string topic)
        => $"Editing mode help: {topic}";
}
