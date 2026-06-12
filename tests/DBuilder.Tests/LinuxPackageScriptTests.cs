// ABOUTME: Verifies the unsigned Linux tar package script for published editor outputs.
// ABOUTME: Keeps Linux package layout pinned until distro-specific installer work lands.

namespace DBuilder.Tests;

public sealed class LinuxPackageScriptTests
{
    [Fact]
    public void LinuxPackageScriptBuildsTarballFromPublishedOutput()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-linux-tar.sh"));

        Assert.Contains("artifacts/release", script);
        Assert.Contains("artifacts/package/linux", script);
        Assert.Contains("linux-x64", script);
        Assert.Contains("DBuilder.Editor", script);
        Assert.Contains("DBuilder.Editor-$rid.tar.gz", script);
        Assert.Contains("tar -C \"$publish_dir\" -czf \"$archive\" .", script);
        Assert.Contains("chmod +x \"$publish_dir/DBuilder.Editor\"", script);
    }

    [Fact]
    public void LinuxPackageScriptRejectsUnsupportedRuntimeIdsWithoutDeletingOutputs()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-linux-tar.sh"));

        Assert.Contains("unsupported Linux runtime id", script);
        Assert.Contains("published editor host not found", script);
        Assert.DoesNotContain("rm -rf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git clean", script, StringComparison.Ordinal);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
