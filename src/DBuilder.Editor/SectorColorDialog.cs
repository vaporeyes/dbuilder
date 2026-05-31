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
    private readonly CheckBox _removeDefaults;

    public SectorColorField ResultField { get; private set; }
    public ColorRgb ResultColor { get; private set; }
    public bool ResultRemoveDefaults { get; private set; } = true;

    public SectorColorDialog(Sector sector, SectorColorField field)
        : base("Sector Color")
    {
        ResultField = field;
        ResultColor = ColorPickerModel.UnpackRgb(ColorPickerModel.GetSectorColor(sector, field));

        _field = AddCombo(
            "Field",
            new[]
            {
                new CatalogItem((int)SectorColorField.LightColor, "Light color"),
                new CatalogItem((int)SectorColorField.FadeColor, "Fade color"),
            },
            (int)field);
        _red = AddField("Red", ResultColor.Red.ToString(CultureInfo.InvariantCulture));
        _green = AddField("Green", ResultColor.Green.ToString(CultureInfo.InvariantCulture));
        _blue = AddField("Blue", ResultColor.Blue.ToString(CultureInfo.InvariantCulture));
        _hex = AddField("Hex", ColorPickerModel.Format(ResultColor, ColorPickerInfoMode.Hex));
        _removeDefaults = AddCheckBox("Remove default color fields", true);
    }

    protected override void OnConfirm()
    {
        ResultField = (SectorColorField)ComboNumber(_field, (int)ResultField);
        var rgbColor = new ColorRgb(
            ClampByte(ParseInt(_red, ResultColor.Red)),
            ClampByte(ParseInt(_green, ResultColor.Green)),
            ClampByte(ParseInt(_blue, ResultColor.Blue)));
        string originalHex = ColorPickerModel.Format(ResultColor, ColorPickerInfoMode.Hex);
        ResultColor = HexChanged(_hex.Text, originalHex) && TryParseHex(_hex.Text, out ColorRgb hexColor)
            ? hexColor
            : rgbColor;
        ResultRemoveDefaults = _removeDefaults.IsChecked == true;
    }

    private static bool HexChanged(string? text, string originalHex)
        => NormalizeHex(text) != NormalizeHex(originalHex);

    private static string NormalizeHex(string? text)
        => (text ?? "").Trim().TrimStart('#').ToUpperInvariant();

    private static bool TryParseHex(string? text, out ColorRgb color)
    {
        string value = (text ?? "").Trim().TrimStart('#');
        if (value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int packed))
        {
            color = ColorPickerModel.UnpackRgb(packed);
            return true;
        }

        color = default;
        return false;
    }

    private static int ClampByte(int value)
        => Math.Clamp(value, 0, 255);
}
