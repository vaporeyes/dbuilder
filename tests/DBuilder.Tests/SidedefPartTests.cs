// ABOUTME: Tests sidedef part texture slots and UDB-style required wall part semantics.
// ABOUTME: Covers one-sided middle walls, two-sided upper and lower gaps, and sloped height comparisons.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefPartTests
{
    [Fact]
    public void TextureAccessorsUseTheRequestedPart()
    {
        var side = new Sidedef
        {
            HighTexture = "UP",
            MidTexture = "MID",
            LowTexture = "LOW",
        };

        Assert.Equal("UP", side.GetTexture(SidedefPart.Upper));
        Assert.Equal("MID", side.GetTexture(SidedefPart.Middle));
        Assert.Equal("LOW", side.GetTexture(SidedefPart.Lower));
        Assert.Equal("-", side.GetTexture(SidedefPart.None));

        side.SetTexture(SidedefPart.Upper, "NEWUP");
        side.SetTexture(SidedefPart.Middle, "");
        side.SetTexture(SidedefPart.Lower, null);

        Assert.Equal("NEWUP", side.HighTexture);
        Assert.Equal("-", side.MidTexture);
        Assert.Equal("-", side.LowTexture);
    }

    [Fact]
    public void SidedefUpdateAndNamedTextureSettersMatchUdbSurface()
    {
        var side = new Sidedef();

        side.Update(
            offsetX: 8,
            offsetY: -4,
            highTexture: "",
            midTexture: null,
            lowTexture: "LOW",
            flags: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["clipmidtex"] = true,
                ["wrapmidtex"] = false,
            });

        Assert.Equal(8, side.OffsetX);
        Assert.Equal(-4, side.OffsetY);
        Assert.Equal("-", side.HighTexture);
        Assert.Equal("-", side.MidTexture);
        Assert.Equal("LOW", side.LowTexture);
        Assert.Contains("clipmidtex", side.UdmfFlags);
        Assert.DoesNotContain("wrapmidtex", side.UdmfFlags);

        side.SetTextureHigh("UP");
        side.SetTextureMid("");
        side.SetTextureLow(null);

        Assert.Equal("UP", side.HighTexture);
        Assert.Equal("-", side.MidTexture);
        Assert.Equal("-", side.LowTexture);

        side.Update(1, 2, "HI", "MID", "");

        Assert.Equal(1, side.OffsetX);
        Assert.Equal(2, side.OffsetY);
        Assert.Equal("HI", side.HighTexture);
        Assert.Equal("MID", side.MidTexture);
        Assert.Equal("-", side.LowTexture);
        Assert.Empty(side.UdmfFlags);
    }

    [Fact]
    public void OneSidedWallRequiresMiddleTextureOnly()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var side = map.AddSidedef(line, true, sector);
        map.BuildIndexes();

        Assert.False(side.HighRequired());
        Assert.True(side.MiddleRequired());
        Assert.False(side.LowRequired());
        Assert.Equal(128, side.GetPartHeight(SidedefPart.Middle));
    }

    [Fact]
    public void TwoSidedWallReportsUpperAndLowerGapsRelativeToThisSide()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        var backSector = map.AddSector();
        backSector.FloorHeight = 32;
        backSector.CeilHeight = 96;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var front = map.AddSidedef(line, true, frontSector);
        var back = map.AddSidedef(line, false, backSector);
        map.BuildIndexes();

        Assert.True(front.HighRequired());
        Assert.False(front.MiddleRequired());
        Assert.True(front.LowRequired());
        Assert.Equal(32, front.GetHighHeight());
        Assert.Equal(64, front.GetMiddleHeight());
        Assert.Equal(32, front.GetLowHeight());

        Assert.False(back.HighRequired());
        Assert.False(back.LowRequired());
        Assert.Equal(0, back.GetHighHeight());
        Assert.Equal(0, back.GetLowHeight());
    }

    [Fact]
    public void SlopedSectorsAffectRequiredPartsAndHeights()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        var backSector = map.AddSector();
        backSector.FloorHeight = 0;
        backSector.CeilHeight = 128;
        var start = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(start, end);
        var front = map.AddSidedef(line, true, frontSector);
        map.AddSidedef(line, false, backSector);

        double k = 1.0 / System.Math.Sqrt(2);
        backSector.FloorSlope = new Vector3D(-k, 0, k);
        backSector.FloorSlopeOffset = 0;
        map.BuildIndexes();

        Assert.True(front.LowRequired());
        Assert.Equal(64, front.GetLowHeight(), 6);
    }

    [Fact]
    public void RemoveUnneededTexturesClearsUdbOptionalParts()
    {
        var (front, back) = TwoSidedSameHeightSides();
        front.HighTexture = "UP";
        front.MidTexture = "MID";
        front.LowTexture = "LOW";

        Assert.True(front.RemoveUnneededTextures(removeMiddle: true));

        Assert.Equal("-", front.HighTexture);
        Assert.Equal("-", front.MidTexture);
        Assert.Equal("-", front.LowTexture);
        Assert.False(back.HighRequired());
    }

    [Fact]
    public void RemoveUnneededTexturesHonorsAutoClearPreference()
    {
        var (front, _) = TwoSidedSameHeightSides();
        front.HighTexture = "UP";
        front.MidTexture = "MID";
        front.LowTexture = "LOW";

        Assert.True(front.RemoveUnneededTextures(removeMiddle: true, autoClearSidedefTextures: false));

        Assert.Equal("UP", front.HighTexture);
        Assert.Equal("-", front.MidTexture);
        Assert.Equal("LOW", front.LowTexture);
    }

    [Fact]
    public void RemoveUnneededTexturesIsIdempotentWhenAlreadyClear()
    {
        var (front, _) = TwoSidedSameHeightSides();

        Assert.False(front.RemoveUnneededTextures(removeMiddle: true));

        Assert.Equal("-", front.HighTexture);
        Assert.Equal("-", front.MidTexture);
        Assert.Equal("-", front.LowTexture);
    }

    [Fact]
    public void RemoveUnneededTexturesKeepsUpperLowerWhenActionsOrTagsMayNeedThem()
    {
        var (front, _) = TwoSidedSameHeightSides();
        front.Line.Action = 80;
        front.HighTexture = "UP";
        front.LowTexture = "LOW";

        Assert.False(front.RemoveUnneededTextures(removeMiddle: false));

        Assert.Equal("UP", front.HighTexture);
        Assert.Equal("LOW", front.LowTexture);
    }

    [Fact]
    public void RemoveUnneededTexturesForceIgnoresActionTagProtection()
    {
        var (front, _) = TwoSidedSameHeightSides();
        front.Line.Tag = 1;
        front.HighTexture = "UP";
        front.LowTexture = "LOW";

        Assert.True(front.RemoveUnneededTextures(removeMiddle: false, force: true, shiftMiddle: false));

        Assert.Equal("-", front.HighTexture);
        Assert.Equal("-", front.LowTexture);
    }

    [Fact]
    public void RemoveUnneededTexturesCanShiftMiddleToRequiredMissingParts()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        var backSector = map.AddSector();
        backSector.FloorHeight = 32;
        backSector.CeilHeight = 96;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var front = map.AddSidedef(line, true, frontSector);
        map.AddSidedef(line, false, backSector);
        front.HighTexture = "-";
        front.MidTexture = "MID";
        front.LowTexture = "-";
        map.BuildIndexes();

        Assert.True(front.RemoveUnneededTextures(removeMiddle: false, force: false, shiftMiddle: true));

        Assert.Equal("MID", front.HighTexture);
        Assert.Equal("MID", front.LowTexture);
        Assert.Equal("MID", front.MidTexture);
    }

    private static (Sidedef Front, Sidedef Back) TwoSidedSameHeightSides()
    {
        var map = new MapSet();
        var frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        var backSector = map.AddSector();
        backSector.FloorHeight = 0;
        backSector.CeilHeight = 128;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var front = map.AddSidedef(line, true, frontSector);
        var back = map.AddSidedef(line, false, backSector);
        map.BuildIndexes();
        return (front, back);
    }
}
