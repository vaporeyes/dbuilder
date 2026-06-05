// ABOUTME: Defines the UDB reference manual file and default topic targets.
// ABOUTME: Keeps Help menu reference-manual behavior testable outside the editor window.

namespace DBuilder.IO;

public static class ReferenceManualModel
{
    public const string HelpFile = "Refmanual.chm";
    public const string IntroductionTopic = "introduction.html";

    public static string StatusText(string topic)
        => $"Reference Manual: {topic}";
}
