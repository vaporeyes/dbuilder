// ABOUTME: Verifies GitHub Actions coverage for the supported platform verification gate.
// ABOUTME: Keeps CI aligned with the same script used for local parity slices.

namespace DBuilder.Tests;

public sealed class CiWorkflowTests
{
    [Fact]
    public void CiWorkflowRunsVerificationOnSupportedDesktopPlatforms()
    {
        string workflow = File.ReadAllText(RepositoryPath(".github/workflows/ci.yml"));

        Assert.Contains("push:", workflow);
        Assert.Contains("pull_request:", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("fail-fast: false", workflow);
        Assert.Contains("os: [ubuntu-latest, macos-latest, windows-latest]", workflow);
    }

    [Fact]
    public void CiWorkflowUsesRepositoryVerificationScript()
    {
        string workflow = File.ReadAllText(RepositoryPath(".github/workflows/ci.yml"));

        Assert.Contains("actions/checkout@v4", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet-version: 8.0.x", workflow);
        Assert.Contains("shell: bash", workflow);
        Assert.Contains("run: bash scripts/verify.sh", workflow);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
