// ABOUTME: Verifies that release build automation targets the supported desktop runtime set.
// ABOUTME: Keeps the packaging backlog honest by testing publish behavior without invoking packaging.

namespace DBuilder.Tests;

public class ReleaseBuildScriptTests
{
    [Fact]
    public void ReleaseBuildScriptPublishesSupportedDesktopRuntimeIds()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/release-build.sh"));

        Assert.Contains("src/DBuilder.Editor/DBuilder.Editor.csproj", script);
        Assert.Contains("artifacts/release", script);
        Assert.Contains("dotnet publish", script);
        Assert.Contains("--self-contained", script);
        Assert.Contains("-p:PublishSingleFile=false", script);
        Assert.Contains("osx-arm64", script);
        Assert.Contains("osx-x64", script);
        Assert.Contains("win-x64", script);
        Assert.Contains("linux-x64", script);
    }

    [Fact]
    public void ReleaseBuildScriptRejectsUnsupportedRuntimeIdsWithoutDeletingOutputs()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/release-build.sh"));

        Assert.Contains("unsupported runtime id", script);
        Assert.DoesNotContain("rm -rf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git clean", script, StringComparison.Ordinal);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
