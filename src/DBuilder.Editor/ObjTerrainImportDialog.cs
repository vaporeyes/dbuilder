// ABOUTME: Dialog for UDB-style BuilderEffects Wavefront OBJ terrain import settings.
// ABOUTME: Collects up-axis, scale, and vertex-height choices before the editor builds terrain.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class ObjTerrainImportDialog : PropertyDialog
{
    private readonly ComboBox _upAxis;
    private readonly TextBox _scale;
    private readonly CheckBox _useVertexHeights;
    private readonly bool _vertexHeightsSupported;

    public ObjTerrainUpAxis ResultUpAxis { get; private set; } = ObjTerrainUpAxis.Y;
    public double ResultScale { get; private set; } = 1.0;
    public bool ResultUseVertexHeights { get; private set; }

    public ObjTerrainImportDialog(bool vertexHeightsSupported)
        : base("Import Wavefront .obj as terrain")
    {
        _vertexHeightsSupported = vertexHeightsSupported;
        ResultUseVertexHeights = vertexHeightsSupported;
        _upAxis = AddCombo("Up axis", UpAxisItems(), (int)ResultUpAxis);
        _scale = AddField("Scale", ResultScale.ToString(CultureInfo.InvariantCulture));
        _useVertexHeights = AddCheckBox("Use vertex heights", ResultUseVertexHeights);
        _useVertexHeights.IsEnabled = vertexHeightsSupported;
    }

    protected override void OnConfirm()
    {
        ResultUpAxis = (ObjTerrainUpAxis)ComboNumber(_upAxis, (int)ResultUpAxis);
        double scale = ParseDouble(_scale, ResultScale);
        ResultScale = scale == 0.0 ? ResultScale : scale;
        ResultUseVertexHeights = _vertexHeightsSupported && _useVertexHeights.IsChecked == true;
    }

    private static IEnumerable<CatalogItem> UpAxisItems()
    {
        yield return new CatalogItem((int)ObjTerrainUpAxis.Y, "Y");
        yield return new CatalogItem((int)ObjTerrainUpAxis.Z, "Z");
        yield return new CatalogItem((int)ObjTerrainUpAxis.X, "X");
    }
}
