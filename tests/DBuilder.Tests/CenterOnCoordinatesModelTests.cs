// ABOUTME: Verifies Go To Coordinates input parsing and map-format coordinate bounds.
// ABOUTME: Keeps UDB-style center-on-coordinate behavior stable without opening the dialog.

using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class CenterOnCoordinatesModelTests
{
    [Fact]
    public void ParseCoordinateRoundsAndClampsLikeUdb()
    {
        Assert.Equal(12, CenterOnCoordinatesModel.ParseCoordinate("12.5", fallback: 0, MapFormat.Doom));
        Assert.Equal(-12, CenterOnCoordinatesModel.ParseCoordinate("-12.4", fallback: 0, MapFormat.Doom));
        Assert.Equal(short.MaxValue, CenterOnCoordinatesModel.ParseCoordinate("50000", fallback: 0, MapFormat.Doom));
        Assert.Equal(short.MinValue, CenterOnCoordinatesModel.ParseCoordinate("-50000", fallback: 0, MapFormat.Hexen));
    }

    [Fact]
    public void ParseCoordinateFallsBackBeforeRoundingAndClamping()
    {
        Assert.Equal(4, CenterOnCoordinatesModel.ParseCoordinate("not a number", fallback: 3.6, MapFormat.Doom));
    }
}
