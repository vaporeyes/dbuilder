// ABOUTME: Modal dialog for editing the active 2D editor grid setup.
// ABOUTME: Writes grid size, origin and rotation back to MapControl's UDB-compatible GridSetup.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class GridSetupDialog : PropertyDialog
{
    private readonly TextBox _size;
    private readonly TextBox _originX;
    private readonly TextBox _originY;
    private readonly TextBox _rotation;
    private readonly TextBox _background;
    private readonly ComboBox _backgroundSource;
    private readonly TextBox _backgroundX;
    private readonly TextBox _backgroundY;
    private readonly TextBox _backgroundScaleX;
    private readonly TextBox _backgroundScaleY;

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

    public GridSetupDialog(GridSetup grid)
        : base("Grid Setup", "Grid size and transform affect drawing, movement and insertion snapping.")
    {
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
        _rotation = AddField("Rotation radians", grid.GridRotate.ToString("0.###", CultureInfo.InvariantCulture));
        _background = AddField("Background", grid.BackgroundName);
        _backgroundSource = AddCombo("Background source", BackgroundSourceItems(), grid.BackgroundSource);
        _backgroundX = AddField("Background X", grid.BackgroundX.ToString(CultureInfo.InvariantCulture));
        _backgroundY = AddField("Background Y", grid.BackgroundY.ToString(CultureInfo.InvariantCulture));
        _backgroundScaleX = AddField("Background scale X", grid.BackgroundScaleX.ToString("0.###", CultureInfo.InvariantCulture));
        _backgroundScaleY = AddField("Background scale Y", grid.BackgroundScaleY.ToString("0.###", CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        ResultSize = Math.Max(GridSetup.MinimumGridSize, ParseDouble(_size, ResultSize));
        ResultOriginX = ParseDouble(_originX, ResultOriginX);
        ResultOriginY = ParseDouble(_originY, ResultOriginY);
        ResultRotation = ParseDouble(_rotation, ResultRotation);
        ResultBackground = _background.Text?.Trim() ?? "";
        ResultBackgroundSource = ComboNumber(_backgroundSource, ResultBackgroundSource);
        ResultBackgroundX = ParseInt(_backgroundX, ResultBackgroundX);
        ResultBackgroundY = ParseInt(_backgroundY, ResultBackgroundY);
        ResultBackgroundScaleX = ParseDouble(_backgroundScaleX, ResultBackgroundScaleX);
        ResultBackgroundScaleY = ParseDouble(_backgroundScaleY, ResultBackgroundScaleY);
    }

    private static IEnumerable<CatalogItem> BackgroundSourceItems()
        =>
        [
            new CatalogItem(GridSetup.SourceTextures, "Textures"),
            new CatalogItem(GridSetup.SourceFlats, "Flats"),
            new CatalogItem(GridSetup.SourceFile, "File"),
        ];
}
