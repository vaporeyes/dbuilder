// ABOUTME: Tests UDB-style sector flat alignment from linedef front and back sides.
// ABOUTME: Covers UDMF rotation, panning, texture-size wrapping, and missing-sidedef skips.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorFlatAlignmentTests
{
    [Fact]
    public void AlignToLinedefsRequiresSelection()
    {
        SectorFlatAlignmentResult result = SectorFlatAlignment.AlignToLinedefs([], floors: true, frontSide: true);

        Assert.False(result.Applied);
        Assert.Equal(0, result.SectorCount);
        Assert.Equal("This action requires a selection!", result.Message);
    }

    [Fact]
    public void AlignFloorToFrontWritesRotationAndPanning()
    {
        var sector = new Sector();
        var line = LineWithFrontSector(new Vector2D(16, 32), new Vector2D(16, 96), sector);

        SectorFlatAlignmentResult result = SectorFlatAlignment.AlignToLinedefs([line], floors: true, frontSide: true);

        Assert.True(result.Applied);
        Assert.Equal(1, result.SectorCount);
        Assert.Equal("Aligned 1 Floors to Front Side", result.Message);
        Assert.Equal(270.0, sector.GetFloatField("rotationfloor"));
        Assert.Equal(-32.0, sector.GetFloatField("xpanningfloor"));
        Assert.Equal(-16.0, sector.GetFloatField("ypanningfloor"));
    }

    [Fact]
    public void AlignCeilingToBackUsesLineEndAndCeilingFields()
    {
        var sector = new Sector();
        var line = LineWithBackSector(new Vector2D(16, 32), new Vector2D(16, 96), sector);

        SectorFlatAlignment.AlignToLinedefs([line], floors: false, frontSide: false);

        Assert.Equal(270.0, sector.GetFloatField("rotationceiling"));
        Assert.Equal(-96.0, sector.GetFloatField("xpanningceiling"));
        Assert.Equal(-16.0, sector.GetFloatField("ypanningceiling"));
        Assert.DoesNotContain("rotationfloor", sector.Fields.Keys);
    }

    [Fact]
    public void AlignWrapsPanningToFlatDimensionsAndScale()
    {
        var sector = new Sector();
        sector.SetFloatField("xscalefloor", 2.0, 1.0);
        sector.SetFloatField("yscalefloor", 0.5, 1.0);
        var line = LineWithFrontSector(new Vector2D(80, 200), new Vector2D(80, 264), sector);

        SectorFlatAlignment.AlignToLinedefs(
            [line],
            floors: true,
            frontSide: true,
            _ => new SectorFlatAlignmentTexture(64, 64));

        Assert.Equal(-8.0, sector.GetFloatField("xpanningfloor"));
        Assert.Equal(-80.0, sector.GetFloatField("ypanningfloor"));
    }

    [Fact]
    public void AlignSkipsLinesWithoutRequestedSideSector()
    {
        var sector = new Sector();
        var front = LineWithFrontSector(new Vector2D(0, 0), new Vector2D(64, 0), sector);
        var missingBack = LineWithFrontSector(new Vector2D(64, 0), new Vector2D(128, 0), new Sector());

        SectorFlatAlignmentResult result = SectorFlatAlignment.AlignToLinedefs([front, missingBack], floors: true, frontSide: false);

        Assert.False(result.Applied);
        Assert.Equal(0, result.SectorCount);
        Assert.Empty(sector.Fields);
    }

    private static Linedef LineWithFrontSector(Vector2D start, Vector2D end, Sector sector)
    {
        var line = new Linedef(new Vertex(start), new Vertex(end));
        line.AttachFront(new Sidedef { Sector = sector });
        return line;
    }

    private static Linedef LineWithBackSector(Vector2D start, Vector2D end, Sector sector)
    {
        var line = new Linedef(new Vertex(start), new Vertex(end));
        line.AttachBack(new Sidedef { Sector = sector });
        return line;
    }
}
