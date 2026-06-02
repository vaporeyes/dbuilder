// ABOUTME: Tests UDBScript file discovery and leading metadata parsing against upstream conventions.
// ABOUTME: Covers script tree shape, template literal metadata, defaults, and path hashes.

using System.Security.Cryptography;
using System.Text;
using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptDiscoveryTests
{
    [Fact]
    public void ParsesLeadingTemplateMetadata()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "randomize.js");
            File.WriteAllText(file, """
                /// <reference path="../../udbscript.d.ts" />

                `#version 4`;

                `#name Randomize Texture Offsets`;

                `#description Randomized texture offsets.
                Distinct upper, middle, and lower offsets only work if the game configuration supports those local offsets.`;

                `#scriptoptions

                global_x
                {
                    description = "Global X Offset";
                    default = "False";
                    type = 3;
                }
                `;

                function run() {}
                """);

            var info = UdbScriptDiscovery.ParseScript(file);

            Assert.Equal("Randomize Texture Offsets", info.Name);
            Assert.Equal(
                "Randomized texture offsets. Distinct upper, middle, and lower offsets only work if the game configuration supports those local offsets.",
                info.Description);
            Assert.Equal(4u, info.Version);
            Assert.Equal(file, info.ScriptFile);
            Assert.Equal(ExpectedAsciiSha256(file), info.PathHash);
            Assert.NotNull(info.RawOptions);
            Assert.Contains("global_x", info.RawOptions, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UsesUdbDefaultsWhenMetadataIsMissing()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "plain.js");
            File.WriteAllText(file, "function run() {}");

            var info = UdbScriptDiscovery.ParseScript(file);

            Assert.Equal("plain", info.Name);
            Assert.Equal("No description.", info.Description);
            Assert.Equal(1u, info.Version);
            Assert.Null(info.RawOptions);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StopsMetadataParsingAfterNormalJavaScript()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "stop.js");
            File.WriteAllText(file, """
                `#name Before`;
                const value = 1;
                `#name After`;
                """);

            var info = UdbScriptDiscovery.ParseScript(file);

            Assert.Equal("Before", info.Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidVersionMetadata()
    {
        string dir = TempDir();
        try
        {
            string file = Path.Combine(dir, "bad.js");
            File.WriteAllText(file, "`#version nope`;");

            Assert.Throws<ArgumentException>(() => UdbScriptDiscovery.ParseScript(file));

            File.WriteAllText(file, "`#version 0`;");
            Assert.Throws<ArgumentException>(() => UdbScriptDiscovery.ParseScript(file));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DiscoversUdbScriptDirectoryTree()
    {
        string app = TempDir();
        try
        {
            string scripts = Path.Combine(app, "UDBScript", "Scripts");
            string visible = Path.Combine(scripts, "Examples");
            string hidden = Path.Combine(scripts, ".hidden");
            Directory.CreateDirectory(visible);
            Directory.CreateDirectory(hidden);
            File.WriteAllText(Path.Combine(scripts, "root.js"), "`#name Root Script`;");
            File.WriteAllText(Path.Combine(scripts, "notes.txt"), "`#name Not A Script`;");
            File.WriteAllText(Path.Combine(visible, "child.js"), "`#name Child Script`;");
            File.WriteAllText(Path.Combine(hidden, "secret.js"), "`#name Secret Script`;");

            var root = UdbScriptDiscovery.DiscoverFromAppPath(app);

            Assert.Equal("Scripts", root.Name);
            Assert.Equal(ExpectedAsciiSha256(scripts), root.Hash);
            var rootScript = Assert.Single(root.Scripts);
            Assert.Equal("Root Script", rootScript.Name);

            var childDirectory = Assert.Single(root.Directories);
            Assert.Equal("Examples", childDirectory.Name);
            var childScript = Assert.Single(childDirectory.Scripts);
            Assert.Equal("Child Script", childScript.Name);
            Assert.Empty(childDirectory.Directories);
        }
        finally
        {
            Directory.Delete(app, recursive: true);
        }
    }

    [Fact]
    public void MissingScriptDirectoryProducesEmptyRoot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_missing_udbscript_" + Guid.NewGuid().ToString("N"));

        var root = UdbScriptDiscovery.Discover(dir);

        Assert.Equal(Path.GetFileName(dir), root.Name);
        Assert.Equal(ExpectedAsciiSha256(dir), root.Hash);
        Assert.Empty(root.Directories);
        Assert.Empty(root.Scripts);
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_udbscript_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ExpectedAsciiSha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(text))).ToLowerInvariant();
}
