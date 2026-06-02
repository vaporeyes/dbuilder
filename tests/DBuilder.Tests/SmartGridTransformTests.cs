// ABOUTME: Verifies UDB-style grid transform actions for classic map edit modes.
// ABOUTME: Covers selected/highlighted element targeting, reset behavior, and warning messages.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SmartGridTransformTests
{
    [Fact]
    public void AlignToSelectedLinedefUsesLineStartAndAngle()
    {
        var grid = OffsetGrid();
        var (line, start, _) = Linedef();

        SmartGridTransformResult result = SmartGridTransform.AlignToSelectedLinedef(grid, new[] { line });

        Assert.True(result.Applied);
        Assert.Equal("Aligned grid to selected linedef.", result.Message);
        Assert.Equal(start.Position.x, grid.GridOriginX);
        Assert.Equal(start.Position.y, grid.GridOriginY);
        Assert.Equal(line.Angle, grid.GridRotate);
    }

    [Fact]
    public void AlignToSelectedLinedefRequiresExactlyOneLine()
    {
        var grid = OffsetGrid();
        var (line, _, _) = Linedef();

        SmartGridTransformResult result = SmartGridTransform.AlignToSelectedLinedef(grid, new[] { line, line });

        Assert.False(result.Applied);
        Assert.Equal(SmartGridTransform.ExactlyOneLinedefMessage, result.Message);
        Assert.Equal(4, grid.GridOriginX);
        Assert.Equal(8, grid.GridOriginY);
        Assert.Equal(0.25, grid.GridRotate);
    }

    [Fact]
    public void SetOriginToSelectedVertexRequiresExactlyOneVertex()
    {
        var grid = OffsetGrid();
        var vertex = new Vertex(new Vector2D(12, 24));

        SmartGridTransformResult result = SmartGridTransform.SetOriginToSelectedVertex(grid, new[] { vertex });

        Assert.True(result.Applied);
        Assert.Equal(12, grid.GridOriginX);
        Assert.Equal(24, grid.GridOriginY);
        Assert.Equal(0.25, grid.GridRotate);
    }

    [Fact]
    public void SmartFromLinedefsUsesClosestEndpointToCursor()
    {
        var grid = OffsetGrid();
        var (line, _, end) = Linedef();

        SmartGridTransformResult result = SmartGridTransform.SmartFromLinedefs(
            grid,
            Array.Empty<Linedef>(),
            line,
            new Vector2D(120, 8));

        Assert.True(result.Applied);
        Assert.Equal(end.Position.x, grid.GridOriginX);
        Assert.Equal(end.Position.y, grid.GridOriginY);
        Assert.Equal(line.Angle, grid.GridRotate);
    }

    [Fact]
    public void SmartFromVerticesResetsWhenNoVertexIsSelectedOrHighlighted()
    {
        var grid = OffsetGrid();

        SmartGridTransformResult result = SmartGridTransform.SmartFromVertices(grid, Array.Empty<Vertex>(), null);

        Assert.True(result.Applied);
        Assert.Equal("Reset grid transform.", result.Message);
        Assert.Equal(0, grid.GridOriginX);
        Assert.Equal(0, grid.GridOriginY);
        Assert.Equal(0, grid.GridRotate);
    }

    [Fact]
    public void SmartFromThingsWarnsOnMultipleSelectedThings()
    {
        var grid = OffsetGrid();
        var things = new[]
        {
            new Thing(new Vector2D(8, 16), 1),
            new Thing(new Vector2D(32, 64), 1),
        };

        SmartGridTransformResult result = SmartGridTransform.SmartFromThings(grid, things, null);

        Assert.False(result.Applied);
        Assert.Equal(SmartGridTransform.OneOrNoThingMessage, result.Message);
        Assert.Equal(4, grid.GridOriginX);
        Assert.Equal(8, grid.GridOriginY);
        Assert.Equal(0.25, grid.GridRotate);
    }

    [Fact]
    public void SmartFromSectorsResetsGridTransform()
    {
        var grid = OffsetGrid();

        SmartGridTransformResult result = SmartGridTransform.SmartFromSectors(grid);

        Assert.True(result.Applied);
        Assert.Equal(0, grid.GridOriginX);
        Assert.Equal(0, grid.GridOriginY);
        Assert.Equal(0, grid.GridRotate);
    }

    private static GridSetup OffsetGrid()
    {
        var grid = new GridSetup();
        grid.SetGridOrigin(4, 8);
        grid.SetGridRotation(0.25);
        return grid;
    }

    private static (Linedef line, Vertex start, Vertex end) Linedef()
    {
        var start = new Vertex(new Vector2D(10, 20));
        var end = new Vertex(new Vector2D(130, 50));
        return (new Linedef(start, end), start, end);
    }
}
