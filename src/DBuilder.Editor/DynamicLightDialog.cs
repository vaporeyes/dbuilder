// ABOUTME: Dialog for editing selected UDB internal dynamic light thing color and radius values.
// ABOUTME: Uses the ColorPicker model while keeping compact text-field UI parsing in the editor layer.

using System.Globalization;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class DynamicLightDialog : PropertyDialog
{
    private readonly TextBox _red;
    private readonly TextBox _green;
    private readonly TextBox _blue;
    private readonly TextBox _hex;
    private readonly TextBox _float;
    private readonly TextBox _primaryRadius;
    private readonly TextBox? _secondaryRadius;
    private readonly TextBox? _interval;
    private readonly CheckBox _relativeMode;
    private readonly DynamicLightPickerState _state;

    public ColorRgb ResultColor { get; private set; }
    public int ResultPrimaryRadius { get; private set; }
    public int ResultSecondaryRadius { get; private set; }
    public int ResultInterval { get; private set; }
    public bool ResultRelativeMode { get; private set; }

    public DynamicLightDialog(int lightCount, DynamicLightPickerState state, DynamicLightSliderPresentation presentation)
        : base(ColorPickerModel.DynamicLightPickerTitle(lightCount))
    {
        _state = state;
        ResultColor = state.Color;
        ResultPrimaryRadius = state.PrimaryRadius;
        ResultSecondaryRadius = state.SecondaryRadius;
        ResultInterval = state.Interval;

        _red = AddField("Red", state.Color.Red.ToString(CultureInfo.InvariantCulture));
        _green = AddField("Green", state.Color.Green.ToString(CultureInfo.InvariantCulture));
        _blue = AddField("Blue", state.Color.Blue.ToString(CultureInfo.InvariantCulture));
        _hex = AddField("Hex", ColorPickerModel.Format(state.Color, ColorPickerInfoMode.Hex));
        _float = AddField("Float", ColorPickerModel.Format(state.Color, ColorPickerInfoMode.Float));
        _primaryRadius = AddField(Label(presentation.PrimaryLabel, "Radius"), state.PrimaryRadius.ToString(CultureInfo.InvariantCulture));
        if (state.ShowAllControls)
        {
            _secondaryRadius = AddField(Label(presentation.SecondaryLabel, "Secondary radius"), state.SecondaryRadius.ToString(CultureInfo.InvariantCulture));
            _interval = AddField(Label(presentation.IntervalLabel, "Interval"), state.Interval.ToString(CultureInfo.InvariantCulture));
        }
        _relativeMode = AddCheckBox("Relative mode", false);
    }

    protected override void OnConfirm()
    {
        var rgbColor = new ColorRgb(
            ClampByte(ParseInt(_red, _state.Color.Red)),
            ClampByte(ParseInt(_green, _state.Color.Green)),
            ClampByte(ParseInt(_blue, _state.Color.Blue)));
        ResultColor = ColorPickerModel.ResolveTypedColorInput(_state.Color, rgbColor, _hex.Text, _float.Text);
        ResultPrimaryRadius = ColorPickerModel.ClampDynamicLightSliderValue(
            _state.PrimaryLimits,
            ParseInt(_primaryRadius, _state.PrimaryRadius));
        ResultSecondaryRadius = _secondaryRadius == null
            ? _state.SecondaryRadius
            : ColorPickerModel.ClampDynamicLightSliderValue(_state.SecondaryLimits, ParseInt(_secondaryRadius, _state.SecondaryRadius));
        ResultInterval = _interval == null
            ? _state.Interval
            : ColorPickerModel.ClampDynamicLightSliderValue(_state.IntervalLimits, ParseInt(_interval, _state.Interval));
        ResultRelativeMode = _relativeMode.IsChecked == true;
    }

    private static int ClampByte(int value)
        => Math.Clamp(value, 0, 255);

    private static string Label(string label, string fallback)
        => string.IsNullOrWhiteSpace(label) ? fallback : label.TrimEnd(':');
}
