// ABOUTME: Verifies packaged-output smoke checks for release build artifacts.
// ABOUTME: Keeps package layout validation aligned with the editor publish script and bundled assets.

namespace DBuilder.Tests;

public sealed class PackageSmokeScriptTests
{
    [Fact]
    public void PackageSmokeScriptChecksEditorLaunchFilesAndAssets()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-smoke.sh"));

        Assert.Contains("artifacts/release", script);
        Assert.Contains("DBuilder.Editor", script);
        Assert.Contains("DBuilder.Editor.exe", script);
        Assert.Contains("DBuilder.Editor.dll", script);
        Assert.Contains("DBuilder.Editor.deps.json", script);
        Assert.Contains("DBuilder.Editor.runtimeconfig.json", script);
        Assert.Contains("main.png", script);
        Assert.Contains("assets/Common/Configurations/README.md", script);
        Assert.Contains("assets/Common/Scripting/README.md", script);
    }

    [Fact]
    public void PackageSmokeScriptDiscoversRuntimeDirectoriesAndFailsMissingFiles()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/package-smoke.sh"));

        Assert.Contains("find \"$output_root\" -mindepth 1 -maxdepth 1 -type d | sort", script);
        Assert.Contains("release output root not found", script);
        Assert.Contains("no release runtime directories found", script);
        Assert.Contains("missing $rid/$relative", script);
        Assert.Contains("not executable: $rid/$executable", script);
        Assert.DoesNotContain("rm -rf", script, StringComparison.Ordinal);
        Assert.DoesNotContain("git clean", script, StringComparison.Ordinal);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
