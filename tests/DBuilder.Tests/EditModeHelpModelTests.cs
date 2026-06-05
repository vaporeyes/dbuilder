// ABOUTME: Verifies classic 2D edit modes resolve to UDB reference manual topics.
// ABOUTME: Keeps Help > About This Editing Mode target behavior aligned with upstream mode OnHelp calls.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class EditModeHelpModelTests
{
    [Theory]
    [InlineData("Vertices", "e_vertices.html")]
    [InlineData("Linedefs", "e_linedefs.html")]
    [InlineData("Sectors", "e_sectors.html")]
    [InlineData("Things", "e_things.html")]
    public void TopicForModeMatchesUdbClassicModeHelp(string mode, string expected)
        => Assert.Equal(expected, EditModeHelpModel.TopicForMode(mode));

    [Fact]
    public void UnknownModeFallsBackToIntroduction()
    {
        Assert.Equal(ReferenceManualModel.IntroductionTopic, EditModeHelpModel.TopicForMode("Unknown"));
        Assert.Equal("Editing mode help: e_linedefs.html", EditModeHelpModel.StatusText(EditModeHelpModel.LinedefsTopic));
    }
}
