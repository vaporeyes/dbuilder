// ABOUTME: Covers configured required-archive detection for resource option defaults.
// ABOUTME: Uses synthetic resources so editor resource dialogs can prefill UDB-compatible metadata.

using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class RequiredArchiveDetectorTests
{
    [Fact]
    public void DetectsRequiredArchiveByLumpAndDecorateClass()
    {
        var config = GameConfiguration.FromText("""
            requiredarchives
            {
                gzdoom
                {
                    filename = "gzdoom.pk3";
                    need_exclude = true;
                    marker { lump = "MARKER"; }
                    actor { lump = "DECORATE"; class = "RequiredActor"; }
                }
                missing
                {
                    filename = "missing.pk3";
                    need_exclude = false;
                    actor { class = "OtherActor"; }
                }
            }
            """);
        string path = TestArtifacts.BuildPwadFile(
            ("MARKER", Encoding.ASCII.GetBytes("present")),
            ("DECORATE", Encoding.ASCII.GetBytes("ACTOR RequiredActor 32000 { Radius 16 }")));

        try
        {
            var detected = RequiredArchiveDetector.Detect(config, new DataLocation(DataLocationType.Wad, path));

            Assert.Equal(new[] { "gzdoom" }, detected);
            Assert.True(RequiredArchiveDetector.RequiresTestExclusion(config, detected));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectsRequiredArchiveFromPk3TextPath()
    {
        var config = GameConfiguration.FromText("""
            requiredarchives
            {
                resource
                {
                    filename = "resource.pk3";
                    need_exclude = false;
                    script { lump = "ZSCRIPT"; class = "Pk3RequiredActor"; }
                }
            }
            """);
        string path = TestArtifacts.BuildPk3(
            ("ZSCRIPT", Encoding.ASCII.GetBytes("class Pk3RequiredActor : Actor { Default { Radius 16; } }")));

        try
        {
            var detected = RequiredArchiveDetector.Detect(config, new DataLocation(DataLocationType.Pk3, path));

            Assert.Equal(new[] { "resource" }, detected);
            Assert.False(RequiredArchiveDetector.RequiresTestExclusion(config, detected));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
