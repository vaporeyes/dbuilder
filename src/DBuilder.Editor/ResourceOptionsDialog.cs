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
    private readonly TextBox _requiredArchives;

    public DataLocation ResultLocation { get; private set; }

    public ResourceOptionsDialog(DataLocation location)
        : base("Resource Options", $"Resource type: {Describe(location.Type)}")
    {
        _type = location.Type;
        ResultLocation = new DataLocation(location.Type, location.Location, location.Option1, location.Option2, location.NotForTesting)
        {
            InitialLocation = location.InitialLocation,
            RequiredArchives = location.RequiredArchives == null ? null! : new List<string>(location.RequiredArchives),
        };

        _location = AddField("Location", location.Location);
        _location.IsReadOnly = true;
        _strictPatches = AddCheckBox("Strict WAD patch namespace", _type == DataLocationType.Wad && location.Option1);
        _rootTextures = AddCheckBox("Load root textures", SupportsRootImages(_type) && location.Option1);
        _rootFlats = AddCheckBox("Load root flats", SupportsRootImages(_type) && location.Option2);
        _notForTesting = AddCheckBox("Exclude from Test Map launch", location.NotForTesting);
        _requiredArchives = AddField("Required archives", location.RequiredArchivesText);

        _strictPatches.IsVisible = _type == DataLocationType.Wad;
        _rootTextures.IsVisible = SupportsRootImages(_type);
        _rootFlats.IsVisible = SupportsRootImages(_type);
    }

    protected override void OnConfirm()
    {
        ResultLocation = ResourceOptionsDialogModel.BuildLocation(
            _type,
            _location.Text ?? "",
            ResultLocation.InitialLocation,
            _strictPatches.IsChecked == true,
            _rootTextures.IsChecked == true,
            _rootFlats.IsChecked == true,
            _notForTesting.IsChecked == true,
            _requiredArchives.Text ?? "");
    }

    private static string Describe(DataLocationType type) => type switch
    {
        DataLocationType.Wad => "WAD",
        DataLocationType.Pk3 => "PK3 or ZIP",
        DataLocationType.Directory => "Directory",
        _ => type.ToString(),
    };

    private static bool SupportsRootImages(DataLocationType type)
        => ResourceOptionsDialogModel.SupportsRootImages(type);
}
