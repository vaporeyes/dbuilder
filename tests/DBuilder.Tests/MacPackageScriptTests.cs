// ABOUTME: Verifies the unsigned macOS app bundle package script for published editor outputs.
// ABOUTME: Keeps .app bundle layout pinned until signing, notarization, and installer packaging land.

namespace DBuilder.Tests;

public sealed class MacPackageScriptTests
{
    [Fact]
    public void MacPackageScriptBuildsUnsignedAppBundleFromPublishedOutput()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-macos-app.sh"));

        Assert.Contains("artifacts/release", script);
        Assert.Contains("artifacts/package/macos", script);
        Assert.Contains("DBuilder.Editor.app", script);
        Assert.Contains("contents_dir=\"$app_dir/Contents\"", script);
        Assert.Contains("macos_dir=\"$contents_dir/MacOS\"", script);
        Assert.Contains("resources_dir=\"$contents_dir/Resources\"", script);
        Assert.Contains("Info.plist", script);
        Assert.Contains("PkgInfo", script);
        Assert.Contains("CFBundleExecutable", script);
        Assert.Contains("dev.jsh.dbuilder.editor", script);
        Assert.Contains("LSMinimumSystemVersion", script);
    }

    [Fact]
    public void MacPackageScriptOnlyAcceptsMacRuntimeIdsAndDoesNotDeleteOutputs()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-macos-app.sh"));

        Assert.Contains("osx-arm64|osx-x64", script);
        Assert.Contains("unsupported macOS runtime id", script);
        Assert.Contains("published editor host not found", script);
        Assert.DoesNotContain("rm -rf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git clean", script, StringComparison.Ordinal);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
