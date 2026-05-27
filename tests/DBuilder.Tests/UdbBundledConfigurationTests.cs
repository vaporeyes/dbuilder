// ABOUTME: Smoke-tests selected bundled Ultimate Doom Builder configuration files when a local UDB clone is available.
// ABOUTME: Guards include resolution and high-level metadata parsing against real UDB asset files without vendoring them.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbBundledConfigurationTests
{
    [Fact]
    public void LoadsBundledGzdoomDoomUdmfGameConfiguration()
    {
        string? root = FindUdbCommonAssetsRoot();
        if (root == null) return;

        string path = Path.Combine(root, "Configurations", "GZDoom_DoomUDMF.cfg");
        var raw = new Configuration(path);
        Assert.False(raw.ErrorResult, raw.ErrorDescription);

        var game = GameConfiguration.FromConfiguration(raw);
        Assert.Equal("GZDoom: Doom 2 (UDMF)", raw.ReadSetting("game", ""));
        Assert.True(game.Things.Count > 100);
        Assert.True(game.LinedefActions.Count > 50);
        Assert.NotEmpty(game.MapLumpNames);
        Assert.NotEmpty(game.EnumLists);
        Assert.NotEmpty(game.TextureSets);
    }

    [Fact]
    public void LoadsBundledZdoomAcsScriptConfiguration()
    {
        string? root = FindUdbCommonAssetsRoot();
        if (root == null) return;

        string path = Path.Combine(root, "Scripting", "ZDoom_ACS.cfg");
        var cfg = ScriptConfigurationInfo.FromFile(path);
        Assert.Equal("ZDoom ACS", cfg.Description);
        Assert.Equal(ScriptType.Acs, cfg.ScriptType);
        Assert.True(cfg.IsKeyword("acs_execute"));
        Assert.Equal("ACS_Execute", cfg.GetKeywordCase("acs_execute"));
        Assert.NotEmpty(cfg.Constants);
    }

    private static string? FindUdbCommonAssetsRoot()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder", "Assets", "Common");
        return Directory.Exists(root) ? root : null;
    }
}
