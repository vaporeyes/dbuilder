// ABOUTME: Tests for X11RgbParser over rgb.txt color rows.
// ABOUTME: Covers named colors, multi-word names, comments, and malformed row skipping.

using DBuilder.IO;

namespace DBuilder.Tests;

public class X11RgbParserTests
{
    [Fact]
    public void ParsesRgbRows()
    {
        const string text = @"
! comment
255 250 250 snow
248 248 255 ghost white
bad row
# also skipped";

        var rgb = X11RgbParser.Parse(text);

        Assert.Equal(new X11Color("snow", 255, 250, 250), rgb.Colors["snow"]);
        Assert.Equal(new X11Color("ghost white", 248, 248, 255), rgb.Colors["ghost white"]);
        Assert.Equal(2, rgb.Colors.Count);
    }
}
