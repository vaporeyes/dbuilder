// ABOUTME: Modal Settings dialog editing the persisted paths (game-config dir, test source port/IWAD/args, node builder).
// ABOUTME: Reads current values from Settings on open; the host writes the results back and saves on OK.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class SettingsWindow : PropertyDialog
{
    private readonly TextBox _configDir, _testPort, _testIwad, _testArgs, _nodePath, _nodeArgs, _maxRecentFiles, _statusHistoryLimit, _shortcutOverrides;
    private readonly ComboBox _pasteTagMode;
    private readonly CheckBox _autoClearSidedefTextures, _pasteRemoveActions;

    public string? ConfigDir, TestPort, TestIwad, TestPortArgs, NodeBuilderPath, NodeBuilderArgs;
    public int? MaxRecentFiles;
    public bool AutoClearSidedefTextures;
    public int? StatusHistoryLimit;
    public PasteOptions PasteOptions = new();
    public List<EditorShortcutBinding> ShortcutOverrides = new();

    public SettingsWindow(Settings s) : base("Settings", "Leave a field blank to use the built-in default.")
    {
        Width = 540;
        _configDir = AddField("Game config dir", s.ConfigDir ?? "");
        _testPort  = AddField("Test source port", s.TestPort ?? "");
        _testIwad  = AddField("Test IWAD", s.TestIwad ?? "");
        _testArgs  = AddField("Test port args", s.TestPortArgs ?? "");
        _nodePath  = AddField("Node builder", s.NodeBuilderPath ?? "");
        _nodeArgs  = AddField("Node builder args", s.NodeBuilderArgs ?? "");
        _maxRecentFiles = AddField("Max recent files", s.MaxRecentFiles?.ToString() ?? "");
        _statusHistoryLimit = AddField("Status history", s.StatusHistoryLimit?.ToString() ?? "");
        _shortcutOverrides = AddField("Shortcut overrides", EditorCommandCatalog.OverrideText(s.ShortcutOverrides));
        _autoClearSidedefTextures = AddCheckBox("Auto-clear sidedef textures", s.AutoClearSidedefTextures);
        _pasteTagMode = AddCombo("Pasted tags", PasteTagModeItems(), (int)s.NormalizedPasteOptions.ChangeTags);
        _pasteRemoveActions = AddCheckBox("Remove pasted actions", s.NormalizedPasteOptions.RemoveActions);
    }

    protected override void OnConfirm()
    {
        ConfigDir = NullIfBlank(_configDir.Text);
        TestPort = NullIfBlank(_testPort.Text);
        TestIwad = NullIfBlank(_testIwad.Text);
        TestPortArgs = NullIfBlank(_testArgs.Text);
        NodeBuilderPath = NullIfBlank(_nodePath.Text);
        NodeBuilderArgs = NullIfBlank(_nodeArgs.Text);
        MaxRecentFiles = int.TryParse(_maxRecentFiles.Text, out int maxRecent) && maxRecent > 0 ? maxRecent : null;
        StatusHistoryLimit = int.TryParse(_statusHistoryLimit.Text, out int limit) && limit > 0 ? limit : null;
        AutoClearSidedefTextures = _autoClearSidedefTextures.IsChecked == true;
        ShortcutOverrides = EditorCommandCatalog.ParseOverrideText(_shortcutOverrides.Text);
        PasteOptions = new PasteOptions
        {
            ChangeTags = (PasteTagMode)ComboNumber(_pasteTagMode, (int)PasteTagMode.Keep),
            RemoveActions = _pasteRemoveActions.IsChecked == true,
        };
    }

    private static string? NullIfBlank(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    private static IEnumerable<CatalogItem> PasteTagModeItems()
    {
        yield return new CatalogItem((int)PasteTagMode.Keep, "Keep tags");
        yield return new CatalogItem((int)PasteTagMode.Renumber, "Renumber conflicting tags");
        yield return new CatalogItem((int)PasteTagMode.Remove, "Remove tags");
    }
}
