// ABOUTME: Modal dialog for entering the 2D map coordinates the editor view should center on.
// ABOUTME: Mirrors UDB's go-to-coordinates workflow with simple X and Y coordinate fields.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.IO;
using DBuilder.Map;
using Vec2D = DBuilder.Geometry.Vector2D;

namespace DBuilder.Editor;

public sealed class CenterOnCoordinatesDialog : PropertyDialog
{
    private readonly TextBox _x;
    private readonly TextBox _y;
    private readonly MapFormat _format;

    public double ResultX { get; private set; }
    public double ResultY { get; private set; }

    public CenterOnCoordinatesDialog(Vec2D currentCenter, MapFormat format)
        : base("Go To Coordinates", "Center the 2D view on the entered map coordinates.")
    {
        _format = format;
        ResultX = currentCenter.x;
        ResultY = currentCenter.y;

        _x = AddField("X", currentCenter.x.ToString("0.###", CultureInfo.InvariantCulture));
        _y = AddField("Y", currentCenter.y.ToString("0.###", CultureInfo.InvariantCulture));
    }

    protected override void OnConfirm()
    {
        ResultX = CenterOnCoordinatesModel.ParseCoordinate(_x.Text, ResultX, _format);
        ResultY = CenterOnCoordinatesModel.ParseCoordinate(_y.Text, ResultY, _format);
    }
}
