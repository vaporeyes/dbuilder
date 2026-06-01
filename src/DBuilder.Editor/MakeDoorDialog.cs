// ABOUTME: Modal dialog for the BuilderModes Make Door command.
// ABOUTME: Captures UDB-style door textures and action/tag toggles before sector mutation.

using Avalonia.Controls;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class MakeDoorDialog : PropertyDialog
{
    private static StoredMakeDoorOptions storedOptions = new();

    private readonly TextBox _doorTexture;
    private readonly TextBox _trackTexture;
    private readonly TextBox _ceilingTexture;
    private readonly TextBox _floorTexture;
    private readonly CheckBox _resetOffsets;
    private readonly CheckBox _applyActionSpecials;
    private readonly CheckBox _applyTag;
    private readonly int _action;
    private readonly int _activate;
    private readonly int[] _args;
    private readonly IReadOnlyDictionary<string, bool> _flags;

    public MakeDoorOptions ResultOptions { get; private set; }

    public MakeDoorDialog(MakeDoorOptions defaults, ResourceManager? resources)
        : base("Make Door")
    {
        _action = defaults.Action;
        _activate = defaults.Activate;
        _args = defaults.Args.ToArray();
        _flags = defaults.Flags;

        string doorTexture = storedOptions.HasValues ? storedOptions.DoorTexture : defaults.DoorTexture;
        string trackTexture = storedOptions.HasValues ? storedOptions.TrackTexture : defaults.TrackTexture;
        string ceilingTexture = storedOptions.HasValues ? storedOptions.CeilingTexture : defaults.CeilingTexture;
        string floorTexture = storedOptions.HasValues ? storedOptions.FloorTexture : defaults.FloorTexture ?? "";

        _doorTexture = AddTextureInput("Door texture", doorTexture, resources, flats: false, "Browse Door Texture");
        _trackTexture = AddTextureInput("Track texture", trackTexture, resources, flats: false, "Browse Track Texture");
        _ceilingTexture = AddTextureInput("Ceiling flat", ceilingTexture, resources, flats: true, "Browse Ceiling Flat");
        _floorTexture = AddTextureInput("Floor flat", floorTexture, resources, flats: true, "Browse Floor Flat");
        _resetOffsets = AddCheckBox("Reset sidedef offsets", storedOptions.HasValues ? storedOptions.ResetOffsets : defaults.ResetOffsets);
        _applyActionSpecials = AddCheckBox("Apply action special", storedOptions.HasValues ? storedOptions.ApplyActionSpecials : defaults.ApplyActionSpecials);
        _applyTag = AddCheckBox("Apply sector tag", storedOptions.HasValues ? storedOptions.ApplyTag : defaults.ApplyTag);

        ResultOptions = defaults;
    }

    protected override void OnConfirm()
    {
        storedOptions = new StoredMakeDoorOptions(
            HasValues: true,
            DoorTexture: Value(_doorTexture),
            TrackTexture: Value(_trackTexture),
            CeilingTexture: Value(_ceilingTexture),
            FloorTexture: Value(_floorTexture),
            ResetOffsets: _resetOffsets.IsChecked == true,
            ApplyActionSpecials: _applyActionSpecials.IsChecked == true,
            ApplyTag: _applyTag.IsChecked == true);

        ResultOptions = new MakeDoorOptions
        {
            DoorTexture = storedOptions.DoorTexture,
            TrackTexture = storedOptions.TrackTexture,
            CeilingTexture = storedOptions.CeilingTexture,
            FloorTexture = string.IsNullOrWhiteSpace(storedOptions.FloorTexture) ? null : storedOptions.FloorTexture,
            ResetOffsets = storedOptions.ResetOffsets,
            ApplyActionSpecials = storedOptions.ApplyActionSpecials,
            ApplyTag = storedOptions.ApplyTag,
            Action = _action,
            Activate = _activate,
            Args = _args.ToArray(),
            Flags = _flags,
        };
    }

    private TextBox AddTextureInput(string label, string value, ResourceManager? resources, bool flats, string title)
        => resources is null ? AddField(label, value) : AddTextureField(label, value, resources, flats, title);

    private static string Value(TextBox box) => (box.Text ?? "").Trim();

    private readonly record struct StoredMakeDoorOptions(
        bool HasValues = false,
        string DoorTexture = "",
        string TrackTexture = "",
        string CeilingTexture = "",
        string FloorTexture = "",
        bool ResetOffsets = true,
        bool ApplyActionSpecials = true,
        bool ApplyTag = false);
}
