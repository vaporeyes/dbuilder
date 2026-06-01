// ABOUTME: Verifies selected-element UDMF flag choice lists.
// ABOUTME: Covers known flag sources and result application without opening Avalonia windows.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class UdmfFlagChoicesTests
{
    [Fact]
    public void KnownSectorFlagsIncludeSectorAndPortalMetadata()
    {
        var config = GameConfiguration.FromText("""
            sectorflags { secret = "Secret"; }
            ceilingportalflags { portal_ceil_nopass = "Impassable"; }
            floorportalflags { portal_floor_norender = "Not rendered"; }
            """);
        var sector = new Sector();
        sector.UdmfFlags.Add("customsector");

        var known = UdmfFlagChoices.KnownSectorFlags(config, sector);

        Assert.Equal(
            new[] { "customsector", "portal_ceil_nopass", "portal_floor_norender", "secret" },
            known);
    }

    [Fact]
    public void KnownLinedefAndThingFlagsIncludeCurrentAndTranslatedFlags()
    {
        var config = GameConfiguration.FromText("""
            linedefflagstranslation
            {
                1 = "blocking";
            }
            thingflagstranslation
            {
                1 = "skill1";
            }
            """);
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        line.UdmfFlags.Add("customline");
        var thing = new Thing(new Vector2D(0, 0), 3001);
        thing.UdmfFlags.Add("customthing");

        Assert.Equal(new[] { "blocking", "customline" }, UdmfFlagChoices.KnownLinedefFlags(config, line));
        Assert.Equal(new[] { "customthing", "skill1" }, UdmfFlagChoices.KnownThingFlags(config, thing));
    }

    [Fact]
    public void ApplyFlagsTrimsAndReplacesResult()
    {
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "old" };

        UdmfFlagChoices.ApplyFlags(flags, new[] { " secret ", "", "secret", "portal" });

        Assert.Equal(new[] { "portal", "secret" }, flags.OrderBy(flag => flag, StringComparer.OrdinalIgnoreCase));
    }
}
