// ABOUTME: Tests 2D thing icon zoom policy for compact map-scale rendering.
// ABOUTME: Verifies sprite-backed things collapse to small markers only in dense fixed-scale views.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ThingIconRenderPolicyTests
{
    [Fact]
    public void UsesCompactMarkersAtOverviewScaleWhenThingSizeIsFixed()
    {
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: true,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: 1.0,
            fixedThingsScale: true,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: false,
            thingArrows: false));
    }

    [Fact]
    public void KeepsSpritesAtCloseZoomWithoutFixedThingScale()
    {
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: false,
            thingArrows: false));
    }

    [Fact]
    public void UsesCompactMarkersBeforeSpritesCrowdAtOverviewScale()
    {
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: 0.125,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: false,
            thingArrows: false));
    }

    [Fact]
    public void KeepsSpritesWhenZoomedInAndCompactsArrowMarkersAtOverviewScale()
    {
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: true,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
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

    [Fact]
    public void CompactMarkersKeepFixedScreenFootprintWithoutFixedThingScale()
    {
        double worldSize = ThingIconRenderPolicy.MarkerWorldSize(
            baseSize: 4,
            viewScale: 0.25,
            fixedThingsScale: false,
            compactMarkers: true);

        Assert.Equal(1, worldSize);
    }

    [Fact]
    public void RegularMarkersKeepMapSizeWithoutFixedThingScale()
    {
        double worldSize = ThingIconRenderPolicy.MarkerWorldSize(
            baseSize: 10,
            viewScale: 0.25,
            fixedThingsScale: false,
            compactMarkers: false);

        Assert.Equal(10, worldSize);
    }

    [Fact]
    public void OverviewMarkersUseSmallScreenFootprint()
    {
        Assert.True(ThingIconRenderPolicy.UseOverviewMarkers(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseOverviewMarkers(
            viewScale: ThingIconRenderPolicy.OverviewMarkerScaleThreshold - 0.01,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseOverviewMarkers(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold - 0.01,
            thingArrows: true));
        Assert.True(ThingIconRenderPolicy.UseOverviewMarkers(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: true));
        Assert.True(ThingIconRenderPolicy.MarkerBaseSize(compactMarkers: true, overviewMarkers: true)
            < ThingIconRenderPolicy.MarkerBaseSize(compactMarkers: true));
    }

    [Fact]
    public void FarOverviewStartsAfterOverviewMarkers()
    {
        Assert.True(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold
            > ThingIconRenderPolicy.OverviewMarkerScaleThreshold);
        Assert.Equal(1.0, ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold);
        Assert.False(ThingIconRenderPolicy.UseFarOverviewMarkers(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold - 0.01,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseFarOverviewMarkers(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseFarOverviewMarkers(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: true));
        Assert.True(ThingIconRenderPolicy.MarkerBaseSize(
                compactMarkers: true,
                overviewMarkers: true,
                farOverviewMarkers: true)
            < ThingIconRenderPolicy.MarkerBaseSize(compactMarkers: true, overviewMarkers: true));
    }

    [Fact]
    public void OverviewMarkersSuppressDirectionTicks()
    {
        Assert.False(ThingIconRenderPolicy.ShouldDrawDirectionTicks(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.ShouldDrawDirectionTicks(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.ShouldDrawDirectionTicks(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.ShouldDrawDirectionTicks(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            thingArrows: true));
    }

    [Fact]
    public void OverviewCullingStartsWithCompactMarkers()
    {
        Assert.True(ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(
            ThingIconRenderPolicy.OverlapCullScaleThreshold,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(
            ThingIconRenderPolicy.OverlapCullScaleThreshold - 0.01,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            thingArrows: true));
        Assert.True(ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(
            ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            thingArrows: true));

        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(0));
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.OverviewCullCellPixels - 0.01));
        Assert.Equal(1, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.OverviewCullCellPixels));
        Assert.Equal(144.0, ThingIconRenderPolicy.OverviewCullCellPixels);
    }

    [Fact]
    public void FarOverviewCullsWithLargerScreenCells()
    {
        Assert.Equal(144.0, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold - 0.01,
            thingArrows: false));
        Assert.Equal(320.0, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(320.0, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: true));
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.FarOverviewCullCellPixels - 0.01,
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(1, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.FarOverviewCullCellPixels,
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
    }

    [Fact]
    public void SkipsThingsWhoseProjectedRadiusIsTooSmall()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 10,
            viewScale: 8,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 12,
            viewScale: 8,
            fixedThingsScale: false));
    }

    [Fact]
    public void FixedThingScalePreservesProjectedRadiusAtCloseZoom()
    {
        Assert.Equal(48, ThingIconRenderPolicy.ProjectedThingScreenRadius(
            mapRadius: 80,
            viewScale: 0.5,
            fixedThingsScale: true));
        Assert.True(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 80,
            viewScale: 0.5,
            fixedThingsScale: true));
    }

    [Fact]
    public void SpriteHalfSizeUsesThingRadiusInsteadOfRawSpritePixels()
    {
        var (halfWidth, halfHeight) = ThingIconRenderPolicy.SpriteHalfSize(
            imageWidth: 64,
            imageHeight: 32,
            thingRadius: 20,
            viewScale: 6,
            fixedThingsScale: true);

        Assert.Equal(18, halfWidth);
        Assert.Equal(9, halfHeight);
    }

    [Fact]
    public void FixedThingScaleCapsOnlyWhenZoomedInTooFar()
    {
        Assert.Equal(20, ThingIconRenderPolicy.ScaledWorldRadius(
            mapRadius: 20,
            viewScale: 6,
            fixedThingsScale: true));
        Assert.Equal(24, ThingIconRenderPolicy.ScaledWorldRadius(
            mapRadius: 80,
            viewScale: 0.5,
            fixedThingsScale: true));
    }
}
