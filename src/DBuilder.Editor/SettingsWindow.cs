// ABOUTME: Modal Settings dialog editing the persisted paths (game-config dir, test source port/IWAD/args, node builder).
// ABOUTME: Reads current values from Settings on open; the host writes the results back and saves on OK.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class SettingsWindow : PropertyDialog
{
    private readonly TextBox _configDir, _testPort, _testIwad, _testArgs, _nodePath, _nodeArgs, _statusHistoryLimit;

    public string? ConfigDir, TestPort, TestIwad, TestPortArgs, NodeBuilderPath, NodeBuilderArgs;
    public int? StatusHistoryLimit;

    public SettingsWindow(Settings s) : base("Settings", "Leave a field blank to use the built-in default.")
    {
        Width = 540;
        _configDir = AddField("Game config dir", s.ConfigDir ?? "");
        _testPort  = AddField("Test source port", s.TestPort ?? "");
        _testIwad  = AddField("Test IWAD", s.TestIwad ?? "");
        _testArgs  = AddField("Test port args", s.TestPortArgs ?? "");
        _nodePath  = AddField("Node builder", s.NodeBuilderPath ?? "");
        _nodeArgs  = AddField("Node builder args", s.NodeBuilderArgs ?? "");
        _statusHistoryLimit = AddField("Status history", s.StatusHistoryLimit?.ToString() ?? "");
    }

    protected override void OnConfirm()
    {
        ConfigDir = NullIfBlank(_configDir.Text);
        TestPort = NullIfBlank(_testPort.Text);
        TestIwad = NullIfBlank(_testIwad.Text);
        TestPortArgs = NullIfBlank(_testArgs.Text);
        NodeBuilderPath = NullIfBlank(_nodePath.Text);
        NodeBuilderArgs = NullIfBlank(_nodeArgs.Text);
        StatusHistoryLimit = int.TryParse(_statusHistoryLimit.Text, out int limit) && limit > 0 ? limit : null;
    }

    private static string? NullIfBlank(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
}
