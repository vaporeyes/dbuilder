// ABOUTME: Verifies UDB reference manual target metadata used by Help menu commands.
// ABOUTME: Keeps the default manual topic aligned with upstream MainForm.ShowHelp calls.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ReferenceManualModelTests
{
    [Fact]
    public void DefaultReferenceManualTargetMatchesUdb()
    {
        Assert.Equal("Refmanual.chm", ReferenceManualModel.HelpFile);
        Assert.Equal("introduction.html", ReferenceManualModel.IntroductionTopic);
        Assert.Equal("Reference Manual: introduction.html", ReferenceManualModel.StatusText(ReferenceManualModel.IntroductionTopic));
    }
}
