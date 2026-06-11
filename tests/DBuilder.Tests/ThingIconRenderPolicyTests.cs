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
        Assert.Equal(ThingIconRenderPolicy.SpriteIconScaleThreshold, ThingIconRenderPolicy.CompactMarkerScaleThreshold);
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.CompactMarkerScaleThreshold,
            fixedThingsScale: false,
            thingArrows: false));
    }

    [Fact]
    public void CompactThresholdStartsWhenSpriteIconsCollapse()
    {
        Assert.Equal(0.025, ThingIconRenderPolicy.CompactMarkerScaleThreshold);
        Assert.Equal(0.02, ThingIconRenderPolicy.MinimumInteractiveViewScale);
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.MinimumInteractiveViewScale,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.False(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.CompactMarkerScaleThreshold - 0.01,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.UseOverviewMarkers(
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold,
            thingArrows: false));
    }

    [Fact]
    public void IntermediateOverviewZoomCollapsesSpritesBeforeTheyPileUp()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 0.03,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.UseCompactMarkers(
            viewScale: 0.03,
            fixedThingsScale: false,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.ShouldCullOverlappingOverviewThings(
            viewScale: 0.03,
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
    public void SpriteIconsCollapseBeforeTheyBecomeUnreadable()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold + 0.01,
            fixedThingsScale: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 2,
            fixedThingsScale: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 2,
            fixedThingsScale: true));
    }

    [Fact]
    public void SpriteIconsRenderOnlyWhenScreenFootprintStaysReadable()
    {
        Assert.Equal(0.025, ThingIconRenderPolicy.SpriteIconScaleThreshold);
        Assert.Equal(8.0, ThingIconRenderPolicy.MinimumSpriteScreenRadius);
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 1.2,
            fixedThingsScale: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 0.04,
            fixedThingsScale: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 0.05,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 0.02,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold - 0.01,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 40,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold - 0.01,
            fixedThingsScale: false));
    }

    [Fact]
    public void FixedSizeSpriteIconsCollapseAtOverviewScale()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold,
            fixedThingsScale: false,
            fixedSize: true));
        Assert.True(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 32,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold - 0.01,
            fixedThingsScale: false,
            fixedSize: true));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 32,
            viewScale: 1.0,
            fixedThingsScale: false,
            fixedSize: true));
    }

    [Fact]
    public void FixedThingScaleSpriteIconsRenderWhenScaleIsClamped()
    {
        Assert.True(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 80,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold - 0.01,
            fixedThingsScale: true));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 80,
            viewScale: ThingIconRenderPolicy.SpriteIconScaleThreshold,
            fixedThingsScale: true));
        Assert.False(ThingIconRenderPolicy.ShouldRenderSpriteIcon(
            mapRadius: 20,
            viewScale: 0.5,
            fixedThingsScale: true));
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
        Assert.Equal(48.0, ThingIconRenderPolicy.OverviewCullCellPixels);
    }

    [Fact]
    public void FarOverviewCullsWithLargerScreenCells()
    {
        Assert.Equal(48.0, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold - 0.01,
            thingArrows: false));
        Assert.True(ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 4,
            thingArrows: false)
            > ThingIconRenderPolicy.OverviewCullCellPixels);
        Assert.Equal(ThingIconRenderPolicy.FarOverviewCullCellPixels, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 4,
            thingArrows: false));
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.FarOverviewCullCellPixels - 0.01,
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 4,
            thingArrows: false));
        Assert.Equal(1, ThingIconRenderPolicy.OverviewCullCell(
            ThingIconRenderPolicy.FarOverviewCullCellPixels,
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 4,
            thingArrows: false));
    }

    [Fact]
    public void OverviewCullCellsGrowAsZoomMovesOut()
    {
        Assert.Equal(48.0, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(ThingIconRenderPolicy.MaxFarOverviewCullCellPixels, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 16,
            thingArrows: false));
        Assert.Equal(ThingIconRenderPolicy.MaxFarOverviewCullCellPixels, ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            ThingIconRenderPolicy.OverviewMarkerScaleThreshold * 25,
            thingArrows: false));
    }

    [Fact]
    public void DeepOverviewCullCellsAreWideEnoughToPreventThingIconPileups()
    {
        double cellPixels = ThingIconRenderPolicy.OverviewCullCellPixelsFor(
            viewScale: 0.8,
            thingArrows: false);

        Assert.Equal(640.0, cellPixels);
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: 120,
            viewScale: 0.8,
            thingArrows: false));
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: 600,
            viewScale: 0.8,
            thingArrows: false));
    }

    [Fact]
    public void OverviewCellsGroupNearbyScreenThings()
    {
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: 47.99,
            viewScale: ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(1, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: 48.0,
            viewScale: ThingIconRenderPolicy.OverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(0, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: ThingIconRenderPolicy.OverviewCullCellPixelsFor(
                ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
                thingArrows: false) - 0.01,
            viewScale: ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
        Assert.Equal(1, ThingIconRenderPolicy.OverviewCullCell(
            screenCoordinate: ThingIconRenderPolicy.OverviewCullCellPixelsFor(
                ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
                thingArrows: false),
            viewScale: ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold,
            thingArrows: false));
    }

    [Fact]
    public void OverviewCellsRenderOneThingAndPreferSelectedThings()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderOverviewCellThing(
            thingSelected: false,
            cellHasSelectedThing: true,
            cellAlreadyRendered: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderOverviewCellThing(
            thingSelected: true,
            cellHasSelectedThing: true,
            cellAlreadyRendered: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderOverviewCellThing(
            thingSelected: true,
            cellHasSelectedThing: true,
            cellAlreadyRendered: true));
        Assert.True(ThingIconRenderPolicy.ShouldRenderOverviewCellThing(
            thingSelected: false,
            cellHasSelectedThing: false,
            cellAlreadyRendered: false));
    }

    [Fact]
    public void OverviewCellsChooseSelectedRepresentativeBeforeRadius()
    {
        Assert.True(ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
            existingSelected: false,
            existingMapRadius: 64,
            candidateSelected: true,
            candidateMapRadius: 8));
        Assert.False(ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
            existingSelected: true,
            existingMapRadius: 8,
            candidateSelected: false,
            candidateMapRadius: 64));
    }

    [Fact]
    public void OverviewCellsChooseLargestRepresentativeWhenSelectionMatches()
    {
        Assert.True(ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
            existingSelected: false,
            existingMapRadius: 16,
            candidateSelected: false,
            candidateMapRadius: 32));
        Assert.False(ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
            existingSelected: false,
            existingMapRadius: 32,
            candidateSelected: false,
            candidateMapRadius: 16));
        Assert.True(ThingIconRenderPolicy.ShouldReplaceOverviewCellThing(
            existingSelected: true,
            existingMapRadius: 16,
            candidateSelected: true,
            candidateMapRadius: 32));
    }

    [Fact]
    public void SkipsThingsWhoseProjectedRadiusIsTooSmall()
    {
        Assert.False(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 10,
            viewScale: 8,
            fixedThingsScale: false));
        Assert.False(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 12,
            viewScale: 8,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 32,
            viewScale: 8,
            fixedThingsScale: false));
    }

    [Fact]
    public void FarOverviewRequiresReadableThingMarkers()
    {
        Assert.Equal(ThingIconRenderPolicy.MinimumThingScreenRadius, ThingIconRenderPolicy.MinimumThingScreenRadiusFor(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold - 0.01));
        Assert.Equal(ThingIconRenderPolicy.MinimumFarOverviewThingScreenRadius, ThingIconRenderPolicy.MinimumThingScreenRadiusFor(
            ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold));
        Assert.False(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 20,
            viewScale: 8,
            fixedThingsScale: false));
        Assert.True(ThingIconRenderPolicy.ShouldRenderThing(
            mapRadius: 20,
            viewScale: ThingIconRenderPolicy.FarOverviewMarkerScaleThreshold - 0.01,
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
