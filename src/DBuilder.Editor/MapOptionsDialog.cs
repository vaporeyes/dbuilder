// ABOUTME: Modal dialog for editing the active map's basic identity options.
// ABOUTME: Covers the map marker and UDMF namespace slice of UDB's broader Map Options form.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class MapOptionsDialog : PropertyDialog
{
    private readonly TextBox _marker;
    private readonly TextBox _namespace;

    public string ResultMarker { get; private set; }
    public string ResultNamespace { get; private set; }

    public MapOptionsDialog(string marker, string mapNamespace)
        : base("Map Options", "Basic map identity options for the currently loaded map.")
    {
        ResultMarker = MapNameRules.NormalizeMarker(marker);
        ResultNamespace = mapNamespace;

        _marker = AddField("Map marker", ResultMarker);
        _namespace = AddField("UDMF namespace", mapNamespace);
    }

    protected override void OnConfirm()
    {
        ResultMarker = MapNameRules.NormalizeMarker(_marker.Text, ResultMarker);
        ResultNamespace = _namespace.Text?.Trim() ?? "";
    }
}
