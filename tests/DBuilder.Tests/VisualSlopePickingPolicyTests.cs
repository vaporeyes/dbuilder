// ABOUTME: Verifies UDB-style availability guards for visual slope picking commands.
// ABOUTME: Covers map-format and game-configuration plane equation support before editor dispatch uses the policy.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualSlopePickingPolicyTests
{
    [Fact]
    public void RejectsNonUdmfMaps()
    {
        var configuration = GameConfiguration.FromText("planeequationsupport = true;");

        bool canUse = VisualSlopePickingPolicy.CanUse(MapFormat.Doom, configuration, out string warning);

        Assert.False(canUse);
        Assert.Equal(VisualSlopePickingPolicy.UdmfRequiredMessage, warning);
    }

    [Fact]
    public void RejectsUdmfConfigWithoutPlaneEquationSupport()
    {
        var configuration = GameConfiguration.FromText("planeequationsupport = false;");

        bool canUse = VisualSlopePickingPolicy.CanUse(MapFormat.Udmf, configuration, out string warning);

        Assert.False(canUse);
        Assert.Equal(VisualSlopePickingPolicy.PlaneEquationRequiredMessage, warning);
    }

    [Fact]
    public void AllowsUdmfConfigWithPlaneEquationSupport()
    {
        var configuration = GameConfiguration.FromText("planeequationsupport = true;");

        bool canUse = VisualSlopePickingPolicy.CanUse(MapFormat.Udmf, configuration, out string warning);

        Assert.True(canUse);
        Assert.Equal("", warning);
    }

    [Fact]
    public void AdjacentVertexSlopeSelectionStatusMatchesUdbText()
    {
        Assert.Equal(
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionEnabledMessage,
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionStatus(true));
        Assert.Equal(
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionDisabledMessage,
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionStatus(false));
        Assert.Equal(
            "Adjacant selection of visual vertex slop handles is ENABLED",
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionEnabledMessage);
        Assert.Equal(
            "Adjacant selection of visual vertex slop handles is DISABLED",
            VisualSlopePickingPolicy.AdjacentVertexSlopeSelectionDisabledMessage);
    }
}
