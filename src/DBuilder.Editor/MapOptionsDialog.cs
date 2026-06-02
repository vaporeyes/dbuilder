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
    private readonly CheckBox _overrideFloorTexture;
    private readonly CheckBox _overrideCeilingTexture;
    private readonly CheckBox _overrideTopTexture;
    private readonly CheckBox _overrideMiddleTexture;
    private readonly CheckBox _overrideBottomTexture;
    private readonly CheckBox _overrideFloorHeight;
    private readonly CheckBox _overrideCeilingHeight;
    private readonly CheckBox _overrideBrightness;
    private readonly CheckBox _useLongTextureNames;
    private readonly TextBox? _scriptCompiler;
    private readonly ComboBox? _scriptCompilerCombo;
    private readonly TextBox _reloadResourcePreCommand;
    private readonly TextBox _reloadResourcePreWorkingDirectory;
    private readonly CheckBox _reloadResourcePreAutoClose;
    private readonly CheckBox _reloadResourcePreExitCodeIsError;
    private readonly CheckBox _reloadResourcePreStdErrIsError;
    private readonly TextBox _reloadResourcePostCommand;
    private readonly TextBox _reloadResourcePostWorkingDirectory;
    private readonly CheckBox _reloadResourcePostAutoClose;
    private readonly CheckBox _reloadResourcePostExitCodeIsError;
    private readonly CheckBox _reloadResourcePostStdErrIsError;
    private readonly TextBox _testPreCommand;
    private readonly TextBox _testPreWorkingDirectory;
    private readonly CheckBox _testPreAutoClose;
    private readonly CheckBox _testPreExitCodeIsError;
    private readonly CheckBox _testPreStdErrIsError;
    private readonly TextBox _testPostCommand;
    private readonly TextBox _testPostWorkingDirectory;
    private readonly CheckBox _testPostAutoClose;
    private readonly CheckBox _testPostExitCodeIsError;
    private readonly CheckBox _testPostStdErrIsError;
    private readonly bool _longTextureNamesSupported;

    public string ResultMarker { get; private set; }
    public string ResultNamespace { get; private set; }

    public MapOptionsDialog(
        string marker,
        string mapNamespace,
        MapOptions options,
        bool longTextureNamesSupported,
        ResourceManager? resources = null,
        ScriptConfigurationCatalog? scriptConfigurations = null,
        string defaultScriptCompiler = "")
        : base("Map Options", "Basic map identity options for the currently loaded map.")
    {
        ResultMarker = MapNameRules.NormalizeMarker(marker);
        ResultNamespace = mapNamespace;
        _longTextureNamesSupported = longTextureNamesSupported;

        _marker = AddField("Map marker", ResultMarker);
        _namespace = AddField("UDMF namespace", mapNamespace);
        if (resources is null)
        {
            _floorTexture = AddField("Default floor", options.DefaultFloorTexture);
            _ceilingTexture = AddField("Default ceiling", options.DefaultCeilingTexture);
            _topTexture = AddField("Default upper", options.DefaultTopTexture);
            _wallTexture = AddField("Default middle", options.DefaultWallTexture);
            _bottomTexture = AddField("Default lower", options.DefaultBottomTexture);
        }
        else
        {
            _floorTexture = AddTextureField("Default floor", options.DefaultFloorTexture, resources, flats: true, "Browse Default Floor");
            _ceilingTexture = AddTextureField("Default ceiling", options.DefaultCeilingTexture, resources, flats: true, "Browse Default Ceiling");
            _topTexture = AddTextureField("Default upper", options.DefaultTopTexture, resources, flats: false, "Browse Default Upper Texture");
            _wallTexture = AddTextureField("Default middle", options.DefaultWallTexture, resources, flats: false, "Browse Default Middle Texture");
            _bottomTexture = AddTextureField("Default lower", options.DefaultBottomTexture, resources, flats: false, "Browse Default Lower Texture");
        }
        _floorHeight = AddField("Floor height", options.CustomFloorHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _ceilingHeight = AddField("Ceiling height", options.CustomCeilingHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _brightness = AddField("Brightness", options.CustomBrightness.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _overrideFloorTexture = AddCheckBox("Override floor texture", options.OverrideFloorTexture);
        _overrideCeilingTexture = AddCheckBox("Override ceiling texture", options.OverrideCeilingTexture);
        _overrideTopTexture = AddCheckBox("Override upper texture", options.OverrideTopTexture);
        _overrideMiddleTexture = AddCheckBox("Override middle texture", options.OverrideMiddleTexture);
        _overrideBottomTexture = AddCheckBox("Override lower texture", options.OverrideBottomTexture);
        _overrideFloorHeight = AddCheckBox("Override floor height", options.OverrideFloorHeight);
        _overrideCeilingHeight = AddCheckBox("Override ceiling height", options.OverrideCeilingHeight);
        _overrideBrightness = AddCheckBox("Override brightness", options.OverrideBrightness);
        _useLongTextureNames = AddCheckBox("Use long texture names", longTextureNamesSupported && options.UseLongTextureNames);
        _useLongTextureNames.IsEnabled = longTextureNamesSupported;
        if (scriptConfigurations is null)
        {
            _scriptCompiler = AddField("Script compiler", options.ScriptCompiler);
        }
        else
        {
            var selection = MapOptionsScriptCompilerModel.BuildSelection(
                scriptConfigurations,
                options.ScriptCompiler,
                defaultScriptCompiler);
            _scriptCompilerCombo = AddStringCombo(
                "Script type",
                selection.Choices.Select(c => new CatalogTextItem(c.Key, c.Description)),
                selection.SelectedKey);
            _scriptCompilerCombo.IsEnabled = selection.Enabled;
        }
        _reloadResourcePreCommand = AddField("Before reload resources", options.ReloadResourcePreCommand.Commands);
        _reloadResourcePreWorkingDirectory = AddField("Reload pre dir", options.ReloadResourcePreCommand.WorkingDirectory);
        _reloadResourcePreAutoClose = AddCheckBox("Reload pre auto close", options.ReloadResourcePreCommand.AutoCloseOnSuccess);
        _reloadResourcePreExitCodeIsError = AddCheckBox("Reload pre fail on exit code", options.ReloadResourcePreCommand.ExitCodeIsError);
        _reloadResourcePreStdErrIsError = AddCheckBox("Reload pre fail on stderr", options.ReloadResourcePreCommand.StdErrIsError);
        _reloadResourcePostCommand = AddField("After reload resources", options.ReloadResourcePostCommand.Commands);
        _reloadResourcePostWorkingDirectory = AddField("Reload post dir", options.ReloadResourcePostCommand.WorkingDirectory);
        _reloadResourcePostAutoClose = AddCheckBox("Reload post auto close", options.ReloadResourcePostCommand.AutoCloseOnSuccess);
        _reloadResourcePostExitCodeIsError = AddCheckBox("Reload post fail on exit code", options.ReloadResourcePostCommand.ExitCodeIsError);
        _reloadResourcePostStdErrIsError = AddCheckBox("Reload post fail on stderr", options.ReloadResourcePostCommand.StdErrIsError);
        _testPreCommand = AddField("Before test map", options.TestPreCommand.Commands);
        _testPreWorkingDirectory = AddField("Test pre dir", options.TestPreCommand.WorkingDirectory);
        _testPreAutoClose = AddCheckBox("Test pre auto close", options.TestPreCommand.AutoCloseOnSuccess);
        _testPreExitCodeIsError = AddCheckBox("Test pre fail on exit code", options.TestPreCommand.ExitCodeIsError);
        _testPreStdErrIsError = AddCheckBox("Test pre fail on stderr", options.TestPreCommand.StdErrIsError);
        _testPostCommand = AddField("After test map", options.TestPostCommand.Commands);
        _testPostWorkingDirectory = AddField("Test post dir", options.TestPostCommand.WorkingDirectory);
        _testPostAutoClose = AddCheckBox("Test post auto close", options.TestPostCommand.AutoCloseOnSuccess);
        _testPostExitCodeIsError = AddCheckBox("Test post fail on exit code", options.TestPostCommand.ExitCodeIsError);
        _testPostStdErrIsError = AddCheckBox("Test post fail on stderr", options.TestPostCommand.StdErrIsError);
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
        options.OverrideFloorTexture = _overrideFloorTexture.IsChecked == true;
        options.OverrideCeilingTexture = _overrideCeilingTexture.IsChecked == true;
        options.OverrideTopTexture = _overrideTopTexture.IsChecked == true;
        options.OverrideMiddleTexture = _overrideMiddleTexture.IsChecked == true;
        options.OverrideBottomTexture = _overrideBottomTexture.IsChecked == true;
        options.OverrideFloorHeight = _overrideFloorHeight.IsChecked == true;
        options.OverrideCeilingHeight = _overrideCeilingHeight.IsChecked == true;
        options.OverrideBrightness = _overrideBrightness.IsChecked == true;
        options.UseLongTextureNames = _longTextureNamesSupported && _useLongTextureNames.IsChecked == true;
        options.ScriptCompiler = _scriptCompilerCombo is not null
            ? (_scriptCompilerCombo.IsEnabled ? ComboText(_scriptCompilerCombo, "") : "")
            : _scriptCompiler?.Text?.Trim() ?? "";
        options.ReloadResourcePreCommand.Commands = _reloadResourcePreCommand.Text?.Trim() ?? "";
        options.ReloadResourcePreCommand.WorkingDirectory = _reloadResourcePreWorkingDirectory.Text?.Trim() ?? "";
        options.ReloadResourcePreCommand.AutoCloseOnSuccess = _reloadResourcePreAutoClose.IsChecked == true;
        options.ReloadResourcePreCommand.ExitCodeIsError = _reloadResourcePreExitCodeIsError.IsChecked == true;
        options.ReloadResourcePreCommand.StdErrIsError = _reloadResourcePreStdErrIsError.IsChecked == true;
        options.ReloadResourcePostCommand.Commands = _reloadResourcePostCommand.Text?.Trim() ?? "";
        options.ReloadResourcePostCommand.WorkingDirectory = _reloadResourcePostWorkingDirectory.Text?.Trim() ?? "";
        options.ReloadResourcePostCommand.AutoCloseOnSuccess = _reloadResourcePostAutoClose.IsChecked == true;
        options.ReloadResourcePostCommand.ExitCodeIsError = _reloadResourcePostExitCodeIsError.IsChecked == true;
        options.ReloadResourcePostCommand.StdErrIsError = _reloadResourcePostStdErrIsError.IsChecked == true;
        options.TestPreCommand.Commands = _testPreCommand.Text?.Trim() ?? "";
        options.TestPreCommand.WorkingDirectory = _testPreWorkingDirectory.Text?.Trim() ?? "";
        options.TestPreCommand.AutoCloseOnSuccess = _testPreAutoClose.IsChecked == true;
        options.TestPreCommand.ExitCodeIsError = _testPreExitCodeIsError.IsChecked == true;
        options.TestPreCommand.StdErrIsError = _testPreStdErrIsError.IsChecked == true;
        options.TestPostCommand.Commands = _testPostCommand.Text?.Trim() ?? "";
        options.TestPostCommand.WorkingDirectory = _testPostWorkingDirectory.Text?.Trim() ?? "";
        options.TestPostCommand.AutoCloseOnSuccess = _testPostAutoClose.IsChecked == true;
        options.TestPostCommand.ExitCodeIsError = _testPostExitCodeIsError.IsChecked == true;
        options.TestPostCommand.StdErrIsError = _testPostStdErrIsError.IsChecked == true;
    }

    protected override void OnConfirm()
    {
        ResultMarker = MapNameRules.NormalizeMarker(_marker.Text, ResultMarker);
        ResultNamespace = _namespace.Text?.Trim() ?? "";
    }
}
