// ABOUTME: Modal dialog for editing resource loader options before adding a WAD or PK3.
// ABOUTME: Maps UI choices onto DataLocation option flags consumed by ResourceManager.

using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ResourceOptionsDialog : PropertyDialog
{
    private readonly DataLocationType _type;
    private readonly TextBox _location;
    private readonly CheckBox _strictPatches;
    private readonly CheckBox _rootTextures;
    private readonly CheckBox _rootFlats;
    private readonly CheckBox _notForTesting;

    public DataLocation ResultLocation { get; private set; }

    public ResourceOptionsDialog(DataLocation location)
        : base("Resource Options", $"Resource type: {Describe(location.Type)}")
    {
        _type = location.Type;
        ResultLocation = new DataLocation(location.Type, location.Location, location.Option1, location.Option2, location.NotForTesting)
        {
            InitialLocation = location.InitialLocation,
        };

        _location = AddField("Location", location.Location);
        _location.IsReadOnly = true;
        _strictPatches = AddCheckBox("Strict WAD patch namespace", _type == DataLocationType.Wad && location.Option1);
        _rootTextures = AddCheckBox("Load root textures", _type == DataLocationType.Pk3 && location.Option1);
        _rootFlats = AddCheckBox("Load root flats", _type == DataLocationType.Pk3 && location.Option2);
        _notForTesting = AddCheckBox("Exclude from Test Map launch", location.NotForTesting);

        _strictPatches.IsVisible = _type == DataLocationType.Wad;
        _rootTextures.IsVisible = _type == DataLocationType.Pk3;
        _rootFlats.IsVisible = _type == DataLocationType.Pk3;
    }

    protected override void OnConfirm()
    {
        string path = _location.Text?.Trim() ?? "";
        var location = new DataLocation(_type, path);
        if (_type == DataLocationType.Wad) location.Option1 = _strictPatches.IsChecked == true;
        if (_type == DataLocationType.Pk3)
        {
            location.Option1 = _rootTextures.IsChecked == true;
            location.Option2 = _rootFlats.IsChecked == true;
        }
        location.NotForTesting = _notForTesting.IsChecked == true;
        ResultLocation = location;
    }

    private static string Describe(DataLocationType type) => type switch
    {
        DataLocationType.Wad => "WAD",
        DataLocationType.Pk3 => "PK3 or ZIP",
        DataLocationType.Directory => "Directory",
        _ => type.ToString(),
    };
}
