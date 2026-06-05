// ABOUTME: Tests 2D thing icon zoom policy for compact map-scale rendering.
// ABOUTME: Verifies sprite-backed things collapse to small markers only in dense fixed-scale views.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ThingIconRenderPolicyTests
{
    [Fact]
    public void UsesCompactMarkersAtMapScaleWhenThingSizeIsFixed()
    {
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: true,
            thingArrows: false));
    }

    [Fact]
    public void KeepsSpritesWhenZoomedInOrAlreadyUsingArrowMarkers()
    {
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: true,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: true,
            thingArrows: true));
    }

    [Fact]
    public void CompactMarkersUseSmallerScreenFootprint()
    {
        Assert.True(ThingIconRenderPolicy.MarkerBaseSize(compactMarkers: true)
            < ThingIconRenderPolicy.MarkerBaseSize(compactMarkers: false));
        Assert.True(ThingIconRenderPolicy.DirectionTickBaseSize(compactMarkers: true)
            < ThingIconRenderPolicy.DirectionTickBaseSize(compactMarkers: false));
    }
}
