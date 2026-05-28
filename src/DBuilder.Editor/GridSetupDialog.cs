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

    public double ResultSize { get; private set; }
    public double ResultOriginX { get; private set; }
    public double ResultOriginY { get; private set; }
    public double ResultRotation { get; private set; }

    public GridSetupDialog(GridSetup grid)
        : base("Grid Setup", "Grid size and transform affect drawing, movement and insertion snapping.")
    {
        ResultSize = grid.GridSizeF;
        ResultOriginX = grid.GridOriginX;
        ResultOriginY = grid.GridOriginY;
        ResultRotation = grid.GridRotate;

        _size = AddField("Grid size", grid.GridSizeF.ToString("0.###", CultureInfo.InvariantCulture));
        _originX = AddField("Origin X", grid.GridOriginX.ToString("0.###", CultureInfo.InvariantCulture));
        _originY = AddField("Origin Y", grid.GridOriginY.ToString("0.###", CultureInfo.InvariantCulture));
        _rotation = AddField("Rotation radians", grid.GridRotate.ToString("0.###", CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        ResultSize = Math.Max(GridSetup.MinimumGridSize, ParseDouble(_size, ResultSize));
        ResultOriginX = ParseDouble(_originX, ResultOriginX);
        ResultOriginY = ParseDouble(_originY, ResultOriginY);
        ResultRotation = ParseDouble(_rotation, ResultRotation);
    }
}
