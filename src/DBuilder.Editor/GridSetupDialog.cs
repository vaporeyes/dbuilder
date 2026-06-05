// ABOUTME: Modal dialog for editing the active 2D editor grid setup.
// ABOUTME: Writes grid size, origin and rotation back to MapControl's UDB-compatible GridSetup.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class GridSetupDialog : PropertyDialog
{
    private readonly TextBox _size;
    private readonly TextBox _originX;
    private readonly TextBox _originY;
    private readonly TextBox _rotation;
    private readonly CheckBox _showBackground;
    private readonly TextBox _background;
    private readonly ComboBox _backgroundSource;
    private readonly TextBox _backgroundX;
    private readonly TextBox _backgroundY;
    private readonly TextBox _backgroundScaleX;
    private readonly TextBox _backgroundScaleY;
    private readonly ResourceManager? _resources;
    private readonly double _minimumGridSize;

    public double ResultSize { get; private set; }
    public double ResultOriginX { get; private set; }
    public double ResultOriginY { get; private set; }
    public double ResultRotation { get; private set; }
    public string ResultBackground { get; private set; }
    public int ResultBackgroundSource { get; private set; }
    public int ResultBackgroundX { get; private set; }
    public int ResultBackgroundY { get; private set; }
    public double ResultBackgroundScaleX { get; private set; }
    public double ResultBackgroundScaleY { get; private set; }

    public GridSetupDialog(GridSetup grid, ResourceManager? resources = null)
        : base("Grid Setup", "Grid size and transform affect drawing, movement and insertion snapping.")
    {
        _resources = resources;
        _minimumGridSize = grid.MinimumSize;
        ResultSize = grid.GridSizeF;
        ResultOriginX = grid.GridOriginX;
        ResultOriginY = grid.GridOriginY;
        ResultRotation = grid.GridRotate;
        ResultBackground = grid.BackgroundName;
        ResultBackgroundSource = grid.BackgroundSource;
        ResultBackgroundX = grid.BackgroundX;
        ResultBackgroundY = grid.BackgroundY;
        ResultBackgroundScaleX = grid.BackgroundScaleX;
        ResultBackgroundScaleY = grid.BackgroundScaleY;

        _size = AddField("Grid size", grid.GridSizeF.ToString("0.###", CultureInfo.InvariantCulture));
        _originX = AddField("Origin X", grid.GridOriginX.ToString("0.###", CultureInfo.InvariantCulture));
        _originY = AddField("Origin Y", grid.GridOriginY.ToString("0.###", CultureInfo.InvariantCulture));
        _rotation = AddField("Rotation degrees", GridSetupDialogModel.FormatRotationDegrees(grid.GridRotate));
        _showBackground = AddCheckBox("Show background image", !string.IsNullOrWhiteSpace(grid.BackgroundName));
        _background = AddBackgroundField(grid.BackgroundName);
        _backgroundSource = AddCombo("Background source", BackgroundSourceItems(), grid.BackgroundSource);
        _backgroundX = AddField("Background X", grid.BackgroundX.ToString(CultureInfo.InvariantCulture));
        _backgroundY = AddField("Background Y", grid.BackgroundY.ToString(CultureInfo.InvariantCulture));
        _backgroundScaleX = AddField("Background scale X %", GridSetupDialogModel.FormatBackgroundScalePercent(grid.BackgroundScaleX));
        _backgroundScaleY = AddField("Background scale Y %", GridSetupDialogModel.FormatBackgroundScalePercent(grid.BackgroundScaleY));
        _showBackground.IsCheckedChanged += (_, _) => SetBackgroundControlsEnabled(_showBackground.IsChecked == true);
        SetBackgroundControlsEnabled(_showBackground.IsChecked == true);
    }

    protected override void OnConfirm()
    {
        ResultSize = GridSetupDialogModel.ParseGridSize(_size.Text, ResultSize, _minimumGridSize);
        ResultOriginX = GridSetupDialogModel.ParseDouble(_originX.Text, ResultOriginX);
        ResultOriginY = GridSetupDialogModel.ParseDouble(_originY.Text, ResultOriginY);
        ResultRotation = GridSetupDialogModel.ParseRotationDegrees(_rotation.Text, ResultRotation);
        (ResultBackground, ResultBackgroundSource) = GridSetupDialogModel.BackgroundSelection(
            _showBackground.IsChecked == true,
            _background.Text,
            ComboNumber(_backgroundSource, ResultBackgroundSource));
        ResultBackgroundX = GridSetupDialogModel.ParseInt(_backgroundX.Text, ResultBackgroundX);
        ResultBackgroundY = GridSetupDialogModel.ParseInt(_backgroundY.Text, ResultBackgroundY);
        ResultBackgroundScaleX = GridSetupDialogModel.ParseBackgroundScalePercent(_backgroundScaleX.Text, ResultBackgroundScaleX);
        ResultBackgroundScaleY = GridSetupDialogModel.ParseBackgroundScalePercent(_backgroundScaleY.Text, ResultBackgroundScaleY);
    }

    private static IEnumerable<CatalogItem> BackgroundSourceItems()
        =>
        [
            new CatalogItem(GridSetup.SourceTextures, "Textures"),
            new CatalogItem(GridSetup.SourceFlats, "Flats"),
            new CatalogItem(GridSetup.SourceFile, "File"),
        ];

    private async Task BrowseBackgroundFile(TextBox box)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Background Image File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All supported images") { Patterns = new[] { "*.bmp", "*.gif", "*.png" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;

        box.Text = path;
        _showBackground.IsChecked = true;
        SelectBackgroundSource(GridSetup.SourceFile);
    }

    private TextBox AddBackgroundField(string value)
    {
        if (_resources is null)
            return AddFieldWithButton("Background", value, "...", BrowseBackgroundFile);

        return AddFieldWithButtons(
            "Background",
            value,
            [
                ("Tex", BrowseBackgroundTexture),
                ("Flat", BrowseBackgroundFlat),
                ("File", BrowseBackgroundFile),
            ]);
    }

    private async Task BrowseBackgroundTexture(TextBox box)
        => await BrowseBackgroundResource(box, flats: false, GridSetup.SourceTextures, "Browse Background Texture");

    private async Task BrowseBackgroundFlat(TextBox box)
        => await BrowseBackgroundResource(box, flats: true, GridSetup.SourceFlats, "Browse Background Flat");

    private async Task BrowseBackgroundResource(TextBox box, bool flats, int source, string title)
    {
        if (_resources is null) return;

        var dlg = new TextureBrowserDialog(_resources, flats) { Title = title };
        if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } selected)
        {
            box.Text = selected;
            _showBackground.IsChecked = true;
            SelectBackgroundSource(source);
        }
    }

    private void SelectBackgroundSource(int source)
    {
        if (_backgroundSource.ItemsSource is not IEnumerable<CatalogItem> items) return;

        foreach (var item in items)
        {
            if (item.Number == source)
            {
                _backgroundSource.SelectedItem = item;
                return;
            }
        }
    }

    private void SetBackgroundControlsEnabled(bool enabled)
    {
        SetRowEnabled(_background, enabled);
        SetRowEnabled(_backgroundSource, enabled);
        SetRowEnabled(_backgroundX, enabled);
        SetRowEnabled(_backgroundY, enabled);
        SetRowEnabled(_backgroundScaleX, enabled);
        SetRowEnabled(_backgroundScaleY, enabled);
    }

    private static void SetRowEnabled(Control control, bool enabled)
    {
        if (control.Parent is Control row) row.IsEnabled = enabled;
        else control.IsEnabled = enabled;
    }
}
