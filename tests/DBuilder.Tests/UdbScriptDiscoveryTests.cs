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
            var option = Assert.Single(info.Options);
            Assert.Equal("global_x", option.Name);
            Assert.Equal("Global X Offset", option.Description);
            Assert.Equal((int)UniversalType.Boolean, option.Type);
            Assert.Equal("False", option.DefaultValue);
            Assert.Equal(option.DefaultValue, option.Value);
            Assert.Equal("scripts." + ExpectedAsciiSha256(file) + ".options.global_x", option.SettingKey);
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
            Assert.Empty(info.Options);
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
    public void ParsesUdbScriptOptionsWithEnumDefaults()
    {
        string file = Path.Combine(TempDir(), "options.js");
        try
        {
            const string options = """
                length
                {
                    description = "Length";
                    default = 128;
                    type = 0;
                }

                direction
                {
                    description = "Direction";
                    default = 2;
                    type = 11;
                    enumvalues
                    {
                        1 = "Up";
                        2 = "Down";
                    }
                }

                texture
                {
                    type = 6;
                }
                """;

            IReadOnlyList<UdbScriptOption> parsed = UdbScriptDiscovery.ParseOptions(options, file);

            Assert.Equal(3, parsed.Count);
            var length = parsed[0];
            Assert.Equal("length", length.Name);
            Assert.Equal("Length", length.Description);
            Assert.Equal((int)UniversalType.Integer, length.Type);
            Assert.Equal(128, length.DefaultValue);
            Assert.Empty(length.EnumValues);

            var direction = parsed[1];
            Assert.Equal("direction", direction.Name);
            Assert.Equal((int)UniversalType.EnumOption, direction.Type);
            Assert.Equal("Down", direction.DefaultValue);
            Assert.Equal("Down", direction.Value);
            Assert.Equal(new[] { "1:Up", "2:Down" }, direction.EnumValues.Select(v => v.Key + ":" + v.Label).ToArray());

            var texture = parsed[2];
            Assert.Equal("texture", texture.Name);
            Assert.Equal("no description", texture.Description);
            Assert.Equal((int)UniversalType.Texture, texture.Type);
            Assert.Equal("", texture.DefaultValue);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidScriptOptionConfig()
    {
        string file = Path.Combine(TempDir(), "badoptions.js");
        try
        {
            Assert.Throws<ArgumentException>(() => UdbScriptDiscovery.ParseOptions("broken = @;", file));
            Assert.Throws<ArgumentException>(() => UdbScriptDiscovery.ParseOptions("""
                unsupported
                {
                    type = 12;
                }
                """, file));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void AppliesSavedOptionValuesByUdbSettingKey()
    {
        string file = Path.Combine(TempDir(), "saved.js");
        try
        {
            UdbScriptInfo script = UdbScriptDiscovery.ParseScript(WriteScriptWithOptions(file));
            UdbScriptOption length = script.Options.Single(option => option.Name == "length");
            UdbScriptOption texture = script.Options.Single(option => option.Name == "texture");

            UdbScriptInfo result = UdbScriptDiscovery.ApplySavedOptionValues(script, new Dictionary<string, object?>
            {
                [length.SettingKey] = "256",
                [texture.SettingKey] = "   ",
            });

            Assert.Equal("256", result.Options.Single(option => option.Name == "length").Value);
            Assert.Equal(texture.DefaultValue, result.Options.Single(option => option.Name == "texture").Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void PlansUdbOptionValuePersistenceOperations()
    {
        string file = Path.Combine(TempDir(), "saveops.js");
        try
        {
            UdbScriptInfo script = UdbScriptDiscovery.ParseScript(WriteScriptWithOptions(file));
            UdbScriptInfo edited = script with
            {
                Options = script.Options
                    .Select(option => option.Name == "length" ? option with { Value = 256 } : option)
                    .ToArray(),
            };

            IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDiscovery.SaveOptionValueOperations(edited);

            Assert.Equal(2, operations.Count);
            UdbScriptSettingOperation write = operations[0];
            Assert.Equal(UdbScriptSettingOperationKind.Write, write.Kind);
            Assert.Equal("scripts." + script.PathHash + ".options.length", write.Key);
            Assert.Equal(256, write.Value);
            UdbScriptSettingOperation delete = operations[1];
            Assert.Equal(UdbScriptSettingOperationKind.Delete, delete.Kind);
            Assert.Equal("scripts." + script.PathHash + ".options.texture", delete.Key);
            Assert.Null(delete.Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void PlansUdbOptionBlockCleanupWhenAllOptionsAreDefaults()
    {
        string file = Path.Combine(TempDir(), "defaults.js");
        try
        {
            UdbScriptInfo script = UdbScriptDiscovery.ParseScript(WriteScriptWithOptions(file));

            IReadOnlyList<UdbScriptSettingOperation> operations = UdbScriptDiscovery.SaveOptionValueOperations(script);

            Assert.Equal(
                new[]
                {
                    "scripts." + script.PathHash + ".options.length",
                    "scripts." + script.PathHash + ".options.texture",
                    "scripts." + script.PathHash + ".options",
                    "scripts." + script.PathHash,
                },
                operations.Select(operation => operation.Key));
            Assert.All(operations, operation => Assert.Equal(UdbScriptSettingOperationKind.Delete, operation.Kind));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
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

    [Fact]
    public void WatcherReloadFilterMatchesUdbScriptPlugin()
    {
        Assert.True(UdbScriptDiscovery.ShouldReloadAfterWatcherEvent(
            WatcherChangeTypes.Deleted,
            "/scripts/notes.txt",
            fullPathIsDirectory: false));
        Assert.True(UdbScriptDiscovery.ShouldReloadAfterWatcherEvent(
            WatcherChangeTypes.Created,
            "/scripts/NewFolder",
            fullPathIsDirectory: true));
        Assert.True(UdbScriptDiscovery.ShouldReloadAfterWatcherEvent(
            WatcherChangeTypes.Changed,
            "/scripts/run.JS",
            fullPathIsDirectory: false));

        Assert.False(UdbScriptDiscovery.ShouldReloadAfterWatcherEvent(
            WatcherChangeTypes.Changed,
            "/scripts/Examples",
            fullPathIsDirectory: true));
        Assert.False(UdbScriptDiscovery.ShouldReloadAfterWatcherEvent(
            WatcherChangeTypes.Changed,
            "/scripts/readme.txt",
            fullPathIsDirectory: false));
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_udbscript_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ExpectedAsciiSha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(text))).ToLowerInvariant();

    private static string WriteScriptWithOptions(string file)
    {
        File.WriteAllText(file, """
            `#scriptoptions
            length
            {
                default = 128;
                type = 0;
            }
            texture
            {
                default = "STARTAN3";
                type = 6;
            }
            `;
            """);
        return file;
    }
}
