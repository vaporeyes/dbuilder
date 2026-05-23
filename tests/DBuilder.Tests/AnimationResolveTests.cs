// ABOUTME: Tests ResourceManager ANIMDEFS frame resolution: a flat range cycles over time with correct phase.
// ABOUTME: Builds a PK3 with FWATER1..4 PNG flats and an ANIMDEFS range, then checks CurrentFlatFrame.

using System.IO;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class AnimationResolveTests
{
    private static string BuildAnimatedPk3()
    {
        byte[] Px(byte v) => TestArtifacts.Png(2, 2, TestArtifacts.SolidRgba(2, 2, v, v, v, 255));
        return TestArtifacts.BuildPk3(
            ("ANIMDEFS.txt", Encoding.ASCII.GetBytes("flat FWATER1 range FWATER4 tics 8\n")),
            ("flats/FWATER1.png", Px(1)),
            ("flats/FWATER2.png", Px(2)),
            ("flats/FWATER3.png", Px(3)),
            ("flats/FWATER4.png", Px(4)));
    }

    [Fact]
    public void FlatRangeCyclesOverTime()
    {
        string pk3 = BuildAnimatedPk3();
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.True(rm.HasAnimations);

            // tics=8 at 35 tics/sec: step = floor(sec*35/8). step 0 at t=0, step 1 at t>=8/35 (~0.229s).
            Assert.Equal("FWATER1", rm.CurrentFlatFrame("FWATER1", 0.0));
            Assert.Equal("FWATER2", rm.CurrentFlatFrame("FWATER1", 0.25)); // step 1
            Assert.Equal("FWATER1", rm.CurrentFlatFrame("FWATER1", 4 * 8.0 / 35.0 + 0.001)); // step 4 -> wraps to 0
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void PhaseFollowsTheRequestedFrame()
    {
        string pk3 = BuildAnimatedPk3();
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            // FWATER3 has phase 2; at step 0 it shows itself, at step 1 it shows FWATER4.
            Assert.Equal("FWATER3", rm.CurrentFlatFrame("FWATER3", 0.0));
            Assert.Equal("FWATER4", rm.CurrentFlatFrame("FWATER3", 0.25));
        }
        finally { File.Delete(pk3); }
    }

    [Fact]
    public void NonAnimatedNameReturnedUnchanged()
    {
        string pk3 = BuildAnimatedPk3();
        try
        {
            using var rm = new ResourceManager();
            rm.AddResource(pk3);
            Assert.Equal("FLOOR0_1", rm.CurrentFlatFrame("FLOOR0_1", 1.0));
        }
        finally { File.Delete(pk3); }
    }
}
