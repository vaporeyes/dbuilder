// ABOUTME: Modal dialog for editing the active map's basic identity options.
// ABOUTME: Covers the map marker and UDMF namespace slice of UDB's broader Map Options form.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class MapOptionsDialog : PropertyDialog
{
    private readonly TextBox _marker;
    private readonly TextBox _namespace;
    private readonly TextBox _floorTexture;
    private readonly TextBox _ceilingTexture;
    private readonly TextBox _topTexture;
    private readonly TextBox _wallTexture;
    private readonly TextBox _bottomTexture;
    private readonly TextBox _floorHeight;
    private readonly TextBox _ceilingHeight;
    private readonly TextBox _brightness;
    private readonly CheckBox _useLongTextureNames;
    private readonly bool _longTextureNamesSupported;

    public string ResultMarker { get; private set; }
    public string ResultNamespace { get; private set; }

    public MapOptionsDialog(string marker, string mapNamespace, MapOptions options, bool longTextureNamesSupported)
        : base("Map Options", "Basic map identity options for the currently loaded map.")
    {
        ResultMarker = MapNameRules.NormalizeMarker(marker);
        ResultNamespace = mapNamespace;
        _longTextureNamesSupported = longTextureNamesSupported;

        _marker = AddField("Map marker", ResultMarker);
        _namespace = AddField("UDMF namespace", mapNamespace);
        _floorTexture = AddField("Default floor", options.DefaultFloorTexture);
        _ceilingTexture = AddField("Default ceiling", options.DefaultCeilingTexture);
        _topTexture = AddField("Default upper", options.DefaultTopTexture);
        _wallTexture = AddField("Default middle", options.DefaultWallTexture);
        _bottomTexture = AddField("Default lower", options.DefaultBottomTexture);
        _floorHeight = AddField("Floor height", options.CustomFloorHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _ceilingHeight = AddField("Ceiling height", options.CustomCeilingHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _brightness = AddField("Brightness", options.CustomBrightness.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _useLongTextureNames = AddCheckBox("Use long texture names", longTextureNamesSupported && options.UseLongTextureNames);
        _useLongTextureNames.IsEnabled = longTextureNamesSupported;
    }

    public void ApplyTo(MapOptions options)
    {
        options.DefaultFloorTexture = _floorTexture.Text?.Trim() ?? "";
        options.DefaultCeilingTexture = _ceilingTexture.Text?.Trim() ?? "";
        options.DefaultTopTexture = _topTexture.Text?.Trim() ?? "";
        options.DefaultWallTexture = _wallTexture.Text?.Trim() ?? "";
        options.DefaultBottomTexture = _bottomTexture.Text?.Trim() ?? "";
        options.CustomFloorHeight = ParseInt(_floorHeight, options.CustomFloorHeight);
        options.CustomCeilingHeight = ParseInt(_ceilingHeight, options.CustomCeilingHeight);
        options.CustomBrightness = System.Math.Clamp(ParseInt(_brightness, options.CustomBrightness), 0, 255);
        options.UseLongTextureNames = _longTextureNamesSupported && _useLongTextureNames.IsChecked == true;
    }

    protected override void OnConfirm()
    {
        ResultMarker = MapNameRules.NormalizeMarker(_marker.Text, ResultMarker);
        ResultNamespace = _namespace.Text?.Trim() ?? "";
    }
}
