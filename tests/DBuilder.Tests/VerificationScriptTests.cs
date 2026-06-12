// ABOUTME: Verifies the standard local validation script covers the main build and test loop.
// ABOUTME: Keeps release criteria evidence tied to the repository solution instead of ad hoc commands.

namespace DBuilder.Tests;

public sealed class VerificationScriptTests
{
    [Fact]
    public void VerificationScriptRestoresBuildsAndTestsTheMainSolution()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/verify.sh"));

        Assert.Contains("dotnet restore DBuilder.slnx -m:1", script);
        Assert.Contains("dotnet build DBuilder.slnx --no-restore -m:1", script);
        Assert.Contains("dotnet test DBuilder.slnx --no-build -m:1", script);
    }

    [Fact]
    public void VerificationScriptRunsRustTestsWhenRustWorkspaceExists()
    {
        string script = File.ReadAllText(RepositoryPath("scripts/verify.sh"));

        Assert.Contains("if [[ -f rust/Cargo.toml ]]; then", script);
        Assert.Contains("cargo test --manifest-path rust/Cargo.toml", script);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
