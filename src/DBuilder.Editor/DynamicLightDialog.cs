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
        _relativeMode.IsCheckedChanged += (_, _) => RefreshRelativeModeFields();
    }

    protected override void OnConfirm()
    {
        ResultRelativeMode = _relativeMode.IsChecked == true;
        DynamicLightSliderLimits radiusLimits = ResultRelativeMode
            ? ColorPickerModel.DynamicLightRadiusLimits(relativeMode: true)
            : _state.PrimaryLimits;
        DynamicLightSliderLimits intervalLimits = ResultRelativeMode
            ? ColorPickerModel.DynamicLightIntervalLimits(relativeMode: true)
            : _state.IntervalLimits;
        var rgbColor = new ColorRgb(
            ClampByte(ParseInt(_red, _state.Color.Red)),
            ClampByte(ParseInt(_green, _state.Color.Green)),
            ClampByte(ParseInt(_blue, _state.Color.Blue)));
        ResultColor = ColorPickerModel.ResolveTypedColorInput(_state.Color, rgbColor, _hex.Text, _float.Text);
        ResultPrimaryRadius = ColorPickerModel.ClampDynamicLightSliderValue(
            radiusLimits,
            ParseInt(_primaryRadius, _state.PrimaryRadius));
        ResultSecondaryRadius = _secondaryRadius == null
            ? _state.SecondaryRadius
            : ColorPickerModel.ClampDynamicLightSliderValue(radiusLimits, ParseInt(_secondaryRadius, _state.SecondaryRadius));
        ResultInterval = _interval == null
            ? _state.Interval
            : ColorPickerModel.ClampDynamicLightSliderValue(intervalLimits, ParseInt(_interval, _state.Interval));
    }

    private static int ClampByte(int value)
        => Math.Clamp(value, 0, 255);

    private void RefreshRelativeModeFields()
    {
        bool relativeMode = _relativeMode.IsChecked == true;
        _primaryRadius.Text = FormatInt(relativeMode ? 0 : _state.PrimaryRadius);
        if (_secondaryRadius != null) _secondaryRadius.Text = FormatInt(relativeMode ? 0 : _state.SecondaryRadius);
        if (_interval != null) _interval.Text = FormatInt(relativeMode ? 0 : _state.Interval);
    }

    private static string FormatInt(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string Label(string label, string fallback)
        => string.IsNullOrWhiteSpace(label) ? fallback : label.TrimEnd(':');
}
