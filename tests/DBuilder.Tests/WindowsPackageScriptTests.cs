// ABOUTME: Verifies the unsigned Windows zip package script for published editor outputs.
// ABOUTME: Keeps Windows package layout pinned until installer and code-signing work land.

namespace DBuilder.Tests;

public sealed class WindowsPackageScriptTests
{
    [Fact]
    public void WindowsPackageScriptBuildsZipFromPublishedOutput()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-windows-zip.sh"));

        Assert.Contains("artifacts/release", script);
        Assert.Contains("artifacts/package/windows", script);
        Assert.Contains("win-x64", script);
        Assert.Contains("DBuilder.Editor.exe", script);
        Assert.Contains("pwd -P", script);
        Assert.Contains("DBuilder.Editor-$rid.zip", script);
        Assert.Contains("zip -qr", script);
    }

    [Fact]
    public void WindowsPackageScriptRejectsUnsupportedRuntimeIdsWithoutDeletingOutputs()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-windows-zip.sh"));

        Assert.Contains("unsupported Windows runtime id", script);
        Assert.Contains("published editor host not found", script);
        Assert.DoesNotContain("rm -rf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git clean", script, StringComparison.Ordinal);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
