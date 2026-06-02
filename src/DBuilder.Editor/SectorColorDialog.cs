// ABOUTME: Dialog for applying UDMF sector lightcolor or fadecolor values.
// ABOUTME: Uses the ColorPicker model for packed RGB fields while keeping UI parsing local.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class SectorColorDialog : PropertyDialog
{
    private readonly ComboBox _field;
    private readonly TextBox _red;
    private readonly TextBox _green;
    private readonly TextBox _blue;
    private readonly TextBox _hex;
    private readonly TextBox _float;
    private readonly CheckBox _removeDefaults;
    private SectorColorPickerState _state;
    private SectorColorField _activeField;

    public SectorColorField ResultField { get; private set; }
    public ColorRgb ResultColor { get; private set; }
    public bool ResultRemoveDefaults { get; private set; } = true;

    public SectorColorDialog(Sector sector, SectorColorField field, int sectorCount = 1)
        : base(ColorPickerModel.SectorColorPickerTitle(sectorCount))
    {
        ResultField = field;
        _activeField = field;
        _state = ColorPickerModel.CreateSectorColorPickerState(sector, field);
        ResultColor = _state.ActiveColor;

        _field = AddCombo(
            "Field",
            new[]
            {
                new CatalogItem((int)SectorColorField.LightColor, "Sector color"),
                new CatalogItem((int)SectorColorField.FadeColor, "Fade color"),
            },
            (int)field);
        _red = AddField("Red", ResultColor.Red.ToString(CultureInfo.InvariantCulture));
        _green = AddField("Green", ResultColor.Green.ToString(CultureInfo.InvariantCulture));
        _blue = AddField("Blue", ResultColor.Blue.ToString(CultureInfo.InvariantCulture));
        _hex = AddField("Hex", ColorPickerModel.Format(ResultColor, ColorPickerInfoMode.Hex));
        _float = AddField("Float", ColorPickerModel.Format(ResultColor, ColorPickerInfoMode.Float));
        _removeDefaults = AddCheckBox("Remove default color fields", true);
        _field.SelectionChanged += (_, _) => SwitchFieldFromCombo();
    }

    protected override void OnConfirm()
    {
        StoreActiveColor();
        ResultField = (SectorColorField)ComboNumber(_field, (int)_activeField);
        _state = ColorPickerModel.SwitchSectorColorPickerField(_state, ResultField);
        ResultColor = _state.ActiveColor;
        ResultRemoveDefaults = _removeDefaults.IsChecked == true;
    }

    private void SwitchFieldFromCombo()
    {
        var next = (SectorColorField)ComboNumber(_field, (int)_activeField);
        if (next == _activeField) return;

        StoreActiveColor();
        _state = ColorPickerModel.SwitchSectorColorPickerField(_state, next);
        _activeField = next;
        LoadColor(_state.ActiveColor);
    }

    private void StoreActiveColor()
    {
        var rgbColor = new ColorRgb(
            ClampByte(ParseInt(_red, _state.ActiveColor.Red)),
            ClampByte(ParseInt(_green, _state.ActiveColor.Green)),
            ClampByte(ParseInt(_blue, _state.ActiveColor.Blue)));
        ColorRgb color = ColorPickerModel.ResolveTypedColorInput(_state.ActiveColor, rgbColor, _hex.Text, _float.Text);
        _state = ColorPickerModel.SetSectorColorPickerActiveColor(_state, color);
    }

    private void LoadColor(ColorRgb color)
    {
        _red.Text = color.Red.ToString(CultureInfo.InvariantCulture);
        _green.Text = color.Green.ToString(CultureInfo.InvariantCulture);
        _blue.Text = color.Blue.ToString(CultureInfo.InvariantCulture);
        _hex.Text = ColorPickerModel.Format(color, ColorPickerInfoMode.Hex);
        _float.Text = ColorPickerModel.Format(color, ColorPickerInfoMode.Float);
    }

    private static int ClampByte(int value)
        => Math.Clamp(value, 0, 255);
}
